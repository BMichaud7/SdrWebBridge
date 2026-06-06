'use strict';

// ── DOM refs ─────────────────────────────────────────────────────

const $ = id => document.getElementById(id);

const bridgeUrl  = $('bridge-url');
const amqpHost   = $('amqp-host');
const amqpPort   = $('amqp-port');
const amqpUser   = $('amqp-user');
const amqpPass   = $('amqp-pass');
const btnConnect = $('btn-connect');
const statusPill = $('status-pill');

const taskType = $('task-type');
const taskFreq = $('task-freq');
const taskBw   = $('task-bw');
const taskSr   = $('task-sr');
const taskDur  = $('task-dur');
const taskIp   = $('task-ip');
const taskRank = $('task-rank');
const btnSend  = $('btn-send');

const stopTaskId = $('stop-taskid');
const btnStop    = $('btn-stop');

const log      = $('log');
const btnClear = $('btn-clear');

// ── State ────────────────────────────────────────────────────────

let ws        = null;
let connected = false;

// ── WebSocket bridge ─────────────────────────────────────────────

function connect() {
    const url = `ws://${bridgeUrl.value.trim()}`;
    ws = new WebSocket(url);

    ws.onopen = () => {
        appendLog('info', `Bridge connected (${url})`);
        ws.send(JSON.stringify({
            cmd:      'connect',
            host:     amqpHost.value.trim(),
            port:     parseInt(amqpPort.value, 10),
            user:     amqpUser.value.trim(),
            password: amqpPass.value,
        }));
    };

    ws.onmessage = ev => {
        let msg;
        try { msg = JSON.parse(ev.data); } catch { return; }

        if (msg.type === 'status') {
            setConnected(msg.connected);
        } else if (msg.type === 'message') {
            onAmqpMessage(msg.payload);
        } else if (msg.type === 'error') {
            appendLog('error', `Error: ${msg.message}`);
        }
    };

    ws.onclose = () => {
        setConnected(false);
        appendLog('info', 'Bridge disconnected');
        ws = null;
    };

    ws.onerror = () => appendLog('error', `WebSocket error — is bridge running at ${url}?`);
}

function disconnect() {
    if (ws) { ws.send(JSON.stringify({ cmd: 'disconnect' })); ws.close(); ws = null; }
    setConnected(false);
}

function setConnected(state) {
    connected = state;
    statusPill.textContent = state ? '● Connected' : '● Disconnected';
    statusPill.className   = `pill ${state ? 'connected' : 'disconnected'}`;
    btnConnect.textContent = state ? 'Disconnect' : 'Connect';
    btnSend.disabled       = !state;
    btnStop.disabled       = !state;
}

// ── Send task ────────────────────────────────────────────────────

function sendTask() {
    const type  = taskType.value;
    const freqHz = parseFloat(taskFreq.value) * 1e6;
    const bwHz   = parseFloat(taskBw.value)   * 1e6;
    const srSps  = parseFloat(taskSr.value)   * 1e6;
    const durMs  = parseInt(taskDur.value, 10);
    const destIp = taskIp.value.trim();
    const rank   = parseInt(taskRank.value, 10);
    const reqId  = crypto.randomUUID();

    const payload = {
        msg_type:       'TASK_REQUEST',
        schema_version: '2.0',
        request_id:     reqId,
        timestamp_ms:   Date.now(),
        task_type:      type,
        rank,
        schedule:  { mode: 'IMMEDIATE', duration_ms: durMs },
        rf:        { center_freq_hz: freqHz, bandwidth_hz: bwHz, sample_rate_sps: srSps, rx_count: 1 },
        streaming: { dest_ip: destIp },
        ...(type === 'WIDEBAND' ? { wideband: { record_raw_iq: true, fft_size: 2048 } } : {}),
    };

    ws.send(JSON.stringify({ cmd: 'send', payload }));
    appendLog('sent', `SENT [${type}] req_id=${reqId.slice(0,8)}  ${(freqHz/1e6).toFixed(3)} MHz  BW=${bwHz/1e6} MHz  dur=${durMs}ms`);
}

// ── Stop task ────────────────────────────────────────────────────

function stopTask() {
    const taskId = stopTaskId.value.trim();
    if (!taskId) return;
    const payload = {
        msg_type:     'TASK_STOP',
        request_id:   crypto.randomUUID(),
        task_id:      taskId,
        timestamp_ms: Date.now(),
        reason:       'user request',
    };
    ws.send(JSON.stringify({ cmd: 'send', payload }));
    appendLog('sent', `SENT [TASK_STOP] task_id=${taskId}`);
}

// ── AMQP response handler ────────────────────────────────────────

function onAmqpMessage(msg) {
    const status  = msg.status   ?? msg.msg_type ?? '?';
    const taskId  = msg.task_id  ?? null;
    const reason  = msg.reject_reason ?? null;
    const streams = msg.streams ?? [];
    const port    = streams[0]?.udp_port ?? null;

    const cls = status === 'ACCEPTED' ? 'accepted'
              : status === 'REJECTED' ? 'rejected'
              : 'info';

    let text = `RECV [${status}]`;
    if (taskId) text += `  task_id=${taskId.slice(0, 8)}`;
    if (port)   text += `  udp_port=${port}`;
    if (reason) text += `  reason=${reason}`;

    appendLog(cls, text);

    if (taskId && status === 'ACCEPTED') stopTaskId.value = taskId;
}

// ── Log ──────────────────────────────────────────────────────────

function appendLog(cls, text) {
    const ts   = new Date().toLocaleTimeString('en-US', { hour12: false, hour:'2-digit', minute:'2-digit', second:'2-digit', fractionalSecondDigits: 3 });
    const line = document.createElement('div');
    line.className = 'log-line';
    line.innerHTML = `<span class="log-ts">${ts}</span><span class="log-body log-${cls}">${escHtml(text)}</span>`;
    log.appendChild(line);
    log.scrollTop = log.scrollHeight;
}

function escHtml(s) {
    return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// ── Events ───────────────────────────────────────────────────────

btnConnect.addEventListener('click', () => connected || ws ? disconnect() : connect());
btnSend.addEventListener('click',    sendTask);
btnStop.addEventListener('click',    stopTask);
btnClear.addEventListener('click',   () => log.innerHTML = '');
