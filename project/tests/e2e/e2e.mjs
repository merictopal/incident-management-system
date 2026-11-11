// End-to-end test of the running system, through the API Gateway only.
//
//   node e2e.mjs main       full functional + security scenario
//   node e2e.mjs down       (after stopping user-service) monitoring marks it Unhealthy + Critical incident
//   node e2e.mjs recover    (after restarting user-service) Healthy again + incident auto-resolved
//
// GATEWAY env var overrides the gateway address (default http://localhost:5000).

import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { readFileSync, writeFileSync } from 'node:fs';

const GATEWAY = (process.env.GATEWAY ?? 'http://localhost:5000').replace(/\/$/, '');
const CLIENT_ORIGIN = process.env.CLIENT_ORIGIN ?? 'http://localhost:5173';
const STATE_FILE = new URL('./.e2e-state.json', import.meta.url);
const phase = process.argv[2] ?? 'main';

const results = [];
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function check(name, fn) {
  try {
    await fn();
    results.push({ name, ok: true });
    console.log(`  PASS  ${name}`);
  } catch (error) {
    results.push({ name, ok: false, error: error.message });
    console.log(`  FAIL  ${name}\n        -> ${error.message}`);
  }
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

async function api(path, { method = 'GET', token, body, headers = {} } = {}) {
  const response = await fetch(`${GATEWAY}${path}`, {
    method,
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const text = await response.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* not JSON */ }
  return { status: response.status, json, headers: response.headers };
}

async function waitFor(predicate, timeoutMs, description) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const value = await predicate();
    if (value) return value;
    await sleep(300);
  }
  throw new Error(`Timed out after ${timeoutMs} ms waiting for: ${description}`);
}

/** Opens a SignalR connection and records every event it receives. */
async function connect(token, label) {
  const events = [];
  const statusEvents = [];
  const connection = new HubConnectionBuilder()
    .withUrl(`${GATEWAY}/hubs/notifications`, { accessTokenFactory: () => token })
    .configureLogging(LogLevel.None)
    .build();
  connection.on('IncidentChanged', (e) => events.push(e));
  connection.on('ServiceStatusUpdated', (s) => statusEvents.push(s));
  await connection.start();
  return { label, connection, events, statusEvents };
}

async function login(name, password, role) {
  const r = await api('/api/users/login', { method: 'POST', body: { name, password, role } });
  assert(r.status === 200 || r.status === 201, `login ${name}/${role} returned HTTP ${r.status}`);
  return r.json;
}

const run = Date.now().toString(36);

