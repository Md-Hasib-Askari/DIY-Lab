import http from 'k6/http';
import { check } from 'k6';

export const options = {
    vus: 1,
    duration: '10s',
    summaryTrendStats: ['avg', 'min', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5130';

export default function () {
    const res = http.get(`${BASE_URL}/products`);
    check(res, { 'status is 200': (r) => r.status === 200 });
}