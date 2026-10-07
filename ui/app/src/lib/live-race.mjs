/*
 * The race mode's reading of a race tape (PP-052). Display only (TECH §3): every value here is folded from the events and
 * frames the bridge sent (`liveRace`, `liveFrames`), at the race time the host's clock gives. Nothing is simulated, nothing is
 * decided, and the result is already written: this only answers "what did the timing screen show at time t".
 */

/** The family of a compound id ("treaded.hard" -> "hard", "slick.early.soft" -> "soft", "wet" -> "wet", "C3" -> null). */
export function tyreFamily(id) {
  if (!id) return null;
  const last = String(id).split('.').pop();
  return ['hard', 'medium', 'soft', 'qualifier', 'wet'].includes(last) ? last : null;
}

/** The letter on a tyre chip: H, M, S, Q, W, or the compound's own short code ("C3"). */
export function tyreLetter(id) {
  if (!id) return '';
  const family = tyreFamily(id);
  if (family) return { hard: 'H', medium: 'M', soft: 'S', qualifier: 'Q', wet: 'W' }[family];
  return String(id).split('.').pop().toUpperCase().slice(0, 3);
}

/** Index of the last event at or before `t` (events are in time order), or -1. */
export function lastIndexAt(events, t) {
  let lo = 0;
  let hi = events.length - 1;
  let found = -1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (events[mid].timeMs <= t) {
      found = mid;
      lo = mid + 1;
    } else hi = mid - 1;
  }
  return found;
}

/** The flag race control shows at time `t`: green, sc, vsc, red or chequered. */
export function flagAt(events, t) {
  let flag = 'green';
  const end = lastIndexAt(events, t);
  for (let i = 0; i <= end; i++) {
    const e = events[i];
    if (e.kind === 'finish') return 'chequered';
    if (e.kind === 'sc') flag = e.key && e.key.endsWith('.vsc') ? 'vsc' : 'sc';
    else if (e.kind === 'scEnd') flag = 'green';
    else if (e.kind === 'red') flag = 'red';
  }
  return flag;
}

/** The track state at time `t`: the start condition, then the last weather change ("dry", "damp", "wet", "extreme"). */
export function conditionAt(race, t) {
  let condition = race.startCondition ?? null;
  const end = lastIndexAt(race.events, t);
  for (let i = 0; i <= end; i++) {
    const e = race.events[i];
    if (e.kind === 'weather' && e.key) condition = e.key.split('.').pop();
  }
  return condition;
}

/**
 * The timing tower at race time `t`. Order: cars still in the race by laps completed, then by when they last crossed the
 * line (that is what a timing screen does); retired cars below, by laps. The gap is the time behind the first car to cross
 * the same line on the same lap; laps down count the leader's crossings before the car's own last crossing.
 */
export function towerAt(race, t) {
  const cars = new Map();
  for (const car of race.cars) {
    cars.set(car.carId, {
      carId: car.carId,
      laps: 0,
      crossMs: 0,
      grid: car.grid ?? 999,
      lastLapMs: null,
      bestLapMs: null,
      stops: 0,
      tyres: null,
      stintFrom: 0,
      inPit: false,
      out: false,
      finished: false,
      finishPos: null,
      call: null,
    });
  }
  const leaderCross = [0];
  let leaderLaps = 0;
  const end = lastIndexAt(race.events, t);
  for (let i = 0; i <= end; i++) {
    const e = race.events[i];
    const row = e.carId ? cars.get(e.carId) : null;
    switch (e.kind) {
      case 'lap':
        if (!row) break;
        row.laps = e.lap;
        row.crossMs = e.timeMs;
        row.lastLapMs = e.lapTimeMs;
        if (e.lapTimeMs !== null && (row.bestLapMs === null || e.lapTimeMs < row.bestLapMs)) row.bestLapMs = e.lapTimeMs;
        if (e.lap > leaderLaps) {
          leaderLaps = e.lap;
          leaderCross[e.lap] = e.timeMs;
        }
        break;
      case 'pitIn':
        if (row) row.inPit = true;
        break;
      case 'pitOut':
        if (!row) break;
        row.inPit = false;
        row.stops += 1;
        row.stintFrom = row.laps;
        if (e.tyres) row.tyres = e.tyres;
        break;
      case 'retire':
        if (row) row.out = true;
        break;
      case 'finish':
        if (!row) break;
        row.finished = true;
        row.finishPos = e.position;
        break;
      case 'call':
        if (row) row.call = e;
        break;
      default:
        break;
    }
  }

  const rows = [...cars.values()].sort((a, b) => {
    if (a.out !== b.out) return a.out ? 1 : -1;
    if (a.finished && b.finished) return a.finishPos - b.finishPos;
    if (b.laps !== a.laps) return b.laps - a.laps;
    if (a.laps === 0) return a.grid - b.grid;
    return a.crossMs - b.crossMs;
  });

  const leadersBefore = (time) => {
    let n = 0;
    for (let lap = 1; lap < leaderCross.length; lap++) if (leaderCross[lap] <= time) n = lap;
    return n;
  };
  let previous = null;
  rows.forEach((row, index) => {
    row.pos = index + 1;
    row.tyreLaps = row.laps - row.stintFrom;
    row.gained = row.out || row.grid === 999 ? 0 : row.grid - row.pos;
    row.lapsDown = row.laps > 0 ? Math.max(0, leadersBefore(row.crossMs) - row.laps) : 0;
    row.gapMs = row.laps > 0 && leaderCross[row.laps] !== undefined ? row.crossMs - leaderCross[row.laps] : null;
    row.intervalMs =
      previous && !row.out && row.lapsDown === previous.lapsDown && row.laps === previous.laps && row.gapMs !== null && previous.gapMs !== null
        ? row.gapMs - previous.gapMs
        : null;
    previous = row;
  });

  const finished = rows.some((row) => row.finished);
  const lap = finished ? race.totalLaps : Math.min(race.totalLaps, leaderLaps + 1);
  return { lap, leaderLaps, rows };
}

