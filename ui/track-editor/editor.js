'use strict';
/* Edytor torów (PP-048, PP-049). Czysty JS, bez builda.
   Geometria jest portem src/Paddock.Domain/World/Tracks/TrackGeometry.cs.
   Model samochodu to SZACUNEK (do kalibracji), nie fakty. */

const $ = (s) => document.querySelector(s);

// ---------- Stałe modelu (SZACUNEK) ----------
const CFG = { a0: 28, c: 0.0028, vmaxKmh: 350, acc: 11, brake: 40 };
const CORNER_MIN_PROMINENCE_KMH = 15; // SZACUNEK
const BRAKE_MIN_DECEL = 10;           // m/s², SZACUNEK: od tylu uznajemy odcinek za hamowanie
const CLASSES = [
  { id: 'slow',   name: 'Wolne',     max: 150,      color: '#e5533d' },
  { id: 'medium', name: 'Średnie',   max: 230,      color: '#e9b23c' },
  { id: 'fast',   name: 'Szybkie',   max: 310,      color: '#3fb8c9' },
  { id: 'flat',   name: 'Pełny gaz', max: Infinity, color: '#7bd88f' },
];

// ---------- Geometria: port TrackGeometry.cs ----------
function dist(ax, ay, bx, by) { return Math.sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); }

function catmull(p0, p1, p2, p3, u) {
  const d01 = dist(p0[0], p0[1], p1[0], p1[1]);
  const d12 = dist(p1[0], p1[1], p2[0], p2[1]);
  const d23 = dist(p2[0], p2[1], p3[0], p3[1]);
  const t0 = 0, t1 = t0 + Math.sqrt(d01), t2 = t1 + Math.sqrt(d12), t3 = t2 + Math.sqrt(d23);
  const t = t1 + u * (t2 - t1);
  const dt01 = t1 - t0, dt12 = t2 - t1, dt23 = t3 - t2;
  const lerp = (a, b, ta, tb, dt, alt) => dt > 1e-12
    ? [((tb - t) * a[0] + (t - ta) * b[0]) / dt, ((tb - t) * a[1] + (t - ta) * b[1]) / dt] : alt;
  const a1 = lerp(p0, p1, t0, t1, dt01, p1);
  const a2 = lerp(p1, p2, t1, t2, dt12, p1);
  const a3 = lerp(p2, p3, t2, t3, dt23, p2);
  const dt02 = t2 - t0, dt13 = t3 - t1;
  const b1 = lerp(a1, a2, t0, t2, dt02, a2);
  const b2 = lerp(a2, a3, t1, t3, dt13, a2);
  return dt12 > 1e-12 ? lerp(b1, b2, t1, t2, dt12, p1) : p1;
}

/** Gęste próbkowanie splajnu. micro = maks. odstęp (jednostki), maxSteps = limit na segment. */
function sampleSplineDense(pts, micro, maxSteps) {
  const n = pts.length;
  const xs = [pts[0][0]], ys = [pts[0][1]], len = [0], seg = [0];
  let cum = 0, px = pts[0][0], py = pts[0][1];
  for (let i = 0; i < n; i++) {
    const p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n];
    const chord = dist(p1[0], p1[1], p2[0], p2[1]);
    const steps = Math.min(maxSteps, Math.max(20, Math.ceil(chord / micro)));
    for (let m = 1; m <= steps; m++) {
      const pt = catmull(p0, p1, p2, p3, m / steps);
      cum += dist(px, py, pt[0], pt[1]);
      xs.push(pt[0]); ys.push(pt[1]); len.push(cum); seg.push(i);
      px = pt[0]; py = pt[1];
    }
  }
  return { xs, ys, len, seg };
}

/** Odpowiednik TrackGeometry.Build. micro = krok mikro w metrach (0.05 w C#). */
function buildGeometry(pts, refLen, micro = 0.05, stepM = 2.0) {
  if (!pts || pts.length < 3) return null;
  // Surowa długość (gruby przebieg, niezależny od jednostek).
  const raw = sampleSplineDense(pts, 0.5, 300);
  const rawLen = raw.len[raw.len.length - 1];
  if (!(rawLen > 1e-9)) return null;
  const sf = refLen ? refLen / rawLen : 1;
  const finalLen = refLen || rawLen;
  const sp = Math.abs(sf - 1) > 1e-12 ? pts.map((p) => [p[0] * sf, p[1] * sf]) : pts;
  const dense = sampleSplineDense(sp, micro, 4000);
  const count = Math.max(8, Math.round(finalLen / stepM));
  const ds = finalLen / count;
  const xs = new Float64Array(count), ys = new Float64Array(count);
  let di = 0;
  const total = dense.xs.length;
  for (let k = 0; k < count; k++) {
    const target = k * ds;
    while (di < total - 1 && dense.len[di + 1] < target) di++;
    if (di >= total - 1) { xs[k] = dense.xs[total - 1]; ys[k] = dense.ys[total - 1]; }
    else {
      const a = dense.len[di], b = dense.len[di + 1], span = b - a;
      const f = span > 1e-12 ? (target - a) / span : 0;
      xs[k] = dense.xs[di] + f * (dense.xs[di + 1] - dense.xs[di]);
      ys[k] = dense.ys[di] + f * (dense.ys[di + 1] - dense.ys[di]);
    }
  }
  // Krzywizna: zmiana kursu na +-6 próbek.
  const head = new Float64Array(count), curv = new Float64Array(count);
  for (let i = 0; i < count; i++) {
    const a = (i - 1 + count) % count, b = (i + 1) % count;
    head[i] = Math.atan2(ys[b] - ys[a], xs[b] - xs[a]);
  }
  const distance = 12 * ds;
  for (let i = 0; i < count; i++) {
    let d = head[(i + 6) % count] - head[(i - 6 + count) % count];
    while (d > Math.PI) d -= 2 * Math.PI;
    while (d < -Math.PI) d += 2 * Math.PI;
    curv[i] = d / distance;
  }
  return { length: finalLen, rawLength: rawLen, scale: sf, ds, xs, ys, curv, count };
}

// ---------- Analiza (prosty model samochodu, SZACUNEK) ----------
function classOf(kmh) { return CLASSES.findIndex((c) => kmh < c.max); }

