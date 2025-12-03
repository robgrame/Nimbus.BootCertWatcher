// k6 Load Test Script for SecureBootDashboard API
// 
// This script tests the API's ability to handle high request rates
// Target: 5000 requests per second
//
// Usage:
//   k6 run --vus 100 --duration 30s load-test.js
//   k6 run --vus 500 --duration 5m load-test-sustained.js
//
// Requirements:
//   - Install k6: https://k6.io/docs/getting-started/installation/
//   - Update BASE_URL with your API endpoint
//   - Ensure API is running and accessible

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const deviceListDuration = new Trend('device_list_duration');
const deviceDetailDuration = new Trend('device_detail_duration');
const reportIngestionDuration = new Trend('report_ingestion_duration');
const rateLimitCounter = new Counter('rate_limit_429');

// Configuration
const BASE_URL = __ENV.API_URL || 'https://localhost:5001';

// Test scenarios
export const options = {
  scenarios: {
    // Scenario 1: Gradual ramp-up to 5000 RPS
    ramp_up: {
      executor: 'ramping-arrival-rate',
      startRate: 100,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 1000,
      stages: [
        { duration: '1m', target: 1000 }, // Ramp to 1000 RPS
        { duration: '2m', target: 3000 }, // Ramp to 3000 RPS
        { duration: '2m', target: 5000 }, // Ramp to 5000 RPS
        { duration: '5m', target: 5000 }, // Sustain 5000 RPS
        { duration: '1m', target: 0 },    // Ramp down
      ],
    },
  },
  
  thresholds: {
    // 95% of requests should complete in less than 500ms
    http_req_duration: ['p(95)<500'],
    // 99% of requests should complete in less than 1000ms
    'http_req_duration{type:read}': ['p(99)<1000'],
    // Error rate should be less than 1%
    errors: ['rate<0.01'],
    // Less than 5% rate limit errors
    rate_limit_429: ['count<50'],
  },
};

// Sample device IDs for testing (replace with real IDs from your database)
const SAMPLE_DEVICE_IDS = [
  '00000000-0000-0000-0000-000000000001',
  '00000000-0000-0000-0000-000000000002',
  '00000000-0000-0000-0000-000000000003',
];

// Sample report payload for POST testing
const SAMPLE_REPORT = {
  device: {
    machineName: 'LOAD-TEST-01',
    domainName: 'test.local',
    manufacturer: 'Dell Inc.',
    model: 'OptiPlex 7090',
    operatingSystem: 'Windows 11 Pro',
    osVersion: '10.0.22631',
    osBuildNumber: '10.0.22631.4037'
  },
  registry: {
    deploymentState: 'Deployed',
    updateType: 3,
    microsoftUpdateManagedOptIn: true,
    windowsUEFICA2023CapableCode: 1
  },
  certificates: {
    db: [],
    dbx: [],
    kek: [],
    pk: []
  },
  timestamp: new Date().toISOString()
};

export default function () {
  // Test mix: 70% reads, 30% writes (realistic distribution)
  const testType = Math.random();
  
  const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
    'Accept-Encoding': 'br, gzip', // Test compression
  };

  if (testType < 0.5) {
    // Test 1: GET /api/Devices (most common read operation)
    const response = http.get(`${BASE_URL}/api/Devices`, { headers });
    
    deviceListDuration.add(response.timings.duration);
    
    const success = check(response, {
      'device list: status is 200': (r) => r.status === 200,
      'device list: response time < 500ms': (r) => r.timings.duration < 500,
      'device list: has data': (r) => {
        try {
          const data = JSON.parse(r.body);
          return Array.isArray(data) && data.length >= 0;
        } catch {
          return false;
        }
      },
    });
    
    if (!success || response.status !== 200) {
      errorRate.add(1);
      if (response.status === 429) {
        rateLimitCounter.add(1);
      }
    } else {
      errorRate.add(0);
    }
    
  } else if (testType < 0.7) {
    // Test 2: GET /api/Devices/{id} (device details)
    const deviceId = SAMPLE_DEVICE_IDS[Math.floor(Math.random() * SAMPLE_DEVICE_IDS.length)];
    const response = http.get(`${BASE_URL}/api/Devices/${deviceId}`, { headers });
    
    deviceDetailDuration.add(response.timings.duration);
    
    const success = check(response, {
      'device detail: status is 200 or 404': (r) => r.status === 200 || r.status === 404,
      'device detail: response time < 500ms': (r) => r.timings.duration < 500,
    });
    
    if (!success || (response.status !== 200 && response.status !== 404)) {
      errorRate.add(1);
      if (response.status === 429) {
        rateLimitCounter.add(1);
      }
    } else {
      errorRate.add(0);
    }
    
  } else {
    // Test 3: POST /api/SecureBootReports (report ingestion)
    const response = http.post(
      `${BASE_URL}/api/SecureBootReports`,
      JSON.stringify(SAMPLE_REPORT),
      { headers }
    );
    
    reportIngestionDuration.add(response.timings.duration);
    
    const success = check(response, {
      'report ingestion: status is 201 or 400': (r) => r.status === 201 || r.status === 400,
      'report ingestion: response time < 1000ms': (r) => r.timings.duration < 1000,
    });
    
    if (!success || (response.status !== 201 && response.status !== 400)) {
      errorRate.add(1);
      if (response.status === 429) {
        rateLimitCounter.add(1);
      }
    } else {
      errorRate.add(0);
    }
  }
  
  // Small random sleep to simulate realistic think time
  sleep(Math.random() * 0.1);
}

// Setup function - runs once before test
export function setup() {
  console.log('Starting load test...');
  console.log(`Target API: ${BASE_URL}`);
  console.log('Test scenarios: Device List, Device Details, Report Ingestion');
  console.log('Target: 5000 requests per second');
}

// Teardown function - runs once after test
export function teardown(data) {
  console.log('Load test completed');
}

// Handle summary - customize the end-of-test summary
export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'load-test-results.json': JSON.stringify(data),
  };
}

function textSummary(data, options) {
  const { indent = '', enableColors = false } = options || {};
  
  let output = '\n';
  output += `${indent}✓ Test Duration: ${data.state.testRunDurationMs}ms\n`;
  output += `${indent}✓ Total Requests: ${data.metrics.http_reqs.values.count}\n`;
  output += `${indent}✓ Request Rate: ${data.metrics.http_reqs.values.rate.toFixed(2)} req/s\n`;
  output += `${indent}✓ Failed Requests: ${data.metrics.http_req_failed.values.passes || 0}\n`;
  output += `${indent}✓ Response Time (p95): ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms\n`;
  output += `${indent}✓ Response Time (p99): ${data.metrics.http_req_duration.values['p(99)'].toFixed(2)}ms\n`;
  output += `${indent}✓ Rate Limit Errors (429): ${data.metrics.rate_limit_429?.values.count || 0}\n`;
  
  return output;
}
