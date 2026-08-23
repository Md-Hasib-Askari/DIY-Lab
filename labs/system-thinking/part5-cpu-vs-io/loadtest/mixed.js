import http from 'k6/http';
import { check, sleep } from 'k6';

// Two endpoints, hit at the same time. That is the whole experiment.
// One request at a time hides the problem completely, because nothing is
// competing for a thread.
//
//   k6 run -e PHASE=2 loadtest/mixed.js     the broken version
//   k6 run -e PHASE=4 loadtest/mixed.js     the fixed version
//   k6 run -e PHASE=1 -e REPORT_VUS=0 loadtest/mixed.js    the baseline

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5155';
const PHASE = __ENV.PHASE || '2';
const USERS_VUS = Number(__ENV.USERS_VUS || 45);
const REPORT_VUS = Number(__ENV.REPORT_VUS || 5);
const DURATION = __ENV.DURATION || '30s';

// In phase 2 a report request takes about this long, so the virtual user is
// busy waiting for it. In phase 4 it returns instantly, so we wait the same
// amount on purpose: same number of reports asked for, only the handling
// differs.
const REPORT_THINK = Number(__ENV.REPORT_THINK || 8);

const scenarios = {
    users: {
        executor: 'constant-vus',
        vus: USERS_VUS,
        duration: DURATION,
        exec: 'hitUsers',
    },
};

if (REPORT_VUS > 0) {
    scenarios.report = {
        executor: 'constant-vus',
        vus: REPORT_VUS,
        duration: DURATION,
        exec: 'hitReport',
    };
}

export const options = {
    scenarios,
    summaryTrendStats: ['avg', 'min', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
    // These thresholds are here so k6 prints /users and /report as separate
    // lines in the summary. The /users line is the one that matters.
    thresholds: {
        'http_req_duration{scenario:users}': ['p(95)>=0'],
        'http_req_failed{scenario:users}': ['rate>=0'],
        'http_req_duration{scenario:report}': ['p(95)>=0'],
    },
};

export function hitUsers() {
    const res = http.get(`${BASE_URL}/phase${PHASE}/users`);
    check(res, { 'users 200': (r) => r.status === 200 });
}

export function hitReport() {
    if (PHASE === '4') {
        const res = http.post(`${BASE_URL}/phase4/report`);
        check(res, { 'report 202': (r) => r.status === 202 });
        sleep(REPORT_THINK);
        return;
    }

    const res = http.get(`${BASE_URL}/phase${PHASE}/report`, { timeout: '120s' });
    check(res, { 'report 200': (r) => r.status === 200 });
}