function analyze(geo) {
  const n = geo.count, ds = geo.ds;
  const vmax = CFG.vmaxKmh / 3.6;
  const lim = new Float64Array(n);
  for (let i = 0; i < n; i++) {
    const k = Math.abs(geo.curv[i]);
    if (k < 1e-9) { lim[i] = vmax; continue; }
    const r = 1 / k, den = 1 - CFG.c * r;
    lim[i] = den > 0 ? Math.min(vmax, Math.sqrt(CFG.a0 * r / den)) : vmax;
  }
  let start = 0;
  for (let i = 1; i < n; i++) if (lim[i] < lim[start]) start = i;
  const v = Float64Array.from(lim);
  for (let s = 0; s < n; s++) { // przód: przyspieszanie
    const i = (start + s) % n, j = (i + 1) % n;
    const a = CFG.acc * (1 - (v[i] / vmax) * (v[i] / vmax));
    const vn = Math.sqrt(v[i] * v[i] + 2 * Math.max(a, 0) * ds);
    if (vn < v[j]) v[j] = vn;
  }
  for (let s = 0; s < n; s++) { // tył: hamowanie
    const i = (start - s + n) % n, p = (i - 1 + n) % n;
    const vp = Math.sqrt(v[i] * v[i] + 2 * CFG.brake * ds);
    if (vp < v[p]) v[p] = vp;
  }
  let lap = 0, top = 0;
  const cls = new Uint8Array(n);
  const share = CLASSES.map(() => 0);
  for (let i = 0; i < n; i++) {
    const j = (i + 1) % n;
    lap += ds / (0.5 * (v[i] + v[j]));
    top = Math.max(top, v[i]);
    cls[i] = classOf(v[i] * 3.6);
    share[cls[i]] += 1;
  }
  const kmh = Array.from(v, (x) => x * 3.6);
  // Zakręty = minima prędkości z wybitnością.
  const corners = [];
  for (let i = 0; i < n; i++) {
    const a = kmh[(i - 1 + n) % n], b = kmh[i], c = kmh[(i + 1) % n];
    if (!(b < a && b <= c) || b >= CLASSES[2].max) continue;
    let lmax = b, rmax = b;
    for (let k = 1; k < n; k++) { const x = kmh[(i - k + n) % n]; if (x < b) break; if (x > lmax) lmax = x; }
    for (let k = 1; k < n; k++) { const x = kmh[(i + k) % n]; if (x < b) break; if (x > rmax) rmax = x; }
    if (Math.min(lmax, rmax) - b >= CORNER_MIN_PROMINENCE_KMH) corners.push({ i, dist: i * ds, kmh: b });
  }
  // Strefy hamowania.
  const br = new Uint8Array(n);
  for (let i = 0; i < n; i++) br[i] = ((v[i] * v[i] - v[(i + 1) % n] * v[(i + 1) % n]) / (2 * ds) >= BRAKE_MIN_DECEL) ? 1 : 0;
  const brakes = [];
  let s0 = br.indexOf(0);
  if (s0 >= 0) {
    const runs = [];
    let cur = null;
    for (let t = 0; t < n; t++) {
      const i = (s0 + t) % n;
      if (br[i]) { if (!cur) cur = { a: i, b: i, t0: t, t1: t }; else { cur.b = i; cur.t1 = t; } }
      else if (cur) { runs.push(cur); cur = null; }
    }
    if (cur) runs.push(cur);
    const gap = 10; // próbek (20 m)
    const merged = [];
    for (const r of runs) {
      const last = merged[merged.length - 1];
      if (last && r.t0 - last.t1 - 1 <= gap) { last.b = r.b; last.t1 = r.t1; } else merged.push({ ...r });
    }
    for (const r of merged) {
      const cnt = r.t1 - r.t0 + 1;
      if (cnt < 4) continue;
      const e = (r.b + 1) % n;
      brakes.push({ a: r.a, b: e, start: r.a * ds, end: e * ds, length: cnt * ds, from: kmh[r.a], to: kmh[e] });
    }
    brakes.sort((x, y) => x.start - y.start);
  }
  return { v, kmh, cls, share: share.map((c) => c / n), lap, top: top * 3.6, corners, brakes, br };
}

// ---------- Walidacja (jak walidator C#) ----------
function segIntersect(ax, ay, bx, by, cx, cy, dx, dy) {
  const o = (px, py, qx, qy, rx, ry) => (qx - px) * (ry - py) - (qy - py) * (rx - px);
  const d1 = o(ax, ay, bx, by, cx, cy), d2 = o(ax, ay, bx, by, dx, dy);
  const d3 = o(cx, cy, dx, dy, ax, ay), d4 = o(cx, cy, dx, dy, bx, by);
  return ((d1 > 0) !== (d2 > 0)) && ((d3 > 0) !== (d4 > 0)) && d1 !== 0 && d2 !== 0 && d3 !== 0 && d4 !== 0;
}

function findSelfIntersections(pts, stepsPerSeg = 12) {
  const n = pts.length, out = [];
  if (n < 3) return out;
  const px = [], py = [];
  for (let i = 0; i < n; i++) {
    const p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n];
    for (let m = 0; m < stepsPerSeg; m++) { const q = catmull(p0, p1, p2, p3, m / stepsPerSeg); px.push(q[0]); py.push(q[1]); }
  }
  const N = px.length;
  for (let i = 0; i < N; i++) {
    const i2 = (i + 1) % N;
    for (let j = i + 2; j < N; j++) {
      if (i === 0 && j === N - 1) continue;
      const j2 = (j + 1) % N;
      if (Math.max(px[i], px[i2]) < Math.min(px[j], px[j2]) || Math.max(px[j], px[j2]) < Math.min(px[i], px[i2])) continue;
      if (Math.max(py[i], py[i2]) < Math.min(py[j], py[j2]) || Math.max(py[j], py[j2]) < Math.min(py[i], py[i2])) continue;
      if (segIntersect(px[i], py[i], px[i2], py[i2], px[j], py[j], px[j2], py[j2])) {
        out.push([px[i], py[i]]);
        if (out.length >= 8) return out;
      }
    }
  }
  return out;
}

// ---------- Stan ----------
const S = {
  layoutId: 'new_track', source: '', notes: '', extra: {},
  points: [], sel: -1, tool: 'add', targetKm: null, layouts: [],
  img: null, imgOpacity: 0.5, imgScale: 1, imgX: 0, imgY: 0,
  view: { cx: 0, cy: 0, zoom: 1 },
  undo: [], redo: [], trace: null, dragging: false,
  geo: null, an: null, inter: [], curveCache: null,
};

const cv = $('#cv'), ctx = cv.getContext('2d');
let W = 800, H = 600, DPR = 1;

// ---------- Pomocnicze formaty ----------
const nf = (d) => new Intl.NumberFormat('pl-PL', { minimumFractionDigits: d, maximumFractionDigits: d });
const fInt = (x) => nf(0).format(Math.round(x));
const f1 = (x) => nf(1).format(x);
function plural(n, one, few, many) {
  const a = Math.abs(n);
  if (a === 1) return one;
  const m10 = a % 10, m100 = a % 100;
  return (m10 >= 2 && m10 <= 4 && !(m100 >= 12 && m100 <= 14)) ? few : many;
}
function fTime(s) {
  const m = Math.floor(s / 60), r = s - m * 60;
  return m + ':' + (r < 10 ? '0' : '') + r.toFixed(1);
}
function esc(s) { return String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }

// ---------- Widok ----------
const toScreen = (x, y) => [(x - S.view.cx) * S.view.zoom + W / 2, H / 2 - (y - S.view.cy) * S.view.zoom];
const toWorld = (sx, sy) => [(sx - W / 2) / S.view.zoom + S.view.cx, (H / 2 - sy) / S.view.zoom + S.view.cy];

function fitView() {
  let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
  const add = (x, y) => { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); };
  if (S.points.length) for (const p of S.points) add(p[0], p[1]);
  else if (S.img) { add(S.imgX, S.imgY); add(S.imgX + S.img.width * S.imgScale, S.imgY - S.img.height * S.imgScale); }
  else { add(-500, -300); add(500, 300); }
  const w = Math.max(x1 - x0, 1e-6), h = Math.max(y1 - y0, 1e-6);
  S.view.cx = (x0 + x1) / 2; S.view.cy = (y0 + y1) / 2;
  S.view.zoom = 0.88 * Math.min(W / w, H / h);
  schedule();
}

// ---------- Undo ----------
const snap = () => JSON.stringify({ p: S.points, s: S.imgScale, x: S.imgX, y: S.imgY, l: S.layoutId });
function pushUndo() { S.undo.push(snap()); if (S.undo.length > 100) S.undo.shift(); S.redo.length = 0; updateButtons(); }
function restore(str) {
  const o = JSON.parse(str);
  S.points = o.p; S.imgScale = o.s; S.imgX = o.x; S.imgY = o.y; S.layoutId = o.l;
  if (S.sel >= S.points.length) S.sel = -1;
}
function undo() { if (!S.undo.length) return; S.redo.push(snap()); restore(S.undo.pop()); S.trace = S.trace && null; changed(); }
function redo() { if (!S.redo.length) return; S.undo.push(snap()); restore(S.redo.pop()); changed(); }
function updateButtons() { $('#btnUndo').disabled = !S.undo.length; $('#btnRedo').disabled = !S.redo.length; }

// ---------- Przeliczenie ----------
let scheduled = false;
function schedule() { if (scheduled) return; scheduled = true; requestAnimationFrame(() => { scheduled = false; recompute(); draw(); }); }
function changed() { syncFields(); schedule(); }

function targetLenM() { return S.targetKm > 0 ? S.targetKm * 1000 : null; }
function knownLayout() { return S.layouts.find((l) => l.id === S.layoutId) || null; }

function recompute() {
  const pts = S.points;
  S.geo = null; S.an = null; S.inter = []; S.curveCache = null;
  if (pts.length >= 3) {
    S.geo = buildGeometry(pts, targetLenM(), S.dragging ? 0.5 : 0.05);
    if (S.geo) S.an = analyze(S.geo);
    S.inter = findSelfIntersections(pts, S.dragging ? 6 : 12);
    // Cache krzywej do wstawiania punktów (24 próbki na segment).
    const n = pts.length, cache = [];
    for (let i = 0; i < n; i++) {
      const p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n];
      for (let m = 0; m < 24; m++) { const q = catmull(p0, p1, p2, p3, m / 24); cache.push({ x: q[0], y: q[1], seg: i }); }
    }
    S.curveCache = cache;
  }
  updatePanel();
}

// ---------- Rysowanie ----------
function resize() {
  const r = $('#wrap').getBoundingClientRect();
  DPR = window.devicePixelRatio || 1;
  W = Math.max(50, r.width); H = Math.max(50, r.height);
  cv.width = Math.round(W * DPR); cv.height = Math.round(H * DPR);
  schedule();
}

function niceStep(x) {
  const e = Math.pow(10, Math.floor(Math.log10(x))), m = x / e;
  return (m < 1.5 ? 1 : m < 3.5 ? 2 : m < 7.5 ? 5 : 10) * e;
}

