/*
 * The race mode's 2D map (PP-052), ported from the prototype (ui/prototype/js/race-map.js). Display only (TECH §3): it places
 * the frames the bridge sent on the layout's authored centre line and never computes a result. Anything moved for legibility
 * (two-wide spreading of a pack) is a screen-space adjustment.
 */

/** The fallback loop when a layout has no authored points: an oval of the lap's length, so the dots still run. */
export function fallbackPoints(lapLengthM) {
  const length = lapLengthM > 0 ? lapLengthM : 4000;
  const a = length / 5;
  const b = a / 2.2;
  const out = [];
  for (let i = 0; i < 24; i++) {
    const angle = (i / 24) * Math.PI * 2;
    out.push({ x: Math.cos(angle) * a, y: Math.sin(angle) * b });
  }
  return out;
}

/**
 * A closed centripetal Catmull-Rom loop through the layout's points, sampled by arc length (the curve of
 * src/Paddock.Domain/World/Tracks/TrackGeometry.cs). Points are metres with y north; the map's y grows down, so y is flipped.
 */
export class TrackSpline {
  constructor(points) {
    const p = points.map((point) => [point.x, -point.y]);
    const n = p.length;
    const dist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]);
    let perimeter = 0;
    for (let i = 0; i < n; i++) perimeter += dist(p[i], p[(i + 1) % n]);
    const stepLen = perimeter / 4000;
    const raw = [];
    for (let i = 0; i < n; i++) {
      const a = p[(i - 1 + n) % n];
      const b = p[i];
      const c = p[(i + 1) % n];
      const e = p[(i + 2) % n];
      const t1 = Math.sqrt(dist(a, b)) || 1e-9;
      const h = Math.sqrt(dist(b, c)) || 1e-9;
      const t2 = t1 + h;
      const t3 = t2 + (Math.sqrt(dist(c, e)) || 1e-9);
      const m1 = [0, 1].map((k) => h * ((b[k] - a[k]) / t1 - (c[k] - a[k]) / t2 + (c[k] - b[k]) / h));
      const m2 = [0, 1].map((k) => h * ((c[k] - b[k]) / h - (e[k] - b[k]) / (t3 - t1) + (e[k] - c[k]) / (t3 - t2)));
      const b1 = [b[0] + m1[0] / 3, b[1] + m1[1] / 3];
      const b2 = [c[0] - m2[0] / 3, c[1] - m2[1] / 3];
      const steps = Math.max(12, Math.ceil(dist(b, c) / stepLen));
      for (let j = 0; j < steps; j++) {
        const u = j / steps;
        const v = 1 - u;
        const x = v * v * v * b[0] + 3 * v * v * u * b1[0] + 3 * v * u * u * b2[0] + u * u * u * c[0];
        const y = v * v * v * b[1] + 3 * v * v * u * b1[1] + 3 * v * u * u * b2[1] + u * u * u * c[1];
        const dx = 3 * v * v * (b1[0] - b[0]) + 6 * v * u * (b2[0] - b1[0]) + 3 * u * u * (c[0] - b2[0]);
        const dy = 3 * v * v * (b1[1] - b[1]) + 6 * v * u * (b2[1] - b1[1]) + 3 * u * u * (c[1] - b2[1]);
        const len = Math.hypot(dx, dy) || 1e-6;
        raw.push({ x, y, nx: -dy / len, ny: dx / len });
      }
    }
    this.samples = [];
    let d = 0;
    raw.forEach((r, i) => {
      if (i) d += Math.hypot(r.x - raw[i - 1].x, r.y - raw[i - 1].y);
      this.samples.push({ ...r, d });
    });
    this.total = d + Math.hypot(raw[0].x - raw[raw.length - 1].x, raw[0].y - raw[raw.length - 1].y);
    let minX = Infinity;
    let maxX = -Infinity;
    let minY = Infinity;
    let maxY = -Infinity;
    for (const r of raw) {
      minX = Math.min(minX, r.x);
      maxX = Math.max(maxX, r.x);
      minY = Math.min(minY, r.y);
      maxY = Math.max(maxY, r.y);
    }
    this.bounds = { minX, maxX, minY, maxY, w: maxX - minX || 1, h: maxY - minY || 1 };
  }

  /** The point and unit normal at a fraction of the lap, [0, 1). */
  at(fraction) {
    const s = ((fraction % 1) + 1) % 1;
    const target = s * this.total;
    const S = this.samples;
    let lo = 0;
    let hi = S.length - 1;
    while (lo <= hi) {
      const m = (lo + hi) >> 1;
      if (S[m].d < target) lo = m + 1;
      else hi = m - 1;
    }
    const i0 = Math.max(0, lo - 1);
    const p0 = S[i0];
    const p1 = S[(i0 + 1) % S.length];
    const seg = (i0 + 1 < S.length ? p1.d : this.total) - p0.d || 1e-6;
    const t = Math.min(1, Math.max(0, (target - p0.d) / seg));
    const L = (a, b) => a + (b - a) * t;
    const nx = L(p0.nx, p1.nx);
    const ny = L(p0.ny, p1.ny);
    const nl = Math.hypot(nx, ny) || 1;
    return { x: L(p0.x, p1.x), y: L(p0.y, p1.y), nx: nx / nl, ny: ny / nl, tx: ny / nl, ty: -nx / nl };
  }
}

