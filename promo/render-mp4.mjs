// Neemt de promo op in onzichtbare Chrome, codeert met WebCodecs naar H.264 (constrained baseline, 1080x1920, 30 fps)
// en schrijft zelf een standaard MP4 (geen fragmenten, moov vooraan). Geen extra pakketten nodig.
// Gebruik: node render-mp4.mjs "C:/Program Files/Google/Chrome/Application/chrome.exe" "file:///C:/projects/AItraining/VoetbalQuiz/promo/truitje-trek-promo.html" truitje-trek-promo.mp4 <tijdelijke map>
import { spawn } from 'node:child_process';
import { writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';

const [, , chrome, page, outFile, work] = process.argv;
const W = 1080, H = 1920, FPS = 30, SECONDS = 31.5;

const proc = spawn(chrome, [
    '--headless=new', '--remote-debugging-port=9335', `--user-data-dir=${join(work, 'profile2')}`, '--no-first-run',
    '--window-size=1080,1920', '--screen-info={0,0 1200x2200}', '--force-device-scale-factor=1', '--auto-accept-this-tab-capture',
    '--disable-background-timer-throttling', '--disable-renderer-backgrounding', '--disable-backgrounding-occluded-windows', 'about:blank',
], { stdio: 'ignore' });
const sleep = ms => new Promise(r => setTimeout(r, ms));
async function json(path) {
    for (let i = 0; i < 50; i++) { try { return await (await fetch('http://127.0.0.1:9335' + path)).json(); } catch { await sleep(200); } }
    throw new Error('Chrome start niet');
}
function connect(url) {
    const ws = new WebSocket(url);
    let id = 0; const pending = new Map();
    ws.onmessage = e => {
        const m = JSON.parse(e.data);
        if (pending.has(m.id)) { const [ok, ko] = pending.get(m.id); pending.delete(m.id); m.error ? ko(new Error(JSON.stringify(m.error))) : ok(m.result); }
        else if (m.method === 'Runtime.consoleAPICalled') console.log('pagina:', m.params.args.map(a => a.value).join(' '));
    };
    return {
        ready: new Promise(r => ws.onopen = r), close: () => ws.close(),
        send: (method, params = {}) => new Promise((ok, ko) => { const i = ++id; pending.set(i, [ok, ko]); ws.send(JSON.stringify({ id: i, method, params })); }),
    };
}

const CAPTURE = `(async () => {
    const W = ${W}, H = ${H}, FPS = ${FPS};
    document.body.classList.add('rec');
    const stream = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: 60 }, audio: false, preferCurrentTab: true, selfBrowserSurface: 'include' });
    const track = stream.getVideoTracks()[0];
    await track.cropTo(await CropTarget.fromElement(document.getElementById('stage')));
    const reader = new MediaStreamTrackProcessor({ track }).readable.getReader();
    let latest = null;
    (async () => { for (;;) { const { value, done } = await reader.read(); if (done) break; latest?.close(); latest = value; } })();

    const canvas = new OffscreenCanvas(W, H), ctx = canvas.getContext('2d');
    const chunks = []; let desc = null;
    const enc = new VideoEncoder({
        output: (c, meta) => {
            if (meta?.decoderConfig?.description) desc = new Uint8Array(meta.decoderConfig.description);
            const d = new Uint8Array(c.byteLength); c.copyTo(d);
            chunks.push({ key: c.type === 'key', ts: c.timestamp, data: d });
        },
        error: e => console.log('encoder', e.message),
    });
    const config = { codec: 'avc1.42E02A', width: W, height: H, bitrate: 5_000_000, bitrateMode: 'variable', framerate: FPS, avc: { format: 'avc' } };
    const support = await VideoEncoder.isConfigSupported(config);
    if (!support.supported) throw new Error('H.264 niet ondersteund');
    enc.configure(config);

    await new Promise(r => setTimeout(r, 700));
    play();
    const total = Math.round(${SECONDS} * FPS), t0 = performance.now();
    for (let i = 0; i < total; i++) {
        const wait = t0 + i * 1000 / FPS - performance.now();
        if (wait > 0) await new Promise(r => setTimeout(r, wait));
        if (latest) ctx.drawImage(latest, 0, 0, W, H);
        const f = new VideoFrame(canvas, { timestamp: Math.round(i * 1e6 / FPS), duration: Math.round(1e6 / FPS) });
        enc.encode(f, { keyFrame: i % FPS === 0 });
        f.close();
    }
    await enc.flush();
    stream.getTracks().forEach(t => t.stop());

    const blob = new Blob(chunks.map(c => c.data));
    const b64 = await new Promise(r => { const fr = new FileReader(); fr.onload = () => r(fr.result.split(',')[1]); fr.readAsDataURL(blob); });
    const descB64 = btoa(String.fromCharCode(...desc));
    return JSON.stringify({ b64, descB64, sizes: chunks.map(c => c.data.length), keys: chunks.map(c => c.key), ts: chunks.map(c => c.ts) });
})()`;

// ---------- MP4-muxer ----------
const u8 = n => Buffer.from([n & 255]);
const u16 = n => { const b = Buffer.alloc(2); b.writeUInt16BE(n); return b; };
const u32 = n => { const b = Buffer.alloc(4); b.writeUInt32BE(n >>> 0); return b; };
const str = s => Buffer.from(s, 'ascii');
const box = (type, ...parts) => { const body = Buffer.concat(parts); return Buffer.concat([u32(body.length + 8), str(type), body]); };
const full = (type, version, flags, ...parts) => box(type, u8(version), u8(flags >> 16), u16(flags & 0xffff), ...parts);
const MATRIX = Buffer.concat([0x00010000, 0, 0, 0, 0x00010000, 0, 0, 0, 0x40000000].map(u32));

function mux(data, desc, sizes, keys) {
    const n = sizes.length, delta = 1000, timescale = FPS * 1000, durMs = Math.round(n * 1000 / FPS);
    const ftyp = box('ftyp', str('isom'), u32(512), str('isom'), str('iso2'), str('avc1'), str('mp41'));
    const moov = offset => box('moov',
        full('mvhd', 0, 0, u32(0), u32(0), u32(1000), u32(durMs), u32(0x00010000), u16(0x0100), Buffer.alloc(10), MATRIX, Buffer.alloc(24), u32(2)),
        box('trak',
            full('tkhd', 0, 3, u32(0), u32(0), u32(1), u32(0), u32(durMs), Buffer.alloc(8), u16(0), u16(0), u16(0), u16(0), MATRIX, u32(W << 16), u32(H << 16)),
            box('mdia',
                full('mdhd', 0, 0, u32(0), u32(0), u32(timescale), u32(n * delta), u16(0x55c4), u16(0)),
                full('hdlr', 0, 0, u32(0), str('vide'), Buffer.alloc(12), str('VideoHandler\0')),
                box('minf',
                    full('vmhd', 0, 1, u16(0), u16(0), u16(0), u16(0)),
                    box('dinf', full('dref', 0, 0, u32(1), full('url ', 0, 1))),
                    box('stbl',
                        full('stsd', 0, 0, u32(1), box('avc1',
                            Buffer.alloc(6), u16(1), u16(0), u16(0), Buffer.alloc(12), u16(W), u16(H),
                            u32(0x00480000), u32(0x00480000), u32(0), u16(1), Buffer.alloc(32), u16(0x0018), u16(0xffff),
                            box('avcC', desc))),
                        full('stts', 0, 0, u32(1), u32(n), u32(delta)),
                        full('stss', 0, 0, u32(keys.filter(Boolean).length), ...keys.flatMap((k, i) => k ? [u32(i + 1)] : [])),
                        full('stsc', 0, 0, u32(1), u32(1), u32(n), u32(1)),
                        full('stsz', 0, 0, u32(0), u32(n), ...sizes.map(u32)),
                        full('stco', 0, 0, u32(1), u32(offset)))))));
    const moovLen = moov(0).length;
    const offset = ftyp.length + moovLen + 8;
    return Buffer.concat([ftyp, moov(offset), u32(data.length + 8), str('mdat'), data]);
}

try {
    const browser = connect((await json('/json/version')).webSocketDebuggerUrl);
    await browser.ready;
    const target = (await json('/json/list')).find(t => t.type === 'page');
    const tab = connect(target.webSocketDebuggerUrl);
    await tab.ready;
    await tab.send('Runtime.enable');
    const { windowId } = await browser.send('Browser.getWindowForTarget', { targetId: target.id });
    await browser.send('Browser.setWindowBounds', { windowId, bounds: { left: 0, top: 0, width: W, height: H } });
    await tab.send('Page.navigate', { url: page });
    await sleep(2500);
    const res = await tab.send('Runtime.evaluate', { expression: CAPTURE, userGesture: true, awaitPromise: true, returnByValue: true, timeout: 90000 });
    if (res.exceptionDetails) throw new Error(JSON.stringify(res.exceptionDetails).slice(0, 500));
    const { b64, descB64, sizes, keys, ts } = JSON.parse(res.result.value);
    const ordered = ts.every((t, i) => i === 0 || t > ts[i - 1]);
    console.log('frames:', sizes.length, 'keyframes:', keys.filter(Boolean).length, 'volgorde ok:', ordered);
    if (!ordered) throw new Error('Frames niet in volgorde (B-frames?)');
    const mp4 = mux(Buffer.from(b64, 'base64'), Buffer.from(descB64, 'base64'), sizes, keys);
    rmSync(outFile, { force: true });   // ook de Zone.Identifier-markering van het oude bestand weg
    writeFileSync(outFile, mp4);
    console.log('KLAAR', outFile, mp4.length, 'bytes');
    tab.close(); browser.close();
} finally {
    proc.kill();
}