async function main() {
  console.log(`E2E main scenario against ${GATEWAY}`);

  await check('gateway is up', async () => {
    const r = await api('/health');
    assert(r.status === 200, `HTTP ${r.status}`);
  });

  // --- identities ---
  let a, b, it;
  await check('login registers users and returns JWT + unique IDs', async () => {
    a = await login(`Meric-${run}`, 'pass-a', 'Employee');
    b = await login(`Ahmet-${run}`, 'pass-b', 'Employee');
    it = await login(`Alex-${run}`, 'pass-it', 'IT Support');
    assert(a.isNewUser && a.token, 'first login should register');
    assert(new Set([a.user.id, b.user.id, it.user.id]).size === 3, 'IDs must be unique');
  });

  await check('same name + password returns the same ID', async () => {
    const again = await login(`meric-${run}`, 'pass-a', 'Employee');
    assert(again.user.id === a.user.id, `expected #${a.user.id}, got #${again.user.id}`);
    assert(again.isNewUser === false, 'second login must not register again');
  });

  await check('wrong password is rejected (401)', async () => {
    const r = await api('/api/users/login', { method: 'POST', body: { name: `Meric-${run}`, password: 'nope!', role: 'Employee' } });
    assert(r.status === 401, `HTTP ${r.status}`);
  });

  // --- security boundaries ---
  await check('API rejects requests without a token (401)', async () => {
    const r = await api('/api/incidents');
    assert(r.status === 401, `HTTP ${r.status}`);
  });

  await check('/internal endpoints are not exposed by the gateway', async () => {
    const r = await api('/internal/notifications', { method: 'POST', body: {} });
    assert(r.status === 404, `HTTP ${r.status}`);
  });

  await check('CORS allows the client origin only', async () => {
    const good = await api('/health', { headers: { Origin: CLIENT_ORIGIN } });
    const evil = await api('/health', { headers: { Origin: 'http://evil.example' } });
    assert(good.headers.get('access-control-allow-origin') === CLIENT_ORIGIN, 'client origin should be allowed');
    assert(!evil.headers.get('access-control-allow-origin'), 'foreign origin must not be allowed');
  });

  await check('employees cannot read monitoring data (403)', async () => {
    const r = await api('/api/monitoring/services', { token: a.token });
    assert(r.status === 403, `HTTP ${r.status}`);
  });

  // --- real-time connections ---
  let hubA, hubB, hubIt;
  await check('SignalR connections authenticate with JWT', async () => {
    [hubA, hubB, hubIt] = await Promise.all([connect(a.token, 'A'), connect(b.token, 'B'), connect(it.token, 'IT')]);
  });

  await check('hub rejects connections without a valid token', async () => {
    let rejected = false;
    try { await connect('not-a-token', 'anon'); } catch { rejected = true; }
    assert(rejected, 'connection should fail');
  });

  // --- incident lifecycle ---
  let incident;
  await check('employee reports incident; reporter is taken from the token', async () => {
    const r = await api('/api/incidents', {
      method: 'POST', token: a.token,
      body: { title: `VPN down ${run}`, description: 'since 9am', priority: 'High', reporterId: String(b.user.id) }
    });
    assert(r.status === 201, `HTTP ${r.status}`);
    incident = r.json;
    assert(incident.reporterId === String(a.user.id), `reporterId spoofed: ${incident.reporterId}`);
  });

  await check('IT Support receives the Created event in real time', async () => {
    await waitFor(() => hubIt.events.find((e) => e.incidentId === incident.id && e.type === 'Created'), 3000, 'IT Created event');
  });

  await check('reporter receives own Created event', async () => {
    await waitFor(() => hubA.events.find((e) => e.incidentId === incident.id), 3000, 'A Created event');
  });

  await check("another employee does NOT receive the event (SignalR groups)", async () => {
    await sleep(500);
    assert(!hubB.events.some((e) => e.incidentId === incident.id), 'B received A\'s event');
  });

  await check("another employee cannot list or open the incident", async () => {
    const list = await api(`/api/incidents?reporterId=${a.user.id}`, { token: b.token });
    const one = await api(`/api/incidents/${incident.id}`, { token: b.token });
    assert(list.status === 200 && !list.json.some((i) => i.id === incident.id), 'B can list A\'s incident');
    assert(one.status === 404, `B opening A's incident returned HTTP ${one.status}`);
  });

  await check('employee cannot change status (403)', async () => {
    const r = await api(`/api/incidents/${incident.id}/status`, { method: 'PATCH', token: a.token, body: { status: 'Closed' } });
    assert(r.status === 403, `HTTP ${r.status}`);
  });

  await check('invalid status is rejected (400)', async () => {
    const r = await api(`/api/incidents/${incident.id}/status`, { method: 'PATCH', token: it.token, body: { status: 'Banana' } });
    assert(r.status === 400, `HTTP ${r.status}`);
  });

  await check('IT changes status -> reporter notified in real time', async () => {
    const r = await api(`/api/incidents/${incident.id}/status`, { method: 'PATCH', token: it.token, body: { status: 'In Progress' } });
    assert(r.status === 200 && r.json.status === 'In Progress', `HTTP ${r.status}`);
    const event = await waitFor(() => hubA.events.find((e) => e.type === 'StatusChanged' && e.incidentId === incident.id), 3000, 'A StatusChanged');
    assert(event.actorId === String(it.user.id), 'actor should be the IT user');
  });

  await check('employee replies -> IT notified in real time', async () => {
    const r = await api(`/api/incidents/${incident.id}/updates`, { method: 'POST', token: a.token, body: { message: 'Still broken' } });
    assert(r.status === 200, `HTTP ${r.status}`);
    await waitFor(() => hubIt.events.find((e) => e.type === 'Updated' && e.actorId === String(a.user.id)), 3000, 'IT Updated');
  });

  await check('notification history only contains own events for employees', async () => {
    const forA = await api('/api/notifications', { token: a.token });
    const forB = await api('/api/notifications', { token: b.token });
    assert(forA.json.some((n) => n.incidentId === incident.id), 'A history is missing own events');
    assert(!forB.json.some((n) => n.incidentId === incident.id), 'B history leaks A\'s events');
  });

  await check('monitoring status is pushed to IT Support over SignalR', async () => {
    const snapshot = await waitFor(() => hubIt.statusEvents.at(-1), 12000, 'ServiceStatusUpdated');
    assert(snapshot.length === 3, `expected 3 services, got ${snapshot.length}`);
    assert(!hubA.statusEvents.length, 'employee should not receive monitoring events');
  });

  await check('all monitored services report Healthy', async () => {
    await waitFor(async () => {
      const r = await api('/api/monitoring/services', { token: it.token });
      return r.status === 200 && r.json.every((s) => s.status === 'Healthy') ? r.json : null;
    }, 15000, 'all Healthy');
  });

  await check('only IT can delete; reporter receives Deleted event', async () => {
    const byA = await api(`/api/incidents/${incident.id}`, { method: 'DELETE', token: a.token });
    assert(byA.status === 403, `employee delete returned HTTP ${byA.status}`);
    const byIt = await api(`/api/incidents/${incident.id}`, { method: 'DELETE', token: it.token });
    assert(byIt.status === 204, `IT delete returned HTTP ${byIt.status}`);
    await waitFor(() => hubA.events.find((e) => e.type === 'Deleted' && e.incidentId === incident.id), 3000, 'A Deleted');
  });

  await Promise.all([hubA, hubB, hubIt].filter(Boolean).map((h) => h.connection.stop()));
  writeFileSync(STATE_FILE, JSON.stringify({ itToken: it?.token }));
}