/**
 * Spreads cars that would draw on top of each other. `items` = [{ id, s }] with s the screen distance along the track (larger
 * is further ahead); returns Map id -> { s, lane } with lane -1, 0 or 1. A lone car keeps its spot; a pack goes two-wide.
 */
export function separate(items, spacing) {
  const out = new Map();
  const sorted = [...items].sort((a, b) => b.s - a.s);
  let prev = null;
  let laneLast = null;
  let clusterHead = null;
  for (const it of sorted) {
    const inCluster = prev && prev.d - it.s < spacing;
    if (!inCluster) {
      const rec = { s: it.s, lane: 0 };
      out.set(it.id, rec);
      prev = { d: it.s, lane: 0 };
      clusterHead = rec;
      laneLast = null;
      continue;
    }
    if (!laneLast) {
      clusterHead.lane = -1;
      prev.lane = -1;
      laneLast = { '-1': clusterHead.s, 1: Infinity };
    }
    const lane = -prev.lane;
    const d = Math.min(it.s, laneLast[lane] - spacing, prev.d - spacing / 2);
    laneLast[lane] = d;
    out.set(it.id, { s: d, lane });
    prev = { d, lane };
  }
  return out;
}

/** The pit lane along the main straight, as a fraction of the lap (ESTIMATE: drawn, not simulated). */
const PIT_FROM = 0.93;
const PIT_SPAN = 0.14;

/**
 * The canvas map. `entries` = Map carId -> { label, livery: { main, accent, on }, own }. `colors` comes from the page's tokens.
 * Callbacks: onSelect(carId | null), onFollow(bool).
 */
export class RaceMap {
  constructor(canvas, spline, lapLengthM, entries, colors) {
    this.cv = canvas;
    this.ctx = canvas.getContext('2d');
    this.sp = spline;
    this.lapM = lapLengthM > 0 ? lapLengthM : spline.total;
    this.entries = entries;
    this.colors = colors;
    this.inset = { l: 0, t: 0, r: 0, b: 0 };
    this.zoom = 1;
    this.fitZoom = 1;
    this.panX = 0;
    this.panY = 0;
    this.follow = false;
    this.selected = null;
    this.hover = null;
    this.cars = [];
    this.drawn = [];
    this.onSelect = null;
    this.onFollow = null;
    this.resizeCanvas();
    this.bind();
    this.fit();
  }

  destroy() {
    this.abort.abort();
  }

  setInset(inset) {
    this.inset = inset;
  }

  setFollow(on) {
    this.follow = on;
    if (this.onFollow) this.onFollow(on);
  }

  resizeCanvas() {
    const r = this.cv.parentElement.getBoundingClientRect();
    const dpr = globalThis.devicePixelRatio || 1;
    this.W = r.width;
    this.H = r.height;
    this.cv.width = Math.round(r.width * dpr);
    this.cv.height = Math.round(r.height * dpr);
    this.cv.style.width = `${r.width}px`;
    this.cv.style.height = `${r.height}px`;
    this.dpr = dpr;
  }

  resize() {
    const keep = this.zoom / this.fitZoom;
    this.resizeCanvas();
    this.fit();
    if (keep !== 1) this.zoomBy(keep);
    this.draw();
  }

  area() {
    const i = this.inset;
    return { x0: i.l, y0: i.t, x1: this.W - i.r, y1: this.H - i.b };
  }

  fit() {
    const b = this.sp.bounds;
    const a = this.area();
    const pad = 40;
    const w = Math.max(100, a.x1 - a.x0 - pad * 2);
    const h = Math.max(100, a.y1 - a.y0 - pad * 2);
    this.fitZoom = this.zoom = Math.min(w / b.w, h / b.h);
    this.panX = (a.x0 + a.x1) / 2 - (b.minX + b.w / 2) * this.zoom;
    this.panY = (a.y0 + a.y1) / 2 - (b.minY + b.h / 2) * this.zoom;
  }