function draw() {
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  ctx.clearRect(0, 0, W, H);
  const z = S.view.zoom;
  // Tło
  if (S.img) {
    const [ox, oy] = toScreen(S.imgX, S.imgY);
    ctx.save();
    ctx.globalAlpha = S.imgOpacity;
    ctx.translate(ox, oy); ctx.scale(S.imgScale * z, S.imgScale * z);
    ctx.drawImage(S.img, 0, 0);
    ctx.restore();
  }
  // Osie przez (0,0)
  ctx.lineWidth = 1; ctx.strokeStyle = 'rgba(242,232,213,.08)';
  const [zx, zy] = toScreen(0, 0);
  ctx.beginPath(); ctx.moveTo(zx, 0); ctx.lineTo(zx, H); ctx.moveTo(0, zy); ctx.lineTo(W, zy); ctx.stroke();

  const pts = S.points;
  ctx.lineJoin = 'round'; ctx.lineCap = 'round';
  if (S.geo && S.an) {
    const g = S.geo, an = S.an, n = g.count, sf = g.scale;
    const P = (i) => toScreen(g.xs[i % n] / sf, g.ys[i % n] / sf);
    ctx.strokeStyle = 'rgba(0,0,0,.55)'; ctx.lineWidth = 7;
    ctx.beginPath();
    for (let i = 0; i <= n; i++) { const p = P(i); if (i) ctx.lineTo(p[0], p[1]); else ctx.moveTo(p[0], p[1]); }
    ctx.stroke();
    ctx.lineWidth = 4;
    let i = 0;
    while (i < n) {
      const c = an.cls[i]; let j = i;
      while (j < n && an.cls[j] === c) j++;
      ctx.strokeStyle = CLASSES[c].color;
      ctx.beginPath();
      for (let k = i; k <= j; k++) { const p = P(k); if (k > i) ctx.lineTo(p[0], p[1]); else ctx.moveTo(p[0], p[1]); }
      ctx.stroke();
      i = j;
    }
    // Hamowanie: cienka biała linia
    ctx.strokeStyle = 'rgba(255,255,255,.9)'; ctx.lineWidth = 1.2;
    for (const b of an.brakes) {
      ctx.beginPath();
      let k = b.a, first = true, guard = 0;
      while (guard++ <= n) {
        const p = P(k);
        if (first) { ctx.moveTo(p[0], p[1]); first = false; } else ctx.lineTo(p[0], p[1]);
        if (k === b.b) break;
        k = (k + 1) % n;
      }
      ctx.stroke();
    }
    // Numery zakrętów
    ctx.font = '700 11px Archivo, sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    an.corners.forEach((c, idx) => {
      const a = (c.i - 1 + n) % n, b = (c.i + 1) % n;
      let tx = g.xs[b] - g.xs[a], ty = g.ys[b] - g.ys[a];
      const tl = Math.hypot(tx, ty) || 1; tx /= tl; ty /= tl;
      const sg = g.curv[c.i] >= 0 ? -1 : 1; // na zewnątrz łuku
      const nx = -ty * sg, ny = tx * sg;
      const p = P(c.i);
      const x = p[0] + nx * 17, y = p[1] - ny * 17;
      ctx.fillStyle = '#14100d'; ctx.strokeStyle = '#f2e8d5'; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.arc(x, y, 9, 0, 6.2832); ctx.fill(); ctx.stroke();
      ctx.fillStyle = '#f2e8d5'; ctx.fillText(String(idx + 1), x, y + 0.5);
    });
  } else if (pts.length >= 2) {
    ctx.strokeStyle = 'rgba(242,232,213,.55)'; ctx.lineWidth = 2; ctx.setLineDash([6, 5]);
    ctx.beginPath();
    pts.forEach((p, i) => { const s = toScreen(p[0], p[1]); if (i) ctx.lineTo(s[0], s[1]); else ctx.moveTo(s[0], s[1]); });
    ctx.stroke(); ctx.setLineDash([]);
  }
  // Przecięcia
  ctx.strokeStyle = '#e5533d'; ctx.lineWidth = 2.5;
  for (const q of S.inter) { const s = toScreen(q[0], q[1]); ctx.beginPath(); ctx.arc(s[0], s[1], 11, 0, 6.2832); ctx.stroke(); }
  // Punkty kontrolne
  ctx.font = '600 10px Archivo, sans-serif'; ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  pts.forEach((p, i) => {
    const [x, y] = toScreen(p[0], p[1]);
    const sel = i === S.sel;
    ctx.fillStyle = sel ? '#e8553d' : '#1c1612'; ctx.strokeStyle = '#f2e8d5'; ctx.lineWidth = 1.5;
    ctx.beginPath();
    if (i === 0) ctx.rect(x - 6, y - 6, 12, 12); else ctx.arc(x, y, sel ? 7 : 5, 0, 6.2832);
    ctx.fill(); ctx.stroke();
    if (pts.length <= 80 || sel) { ctx.fillStyle = 'rgba(242,232,213,.8)'; ctx.fillText(String(i + 1), x + 8, y - 8); }
  });
  // Strzałka kierunku przy punkcie 1
  if (pts.length >= 2) {
    const a = toScreen(pts[0][0], pts[0][1]), b = toScreen(pts[1][0], pts[1][1]);
    const ang = Math.atan2(b[1] - a[1], b[0] - a[0]);
    const cx = a[0] + Math.cos(ang) * 24, cy = a[1] + Math.sin(ang) * 24;
    ctx.fillStyle = '#f2e8d5'; ctx.beginPath();
    ctx.moveTo(cx + Math.cos(ang) * 8, cy + Math.sin(ang) * 8);
    ctx.lineTo(cx + Math.cos(ang + 2.5) * 7, cy + Math.sin(ang + 2.5) * 7);
    ctx.lineTo(cx + Math.cos(ang - 2.5) * 7, cy + Math.sin(ang - 2.5) * 7);
    ctx.closePath(); ctx.fill();
  }
  // Podziałka
  const step = niceStep(110 / z), wpx = step * z;
  const unit = S.geo && Math.abs(S.geo.scale - 1) < 1e-6 ? ' m' : ' j.';
  ctx.strokeStyle = '#f2e8d5'; ctx.lineWidth = 2; ctx.beginPath();
  ctx.moveTo(16, H - 20); ctx.lineTo(16 + wpx, H - 20); ctx.moveTo(16, H - 25); ctx.lineTo(16, H - 15); ctx.moveTo(16 + wpx, H - 25); ctx.lineTo(16 + wpx, H - 15); ctx.stroke();
  ctx.fillStyle = '#f2e8d5'; ctx.font = '600 12px Archivo, sans-serif'; ctx.fillText(fInt(step) + unit, 16, H - 30);
}

// ---------- Panel ----------
function chip(state, text) { return `<span class="chip ${state}"><i></i>${esc(text)}</span>`; }

function updatePanel() {
  const pts = S.points, n = pts.length;
  // Chipsy walidacji
  const chips = [];
  chips.push(chip(n >= 8 ? 'ok' : 'bad', `${n} ${plural(n, 'punkt', 'punkty', 'punktów')} (min. 8)`));
  let minGap = Infinity;
  for (let i = 0; i < n; i++) { const a = pts[i], b = pts[(i + 1) % n]; if (n > 1) minGap = Math.min(minGap, dist(a[0], a[1], b[0], b[1])); }
  if (n > 1) chips.push(chip(minGap >= 1 ? 'ok' : 'bad', `Odstęp min. ${f1(minGap)} m`));
  const raw = S.geo ? S.geo.rawLength : 0;
  const kl = knownLayout();
  if (S.geo && kl && kl.lengthKm > 0) {
    const dev = raw / (kl.lengthKm * 1000) - 1;
    chips.push(chip(Math.abs(dev) <= 0.15 ? 'ok' : 'bad', `Długość ${dev >= 0 ? '+' : '−'}${f1(Math.abs(dev) * 100)}% względem wzorca`));
  } else if (S.geo) chips.push(chip('warn', 'Długość bez wzorca'));
  if (n >= 3) chips.push(chip(S.inter.length ? 'bad' : 'ok', S.inter.length ? `Przecięcia: ${S.inter.length}${S.inter.length >= 8 ? '+' : ''}` : 'Bez przecięć'));
  $('#chips').innerHTML = chips.join('');

  // Skala
  const tgt = targetLenM();
  $('#rawLen').textContent = S.geo ? fInt(raw) + ' m' : '-';
  $('#tgtLen').textContent = tgt ? fInt(tgt) + ' m' : '-';
  $('#scaleF').textContent = S.geo && tgt ? '×' + nf(4).format(tgt / raw) : '-';

  // Analiza
  const an = S.an;
  if (!an) {
    $('#stats').innerHTML = '<div><span>Długość</span><b>-</b></div><div><span>Czas okrążenia</span><b>-</b></div><div><span>Prędkość maks.</span><b>-</b></div><div><span>Zakręty</span><b>-</b></div>';
    $('#shareBar').innerHTML = ''; $('#legend').innerHTML = '';
    $('#cornerList').innerHTML = '<div class="empty">Brak danych</div>'; $('#brakeList').innerHTML = '<div class="empty">Brak danych</div>';
  } else {
    $('#stats').innerHTML =
      `<div><span>Długość</span><b>${fInt(S.geo.length)} m</b></div>` +
      `<div><span>Czas okrążenia</span><b>${fTime(an.lap)}</b></div>` +
      `<div><span>Prędkość maks.</span><b>${fInt(an.top)} km/h</b></div>` +
      `<div><span>Zakręty</span><b>${an.corners.length}</b></div>`;
    $('#shareBar').innerHTML = CLASSES.map((c, i) => `<div style="width:${an.share[i] * 100}%;background:${c.color}"></div>`).join('');
    $('#legend').innerHTML = CLASSES.map((c, i) => `<div><i style="background:${c.color}"></i>${c.name}<b>${fInt(an.share[i] * 100)}%</b></div>`).join('');
    $('#cornerList').innerHTML = an.corners.length
      ? an.corners.map((c, i) => `<div class="it"><b>${i + 1}</b><span>${fInt(c.kmh)} km/h</span><span>${fInt(c.dist)} m</span></div>`).join('')
      : '<div class="empty">Brak zakrętów</div>';
    $('#brakeList').innerHTML = an.brakes.length
      ? an.brakes.map((b, i) => `<div class="it"><b>${i + 1}</b><span>${fInt(b.start)} – ${fInt(b.end)} m</span><span>${fInt(b.from)} → ${fInt(b.to)} km/h</span></div>`).join('')
      : '<div class="empty">Brak stref</div>';
  }
  // Zaznaczony punkt
  const has = S.sel >= 0 && S.sel < n;
  for (const id of ['ptX', 'ptY']) $('#' + id).disabled = !has;
  $('#btnDel').disabled = !has; $('#btnStart').disabled = !has || S.sel === 0;
  if (has && document.activeElement !== $('#ptX') && document.activeElement !== $('#ptY')) {
    $('#ptX').value = round3(pts[S.sel][0]); $('#ptY').value = round3(pts[S.sel][1]);
  } else if (!has) { $('#ptX').value = ''; $('#ptY').value = ''; }
  updateButtons();
}
const round3 = (x) => { const r = Math.round(x * 1000) / 1000; return r === 0 ? 0 : r; };

