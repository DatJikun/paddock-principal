/* =========================================================================
 * PADDOCK PRINCIPAL · MOCK RACE FEED (PP-052)
 * -------------------------------------------------------------------------
 * Stand-in for the backend until the race tape's position frames
 * (R-FRAMES, #152 / PR #159) are wired to the UI. It is NOT the race engine
 * and holds no game rules worth keeping: a crude speed-from-curvature model
 * so the screen has something moving. Every number here is a placeholder.
 *
 * Output of step(dtMs), the only thing the race screen reads:
 *   frame  : PositionFrame { raceTimeMs, cars: CarFrame[] }
 *            CarFrame mirrors src/Paddock.Domain/Racing/CarFrame.cs:
 *            { raceTimeMs, carId, distanceM, speedMps, inPitLane, pitDistanceM }
 *   timing : what the timing screens show (order, gaps, laps, tyres, pits)
 *   events : [{ type, raceTimeMs, lap, carId?, other?, value?, key? }]
 *            (data only; the screen turns them into text through I18N)
 * Input: requestPit(carId, compound): a pit call from the player's pit wall.
 * Deterministic: fixed 50 ms internal step and a seeded RNG.
 * ========================================================================= */

(() => {
  'use strict';
  const g = typeof window !== 'undefined' ? window : globalThis;

  const LIVERIES = {
    tyrrell:    { primary: '#1f4f9a', secondary: '#ffffff', accent: '#e03a3e', text: '#ffffff' },
    ferrari:    { primary: '#c4161c', secondary: '#ffffff', accent: '#f5c518', text: '#ffffff' },
    lotus:      { primary: '#16130e', secondary: '#c9a24a', accent: '#f0dba0', text: '#c9a24a' },
    mclaren:    { primary: '#e8e4da', secondary: '#e03a3e', accent: '#e03a3e', text: '#c4161c' },
    brabham:    { primary: '#c22026', secondary: '#f2f2f2', accent: '#ffffff', text: '#ffffff' },
    march:      { primary: '#f07d20', secondary: '#111111', accent: '#ffffff', text: '#111111' },
    shadow:     { primary: '#2a2a2a', secondary: '#bfbfbf', accent: '#ffffff', text: '#ffffff' },
    surtees:    { primary: '#f2f2f2', secondary: '#2b59a2', accent: '#2b59a2', text: '#2b59a2' },
    williams:   { primary: '#1e3350', secondary: '#c8aa44', accent: '#c8aa44', text: '#ffffff' },
    ensign:     { primary: '#1a5030', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    ligier:     { primary: '#1d64b4', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    penske:     { primary: '#7a1fa2', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    copersucar: { primary: '#e6b800', secondary: '#006633', accent: '#006633', text: '#000000' },
  };

  const rng = seed => () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };

  const STEP = 50;                         /* ms per internal step */
  const PIT_FROM = 0.93, PIT_SPAN = 0.14;  /* must match race-map.js */
  const PIT_SPEED = 80 / 3.6;
  const WEAR_PER_LAP = { S: 2.4, M: 1.6, H: 1.1 };   /* % per lap, placeholder */
  const GRIP = { S: 1.006, M: 1, H: 0.994 };

  class MockRaceFeed {
    constructor({ trackKey, laps, seed = 1976, mineTeam = 'tyrrell' }) {
      const tr = DB.tracks[trackKey];
      this.track = tr; this.laps = laps; this.L = tr.len * 1000;
      this.sp = new g.TrackSpline(tr.map, tr.len);
      this.rand = rng(seed);
      this.t = 0; this.acc = 0; this.flag = 'green'; this.finishOrder = [];
      this.conditions = { sky: 'sunny', airC: 24, trackC: 31 };
      this.bucket = 25; this.reach = [];     /* time the race first reached each 25 m mark */
      this.fastest = null;
      /* grid order from the mock DB rating plus seeded noise (stands in for qualifying) */
      const grid = [...DB.grid].map(r => ({ r, q: r[6] + this.rand() * 8 })).sort((a, b) => b.q - a.q);
      this.entries = grid.map(({ r }) => ({
        carId: r[1], no: r[0], name: r[2], short: r[2].split(' ').slice(-1)[0], nat: r[3], teamId: r[4],
        team: DB.teams[r[4]]?.name || r[4], livery: LIVERIES[r[4]] || LIVERIES.shadow, mine: r[4] === mineTeam,
      }));
      this.cars = grid.map(({ r }, i) => ({
        id: r[1], mine: r[4] === mineTeam,
        dist: -(Math.floor(i / 2) * 16 + (i % 2) * 8 + 6),   /* two-by-two grid, 16 m rows */
        v: 0, pace: 0.955 + (r[6] - 50) / 48 * 0.045 + (this.rand() - 0.5) * 0.004,
        pit: null, pitD: 0, boxT: 0, entryDist: 0, call: null,
        tyre: 'M', wear: 100, pits: 0, lapsDone: 0, lapStart: 0, last: null, best: null,
        aiStop: r[4] === mineTeam ? null : 28 + Math.floor(this.rand() * 26),
        finished: false, warned: false, stopT: 0,
      }));
      this.prevOrder = this.cars.map(c => c.id);
      this.started = false;
    }

    vProfile(f) { const k = this.sp.at(f).k; return 1.36 * (90 + 215 / (1 + 45 * Math.min(0.25, k))) / 3.6; }  /* scaled to ~1:21 laps (estimate) */

    requestPit(carId, compound) {
      const c = this.cars.find(x => x.id === carId);
      if (!c || !c.mine || c.pit || c.finished || this.flag === 'chequered') return false;
      c.call = compound;
      this._pending.push({ type: 'radio', key: 'boxThisLap', carId });
      return true;
    }
    cancelPit(carId) { const c = this.cars.find(x => x.id === carId); if (c && !c.pit) c.call = null; }

    step(dtMs) {
      this._pending = this._pending || [];
      const events = this._pending; this._pending = [];
      if (!this.started) { this.started = true; events.push({ type: 'start' }); }
      this.acc += dtMs;
      while (this.acc >= STEP) { this.acc -= STEP; this._tick(STEP / 1000, events); }
      events.forEach(e => { e.raceTimeMs ??= this.t; e.lap ??= this._leaderLap(); });
      return { frame: this._frame(), timing: this._timing(), events };
    }

    _leaderLap() { return Math.min(this.laps, Math.max(1, Math.floor(Math.max(...this.cars.map(c => c.dist)) / this.L) + 1)); }

    _tick(dt, ev) {
      this.t += dt * 1000;
      const L = this.L, ordered = [...this.cars].sort((a, b) => b.dist - a.dist);
      for (const c of this.cars) {
        if (c.finished && c.v < 0.5) continue;
        if (c.pit) { this._pitTick(c, dt, ev); continue; }
        const f = ((c.dist / L) % 1 + 1) % 1;
        let target = c.finished ? 25 : Math.min(this.vProfile(f), this.vProfile(f + 40 / L)) * c.pace * GRIP[c.tyre] * (1 - (100 - c.wear) * 0.0005);
        /* do not drive through the car ahead in corners; on straights it may pass */
        const ahead = ordered.find(o => o !== c && !o.pit && o.dist > c.dist && o.dist - c.dist < 9);
        if (ahead && this.sp.at(f).k > 0.05) target = Math.min(target, ahead.v);
        const a = target > c.v ? 7 : 22;
        c.v += Math.max(-a * dt, Math.min(a * dt, target - c.v));
        if (c.finished && c.v < 26) { c.stopT += dt; if (c.stopT > 25) c.v = Math.max(0, c.v - 3 * dt); }
        const before = c.dist; c.dist += c.v * dt;
        const lapBefore = Math.floor(before / L), lapNow = Math.floor(c.dist / L);
        if (lapNow > lapBefore && before >= 0) this._lapDone(c, ev);
        else if (lapNow > lapBefore) c.lapStart = this.t;
        if (!c.finished) c.wear = Math.max(5, c.wear - WEAR_PER_LAP[c.tyre] * (c.v * dt) / L);
        if (c.mine && !c.warned && c.wear < 50) { c.warned = true; ev.push({ type: 'radio', key: 'tyresGoing', carId: c.id, value: Math.round(c.wear) }); }
        /* pit entry */
        const fb = ((before / L) % 1 + 1) % 1, fn = ((c.dist / L) % 1 + 1) % 1;
        const wants = c.call || (c.aiStop && c.lapsDone + 1 >= c.aiStop);
        if (wants && !c.finished && this.flag !== 'chequered' && fb < PIT_FROM && fn >= PIT_FROM) {
          c.pit = 'lane'; c.pitD = 0; c.entryDist = c.dist; c.v = Math.min(c.v, PIT_SPEED);
          c.boxT = 13 + this.rand() * 8; c.boxTotal = c.boxT;
          ev.push({ type: 'pitIn', carId: c.id });
        }
        if (c.v > 0) this._reach(c.dist);
      }
      this._order(ev);
    }

    _pitTick(c, dt, ev) {
      const len = PIT_SPAN * this.L, box = len * 0.5;
      if (c.pit === 'box') {
        c.v = 0; c.boxT -= dt;
        if (c.boxT <= 0) {
          c.pit = 'lane2'; c.tyre = c.call || 'M'; c.wear = 100; c.pits++; c.call = null; c.warned = false; c.aiStop = null;
          ev.push({ type: 'pitOut', carId: c.id, value: Math.round(c.boxTotal * 10) / 10 });
          if (c.mine) ev.push({ type: 'radio', key: 'pitDone', carId: c.id, value: Math.round(c.boxTotal * 10) / 10 });
        }
        return;
      }
      c.v = Math.min(PIT_SPEED, c.v + 6 * dt);
      const before = c.pitD; c.pitD += c.v * dt;
      if (c.pit === 'lane' && before < box && c.pitD >= box) { c.pitD = box; c.pit = 'box'; c.v = 0; return; }
      if (c.pitD >= len) {
        const before2 = c.entryDist; c.dist = c.entryDist + len; c.pit = null; c.pitD = 0;
        if (Math.floor(c.dist / this.L) > Math.floor(before2 / this.L)) this._lapDone(c, ev);
      }
    }

    _lapDone(c, ev) {
      if (c.finished) return;   /* cool-down lap: nothing counts any more */
      const lt = this.t - c.lapStart; c.lapStart = this.t; c.lapsDone++;
      if (c.lapsDone > 1) {
        c.last = lt; if (!c.best || lt < c.best) c.best = lt;
        if (!this.fastest || lt < this.fastest.ms) {
          this.fastest = { carId: c.id, ms: lt };
          ev.push({ type: 'fastestLap', carId: c.id, value: lt });
        }
      } else c.last = lt;
      if (this.flag === 'chequered') {
        c.finished = true; c.finishT = this.t; this.finishOrder.push(c.id);
        if (c.mine) ev.push({ type: 'radio', key: 'finished', carId: c.id, value: this.finishOrder.length });
      } else if (this.flag === 'green' && c.lapsDone >= this.laps) {
        this.flag = 'chequered'; c.finished = true; c.finishT = this.t; this.finishOrder.push(c.id);
        ev.push({ type: 'chequered', carId: c.id });
        if (c.mine) ev.push({ type: 'radio', key: 'finished', carId: c.id, value: 1 });
      }
    }

    _reach(d) {
      const b = Math.floor(d / this.bucket);
      if (b < 0) return;
      while (this.reach.length <= b) this.reach.push(this.t);
    }

    /* classified by laps completed; on the same lap a car that took the flag is ahead,
       finishers among themselves by the time they took it, the rest by distance */
    _sorted() {
      const laps = c => c.finished ? c.lapsDone : Math.floor(this._effDist(c) / this.L);
      return [...this.cars].sort((a, b) =>
        laps(b) - laps(a) || (b.finished - a.finished) ||
        (a.finished ? a.finishT - b.finishT : this._effDist(b) - this._effDist(a)));
    }
    _effDist(c) { return c.pit ? c.entryDist + Math.min(c.pitD, PIT_SPAN * this.L) : c.dist; }

    _order(ev) {
      const now = this._sorted().map(c => c.id);
      if (this.t > 20000) now.forEach((id, i) => {
        const was = this.prevOrder.indexOf(id), passed = this.prevOrder[i];
        if (was === i + 1 && now[i + 1] === passed) {
          const a = this.cars.find(c => c.id === id), b = this.cars.find(c => c.id === passed);
          if (!a.pit && !b.pit && !a.finished) ev.push({ type: 'overtake', carId: id, other: passed, value: i + 1 });
        }
      });
      this.prevOrder = now;
    }

    _frame() {
      return {
        raceTimeMs: Math.round(this.t),
        cars: this.cars.map(c => ({
          raceTimeMs: Math.round(this.t), carId: c.id,
          distanceM: c.pit ? c.entryDist : c.dist, speedMps: c.v,
          inPitLane: !!c.pit, pitDistanceM: c.pit ? c.pitD : 0,
        })),
      };
    }

    _timing() {
      const rows = this._sorted(), L = this.L;
      const gapOf = c => {
        const w = rows[0];
        if (c.finished && w.finished) return c.lapsDone < w.lapsDone ? { laps: w.lapsDone - c.lapsDone } : { s: (c.finishT - w.finishT) / 1000 };
        const d = this._effDist(c), lead = this._effDist(rows[0]);
        if (lead - d >= L && !c.finished) return { laps: Math.floor((lead - d) / L) };
        const b = Math.floor(d / this.bucket);
        if (b < 0) return null;   /* still behind the start line */
        return { s: b >= 0 && b < this.reach.length ? Math.max(0, (this.t - this.reach[b]) / 1000) : 0 };
      };
      let prevGap = null;
      return {
        raceTimeMs: Math.round(this.t), lap: this._leaderLap(), laps: this.laps, flag: this.flag,
        conditions: this.conditions, fastest: this.fastest,
        rows: rows.map((c, i) => {
          const gap = i === 0 ? { s: 0 } : gapOf(c);
          const int = i === 0 || !gap ? null : gap.laps != null ? gap : prevGap && prevGap.s != null ? { s: Math.max(0, gap.s - prevGap.s) } : gap;
          prevGap = gap;
          return {
            carId: c.id, pos: i + 1, lap: Math.min(this.laps, c.lapsDone + 1), gap: i === 0 ? null : gap, int,
            last: c.last, best: c.best, tyre: c.tyre, wear: Math.round(c.wear), pits: c.pits,
            pit: c.pit ? (c.pit === 'box' ? 'box' : 'lane') : null, called: !!c.call, finished: c.finished,
          };
        }),
      };
    }
  }

  g.MockRaceFeed = MockRaceFeed;
  if (typeof module !== 'undefined') module.exports = { MockRaceFeed };
})();
