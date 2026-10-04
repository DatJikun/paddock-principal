/* =========================================================================
 * PADDOCK PRINCIPAL · 2D TRACK MAP (PP-052)
 * -------------------------------------------------------------------------
 * Pure display. Input is a PositionFrame { raceTimeMs, cars: CarFrame[] }
 * where CarFrame mirrors src/Paddock.Domain/Racing/CarFrame.cs:
 *   { raceTimeMs, carId, distanceM, speedMps, inPitLane, pitDistanceM }
 * The map never computes results; it only places the frames on the geometry.
 * Anything the map moves for legibility (grid spreading, label placement) is
 * a screen-space adjustment and never feeds back into the race.
 * ========================================================================= */

(() => {
  'use strict';

  /* ---------- track geometry: closed centripetal Catmull-Rom loop sampled by arc length ----------
     Same curve as src/Paddock.Domain/World/Tracks/TrackGeometry.cs (alpha = 0.5, knots t += sqrt(chord)).
     Each segment is a cubic, so it is stored as an exact Bezier; the tangents below are the derivatives
     of the Barry-Goldman evaluation at the segment ends. Display only: the backend owns the truth.
     Unit-tested against reference samples exported from TrackGeometry (ui/prototype/tests/track-spline.test.mjs). */
  class TrackSpline {
    constructor(controlPoints, lengthKm) {
      this.lengthM = lengthKm * 1000;
      this.samples = [];
      this.total = 0;
      this.beziers = [];
      this.knotD = [];
      const p = controlPoints, n = p.length, raw = [], firstOfSegment = [];
      const dist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]);
      let perimeter = 0;
      for (let i = 0; i < n; i++) perimeter += dist(p[i], p[(i + 1) % n]);
      const stepLen = perimeter / 8000;   /* nominal arc step of the dense polyline, in control-point units */
      for (let i = 0; i < n; i++) {
        const a = p[(i - 1 + n) % n], b = p[i], c = p[(i + 1) % n], e = p[(i + 2) % n];
        const t1 = Math.sqrt(dist(a, b)) || 1e-9, h = Math.sqrt(dist(b, c)) || 1e-9, t2 = t1 + h, t3 = t2 + (Math.sqrt(dist(c, e)) || 1e-9);
        const m1 = [0, 1].map(k => h * ((b[k] - a[k]) / t1 - (c[k] - a[k]) / t2 + (c[k] - b[k]) / h));
        const m2 = [0, 1].map(k => h * ((c[k] - b[k]) / h - (e[k] - b[k]) / (t3 - t1) + (e[k] - c[k]) / (t3 - t2)));
        const b1 = [b[0] + m1[0] / 3, b[1] + m1[1] / 3], b2 = [c[0] - m2[0] / 3, c[1] - m2[1] / 3];
        this.beziers.push([b, b1, b2, c]);
        firstOfSegment.push(raw.length);
        const steps = Math.max(12, Math.ceil(dist(b, c) / stepLen));
        for (let j = 0; j < steps; j++) {
          const u = j / steps, v = 1 - u;
          const x = v * v * v * b[0] + 3 * v * v * u * b1[0] + 3 * v * u * u * b2[0] + u * u * u * c[0];
          const y = v * v * v * b[1] + 3 * v * v * u * b1[1] + 3 * v * u * u * b2[1] + u * u * u * c[1];
          const dx = 3 * v * v * (b1[0] - b[0]) + 6 * v * u * (b2[0] - b1[0]) + 3 * u * u * (c[0] - b2[0]);
          const dy = 3 * v * v * (b1[1] - b[1]) + 6 * v * u * (b2[1] - b1[1]) + 3 * u * u * (c[1] - b2[1]);
          const ddx = 6 * v * (b2[0] - 2 * b1[0] + b[0]) + 6 * u * (c[0] - 2 * b2[0] + b1[0]);
          const ddy = 6 * v * (b2[1] - 2 * b1[1] + b[1]) + 6 * u * (c[1] - 2 * b2[1] + b1[1]);
          const len = Math.hypot(dx, dy) || 1e-6;
          raw.push({ x, y, tx: dx / len, ty: dy / len, nx: -dy / len, ny: dx / len, k: Math.abs(dx * ddy - dy * ddx) / len ** 3 });
        }
      }
      let d = 0;
      raw.forEach((r, i) => { if (i) d += Math.hypot(r.x - raw[i - 1].x, r.y - raw[i - 1].y); this.samples.push({ ...r, d }); });
      this.total = d + Math.hypot(raw[0].x - raw[raw.length - 1].x, raw[0].y - raw[raw.length - 1].y);
      this.knotD = firstOfSegment.map(i => this.samples[i].d);   /* arc length at every control point */
      let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity, sx = 0, sy = 0;
      for (const r of raw) { minX = Math.min(minX, r.x); maxX = Math.max(maxX, r.x); minY = Math.min(minY, r.y); maxY = Math.max(maxY, r.y); sx += r.x; sy += r.y; }
      this.bounds = { minX, maxX, minY, maxY, w: maxX - minX, h: maxY - minY };
      this.center = { x: sx / raw.length, y: sy / raw.length };
    }

    /* position of control point i as a fraction of the lap [0,1) */
    knotFraction(i) { return this.knotD[((i % this.knotD.length) + this.knotD.length) % this.knotD.length] / this.total; }

    /* the closed curve as an SVG path of cubic Beziers ("M … C … Z"), exactly the curve `at()` samples */
    svgPath(decimals = 1) {
      const f = v => +v.toFixed(decimals), q = pt => `${f(pt[0])},${f(pt[1])}`;
      let s = `M${q(this.beziers[0][0])}`;
      for (const [, b1, b2, c] of this.beziers) s += `C${q(b1)} ${q(b2)} ${q(c)}`;
      return s + 'Z';
    }

    /* point at a fraction of the lap, [0,1) */
    at(f) {
      const s = ((f % 1) + 1) % 1, target = s * this.total, S = this.samples;
      let lo = 0, hi = S.length - 1;
      while (lo <= hi) { const m = (lo + hi) >> 1; if (S[m].d < target) lo = m + 1; else hi = m - 1; }
      const i0 = Math.max(0, lo - 1), p0 = S[i0], p1 = S[(i0 + 1) % S.length];
      const seg = (i0 + 1 < S.length ? p1.d : this.total) - p0.d || 1e-6, t = Math.min(1, Math.max(0, (target - p0.d) / seg));
      const L = (a, b) => a + (b - a) * t;
      const nx = L(p0.nx, p1.nx), ny = L(p0.ny, p1.ny), nl = Math.hypot(nx, ny) || 1;
      return { x: L(p0.x, p1.x), y: L(p0.y, p1.y), nx: nx / nl, ny: ny / nl, tx: ny / nl, ty: -nx / nl, k: L(p0.k, p1.k) };
    }
  }

  /* ---------- pure screen-space layout helpers (unit-tested in ui/prototype/tests) ---------- */
  const RaceLayout = {
    /* Spread cars that would draw on top of each other.
       items: [{ id, s }] where s = position along the track in screen px (larger = further ahead).
       Returns Map id -> { s, lane } with lane in {-1, 0, 1}. Order along the track is kept,
       a lone car keeps its exact position, clustered cars go two-wide (like a starting grid). */
    separate(items, spacing) {
      const out = new Map(), sorted = [...items].sort((a, b) => b.s - a.s);
      let prev = null, laneLast = null, clusterHead = null;
      for (const it of sorted) {
        const inCluster = prev && prev.d - it.s < spacing;
        if (!inCluster) {
          const rec = { s: it.s, lane: 0 };
          out.set(it.id, rec);
          prev = { d: it.s, lane: 0, rec }; clusterHead = rec; laneLast = null;
          continue;
        }
        if (!laneLast) { clusterHead.lane = -1; prev.lane = -1; laneLast = { '-1': clusterHead.s, '1': Infinity }; }
        const lane = -prev.lane;
        const d = Math.min(it.s, laneLast[lane] - spacing, prev.d - spacing / 2);
        laneLast[lane] = d;
        const rec = { s: d, lane };
        out.set(it.id, rec);
        prev = { d, lane, rec };
      }
      return out;
    },

    /* Place text labels next to anchors without overlapping each other, the track or the edges.
       anchors: [{ key, x, y, ox, oy, w, h }] (ox, oy = unit vector pointing away from the track)
       obstacles: [{ x, y }] points the label box must not cover
       area: { x0, y0, x1, y1 } where labels may go
       Returns [{ key, cx, cy, w, h, ax, ay }]; anchors with no free spot are left out. */
    placeLabels(anchors, obstacles, area, pad = 3) {
      const placed = [];
      const hitsBox = (a, b) => Math.abs(a.cx - b.cx) * 2 < a.w + b.w + pad * 2 && Math.abs(a.cy - b.cy) * 2 < a.h + b.h + pad * 2;
      const hitsPoint = (a, p) => Math.abs(a.cx - p.x) * 2 < a.w + pad * 2 && Math.abs(a.cy - p.y) * 2 < a.h + pad * 2;
      for (const an of anchors) {
        let best = null;
        for (const side of [1, -1]) {
          for (const dist of [14, 24, 36, 50]) {
            for (const slide of [0, 0.6, -0.6]) {
              const ox = an.ox * side, oy = an.oy * side;
              /* push the box out by its half-extent along the offset so the near edge sits at `dist` */
              const ext = Math.abs(ox) * an.w / 2 + Math.abs(oy) * an.h / 2;
              const c = { cx: an.x + ox * (dist + ext) - oy * slide * an.w, cy: an.y + oy * (dist + ext) + ox * slide * an.h, w: an.w, h: an.h };
              if (c.cx - c.w / 2 < area.x0 || c.cx + c.w / 2 > area.x1 || c.cy - c.h / 2 < area.y0 || c.cy + c.h / 2 > area.y1) continue;
              if (placed.some(p => hitsBox(c, p))) continue;
              if (obstacles.some(p => hitsPoint(c, p))) continue;
              best = c; break;
            }
            if (best) break;
          }
          if (best) break;
        }
        if (best) placed.push({ key: an.key, ...best, ax: an.x, ay: an.y });
      }
      return placed;
    },
  };

  /* ---------- the map ---------- */
  const PIT_FROM = 0.93, PIT_SPAN = 0.14; /* pit lane along the main straight, fraction of the lap (estimate) */

  class RaceMap {
    /* entries: Map carId -> { no, livery: { primary, secondary, text }, mine } ; corners: [[fraction, name]] */
    constructor(canvas, spline, entries, corners) {
      this.cv = canvas; this.ctx = canvas.getContext('2d');
      this.sp = spline; this.entries = entries; this.corners = corners || [];
      this.inset = { l: 0, t: 0, r: 0, b: 0 };
      this.zoom = 1; this.fitZoom = 1; this.panX = 0; this.panY = 0;
      this.follow = false; this.selected = null; this.hover = null;
      this.frame = null; this.drawn = []; this.onSelect = null; this.onFollow = null; this.onView = null;
      this.colors = { bg: '#15181e', road: '#2c3038', edge: 'rgba(255,255,255,.28)', label: 'rgba(236,231,220,.62)', accent: '#f5c518' };
      this._resize(); this._bind(); this.fit();
    }

    destroy() { this._ac.abort(); }

    setInset(ins) { this.inset = ins; }
    setFollow(on) { this.follow = on; if (this.onFollow) this.onFollow(on); }
    select(id) { this.selected = id; }

    _resize() {
      const r = this.cv.parentElement.getBoundingClientRect(), dpr = devicePixelRatio || 1;
      this.W = r.width; this.H = r.height;
      this.cv.width = Math.round(r.width * dpr); this.cv.height = Math.round(r.height * dpr);
      this.cv.style.width = r.width + 'px'; this.cv.style.height = r.height + 'px';
      this.dpr = dpr;
    }
    resize() { const z = this.zoom / this.fitZoom; this._resize(); const keep = z; this.fit(); if (keep !== 1) this.zoomBy(keep); this.draw(); }

    /* free area = canvas minus the overlays */
    area() { const i = this.inset; return { x0: i.l, y0: i.t, x1: this.W - i.r, y1: this.H - i.b }; }

    fit() {
      const b = this.sp.bounds, a = this.area(), pad = 48;
      const w = Math.max(100, a.x1 - a.x0 - pad * 2), h = Math.max(100, a.y1 - a.y0 - pad * 2);
      this.fitZoom = this.zoom = Math.min(w / b.w, h / b.h);
      this.panX = (a.x0 + a.x1) / 2 - (b.minX + b.w / 2) * this.zoom;
      this.panY = (a.y0 + a.y1) / 2 - (b.minY + b.h / 2) * this.zoom;
      this._view();
    }

    zoomBy(f, cx, cy) {
      const a = this.area();
      cx = cx ?? (a.x0 + a.x1) / 2; cy = cy ?? (a.y0 + a.y1) / 2;
      const z = Math.max(this.fitZoom * 0.7, Math.min(this.fitZoom * 14, this.zoom * f));
      const wx = (cx - this.panX) / this.zoom, wy = (cy - this.panY) / this.zoom;
      this.zoom = z; this.panX = cx - wx * z; this.panY = cy - wy * z;
      this._view(); this.draw();
    }
    _view() { if (this.onView) this.onView(this.zoom / this.fitZoom); }

    _bind() {
      this._ac = new AbortController();
      const o = { signal: this._ac.signal }, c = this.cv;
      let drag = null;
      const local = e => { const r = c.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; };
      c.addEventListener('pointerdown', e => { if (e.button) return; drag = { x: e.clientX, y: e.clientY, moved: false }; c.setPointerCapture(e.pointerId); }, o);
      c.addEventListener('pointermove', e => {
        if (drag) {
          const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
          if (!drag.moved && Math.hypot(dx, dy) < 4) return;
          if (!drag.moved) { drag.moved = true; if (this.follow) this.setFollow(false); }
          this.panX += dx; this.panY += dy; drag.x = e.clientX; drag.y = e.clientY;
          c.style.cursor = 'grabbing'; this.draw(); return;
        }
        const [x, y] = local(e), h = this._hit(x, y);
        if (h !== this.hover) { this.hover = h; this.draw(); }
        c.style.cursor = h ? 'pointer' : 'grab';
      }, o);
      c.addEventListener('pointerup', e => {
        if (drag && !drag.moved) { const [x, y] = local(e), h = this._hit(x, y); if (this.onSelect) this.onSelect(h); }
        drag = null; c.style.cursor = this.hover ? 'pointer' : 'grab';
      }, o);
      c.addEventListener('pointerleave', () => { if (this.hover) { this.hover = null; this.draw(); } }, o);
      c.addEventListener('wheel', e => { e.preventDefault(); const [x, y] = local(e); this.zoomBy(e.deltaY < 0 ? 1.2 : 1 / 1.2, x, y); }, { passive: false, signal: this._ac.signal });
    }

    _hit(x, y) {
      let best = null, bd = 14;
      for (const d of this.drawn) { const k = Math.hypot(d.x - x, d.y - y); if (k < bd) { bd = k; best = d.id; } }
      return best;
    }

    /* screen-space radius of a dot */
    _r() { return Math.max(5.5, Math.min(11, 5.5 * Math.sqrt(this.zoom / this.fitZoom))); }

    /* where every car is drawn this frame (screen px), after legibility spreading */
    _layout(cars) {
      const arcPx = this.sp.total * this.zoom, L = this.sp.lengthM, r = this._r();
      const track = [], pit = [];
      for (const c of cars) {
        /* physical spot on the lap (a lapped car sits where it sits) */
        if (c.inPitLane) pit.push({ id: c.carId, s: (PIT_FROM + PIT_SPAN * Math.min(1, c.pitDistanceM / (PIT_SPAN * L))) * arcPx });
        else track.push({ id: c.carId, s: (((c.distanceM / L) % 1 + 1) % 1) * arcPx });
      }
      const spacing = r * 2 + 2;
      const lay = new Map([...RaceLayout.separate(track, spacing), ...RaceLayout.separate(pit, spacing)]);
      const pitIds = new Set(pit.map(p => p.id)), out = [];
      for (const c of cars) {
        const l = lay.get(c.carId), p = this.sp.at(l.s / arcPx);
        const side = pitIds.has(c.carId) ? -this._pitOff() : l.lane * (r + 1);
        out.push({ id: c.carId, car: c, x: this.panX + p.x * this.zoom + p.nx * side, y: this.panY + p.y * this.zoom + p.ny * side, tx: p.tx, ty: p.ty });
      }
      return out;
    }
    _roadPx() { return Math.max(9, 0.85 * this.zoom); }
    _pitOff() { return this._roadPx() * 0.5 + Math.max(6, this._roadPx() * 0.45); }

    render(frame) { this.frame = frame; this.draw(); }

    draw() {
      if (!this.frame) return;
      const ctx = this.ctx, W = this.W, H = this.H, col = this.colors;
      let pos = this._layout(this.frame.cars);
      if (this.follow && this.selected) {
        const f = pos.find(p => p.id === this.selected), a = this.area();
        if (f) {
          const dx = ((a.x0 + a.x1) / 2 - f.x) * 0.18, dy = ((a.y0 + a.y1) / 2 - f.y) * 0.18;
          this.panX += dx; this.panY += dy; pos = pos.map(p => ({ ...p, x: p.x + dx, y: p.y + dy }));
        }
      }
      this.drawn = pos;
      ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
      ctx.fillStyle = col.bg; ctx.fillRect(0, 0, W, H);
      this._grid(ctx);
      this._track(ctx);
      this._labels(ctx);
      /* draw order: field, then the player's cars, then hover and selection on top */
      const rank = p => (p.id === this.selected ? 3 : p.id === this.hover ? 2 : this.entries.get(p.id)?.mine ? 1 : 0);
      for (const p of [...pos].sort((a, b) => rank(a) - rank(b))) this.renderCar(ctx, p, this.entries.get(p.id), this._r(), p.id === this.selected, p.id === this.hover);
    }

    _grid(ctx) {
      const step = 48;
      ctx.strokeStyle = 'rgba(255,255,255,.028)'; ctx.lineWidth = 1; ctx.beginPath();
      const ox = ((this.panX % step) + step) % step, oy = ((this.panY % step) + step) % step;
      for (let x = ox; x < this.W; x += step) { ctx.moveTo(x, 0); ctx.lineTo(x, this.H); }
      for (let y = oy; y < this.H; y += step) { ctx.moveTo(0, y); ctx.lineTo(this.W, y); }
      ctx.stroke();
    }

    _path(ctx, off = 0, from = 0, to = 1) {
      ctx.beginPath();
      const n = Math.max(24, Math.round((to - from) * 600));
      for (let i = 0; i <= n; i++) {
        const p = this.sp.at(from + (to - from) * i / n);
        const x = this.panX + p.x * this.zoom + p.nx * off, y = this.panY + p.y * this.zoom + p.ny * off;
        i ? ctx.lineTo(x, y) : ctx.moveTo(x, y);
      }
    }

    _track(ctx) {
      const road = this._roadPx(), c = this.colors;
      ctx.lineJoin = 'round'; ctx.lineCap = 'round';
      /* pit lane */
      this._path(ctx, -this._pitOff(), PIT_FROM, PIT_FROM + PIT_SPAN);
      ctx.strokeStyle = '#22262d'; ctx.lineWidth = Math.max(5, road * 0.55); ctx.stroke();
      ctx.strokeStyle = 'rgba(230,184,0,.4)'; ctx.lineWidth = 1; ctx.stroke();
      /* kerb band, road, edge */
      this._path(ctx); ctx.closePath();
      ctx.strokeStyle = 'rgba(255,255,255,.07)'; ctx.lineWidth = road + 8; ctx.stroke();
      ctx.strokeStyle = c.road; ctx.lineWidth = road; ctx.stroke();
      ctx.strokeStyle = c.edge; ctx.lineWidth = 1; ctx.setLineDash([2, 5]); ctx.stroke(); ctx.setLineDash([]);
      /* start / finish: chequer across the road */
      const p = this.sp.at(0), x = this.panX + p.x * this.zoom, y = this.panY + p.y * this.zoom, half = road / 2 + 3, sq = Math.max(2.5, road / 6);
      ctx.save(); ctx.translate(x, y); ctx.rotate(Math.atan2(p.ny, p.nx));
      for (let i = -half, k = 0; i < half; i += sq, k++) for (let j = 0; j < 2; j++) { ctx.fillStyle = (k + j) % 2 ? '#111' : '#f4efe4'; ctx.fillRect(i, -sq + j * sq, sq, sq); }
      ctx.restore();
    }

    _labels(ctx) {
      if (!this.corners.length) return;
      const fs = 11, a = this.area();
      ctx.font = `600 ${fs}px Archivo, sans-serif`;
      const cx = this.panX + this.sp.center.x * this.zoom, cy = this.panY + this.sp.center.y * this.zoom;
      const anchors = this.corners.map(([f, name]) => {
        const p = this.sp.at(f), x = this.panX + p.x * this.zoom, y = this.panY + p.y * this.zoom;
        const out = (x - cx) * p.nx + (y - cy) * p.ny >= 0 ? 1 : -1;
        const text = name.toUpperCase();
        return { key: text, x, y, ox: p.nx * out, oy: p.ny * out, w: ctx.measureText(text).width + 4, h: fs + 4, edge: this._roadPx() / 2 };
      });
      anchors.forEach(an => { an.x += an.ox * an.edge; an.y += an.oy * an.edge; });
      const obstacles = [], half = this._roadPx() / 2 + 3;
      for (let i = 0; i < this.sp.samples.length; i += 3) {
        const s = this.sp.samples[i], x = this.panX + s.x * this.zoom, y = this.panY + s.y * this.zoom;
        obstacles.push({ x, y }, { x: x + s.nx * half, y: y + s.ny * half }, { x: x - s.nx * half, y: y - s.ny * half });
      }
      const placed = RaceLayout.placeLabels(anchors, obstacles, { x0: a.x0 + 6, y0: a.y0 + 6, x1: a.x1 - 6, y1: a.y1 - 6 });
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      for (const l of placed) {
        ctx.strokeStyle = 'rgba(236,231,220,.22)'; ctx.lineWidth = 1; ctx.beginPath();
        /* short leader from the kerb to the label's nearest edge */
        const ex = Math.max(l.cx - l.w / 2, Math.min(l.ax, l.cx + l.w / 2)), ey = Math.max(l.cy - l.h / 2, Math.min(l.ay, l.cy + l.h / 2));
        ctx.moveTo(l.ax, l.ay); ctx.lineTo(ex, ey); ctx.stroke();
        ctx.fillStyle = this.colors.label; ctx.fillText(l.key, l.cx, l.cy + 0.5);
      }
    }

    /* =====================================================================
     * RENDERER SWAP POINT (PP-052)
     * One car, in screen space. `p` = { x, y, tx, ty } (position and unit
     * heading), `e` = entry { no, livery, mine }, `r` = dot radius in px.
     * Today: a dot in team colours with the race number. For car silhouettes
     * replace the body of this method (rotate by atan2(p.ty, p.tx) and draw a
     * sprite); the map, layout and hit-testing do not need to change.
     * ===================================================================== */
    renderCar(ctx, p, e, r, selected, hovered) {
      const lv = e.livery;
      ctx.save(); ctx.translate(p.x, p.y);
      if (e.mine) { ctx.beginPath(); ctx.arc(0, 0, r + 3, 0, 7); ctx.strokeStyle = lv.accent || '#e03a3e'; ctx.lineWidth = 1.6; ctx.stroke(); }
      if (selected) { ctx.beginPath(); ctx.arc(0, 0, r + 6, 0, 7); ctx.strokeStyle = this.colors.accent; ctx.lineWidth = 2; ctx.stroke(); }
      else if (hovered) { ctx.beginPath(); ctx.arc(0, 0, r + 5, 0, 7); ctx.strokeStyle = 'rgba(255,255,255,.7)'; ctx.lineWidth = 1.5; ctx.stroke(); }
      ctx.beginPath(); ctx.arc(0, 0, r, 0, 7);
      ctx.fillStyle = lv.primary; ctx.fill();
      ctx.lineWidth = 1.2; ctx.strokeStyle = lv.secondary; ctx.stroke();
      if (r >= 7.5 || selected || hovered) {
        ctx.fillStyle = lv.text; ctx.font = `700 ${Math.round(r * 1.05)}px Archivo, sans-serif`;
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(e.no, 0, 0.5);
      }
      ctx.restore();
    }
    /* =================== END RENDERER SWAP POINT =================== */
  }

  const g = typeof window !== 'undefined' ? window : globalThis;
  g.TrackSpline = TrackSpline; g.RaceMap = RaceMap; g.RaceLayout = RaceLayout;
  if (typeof module !== 'undefined') module.exports = { TrackSpline, RaceLayout };
})();