/** Narrated lines up to time `t`, newest first; `mine` keeps only lines about the player's own cars. */
export function transcriptAt(race, t, mine = false) {
  const end = lastIndexAt(race.events, t);
  const out = [];
  for (let i = end; i >= 0; i--) {
    const e = race.events[i];
    if (!e.key) continue;
    if (mine && !e.own) continue;
    out.push(e);
  }
  return out;
}

/** The fastest lap so far at time `t` (the last "fastest" event), or null. */
export function fastestAt(events, t) {
  for (let i = lastIndexAt(events, t); i >= 0; i--) if (events[i].kind === 'fastest') return events[i];
  return null;
}

/** What the pit wall hears up to `t`, newest first: our strategist's calls and race control (flags and weather). */
export function radioAt(race, t, limit = 4) {
  const out = [];
  for (let i = lastIndexAt(race.events, t); i >= 0 && out.length < limit; i--) {
    const e = race.events[i];
    if (e.key && RADIO_KINDS.has(e.kind)) out.push(e);
  }
  return out;
}

const RADIO_KINDS = new Set(['call', 'sc', 'scEnd', 'red', 'weather']);

/** Events that became due between two race times (exclusive, inclusive], in tape order, for the radio and race-control pop-ups. */
export function eventsBetween(events, from, to) {
  if (to <= from) return [];
  const start = lastIndexAt(events, from) + 1;
  const end = lastIndexAt(events, to);
  return events.slice(start, end + 1);
}

/**
 * One car's display sample at time `t` from the fetched frame windows: the frames linearly interpolated (racing-line metres,
 * speed, pit lane). Null when no window holds the car at `t` (a retired car, or a window that is still on its way).
 */
export function sampleAt(windows, carId, t) {
  for (const win of windows) {
    if (t < win.fromMs || t > win.toMs) continue;
    const car = win.cars.find((item) => item.carId === carId);
    if (!car || car.timeMs.length === 0) continue;
    const times = car.timeMs;
    if (t < times[0] || t > times[times.length - 1]) continue;
    let lo = 0;
    let hi = times.length - 1;
    while (lo < hi - 1) {
      const mid = (lo + hi) >> 1;
      if (times[mid] <= t) lo = mid;
      else hi = mid;
    }
    const span = times[hi] - times[lo];
    const k = span > 0 ? Math.min(1, Math.max(0, (t - times[lo]) / span)) : 0;
    const pit = car.inPit[lo] && car.inPit[hi];
    return {
      carId,
      distanceM: car.distanceM[lo] + (car.distanceM[hi] - car.distanceM[lo]) * k,
      speedMps: car.speedMps[lo] + (car.speedMps[hi] - car.speedMps[lo]) * k,
      inPitLane: pit,
      pitDistanceM: pit ? car.pitM[lo] + (car.pitM[hi] - car.pitM[lo]) * k : 0,
    };
  }
  return null;
}

/** True when the windows hold every frame between `from` and `to`. */
export function covered(windows, from, to) {
  const spans = windows.map((w) => [w.fromMs, w.toMs]).sort((a, b) => a[0] - b[0]);
  let reach = from;
  for (const [a, b] of spans) {
    if (a > reach) break;
    if (b > reach) reach = b;
    if (reach >= to) return true;
  }
  return reach >= to;
}

/** The host's race time as this viewer should draw it now: the last reading moved on at its speed, never past the end. */
export function clockNow(clock, readAtMs, nowMs) {
  if (!clock) return 0;
  if (clock.paused || clock.finished) return clock.raceTimeMs;
  return Math.min(clock.durationMs, clock.raceTimeMs + Math.max(0, nowMs - readAtMs) * clock.speed);
}

/** Race clock text "1:02:03". */
export function formatClock(ms) {
  const s = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(s / 3600);
  const m = Math.floor(s / 60) % 60;
  return `${h}:${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
}

/** A gap in the tower: "+4.3" or "+1:04.3" (tenths; a tower is read at a glance). */
export function formatTowerGap(ms) {
  if (ms === null || ms === undefined || !Number.isFinite(ms)) return '';
  const tenths = Math.round(ms / 100);
  const minutes = Math.floor(tenths / 600);
  const rest = tenths - minutes * 600;
  const seconds = (rest / 10).toFixed(1);
  return minutes > 0 ? `+${minutes}:${seconds.padStart(4, '0')}` : `+${seconds}`;
}
