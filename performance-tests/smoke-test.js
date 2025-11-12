import http from 'k6/http';
import { check, sleep } from 'k6';

// 冒烟测试配置 - 快速验证基本功能
export const options = {
  vus: 1,           // 1个虚拟用户
  duration: '1m',   // 运行1分钟
  thresholds: {
    http_req_duration: ['p(95)<200'], // 95%请求应在200ms内完成
    http_req_failed: ['rate<0.01'],   // 失败率应低于1%
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  // 测试已知的格口
  const testCases = [
    { parcelId: 'PKG001', targetChuteId: 'CHUTE_A' },
    { parcelId: 'PKG002', targetChuteId: 'CHUTE_B' },
    { parcelId: 'PKG003', targetChuteId: 'CHUTE_C' },
    { parcelId: 'PKG004', targetChuteId: 'CHUTE_UNKNOWN' }, // 测试错误处理
  ];

  for (const testCase of testCases) {
    const payload = JSON.stringify(testCase);
    
    const params = {
      headers: {
        'Content-Type': 'application/json',
      },
    };

    const response = http.post(
      `${BASE_URL}/api/debug/sort`,
      payload,
      params
    );

    check(response, {
      'status is 200': (r) => r.status === 200,
      'has response body': (r) => r.body.length > 0,
      'response is valid JSON': (r) => {
        try {
          JSON.parse(r.body);
          return true;
        } catch (e) {
          return false;
        }
      },
    });

    sleep(1);
  }
}