function syncFields() {
  const set = (id, v) => { const el = $(id); if (document.activeElement !== el) el.value = v; };
  set('#layoutId', S.layoutId); set('#source', S.source); set('#notes', S.notes);
  set('#imgScale', S.img ? +S.imgScale.toPrecision(6) : ''); set('#imgX', S.img ? +S.imgX.toPrecision(6) : ''); set('#imgY', S.img ? +S.imgY.toPrecision(6) : '');
  set('#targetKm', S.targetKm == null ? '' : S.targetKm);
  $('#imgOpacity').value = Math.round(S.imgOpacity * 100); $('#opVal').textContent = Math.round(S.imgOpacity * 100) + '%';
  document.querySelectorAll('#tools button').forEach((b) => b.classList.toggle('on', b.dataset.tool === S.tool));
}

// ---------- Plik ----------
function fmtNum(x) {
  let r = Math.round(x * 1000) / 1000; if (r === 0) r = 0;
  let s = String(r); if (!/[.e]/.test(s)) s += '.0'; return s;
}
function toJsonText() {
  const lines = ['{', `  "layout_id": ${JSON.stringify(S.layoutId)},`, '  "control_points": ['];
  S.points.forEach((p, i) => lines.push(`    [${fmtNum(p[0])}, ${fmtNum(p[1])}]${i < S.points.length - 1 ? ',' : ''}`));
  lines.push('  ],');
  const tail = [`  "source": ${JSON.stringify(S.source)}`, `  "notes": ${JSON.stringify(S.notes)}`];
  for (const k of Object.keys(S.extra)) tail.push(`  ${JSON.stringify(k)}: ${JSON.stringify(S.extra[k])}`);
  lines.push(tail.join(',\n'), '}');
  return lines.join('\n') + '\n';
}
function loadJsonText(text) {
  const o = JSON.parse(text);
  if (!Array.isArray(o.control_points) || !o.control_points.every((p) => Array.isArray(p) && p.length === 2 && isFinite(p[0]) && isFinite(p[1])))
    throw new Error('control_points musi być listą par [x, y].');
  pushUndo();
  S.points = o.control_points.map((p) => [Number(p[0]), Number(p[1])]);
  S.layoutId = o.layout_id || 'new_track'; S.source = o.source || ''; S.notes = o.notes || '';
  S.extra = {}; for (const k of Object.keys(o)) if (!['layout_id', 'control_points', 'source', 'notes'].includes(k)) S.extra[k] = o[k];
  S.sel = -1; S.trace = null; S.tool = "select";
  const kl = knownLayout(); if (kl) S.targetKm = kl.lengthKm;
  fitView(); changed();
}
function readFile(file, asText) {
  return new Promise((res, rej) => { const r = new FileReader(); r.onload = () => res(r.result); r.onerror = rej; asText ? r.readAsText(file) : r.readAsDataURL(file); });
}
function loadCircuitsText(text) {
  const o = JSON.parse(text), out = [];
  for (const c of o.circuits || []) for (const l of c.layouts || []) if (l.layout_id) out.push({ id: l.layout_id, name: c.name || c.circuit_id, lengthKm: Number(l.length_km) || 0 });
  S.layouts = out;
  $('#layoutList').innerHTML = out.map((l) => `<option value="${esc(l.id)}">${esc(l.name)} · ${l.lengthKm} km</option>`).join('');
  $('#circuitsStatus').textContent = `${out.length} ${plural(out.length, 'układ', 'układy', 'układów')}`;
  const kl = knownLayout(); if (kl && S.targetKm == null) S.targetKm = kl.lengthKm;
  changed();
}

// ---------- Auto-trace ----------
function otsu(hist, total) {
  let sum = 0; for (let i = 0; i < 256; i++) sum += i * hist[i];
  let wB = 0, sB = 0, best = 0, thr = 128;
  for (let t = 0; t < 256; t++) {
    wB += hist[t]; if (!wB) continue; const wF = total - wB; if (!wF) break;
    sB += t * hist[t]; const mB = sB / wB, mF = (sum - sB) / wF;
    const v = wB * wF * (mB - mF) * (mB - mF); if (v > best) { best = v; thr = t; }
  }
  return thr;
}

function thin(mask, w, h) { // Zhang-Suen
  const idx = (x, y) => y * w + x;
  let changed = true, guard = 0;
  while (changed && guard++ < 400) {
    changed = false;
    for (let pass = 0; pass < 2; pass++) {
      const del = [];
      for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) {
        const i = idx(x, y); if (!mask[i]) continue;
        const p2 = mask[i - w], p3 = mask[i - w + 1], p4 = mask[i + 1], p5 = mask[i + w + 1], p6 = mask[i + w], p7 = mask[i + w - 1], p8 = mask[i - 1], p9 = mask[i - w - 1];
        const B = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9; if (B < 2 || B > 6) continue;
        const A = (!p2 && p3) + (!p3 && p4) + (!p4 && p5) + (!p5 && p6) + (!p6 && p7) + (!p7 && p8) + (!p8 && p9) + (!p9 && p2);
        if (A !== 1) continue;
        if (pass === 0 ? (p2 * p4 * p6 === 0 && p4 * p6 * p8 === 0) : (p2 * p4 * p8 === 0 && p2 * p6 * p8 === 0)) del.push(i);
      }
      if (del.length) { changed = true; for (const i of del) mask[i] = 0; }
    }
  }
}

