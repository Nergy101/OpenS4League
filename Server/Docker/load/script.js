// OpenS4L — k6 load test for the WebApi HTTP plugin (port 22000).
//
// Loads the read-only observability/admin endpoints that every deployment serves
// under load (/statistics, /channels, /players, /gamedata/*). The four game
// servers themselves speak the custom ProudNet binary protocol over TCP/UDP, which
// k6 cannot drive — real gameplay load needs a protocol bot harness. This script
// targets the HTTP plane, which is what you care about as you scale the stack.
//
// Configurable via k6 environment vars (pass with `-e` on the CLI or docker run):
//   TARGET_URL    base URL of the WebApi. Defaults to the Docker host's published
//                 22000 (host.docker.internal) so the k6 container reaches the
//                 local stack. Override for a remote/cloud deployment:
//                 TARGET_URL=https://game.example.com  (behind an LB, 443)
//   LOAD_PROFILE  smoke | load | soak   (default smoke)
//   VUS           virtual users (profile-specific default)
//   DURATION      steady-state duration for soak (default 30m)
//   ITERATIONS    per-VU iterations for smoke (default 20)
//
// Admin write endpoints (/admin/kick|ban|roomkick|closeroom) are intentionally NOT
// loaded: they need live players and ban writes to the DB, so they don't belong in
// a steady-state benchmark.

import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = __ENV.TARGET_URL || 'http://host.docker.internal:22000';
const PROFILE = __ENV.LOAD_PROFILE || 'smoke';

const READ_ENDPOINTS = [
  '/statistics',
  '/channels',
  '/players',
  '/gamedata/maps',
];

// Channel ids change per environment; probe channel 1 and room lists lazily so the
// test still passes on a fresh/empty deployment. (404s on absent resources are
// expected here, so they're logged but not failed.)
function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

const scenarios = {
  smoke: {
    // Quick sanity: N VUs each do a handful of iterations and stop.
    executor: 'per-vu-iterations',
    vus: Number(__ENV.VUS || 5),
    iterations: Number(__ENV.ITERATIONS || 20),
  },
  load: {
    // Ramp to a target, hold, ramp down. Classic "what can it handle" run.
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '1m', target: Number(__ENV.VUS || 50) },
      { duration: '3m', target: Number(__ENV.VUS || 50) },
      { duration: '1m', target: 0 },
    ],
  },
  hundred: {
    // A "100 players" run: ramp up to 100 concurrent sessions hitting the WebApi/dashboard
    // plane (the endpoints a fleet of players + the admin console exercise), hold, ramp down.
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '30s', target: Number(__ENV.VUS || 100) },
      { duration: '1m', target: Number(__ENV.VUS || 100) },
      { duration: '30s', target: 0 },
    ],
  },
  soak: {
    // Long steady run to catch leaks, latency creep, and memory growth.
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '2m', target: Number(__ENV.VUS || 30) },
      { duration: __ENV.DURATION || '30m', target: Number(__ENV.VUS || 30) },
      { duration: '2m', target: 0 },
    ],
  },
};

export const options = {
  scenarios: { [PROFILE]: scenarios[PROFILE] },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
};

export default function () {
  const res = http.get(BASE + pick(READ_ENDPOINTS));
  check(res, {
    'status is 200': (r) => r.status === 200,
    'responds as JSON': (r) => (r.headers['Content-Type'] || '').includes('json'),
  });
  sleep(1);
}
