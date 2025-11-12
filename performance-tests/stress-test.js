import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// 自定义指标
const errorRate = new Rate('errors');

// 压力测试配置 - 快速增加负载直到系统极限
export const options = {
  stages: [
    { duration: '1m', target: 50 },   // 1分钟达到50用户
    { duration: '2m', target: 100 },  // 2分钟达到100用户
    { duration: '2m', target: 200 },  // 2分钟达到200用户
    { duration: '2m', target: 300 },  // 2分钟达到300用户
    { duration: '2m', target: 400 },  // 2分钟达到400用户
    { duration: '2m', target: 500 },  // 2分钟达到500用户（极限测试）
    { duration: '1m', target: 0 },    // 1分钟降到0
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'], // 放宽到1秒
    errors: ['rate<0.2'],               // 允许20%的错误率
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const chutes = ['CHUTE_A', 'CHUTE_B', 'CHUTE_C'];

export default function () {
  const parcelId = `PKG${Math.floor(Math.random() * 1000000).toString().padStart(7, '0')}`;
  const targetChuteId = chutes[Math.floor(Math.random() * chutes.length)];

  const payload = JSON.stringify({
    parcelId: parcelId,
    targetChuteId: targetChuteId,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    timeout: '30s', // 更长的超时时间
  };

  const response = http.post(
    `${BASE_URL}/api/debug/sort`,
    payload,
    params
  );

  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'response received': (r) => r.body.length > 0,
  });

  errorRate.add(!success);

  // 压力测试下不等待
  sleep(0.01);
}