function extractLoop(img, mode) {
  const maxDim = 900, f = Math.min(1, maxDim / Math.max(img.width, img.height));
  const w = Math.max(8, Math.round(img.width * f)), h = Math.max(8, Math.round(img.height * f));
  const oc = document.createElement('canvas'); oc.width = w; oc.height = h;
  const o = oc.getContext('2d', { willReadFrequently: true }); o.fillStyle = '#fff'; o.fillRect(0, 0, w, h); o.drawImage(img, 0, 0, w, h);
  const d = o.getImageData(0, 0, w, h).data, gray = new Uint8Array(w * h), hist = new Array(256).fill(0);
  for (let i = 0; i < w * h; i++) { const g = Math.round(0.299 * d[i * 4] + 0.587 * d[i * 4 + 1] + 0.114 * d[i * 4 + 2]); gray[i] = g; hist[g]++; }
  const thr = otsu(hist, w * h);
  let dark = mode === 'dark';
  if (mode === 'auto') { let lo = 0; for (let i = 0; i <= thr; i++) lo += hist[i]; dark = lo <= w * h / 2; } // linia = mniejszość
  let mask = new Uint8Array(w * h);
  for (let i = 0; i < w * h; i++) mask[i] = (dark ? gray[i] <= thr : gray[i] > thr) ? 1 : 0;
  // Delikatne domknięcie: dylatacja 3x3, żeby zasklepić 1-pikselowe przerwy.
  const dil = new Uint8Array(w * h);
  for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) {
    const i = y * w + x; if (mask[i] || mask[i - 1] || mask[i + 1] || mask[i - w] || mask[i + w] || mask[i - w - 1] || mask[i - w + 1] || mask[i + w - 1] || mask[i + w + 1]) dil[i] = 1;
  }
  mask = dil;
  thin(mask, w, h);
  // Największa składowa (8-sąsiedztwo).
  const label = new Int32Array(w * h); let best = [], lab = 0;
  for (let s = 0; s < w * h; s++) {
    if (!mask[s] || label[s]) continue;
    lab++; const stack = [s], comp = []; label[s] = lab;
    while (stack.length) {
      const i = stack.pop(); comp.push(i); const x = i % w, y = (i / w) | 0;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        if (!dx && !dy) continue; const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
        const j = ny * w + nx; if (mask[j] && !label[j]) { label[j] = lab; stack.push(j); }
      }
    }
    if (comp.length > best.length) best = comp;
  }
  if (best.length < 30) return null;
  // Przycinanie odgałęzień: usuwamy końcówki aż zostanie sama pętla.
  const alive = new Uint8Array(w * h); for (const i of best) alive[i] = 1;
  const nb = (i) => { const x = i % w, y = (i / w) | 0, r = []; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { if (!dx && !dy) continue; const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue; const j = ny * w + nx; if (alive[j]) r.push(j); } return r; };
  const deg = new Int16Array(w * h), q = [];
  for (const i of best) { deg[i] = nb(i).length; if (deg[i] <= 1) q.push(i); }
  while (q.length) {
    const i = q.pop(); if (!alive[i]) continue; alive[i] = 0;
    for (const j of nb(i)) { deg[j]--; if (deg[j] <= 1) q.push(j); }
  }
  const rest = best.filter((i) => alive[i]); if (rest.length < 30) return { path: null, f, w, h };
  // Marsz po pętli, preferując kierunek zgodny z dotychczasowym.
  let start = rest[0]; for (const i of rest) if (i < start) start = i;
  const visited = new Uint8Array(w * h), path = [start]; visited[start] = 1;
  let cur = start;
  for (;;) {
    const cand = nb(cur).filter((j) => !visited[j]); if (!cand.length) break;
    let pick = cand[0];
    if (path.length > 6 && cand.length > 1) {
      const a = path[path.length - 6], ax = cur % w - a % w, ay = ((cur / w) | 0) - ((a / w) | 0);
      let bs = -2; for (const j of cand) { const dx = j % w - cur % w, dy = ((j / w) | 0) - ((cur / w) | 0); const sc = (dx * ax + dy * ay) / (Math.hypot(dx, dy) * Math.hypot(ax, ay) || 1); if (sc > bs) { bs = sc; pick = j; } }
    }
    visited[pick] = 1; path.push(pick); cur = pick;
  }
  return { path: path.map((i) => [i % w, (i / w) | 0]), f, w, h, comp: rest.length };
}

function rdpOpen(p, a, b, tol, keep) {
  const stack = [[a, b]];
  while (stack.length) {
    const [s, e] = stack.pop(); if (e <= s + 1) continue;
    const ax = p[s][0], ay = p[s][1], bx = p[e][0], by = p[e][1], L = Math.hypot(bx - ax, by - ay);
    let md = -1, mi = -1;
    for (let i = s + 1; i < e; i++) {
      const d = L < 1e-9 ? Math.hypot(p[i][0] - ax, p[i][1] - ay) : Math.abs((bx - ax) * (ay - p[i][1]) - (ax - p[i][0]) * (by - ay)) / L;
      if (d > md) { md = d; mi = i; }
    }
    if (md > tol) { keep[mi] = 1; stack.push([s, mi], [mi, e]); }
  }
}
function rdpClosed(path, tol) {
  const n = path.length; if (n < 4) return path.slice();
  let far = 0, fd = -1; for (let i = 1; i < n; i++) { const d = Math.hypot(path[i][0] - path[0][0], path[i][1] - path[0][1]); if (d > fd) { fd = d; far = i; } }
  const ext = path.concat([path[0]]), keep = new Uint8Array(n + 1); keep[0] = 1; keep[far] = 1; keep[n] = 1;
  rdpOpen(ext, 0, far, tol, keep); rdpOpen(ext, far, n, tol, keep);
  const out = []; for (let i = 0; i < n; i++) if (keep[i]) out.push(path[i]); return out;
}
function smoothClosed(path, k) {
  const n = path.length, out = [];
  for (let i = 0; i < n; i++) { let x = 0, y = 0; for (let d = -k; d <= k; d++) { const p = path[(i + d + n) % n]; x += p[0]; y += p[1]; } out.push([x / (2 * k + 1), y / (2 * k + 1)]); }
  return out;
}