  zoomBy(factor, cx, cy) {
    const a = this.area();
    cx = cx ?? (a.x0 + a.x1) / 2;
    cy = cy ?? (a.y0 + a.y1) / 2;
    const z = Math.max(this.fitZoom * 0.7, Math.min(this.fitZoom * 14, this.zoom * factor));
    const wx = (cx - this.panX) / this.zoom;
    const wy = (cy - this.panY) / this.zoom;
    this.zoom = z;
    this.panX = cx - wx * z;
    this.panY = cy - wy * z;
    this.draw();
  }

  bind() {
    this.abort = new AbortController();
    const o = { signal: this.abort.signal };
    const c = this.cv;
    let drag = null;
    const local = (e) => {
      const r = c.getBoundingClientRect();
      return [e.clientX - r.left, e.clientY - r.top];
    };
    c.addEventListener(
      'pointerdown',
      (e) => {
        if (e.button) return;
        drag = { x: e.clientX, y: e.clientY, moved: false };
        c.setPointerCapture?.(e.pointerId);
      },
      o,
    );
    c.addEventListener(
      'pointermove',
      (e) => {
        if (drag) {
          const dx = e.clientX - drag.x;
          const dy = e.clientY - drag.y;
          if (!drag.moved && Math.hypot(dx, dy) < 4) return;
          if (!drag.moved) {
            drag.moved = true;
            if (this.follow) this.setFollow(false);
          }
          this.panX += dx;
          this.panY += dy;
          drag.x = e.clientX;
          drag.y = e.clientY;
          c.style.cursor = 'grabbing';
          this.draw();
          return;
        }
        const [x, y] = local(e);
        const hit = this.hit(x, y);
        if (hit !== this.hover) {
          this.hover = hit;
          this.draw();
        }
        c.style.cursor = hit ? 'pointer' : 'grab';
      },
      o,
    );
    c.addEventListener(
      'pointerup',
      (e) => {
        if (drag && !drag.moved && this.onSelect) {
          const [x, y] = local(e);
          this.onSelect(this.hit(x, y));
        }
        drag = null;
        c.style.cursor = this.hover ? 'pointer' : 'grab';
      },
      o,
    );
    c.addEventListener(
      'pointerleave',
      () => {
        if (this.hover) {
          this.hover = null;
          this.draw();
        }
      },
      o,
    );
    c.addEventListener(
      'wheel',
      (e) => {
        e.preventDefault();
        const [x, y] = local(e);
        this.zoomBy(e.deltaY < 0 ? 1.2 : 1 / 1.2, x, y);
      },
      { passive: false, signal: this.abort.signal },
    );
  }

  hit(x, y) {
    let best = null;
    let bd = 14;
    for (const d of this.drawn) {
      const k = Math.hypot(d.x - x, d.y - y);
      if (k < bd) {
        bd = k;
        best = d.id;
      }
    }
    return best;
  }

  radius() {
    return Math.max(5.5, Math.min(11, 5.5 * Math.sqrt(this.zoom / this.fitZoom)));
  }

  roadPx() {
    return Math.max(8, Math.min(28, 9 * Math.sqrt(this.zoom / this.fitZoom)));
  }

  pitOffset() {
    return this.roadPx() * 0.5 + Math.max(6, this.roadPx() * 0.45);
  }

  layout(cars) {
    const arcPx = this.sp.total * this.zoom;
    const r = this.radius();
    const track = [];
    const pit = [];
    for (const c of cars) {
      if (c.inPitLane) pit.push({ id: c.carId, s: (PIT_FROM + PIT_SPAN * Math.min(1, c.pitDistanceM / (PIT_SPAN * this.lapM))) * arcPx });
      else track.push({ id: c.carId, s: ((((c.distanceM / this.lapM) % 1) + 1) % 1) * arcPx });
    }
    const spacing = r * 2 + 2;
    const placed = new Map([...separate(track, spacing), ...separate(pit, spacing)]);
    const pitIds = new Set(pit.map((p) => p.id));
    return cars.map((c) => {
      const l = placed.get(c.carId);
      const p = this.sp.at(l.s / arcPx);
      const side = pitIds.has(c.carId) ? -this.pitOffset() : l.lane * (r + 1);
      return {
        id: c.carId,
        x: this.panX + p.x * this.zoom + p.nx * side,
        y: this.panY + p.y * this.zoom + p.ny * side,
        heading: Math.atan2(p.ty, p.tx),
      };
    });
  }

