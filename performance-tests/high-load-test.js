import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// 自定义指标
const errorRate = new Rate('errors');
const sortingDuration = new Trend('sorting_duration');
const successfulSorts = new Counter('successful_sorts');
const failedSorts = new Counter('failed_sorts');
const throughput = new Rate('throughput');

// 高负载测试配置 - 500-1000包裹/分钟
export const options = {
  scenarios: {
    // 场景1: 稳定500包裹/分钟负载
    constant_500_ppm: {
      executor: 'constant-arrival-rate',
      rate: 8,           // 8个请求/秒 = 480包裹/分钟 ≈ 500包裹/分钟
      timeUnit: '1s',
      duration: '5m',    // 持续5分钟
      preAllocatedVUs: 20,
      maxVUs: 50,
      exec: 'sortParcel',
      tags: { scenario: '500ppm' },
      startTime: '0s',
    },
    // 场景2: 稳定1000包裹/分钟负载
    constant_1000_ppm: {
      executor: 'constant-arrival-rate',
      rate: 17,          // 17个请求/秒 = 1020包裹/分钟 ≈ 1000包裹/分钟
      timeUnit: '1s',
      duration: '5m',    // 持续5分钟
      preAllocatedVUs: 40,
      maxVUs: 100,
      exec: 'sortParcel',
      tags: { scenario: '1000ppm' },
      startTime: '5m',   // 在500ppm场景结束后开始
    },
    // 场景3: 渐进式压力测试 - 从500到1500包裹/分钟
    ramping_stress: {
      executor: 'ramping-arrival-rate',
      startRate: 8,      // 从500包裹/分钟开始
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { duration: '2m', target: 8 },   // 500包裹/分钟
        { duration: '2m', target: 17 },  // 1000包裹/分钟
        { duration: '2m', target: 25 },  // 1500包裹/分钟
        { duration: '2m', target: 33 },  // 2000包裹/分钟 (压力测试)
        { duration: '1m', target: 8 },   // 降回500包裹/分钟
        { duration: '1m', target: 0 },   // 结束
      ],
      exec: 'sortParcel',
      tags: { scenario: 'ramping' },
      startTime: '10m',  // 在1000ppm场景结束后开始
    },
    // 场景4: 稳定性测试 - 长时间运行
    stability_test: {
      executor: 'constant-arrival-rate',
      rate: 10,          // 600包裹/分钟
      timeUnit: '1s',
      duration: '30m',   // 持续30分钟
      preAllocatedVUs: 30,
      maxVUs: 80,
      exec: 'sortParcel',
      tags: { scenario: 'stability' },
      startTime: '20m',  // 与ramping场景并行运行
    },
  },
  thresholds: {
    // 整体性能阈值
    'http_req_duration': ['p(95)<500', 'p(99)<1000'],
    'http_req_duration{scenario:500ppm}': ['p(95)<400'],
    'http_req_duration{scenario:1000ppm}': ['p(95)<500'],
    'http_req_duration{scenario:ramping}': ['p(95)<800'],
    'http_req_duration{scenario:stability}': ['p(95)<500'],
    
    // 错误率阈值
    'errors': ['rate<0.05'],                        // 总体错误率 < 5%
    'errors{scenario:500ppm}': ['rate<0.02'],       // 500ppm场景错误率 < 2%
    'errors{scenario:1000ppm}': ['rate<0.05'],      // 1000ppm场景错误率 < 5%
    'errors{scenario:ramping}': ['rate<0.10'],      // 压力场景错误率 < 10%
    'errors{scenario:stability}': ['rate<0.03'],    // 稳定性场景错误率 < 3%
    
    // 分拣时长阈值
    'sorting_duration': ['p(95)<100', 'p(99)<200'],
    
    // 吞吐量检查
    'throughput': ['rate>0.95'],                    // 成功率 > 95%
  },
};

// 基础URL配置
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

// 格口配置 - 扩展到更多格口以模拟真实场景
const chutes = [
  'CHUTE_A', 'CHUTE_B', 'CHUTE_C', 'CHUTE_D', 'CHUTE_E',
  'CHUTE_F', 'CHUTE_G', 'CHUTE_H', 'CHUTE_I', 'CHUTE_J'
];

// 包裹ID生成器
let parcelCounter = 0;

function generateParcelId() {
  parcelCounter++;
  return `PKG${Date.now()}-${parcelCounter.toString().padStart(8, '0')}`;
}

// 主要测试函数
export function sortParcel() {
  const parcelId = generateParcelId();
  const targetChuteId = chutes[Math.floor(Math.random() * chutes.length)];

  const payload = JSON.stringify({
    parcelId: parcelId,
    targetChuteId: targetChuteId,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    timeout: '10s',
    tags: {
      parcelId: parcelId,
      chute: targetChuteId,
    },
  };

  const startTime = new Date().getTime();
  const response = http.post(
    `${BASE_URL}/api/debug/sort`,
    payload,
    params
  );
  const endTime = new Date().getTime();

  // 验证响应
  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'response has data': (r) => r.body && r.body.length > 0,
    'sorting successful': (r) => {
      try {
        const data = JSON.parse(r.body);
        return data.isSuccess === true;
      } catch (e) {
        return false;
      }
    },
    'response time < 1s': (r) => r.timings.duration < 1000,
  });

  // 记录指标
  errorRate.add(!success);
  throughput.add(success);
  
  if (success) {
    successfulSorts.add(1);
    sortingDuration.add(endTime - startTime);
  } else {
    failedSorts.add(1);
  }

  // 根据场景调整等待时间
  // 高负载场景不需要sleep，由executor控制速率
}

// 设置和拆卸函数
export function setup() {
  console.log('Starting high-load performance test...');
  console.log(`Target URL: ${BASE_URL}`);
  console.log('Test scenarios:');
  console.log('  1. 500 parcels/minute for 5 minutes');
  console.log('  2. 1000 parcels/minute for 5 minutes');
  console.log('  3. Ramping stress test (500-2000 parcels/minute)');
  console.log('  4. Stability test (600 parcels/minute for 30 minutes)');
  
  // 健康检查
  const healthCheck = http.get(`${BASE_URL}/health`);
  if (healthCheck.status !== 200) {
    console.error(`Health check failed: ${healthCheck.status}`);
    // 继续执行，但记录警告
  }
  
  return { startTime: new Date() };
}

export function teardown(data) {
  const endTime = new Date();
  const duration = (endTime - data.startTime) / 1000 / 60; // 分钟
  
  console.log('\n=== Test Summary ===');
  console.log(`Total duration: ${duration.toFixed(2)} minutes`);
  console.log('Check detailed metrics above for:');
  console.log('  - Request duration (p95, p99)');
  console.log('  - Error rates per scenario');
  console.log('  - Sorting duration');
  console.log('  - Throughput');
  console.log('  - Success/failure counts');
}

// 导出默认函数以支持简单运行
export default sortParcel;
