import { spawn } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import assert from 'node:assert/strict';

export async function withBrowser(base, readyExpression, check) {
  const chromePath = process.env.CHROME_PATH ?? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
  assert.equal((await fetch(base)).status, 200, 'Start npm start before the browser test');
  const profile = await mkdtemp(join(tmpdir(), 's4-preview-chrome-'));
  const chrome = spawn(chromePath, ['--headless=new', '--no-first-run', '--no-default-browser-check', '--enable-unsafe-swiftshader', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { stdio: 'ignore' });
  let ws;
  const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
  try {
  let port;
  // A roster-sized batch launches one Chrome per map, so allow a slow start (30 s).
  for (let attempt = 0; attempt < 300; attempt++) {
    try { port = Number((await readFile(join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]); break; }
    catch { await delay(100); }
  }
  assert.ok(port, 'Chrome CDP did not start');
  const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
  ws = new WebSocket(targets.find(t => t.type === 'page').webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
  let sequence = 0;
  const pending = new Map(), errors = [], failedRequests = [];
  ws.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.id) {
      const call = pending.get(message.id);
      if (!call) return;
      pending.delete(message.id); clearTimeout(call.timeout);
      if (message.error) call.reject(new Error(JSON.stringify(message.error))); else call.resolve(message.result);
    } else if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails);
    else if (message.method === 'Runtime.consoleAPICalled' && message.params.type === 'error') errors.push(message.params.args);
    else if (message.method === 'Network.responseReceived' && message.params.response.status >= 400) failedRequests.push(message.params.response.url);
    else if (message.method === 'Network.loadingFailed') failedRequests.push(message.params.errorText);
  };
  const cdp = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++sequence;
    const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 30000);
    pending.set(id, { resolve, reject, timeout }); ws.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
    return result.result.value;
  };
  await cdp('Runtime.enable'); await cdp('Network.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 900, deviceScaleFactor: 2, mobile: false });
  await cdp('Page.navigate', { url: base });
  for (let i = 0; i < 120; i++) {
    if (await evaluate(readyExpression)) break;
    if (errors.length) break;
    await delay(250);
  }
  assert.deepEqual(errors, [], 'Browser/GLSL errors');
  assert.deepEqual(failedRequests, [], 'Missing assets');
  assert.equal(await evaluate(readyExpression), true, await evaluate('document.body.innerText'));
  await check({ cdp, evaluate, delay, errors, failedRequests });
  } finally {
  ws?.close();
  chrome.kill('SIGTERM');
  await new Promise(resolve => { if (chrome.exitCode !== null) resolve(); else chrome.once('exit', resolve); });
  await rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  }
}