function applyTrace(recordUndo) {
  const t = S.trace; if (!t || !t.path) return;
  const tol = parseFloat($('#traceTol').value) * t.f; // tolerancja w px obrazu źródłowego
  const sm = smoothClosed(t.path, 2);
  const simp = rdpClosed(sm, tol);
  if (recordUndo) pushUndo();
  S.points = simp.map(([px, py]) => [S.imgX + (px + 0.5) / t.f * S.imgScale, S.imgY - (py + 0.5) / t.f * S.imgScale]);
  S.sel = -1; $('#traceStatus').textContent = `${S.points.length} ${plural(S.points.length, 'punkt', 'punkty', 'punktów')}`;
  changed();
}
function runTrace() {
  if (!S.img) { $('#traceStatus').textContent = 'Najpierw wczytaj obraz'; return; }
  if (S.points.length && !confirm('Zastąpić obecne punkty wykrytym torem?')) return;
  $('#traceStatus').textContent = 'Analiza…';
  setTimeout(() => {
    const r = extractLoop(S.img, $('#traceMode').value);
    if (!r || !r.path) { S.trace = null; $('#traceStatus').textContent = 'Nie znaleziono pętli'; return; }
    S.trace = r; applyTrace(true);
  }, 20);
}

// ---------- Wejście: kanwa ----------
const HIT = 9;
function pointAt(sx, sy) {
  for (let i = S.points.length - 1; i >= 0; i--) { const s = toScreen(S.points[i][0], S.points[i][1]); if (Math.hypot(s[0] - sx, s[1] - sy) <= HIT) return i; }
  return -1;
}
function nearestOnCurve(sx, sy) {
  const c = S.curveCache; if (!c || !c.length) return null;
  let best = null, bd = Infinity;
  for (let i = 0; i < c.length; i++) {
    const a = c[i], b = c[(i + 1) % c.length];
    const A = toScreen(a.x, a.y), B = toScreen(b.x, b.y);
    const dx = B[0] - A[0], dy = B[1] - A[1], l2 = dx * dx + dy * dy;
    const t = l2 ? Math.max(0, Math.min(1, ((sx - A[0]) * dx + (sy - A[1]) * dy) / l2)) : 0;
    const d = Math.hypot(A[0] + t * dx - sx, A[1] + t * dy - sy);
    if (d < bd) { bd = d; best = { seg: a.seg, x: a.x + t * (b.x - a.x), y: a.y + t * (b.y - a.y), d }; }
  }
  return best && best.d <= 18 ? best : null;
}
function insertOnCurve(h) { pushUndo(); S.points.splice(h.seg + 1, 0, [h.x, h.y]); S.sel = h.seg + 1; changed(); }

let drag = null, spaceDown = false;
cv.addEventListener('pointerdown', (e) => {
  const r = cv.getBoundingClientRect(), sx = e.clientX - r.left, sy = e.clientY - r.top;
  cv.setPointerCapture(e.pointerId);
  const hit = pointAt(sx, sy);
  if (e.button === 1 || spaceDown || e.button === 2) { drag = { kind: 'pan', sx, sy, cx: S.view.cx, cy: S.view.cy }; return; }
  if (e.button !== 0) return;
  if (S.tool === 'bg') { drag = { kind: 'bg', sx, sy, ix: S.imgX, iy: S.imgY, pushed: false }; return; }
  if (hit >= 0) { S.sel = hit; drag = { kind: 'pt', i: hit, pushed: false }; changed(); return; }
  if (S.tool === 'add') {
    pushUndo(); const w = toWorld(sx, sy); S.points.push([w[0], w[1]]); S.sel = S.points.length - 1; changed(); return;
  }
  if (S.tool === 'insert' || e.shiftKey) { const h = nearestOnCurve(sx, sy); if (h) { insertOnCurve(h); return; } }
  drag = { kind: 'pan', sx, sy, cx: S.view.cx, cy: S.view.cy, click: true };
});
cv.addEventListener('pointermove', (e) => {
  if (!drag) return;
  const r = cv.getBoundingClientRect(), sx = e.clientX - r.left, sy = e.clientY - r.top, z = S.view.zoom;
  if (drag.kind === 'pan') {
    if (Math.hypot(sx - drag.sx, sy - drag.sy) > 3) drag.click = false;
    S.view.cx = drag.cx - (sx - drag.sx) / z; S.view.cy = drag.cy + (sy - drag.sy) / z; schedule();
  } else if (drag.kind === 'bg') {
    if (!drag.pushed) { pushUndo(); drag.pushed = true; }
    S.imgX = drag.ix + (sx - drag.sx) / z; S.imgY = drag.iy - (sy - drag.sy) / z; syncFields(); schedule();
  } else if (drag.kind === 'pt') {
    if (!drag.pushed) { pushUndo(); drag.pushed = true; S.dragging = true; }
    const w = toWorld(sx, sy); S.points[drag.i] = [w[0], w[1]]; schedule();
  }
});
function endDrag() {
  if (!drag) return;
  if (drag.kind === 'pan' && drag.click && S.sel !== -1) { S.sel = -1; }
  const wasPt = drag.kind === 'pt'; drag = null; S.dragging = false;
  if (wasPt) S.trace = null;
  changed();
}
cv.addEventListener('pointerup', endDrag);
cv.addEventListener('pointercancel', endDrag);
cv.addEventListener('contextmenu', (e) => {
  e.preventDefault();
  const r = cv.getBoundingClientRect(), h = pointAt(e.clientX - r.left, e.clientY - r.top);
  if (h >= 0) { pushUndo(); S.points.splice(h, 1); S.sel = -1; changed(); }
});
cv.addEventListener('dblclick', (e) => {
  if (S.tool !== 'select') return;
  const r = cv.getBoundingClientRect(), sx = e.clientX - r.left, sy = e.clientY - r.top;
  if (pointAt(sx, sy) >= 0) return; const h = nearestOnCurve(sx, sy); if (h) insertOnCurve(h);
});
cv.addEventListener('wheel', (e) => {
  e.preventDefault();
  const r = cv.getBoundingClientRect(), sx = e.clientX - r.left, sy = e.clientY - r.top;
  const before = toWorld(sx, sy), k = Math.exp(-e.deltaY * 0.0015);
  S.view.zoom = Math.max(1e-6, Math.min(1e6, S.view.zoom * k));
  const after = toWorld(sx, sy); S.view.cx += before[0] - after[0]; S.view.cy += before[1] - after[1]; schedule();
}, { passive: false });
cv.addEventListener('mousemove', (e) => {
  if (drag) return; const r = cv.getBoundingClientRect(), sx = e.clientX - r.left, sy = e.clientY - r.top;
  cv.style.cursor = S.tool === 'bg' ? 'move' : pointAt(sx, sy) >= 0 ? 'pointer' : S.tool === 'add' ? 'crosshair' : S.tool === 'insert' ? (nearestOnCurve(sx, sy) ? 'copy' : 'default') : 'default';
});