async function monitoringDown(service = 'user-service') {
  console.log(`E2E monitoring scenario: ${service} stopped`);
  const { itToken } = JSON.parse(readFileSync(STATE_FILE, 'utf8'));

  await check(`${service} becomes Unhealthy (after the failure threshold)`, async () => {
    await waitFor(async () => {
      const r = await api('/api/monitoring/services', { token: itToken });
      return r.json?.find((s) => s.name === service && s.status === 'Unhealthy');
    }, 25000, `${service} Unhealthy`);
  });

  await check('a Critical incident is opened automatically', async () => {
    const found = await waitFor(async () => {
      const r = await api('/api/incidents', { token: itToken });
      return r.json?.find((i) => i.title === `Service unavailable: ${service}` && i.priority === 'Critical' && i.status === 'Open');
    }, 10000, 'automatic incident');
    writeFileSync(STATE_FILE, JSON.stringify({ itToken, incidentId: found.id }));
  });
}

async function monitoringRecover(service = 'user-service') {
  console.log(`E2E monitoring scenario: ${service} restarted`);
  const { itToken, incidentId } = JSON.parse(readFileSync(STATE_FILE, 'utf8'));

  await check(`${service} becomes Healthy again`, async () => {
    await waitFor(async () => {
      const r = await api('/api/monitoring/services', { token: itToken });
      return r.json?.find((s) => s.name === service && s.status === 'Healthy');
    }, 30000, `${service} Healthy`);
  });

  await check('the automatic incident is resolved automatically', async () => {
    await waitFor(async () => {
      const r = await api(`/api/incidents/${incidentId}`, { token: itToken });
      return r.json?.status === 'Resolved' ? r.json : null;
    }, 10000, 'incident Resolved');
  });
}

try {
  if (phase === 'main') await main();
  else if (phase === 'down') await monitoringDown(process.argv[3]);
  else if (phase === 'recover') await monitoringRecover(process.argv[3]);
  else throw new Error(`Unknown phase "${phase}"`);
} catch (error) {
  results.push({ name: 'unexpected error', ok: false, error: error.message });
  console.log(`  FAIL  unexpected error: ${error.message}`);
}

const failed = results.filter((r) => !r.ok);
console.log(`\n${results.length - failed.length}/${results.length} checks passed (${phase}).`);
process.exit(failed.length ? 1 : 0);
