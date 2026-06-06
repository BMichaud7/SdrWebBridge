'use strict';

const WebSocket = require('ws');
const rhea      = require('rhea');

const WS_PORT = parseInt(process.env.SDR_BRIDGE_PORT ?? '8765', 10);
const wss     = new WebSocket.Server({ port: WS_PORT });

console.log(`SDR bridge listening on ws://localhost:${WS_PORT}`);

wss.on('connection', ws => {
    let amqpConn   = null;
    let sender     = null;

    function send(obj) {
        if (ws.readyState === WebSocket.OPEN)
            ws.send(JSON.stringify(obj));
    }

    ws.on('message', raw => {
        let msg;
        try { msg = JSON.parse(raw); } catch { return; }

        if (msg.cmd === 'connect') {
            if (amqpConn) { amqpConn.close(); amqpConn = sender = null; }

            const container = rhea.create_container();

            container.on('connection_open', ctx => {
                sender = ctx.connection.open_sender('sdr.task.request');
                ctx.connection.open_receiver('sdr.task.response');
                send({ type: 'status', connected: true });
            });

            container.on('message', ctx => {
                try {
                    const body = ctx.message.body;
                    const payload = typeof body === 'string' ? JSON.parse(body)
                                  : Buffer.isBuffer(body)   ? JSON.parse(body.toString())
                                  : body;
                    send({ type: 'message', payload });
                } catch { /* malformed response — ignore */ }
            });

            container.on('connection_close', () => send({ type: 'status', connected: false }));
            container.on('disconnected',     () => send({ type: 'status', connected: false }));
            container.on('error',       ctx => send({ type: 'error', message: String(ctx.error ?? ctx) }));

            amqpConn = container.connect({
                host:     msg.host     ?? 'localhost',
                port:     msg.port     ?? 5672,
                username: msg.user     ?? 'sdr_ctrl',
                password: msg.password ?? 'sdr_hw_test',
                transport: 'tcp',
            });

        } else if (msg.cmd === 'send') {
            if (!sender) { send({ type: 'error', message: 'Not connected' }); return; }
            try {
                sender.send({ body: JSON.stringify(msg.payload),
                              content_type: 'application/json' });
            } catch (e) { send({ type: 'error', message: String(e) }); }

        } else if (msg.cmd === 'disconnect') {
            if (amqpConn) { amqpConn.close(); amqpConn = sender = null; }
        }
    });

    ws.on('close', () => { if (amqpConn) { amqpConn.close(); } });
});