  render(cars) {
    this.cars = cars;
    this.draw();
  }

  draw() {
    const ctx = this.ctx;
    let pos = this.layout(this.cars);
    if (this.follow && this.selected) {
      const f = pos.find((p) => p.id === this.selected);
      const a = this.area();
      if (f) {
        const dx = ((a.x0 + a.x1) / 2 - f.x) * 0.18;
        const dy = ((a.y0 + a.y1) / 2 - f.y) * 0.18;
        this.panX += dx;
        this.panY += dy;
        pos = pos.map((p) => ({ ...p, x: p.x + dx, y: p.y + dy }));
      }
    }
    this.drawn = pos;
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    ctx.fillStyle = this.colors.bg;
    ctx.fillRect(0, 0, this.W, this.H);
    this.drawTrack(ctx);
    const rank = (p) => (p.id === this.selected ? 3 : p.id === this.hover ? 2 : this.entries.get(p.id)?.own ? 1 : 0);
    const r = this.radius();
    for (const p of [...pos].sort((a, b) => rank(a) - rank(b))) {
      const entry = this.entries.get(p.id);
      if (entry) this.renderCar(ctx, p, entry, r, p.id === this.selected, p.id === this.hover);
    }
  }

  path(ctx, off = 0, from = 0, to = 1) {
    ctx.beginPath();
    const n = Math.max(24, Math.round((to - from) * 600));
    for (let i = 0; i <= n; i++) {
      const p = this.sp.at(from + ((to - from) * i) / n);
      const x = this.panX + p.x * this.zoom + p.nx * off;
      const y = this.panY + p.y * this.zoom + p.ny * off;
      if (i) ctx.lineTo(x, y);
      else ctx.moveTo(x, y);
    }
  }

  drawTrack(ctx) {
    const road = this.roadPx();
    const c = this.colors;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    this.path(ctx, -this.pitOffset(), PIT_FROM, PIT_FROM + PIT_SPAN);
    ctx.strokeStyle = c.pit;
    ctx.lineWidth = Math.max(4, road * 0.5);
    ctx.stroke();
    this.path(ctx);
    ctx.closePath();
    ctx.strokeStyle = c.kerb;
    ctx.lineWidth = road + 6;
    ctx.stroke();
    ctx.strokeStyle = c.road;
    ctx.lineWidth = road;
    ctx.stroke();
    const p = this.sp.at(0);
    const x = this.panX + p.x * this.zoom;
    const y = this.panY + p.y * this.zoom;
    const half = road / 2 + 3;
    const sq = Math.max(2.5, road / 6);
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(Math.atan2(p.ny, p.nx));
    for (let i = -half, k = 0; i < half; i += sq, k++) {
      for (let j = 0; j < 2; j++) {
        ctx.fillStyle = (k + j) % 2 ? '#111' : '#f4efe4';
        ctx.fillRect(i, -sq + j * sq, sq, sq);
      }
    }
    ctx.restore();
  }

  /*
   * The renderer swap point (PP-052): one car in screen space. `p` = { x, y, heading } with heading the direction of travel in
   * radians. Today a dot in team colours; a car sprite per era rotates by `p.heading` here and nothing else changes.
   */
  renderCar(ctx, p, entry, r, selected, hovered) {
    const lv = entry.livery;
    ctx.save();
    ctx.translate(p.x, p.y);
    if (entry.own) {
      ctx.beginPath();
      ctx.arc(0, 0, r + 3, 0, 7);
      ctx.strokeStyle = this.colors.own;
      ctx.lineWidth = 1.8;
      ctx.stroke();
    }
    if (selected || hovered) {
      ctx.beginPath();
      ctx.arc(0, 0, r + 6, 0, 7);
      ctx.strokeStyle = selected ? this.colors.accent : 'rgba(255,255,255,.7)';
      ctx.lineWidth = 2;
      ctx.stroke();
    }
    ctx.beginPath();
    ctx.arc(0, 0, r, 0, 7);
    ctx.fillStyle = lv.main;
    ctx.fill();
    ctx.lineWidth = 1.2;
    ctx.strokeStyle = lv.accent;
    ctx.stroke();
    if (r >= 8 || selected || hovered) {
      ctx.fillStyle = this.colors.label;
      ctx.font = `700 11px system-ui, sans-serif`;
      ctx.textAlign = 'left';
      ctx.textBaseline = 'middle';
      ctx.fillText(entry.label, r + 4, 0.5);
    }
    ctx.restore();
  }
}