// ---------- Klawiatura ----------
function deleteSelected() { if (S.sel < 0 || S.sel >= S.points.length) return; pushUndo(); S.points.splice(S.sel, 1); S.sel = -1; changed(); }
window.addEventListener('keydown', (e) => {
  const tag = (e.target.tagName || '').toLowerCase();
  const typing = tag === 'input' || tag === 'select' || tag === 'textarea';
  if (e.key === ' ' && !typing) { spaceDown = true; e.preventDefault(); }
  if ((e.ctrlKey || e.metaKey) && !typing) {
    const k = e.key.toLowerCase();
    if (k === 'z' && !e.shiftKey) { e.preventDefault(); undo(); } else if (k === 'y' || (k === 'z' && e.shiftKey)) { e.preventDefault(); redo(); }
    return;
  }
  if (typing) return;
  if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); deleteSelected(); }
  else if (e.key === 'Escape') { S.sel = -1; S.tool = 'select'; changed(); }
  else { const m = { v: 'select', a: 'add', i: 'insert', b: 'bg' }[e.key.toLowerCase()]; if (m) { S.tool = m; changed(); } }
});
window.addEventListener('keyup', (e) => { if (e.key === ' ') spaceDown = false; });

// ---------- Wiązania panelu ----------
document.querySelectorAll('#tools button').forEach((b) => b.addEventListener('click', () => { S.tool = b.dataset.tool; changed(); }));
$('#btnUndo').onclick = undo; $('#btnRedo').onclick = redo; $('#btnFit').onclick = fitView;
$('#btnOpen').onclick = () => $('#fileJson').click();
$('#fileJson').onchange = async (e) => {
  const f = e.target.files[0]; e.target.value = ''; if (!f) return;
  try { loadJsonText(await readFile(f, true)); } catch (err) { alert('Nie udało się wczytać pliku: ' + err.message); }
};
$('#btnSave').onclick = () => {
  const blob = new Blob([toJsonText()], { type: 'application/json' });
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = (S.layoutId || 'track') + '.json';
  document.body.appendChild(a); a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(a.href), 1000);
};
$('#btnNew').onclick = () => {
  if (S.points.length && !confirm('Zacząć nowy tor? Obecne punkty można jeszcze cofnąć.')) return;
  pushUndo(); S.points = []; S.sel = -1; S.layoutId = 'new_track'; S.source = ''; S.notes = ''; S.extra = {}; S.targetKm = null; S.trace = null;
  S.tool = 'add'; fitView(); changed();
};
$('#btnImg').onclick = () => $('#fileImg').click();
$('#fileImg').onchange = async (e) => {
  const f = e.target.files[0]; e.target.value = ''; if (!f) return;
  const url = await readFile(f, false), im = new Image();
  im.onload = () => {
    S.img = im; S.trace = null;
    if (!S.points.length) { S.imgScale = 1; S.imgX = -im.width / 2; S.imgY = im.height / 2; fitView(); }
    else { fitImage(); }
    changed();
  };
  im.src = url;
};
function fitImage() { // ustaw obraz na środku widoku, nie zmieniając skali
  if (!S.img) return;
  S.imgX = S.view.cx - S.img.width * S.imgScale / 2; S.imgY = S.view.cy + S.img.height * S.imgScale / 2; changed();
}
$('#btnImgFit').onclick = () => { if (!S.img) return; pushUndo(); fitImage(); };
$('#btnImgClear').onclick = () => { S.img = null; S.trace = null; changed(); };
$('#imgOpacity').oninput = (e) => { S.imgOpacity = e.target.value / 100; $('#opVal').textContent = e.target.value + '%'; schedule(); };
for (const [id, key] of [['#imgScale', 'imgScale'], ['#imgX', 'imgX'], ['#imgY', 'imgY']]) {
  $(id).addEventListener('input', (e) => { const v = parseFloat(e.target.value); if (isFinite(v) && (key !== 'imgScale' || v > 0)) { S[key] = v; schedule(); } });
}
$('#traceTol').oninput = (e) => { $('#tolVal').textContent = e.target.value; if (S.trace) applyTrace(false); };
$('#btnTrace').onclick = runTrace;
$('#btnReverse').onclick = () => { if (S.points.length < 2) return; pushUndo(); S.points.reverse(); S.sel = -1; changed(); };
$('#targetKm').addEventListener('input', (e) => { const v = parseFloat(e.target.value); S.targetKm = isFinite(v) && v > 0 ? v : null; schedule(); });
$('#btnScale').onclick = () => {
  const tgt = targetLenM(); if (!S.geo || !tgt) return;
  const f = tgt / S.geo.rawLength; if (Math.abs(f - 1) < 1e-9) return;
  pushUndo();
  S.points = S.points.map((p) => [p[0] * f, p[1] * f]);
  S.imgScale *= f; S.imgX *= f; S.imgY *= f; S.view.cx *= f; S.view.cy *= f; S.view.zoom /= f; S.trace = null;
  changed();
};
$('#btnCircuits').onclick = () => $('#fileCircuits').click();
$('#fileCircuits').onchange = async (e) => {
  const f = e.target.files[0]; e.target.value = ''; if (!f) return;
  try { loadCircuitsText(await readFile(f, true)); } catch (err) { alert('Nie udało się wczytać circuits.json: ' + err.message); }
};
$('#layoutId').addEventListener('input', (e) => { S.layoutId = e.target.value; const kl = knownLayout(); if (kl) S.targetKm = kl.lengthKm; changed(); });
$('#source').addEventListener('input', (e) => { S.source = e.target.value; });
$('#notes').addEventListener('input', (e) => { S.notes = e.target.value; });
for (const [id, k] of [['#ptX', 0], ['#ptY', 1]]) {
  $(id).addEventListener('change', (e) => { const v = parseFloat(e.target.value); if (S.sel < 0 || !isFinite(v)) return; pushUndo(); S.points[S.sel][k] = v; changed(); });
}
$('#btnDel').onclick = deleteSelected;
$('#btnStart').onclick = () => { if (S.sel <= 0) return; pushUndo(); S.points = S.points.slice(S.sel).concat(S.points.slice(0, S.sel)); S.sel = 0; changed(); };
document.querySelectorAll('[data-cfg]').forEach((el) => {
  el.value = CFG[el.dataset.cfg];
  el.addEventListener('input', () => { const v = parseFloat(el.value); if (isFinite(v) && v > 0) { CFG[el.dataset.cfg] = v; schedule(); } });
});

new ResizeObserver(resize).observe($('#wrap'));
resize(); fitView(); changed();

// Do testów ręcznych z konsoli.
window.PPEditor = { S, CFG, buildGeometry, analyze, findSelfIntersections, loadJsonText, loadCircuitsText, toJsonText, recompute, extractLoop, rdpClosed };
