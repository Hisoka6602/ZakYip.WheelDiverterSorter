import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// 自定义指标
const errorRate = new Rate('errors');
const sortingDuration = new Trend('sorting_duration');

// 测试配置
export const options = {
  stages: [
    { duration: '30s', target: 10 },  // 30秒内逐渐增加到10个虚拟用户
    { duration: '1m', target: 10 },   // 保持10个虚拟用户运行1分钟
    { duration: '30s', target: 50 },  // 30秒内增加到50个虚拟用户
    { duration: '2m', target: 50 },   // 保持50个虚拟用户运行2分钟
    { duration: '30s', target: 100 }, // 30秒内增加到100个虚拟用户
    { duration: '2m', target: 100 },  // 保持100个虚拟用户运行2分钟
    { duration: '30s', target: 0 },   // 30秒内降到0
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95%的请求应在500ms内完成
    errors: ['rate<0.1'],              // 错误率应低于10%
    sorting_duration: ['p(95)<100'],   // 95%的分拣操作应在100ms内完成
  },
};

// 基础URL配置
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

// 格口列表
const chutes = ['CHUTE_A', 'CHUTE_B', 'CHUTE_C'];

export default function () {
  // 生成随机包裹ID
  const parcelId = `PKG${Math.floor(Math.random() * 100000).toString().padStart(6, '0')}`;
  
  // 随机选择目标格口
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
  };

  // 发送分拣请求
  const response = http.post(
    `${BASE_URL}/api/debug/sort`,
    payload,
    params
  );

  // 验证响应
  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'response has data': (r) => r.body.length > 0,
    'sorting successful': (r) => {
      try {
        const data = JSON.parse(r.body);
        return data.isSuccess === true;
      } catch (e) {
        return false;
      }
    },
  });

  // 记录错误
  errorRate.add(!success);

  // 记录分拣时长
  if (response.status === 200) {
    sortingDuration.add(response.timings.duration);
  }

  // 模拟包裹到达间隔（根据目标吞吐量调整）
  // 目标: 500-1000包裹/分钟 = 60-120ms间隔
  sleep(Math.random() * 0.1 + 0.06); // 60-160ms随机间隔
}
