// Het voetbalveld achter de app: lijnen, af en toe een aanval zoals op een tactiekbord, reacties op je antwoord
// en stadionlichten na zonsondergang. Blazor stuurt het via truitje.pitch (zie Shared/PitchFx.razor).
(() => {
    const NS = 'http://www.w3.org/2000/svg';
    const ROOT = document.documentElement;

    // Zonsondergang en -opgang voor Brussel: zonder toestemming te vragen en goed genoeg voor heel Vlaanderen.
    const LAT = 50.85, LON = 4.35;

    const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)');
    const landscape = matchMedia('(orientation: landscape) and (min-width: 768px)');

    // Coördinaten van een staand veld: zijlijnen op x 6-94, doellijnen op y 6 (boven) en 164 (onder).
    const FIELD = `
        <g class="pf-lines">
            <rect x="6" y="6" width="88" height="158"/>
            <line x1="6" y1="85" x2="94" y2="85"/>
            <circle cx="50" cy="85" r="12"/>
            <rect x="20" y="6" width="60" height="24"/>
            <rect x="36" y="6" width="28" height="8"/>
            <path d="M41.06 30 A12 12 0 0 0 58.94 30"/>
            <rect x="20" y="140" width="60" height="24"/>
            <rect x="36" y="156" width="28" height="8"/>
            <path d="M41.06 140 A12 12 0 0 1 58.94 140"/>
            <path d="M8 6 A2 2 0 0 1 6 8 M92 6 A2 2 0 0 0 94 8 M6 162 A2 2 0 0 1 8 164 M94 162 A2 2 0 0 0 92 164"/>
        </g>
        <g class="pf-spots">
            <circle cx="50" cy="85" r=".9"/>
            <circle cx="50" cy="22" r=".7"/>
            <circle cx="50" cy="148" r=".7"/>
        </g>`;

    // Drie aanvallen, altijd getekend richting het bovenste doel. Een pass of lange bal is gestippeld, een loop
    // met de bal aan de voet doorlopend (zoals op een tactiekbord). Snelheid in veldeenheden per seconde.
    const SPEED = { pass: 32, long: 45, run: 16, cross: 38, shot: 75 };
    const PLAYS = [
        {   // Opbouw door het midden
            players: [[30, 120], [68, 92], [40, 52]],
            moves: [
                { kind: 'pass', from: [30, 120], via: [52, 112], to: [68, 92] },
                { kind: 'pass', from: [68, 92], via: [60, 66], to: [40, 52] },
                { kind: 'shot', from: [40, 52], via: [46, 26], to: [50, 7] },
            ],
        },
        {   // Over de flank: lange bal, de flankspeler loopt door, voorzet en kopbal
            players: [[45, 104], [84, 62], [52, 24]],
            moves: [
                { kind: 'long', from: [45, 104], via: [72, 92], to: [84, 62] },
                { kind: 'run', from: [84, 62], to: [86, 32] },
                { kind: 'cross', from: [86, 32], via: [74, 10], to: [52, 24] },
                { kind: 'shot', from: [52, 24], via: [51, 15], to: [49, 7] },
            ],
        },
        {   // Counter: de doelman trapt ver uit, de spits sprint door en werkt af
            players: [[50, 158], [36, 72]],
            moves: [
                { kind: 'long', from: [50, 158], via: [26, 112], to: [36, 72] },
                { kind: 'run', from: [36, 72], to: [40, 40] },
                { kind: 'shot', from: [40, 40], via: [44, 22], to: [53, 7] },
            ],
        },
    ];

    // Reacties op je antwoord, vanaf de middenstip waar de bal tijdens de vraag ligt te huppen. Sneller dan een
    // gewone aanval: de reactie hoort bij je antwoord.
    const REACTIONS = {
        goal: [
            { kind: 'run', speed: 40, from: [50, 85], to: [50, 26] },
            { kind: 'shot', from: [50, 26], via: [49, 16], to: [51, 7] },
        ],
        post: [
            { kind: 'run', speed: 40, from: [50, 85], to: [44, 28] },
            { kind: 'shot', from: [44, 28], via: [45, 16], to: [46.2, 6.5] },
            { kind: 'rebound', from: [46.2, 6.5], via: [42, 12], to: [33, 22] },
        ],
        out: [
            { kind: 'roll', from: [50, 85], via: [80, 82], to: [108, 66] },
        ],
    };
    Object.assign(SPEED, { rebound: 30, roll: 22 });

    let svg, field, layer;
    let gen = 0;            // elke herstart verhoogt dit; lopende animaties stoppen dan
    let mood = null, moodOwner = null;
    let teams = null, teamsOwner = null;
    let playCount = 0;

    class Cancelled extends Error { }

    const el = (name, attrs = {}, parent = layer) => {
        const node = document.createElementNS(NS, name);
        for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v);
        parent?.appendChild(node);
        return node;
    };

    const frame = () => new Promise(resolve => requestAnimationFrame(resolve));

    async function tween(ms, step, g) {
        const start = performance.now();
        for (; ;) {
            const now = await frame();
            if (g !== gen) throw new Cancelled();
            const t = Math.min(1, (now - start) / ms);
            step(t);
            if (t >= 1) return;
        }
    }

    async function wait(ms, g) {
        await new Promise(resolve => setTimeout(resolve, ms));
        if (g !== gen) throw new Cancelled();
    }

    const easeOut = t => 1 - (1 - t) * (1 - t);
    const lerp = (a, b, t) => a + (b - a) * t;
    const at = (m, t) => {
        const via = m.via ?? [(m.from[0] + m.to[0]) / 2, (m.from[1] + m.to[1]) / 2];
        const u = 1 - t;
        return [u * u * m.from[0] + 2 * u * t * via[0] + t * t * m.to[0],
                u * u * m.from[1] + 2 * u * t * via[1] + t * t * m.to[1]];
    };
    const length = m => {
        let total = 0, prev = m.from;
        for (let i = 1; i <= 20; i++) {
            const p = at(m, i / 20);
            total += Math.hypot(p[0] - prev[0], p[1] - prev[1]);
            prev = p;
        }
        return total;
    };

    // Spiegelen: down = naar het onderste doel, mirror = links en rechts omgewisseld.
    const flip = (p, down, mirror) => p && [mirror ? 100 - p[0] : p[0], down ? 170 - p[1] : p[1]];
    const flipMove = (m, down, mirror) => ({ ...m, from: flip(m.from, down, mirror), via: flip(m.via, down, mirror), to: flip(m.to, down, mirror) });

    function makeBall(pos) {
        const ball = el('g', { class: 'pf-ball', opacity: 0 });
        const inner = el('g', {}, ball);
        el('circle', { r: 1.25, class: 'pf-ball-fill' }, inner);
        el('circle', { r: 1.25, class: 'pf-ball-edge' }, inner);
        moveBall(ball, pos);
        return ball;
    }
    const moveBall = (ball, [x, y]) => ball.setAttribute('transform', `translate(${x.toFixed(2)} ${y.toFixed(2)})`);
    const fade = (node, from, to, ms, g) => tween(ms, t => node.setAttribute('opacity', lerp(from, to, t).toFixed(3)), g);

    // De bal volgt een beweging; de lijn erachter wordt mee getekend.
    async function move(ball, m, color, g, trails = layer) {
        const trail = el('path', { class: `pf-trail ${m.kind}`, stroke: color }, trails);
        const ms = Math.max(350, length(m) / (m.speed ?? SPEED[m.kind]) * 1000);
        await tween(ms, t => {
            const e = m.kind === 'run' ? t : easeOut(t);
            moveBall(ball, at(m, e));
            // De lijn tot waar de bal nu is, berekend uit de curve (niet uit de getekende frames: die kunnen haperen).
            const points = [];
            for (let i = 0; i <= 24; i++) points.push(at(m, e * i / 24));
            trail.setAttribute('d', 'M' + points.map(q => q[0].toFixed(2) + ' ' + q[1].toFixed(2)).join(' L'));
        }, g);
    }

    async function ripple([x, y], g) {
        const ring = el('circle', { cx: x, cy: y, r: 0, class: 'pf-ripple' });
        await tween(700, t => {
            ring.setAttribute('r', (easeOut(t) * 6).toFixed(2));
            ring.setAttribute('opacity', (0.5 * (1 - t)).toFixed(3));
        }, g);
        ring.remove();
    }

    const clear = () => layer.replaceChildren();

    async function runPlay(play, down, mirror, color, g) {
        const group = el('g', { opacity: 0 });
        const trails = el('g', {}, group);
        for (const p of play.players) {
            const [x, y] = flip(p, down, mirror);
            el('circle', { cx: x, cy: y, r: 2.2, class: 'pf-ring', stroke: color }, group);
        }
        const moves = play.moves.map(m => flipMove(m, down, mirror));
        const ball = makeBall(moves[0].from);
        group.appendChild(ball);
        ball.setAttribute('opacity', 1);

        await fade(group, 0, 1, 500, g);
        await wait(500, g);
        for (const [i, m] of moves.entries()) {
            await move(ball, m, color, g, trails);
            if (m.kind === 'shot') ripple(m.to, g).catch(() => { });
            else if (i < moves.length - 1 && moves[i + 1].kind !== 'run') await wait(450, g);
        }
        await fade(ball, 1, 0, 400, g);
        await wait(1400, g);
        await fade(group, 1, 0, 700, g);
        group.remove();
    }

    // Lichter dan de clubkleur zelf, anders verdwijnt paars of donkerblauw in het groen.
    function lighten(hex) {
        const n = parseInt(hex.replace('#', ''), 16);
        const mix = c => Math.round(c + (255 - c) * 0.35);
        return `rgb(${mix(n >> 16 & 255)}, ${mix(n >> 8 & 255)}, ${mix(n & 255)})`;
    }

    async function ambient(g, delay) {
        await wait(delay, g);
        for (; ;) {
            // A↑ B↓ C↑ A↓ B↑ C↓: elke aanval komt om beurten naar boven en naar onder.
            const play = PLAYS[playCount % PLAYS.length];
            const down = playCount % 2 === 1;
            const color = teams ? teams[down ? 1 : 0] : '#fff';
            await runPlay(play, down, Math.random() < 0.5, color, g);
            playCount++;
            await wait(9000, g);
        }
    }

    // Tijdens een vraag: de bal ligt te huppen op de middenstip.
    async function bounce(g) {
        const ball = makeBall([50, 85]);
        ball.firstChild.classList.add('pf-bounce');
        await fade(ball, 0, 1, 500, g);
    }

    async function react(kind, g) {
        const old = layer.querySelector('.pf-ball');
        clear();
        const moves = REACTIONS[kind];
        const ball = makeBall(moves[0].from);
        ball.setAttribute('opacity', old ? 1 : 0);
        if (!old) await fade(ball, 0, 1, 300, g);
        const trails = el('g', { class: 'pf-reaction' });
        layer.insertBefore(trails, ball);
        for (const m of moves) {
            await move(ball, m, '#fff', g, trails);
            if (m.kind === 'shot') ripple(m.to, g).catch(() => { });
        }
        await fade(ball, 1, 0, 400, g);
        await wait(1500, g);
        await fade(trails, 1, 0, 700, g);
        clear();
    }

    function start() {
        const g = ++gen;
        clear();
        if (reduceMotion.matches) return;
        const run = async () => {
            if (mood === 'question') return bounce(g);
            if (REACTIONS[mood]) {
                await react(mood, g);
                return ambient(g, 6000);
            }
            return ambient(g, 2500);
        };
        run().catch(e => { if (!(e instanceof Cancelled)) console.error(e); });
    }

    // Liggend scherm: hetzelfde veld een kwartslag gedraaid (het bovenste doel komt links).
    function orient() {
        if (landscape.matches) {
            svg.setAttribute('viewBox', '-20 0 210 100');
            field.setAttribute('transform', 'translate(0 100) rotate(-90)');
        } else {
            // Weinig ruimte boven de bovenste doellijn: die valt achter de topbar, zodat het veld bovenaan al begint.
            svg.setAttribute('viewBox', '0 -8 100 210');
            field.removeAttribute('transform');
        }
    }

    // Zonsopgang en -ondergang (in UTC-uren) volgens het algoritme van de US Naval Observatory.
    function sun(date, rising) {
        const rad = Math.PI / 180;
        const start = Date.UTC(date.getUTCFullYear(), 0, 0);
        const day = Math.floor((date - start) / 86400000);
        const lngHour = LON / 15;
        const t = day + ((rising ? 6 : 18) - lngHour) / 24;
        const M = 0.9856 * t - 3.289;
        let L = M + 1.916 * Math.sin(M * rad) + 0.020 * Math.sin(2 * M * rad) + 282.634;
        L = (L + 360) % 360;
        let RA = Math.atan(0.91764 * Math.tan(L * rad)) / rad;
        RA = (RA + 360) % 360;
        RA = (RA + Math.floor(L / 90) * 90 - Math.floor(RA / 90) * 90) / 15;
        const sinDec = 0.39782 * Math.sin(L * rad);
        const cosDec = Math.cos(Math.asin(sinDec));
        const cosH = (Math.cos(90.833 * rad) - sinDec * Math.sin(LAT * rad)) / (cosDec * Math.cos(LAT * rad));
        let H = Math.acos(Math.max(-1, Math.min(1, cosH))) / rad;
        if (rising) H = 360 - H;
        const T = H / 15 + RA - 0.06571 * t - 6.622;
        return ((T - lngHour) % 24 + 24) % 24;
    }

    // Stadionlichten: uit elke hoek een lichtbundel richting de middenstip. De hoek hangt af van de vorm van het scherm.
    function buildLights(host) {
        const lights = document.createElement('div');
        lights.className = 'pf-lights';
        for (const corner of ['tl', 'tr', 'bl', 'br']) {
            for (const part of ['beam', 'lamp', 'bank']) {
                const span = document.createElement('span');
                span.className = `pf-${part} ${corner}`;
                lights.appendChild(span);
            }
        }
        host.appendChild(lights);
        const aim = () => {
            const a = Math.atan2(innerWidth, innerHeight) * 180 / Math.PI;
            lights.style.setProperty('--tl', `${180 - a}deg`);
            lights.style.setProperty('--tr', `${180 + a}deg`);
            lights.style.setProperty('--bl', `${a}deg`);
            lights.style.setProperty('--br', `${360 - a}deg`);
        };
        aim();
        addEventListener('resize', aim);
    }

    function updateNight() {
        const now = new Date();
        const hour = now.getUTCHours() + now.getUTCMinutes() / 60;
        const night = hour < sun(now, true) || hour > sun(now, false);
        ROOT.classList.toggle('pitch-night', night);
    }

    function build() {
        const host = document.createElement('div');
        host.className = 'pitch-fx';
        host.setAttribute('aria-hidden', 'true');
        svg = el('svg', { preserveAspectRatio: 'xMidYMid slice', focusable: 'false' }, host);
        field = el('g', {}, svg);
        field.innerHTML = FIELD;
        layer = el('g', { class: 'pf-play' }, field);
        buildLights(host);
        document.body.prepend(host);

        orient();
        updateNight();
        setInterval(updateNight, 10 * 60 * 1000);
        document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') updateNight(); });
        landscape.addEventListener('change', orient);
        reduceMotion.addEventListener('change', start);
        start();
    }

    window.truitje ??= {};
    window.truitje.pitch = {
        /** "question" (bal hupt), "goal", "post", "out" of null (gewone aanvallen). Owner: wie het zette. */
        mood(value, owner) {
            moodOwner = owner;
            if (value === mood) return;
            mood = value;
            start();
        },
        /** De clubkleuren van een affiche (["#5b2a86", "#1f4fa3"]) of null voor wit. */
        teams(colors, owner) {
            teamsOwner = owner;
            teams = colors?.length === 2 ? colors.map(lighten) : null;
        },
        /** Een component verdwijnt: zet terug wat het zelf gezet had, niet wat een nieuwe pagina intussen zette. */
        release(owner) {
            if (moodOwner === owner) { moodOwner = null; if (mood !== null) { mood = null; start(); } }
            if (teamsOwner === owner) { teamsOwner = null; teams = null; }
        },
    };

    build();
})();
