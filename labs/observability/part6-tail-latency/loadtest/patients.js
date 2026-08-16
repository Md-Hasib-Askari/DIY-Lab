import http from 'k6/http';
import { check } from 'k6';

export const options = {
    summaryTrendStats: ['avg', 'min', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
    scenarios: {
        steady: {
            executor: 'constant-vus',
            vus: 20,
            duration: '10s',
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<2000', 'p(99)<5000'],
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5118';

export default function () {
    const id = Math.floor(Math.random() * 3) + 1;
    const res = http.get(`${BASE_URL}/patients/${id}`);
    check(res, { 'status is 200': (r) => r.status === 200 });
}
