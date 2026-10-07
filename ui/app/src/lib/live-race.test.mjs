import assert from 'node:assert/strict';
import test from 'node:test';
import {
  clockNow,
  conditionAt,
  covered,
  eventsBetween,
  flagAt,
  formatClock,
  formatTowerGap,
  sampleAt,
  towerAt,
  transcriptAt,
  tyreFamily,
  tyreLetter,
} from './live-race.mjs';

const ev = (timeMs, kind, extra = {}) => ({
  timeMs,
  kind,
  carId: null,
  lap: 0,
  position: null,
  lapTimeMs: null,
  tyres: null,
  own: false,
  key: null,
  ...extra,
});

const race = {
  totalLaps: 3,
  startCondition: 'dry',
  cars: [
    { carId: 'a', grid: 2 },
    { carId: 'b', grid: 1 },
    { carId: 'c', grid: 3 },
  ],
  events: [
    ev(0, 'start', { key: 'live.event.start' }),
    ev(90_000, 'lap', { carId: 'a', lap: 1, lapTimeMs: 90_000 }),
    ev(91_500, 'lap', { carId: 'b', lap: 1, lapTimeMs: 91_500 }),
    ev(95_000, 'lap', { carId: 'c', lap: 1, lapTimeMs: 95_000 }),
    ev(100_000, 'sc', { key: 'live.event.sc' }),
    ev(120_000, 'weather', { key: 'live.event.weather.wet' }),
    ev(150_000, 'scEnd', { key: 'live.event.scEnd' }),
    ev(170_000, 'pitIn', { carId: 'a', own: true, key: 'live.event.pitIn' }),
    ev(178_000, 'lap', { carId: 'b', lap: 2, lapTimeMs: 86_500 }),
    ev(185_000, 'pitOut', { carId: 'a', own: true, tyres: 'wet', key: 'live.event.pitOut' }),
    ev(186_000, 'lap', { carId: 'c', lap: 2, lapTimeMs: 91_000 }),
    ev(190_000, 'lap', { carId: 'a', lap: 2, lapTimeMs: 100_000 }),
    ev(200_000, 'retire', { carId: 'c', key: 'live.event.retire.engine' }),
    ev(265_000, 'lap', { carId: 'b', lap: 3, lapTimeMs: 87_000 }),
    ev(265_000, 'finish', { carId: 'b', position: 1, key: 'live.event.winner' }),
    ev(280_000, 'lap', { carId: 'a', lap: 3, lapTimeMs: 90_000 }),
    ev(280_000, 'finish', { carId: 'a', position: 2, own: true, key: 'live.event.ownFinish' }),
  ],
};

test('tyre chips read the family or the compound code', () => {
  assert.equal(tyreFamily('treaded.hard'), 'hard');
  assert.equal(tyreFamily('wet'), 'wet');
  assert.equal(tyreFamily('C3'), null);
  assert.equal(tyreLetter('slick.early.soft'), 'S');
  assert.equal(tyreLetter('C3'), 'C3');
  assert.equal(tyreLetter(null), '');
});

test('before the line the tower is the grid; after it, laps then crossing time', () => {
  const grid = towerAt(race, 10_000);
  assert.deepEqual(grid.rows.map((r) => r.carId), ['b', 'a', 'c']);
  assert.equal(grid.lap, 1);

  const lap1 = towerAt(race, 96_000);
  assert.deepEqual(lap1.rows.map((r) => r.carId), ['a', 'b', 'c']);
  assert.equal(lap1.rows[1].gapMs, 1_500);
  assert.equal(lap1.rows[2].intervalMs, 3_500);
  assert.equal(lap1.lap, 2);
});

test('a stop is counted on pit exit with the new tyres, and a retired car drops to the bottom', () => {
  const t = towerAt(race, 210_000);
  assert.deepEqual(t.rows.map((r) => r.carId), ['b', 'a', 'c']);
  const a = t.rows.find((r) => r.carId === 'a');
  assert.equal(a.stops, 1);
  assert.equal(a.tyres, 'wet');
  assert.equal(a.inPit, false);
  assert.equal(a.bestLapMs, 90_000);
  assert.ok(t.rows[2].out);

  const inLane = towerAt(race, 175_000).rows.find((r) => r.carId === 'a');
  assert.equal(inLane.inPit, true);
  assert.equal(inLane.stops, 0);
});

test('at the flag the classification decides the order', () => {
  const end = towerAt(race, 300_000);
  assert.deepEqual(end.rows.map((r) => r.carId), ['b', 'a', 'c']);
  assert.equal(end.lap, 3);
  assert.equal(end.rows[0].finishPos, 1);
});

test('the flag and the track state follow race control and the weather', () => {
  assert.equal(flagAt(race.events, 50_000), 'green');
  assert.equal(flagAt(race.events, 110_000), 'sc');
  assert.equal(flagAt(race.events, 160_000), 'green');
  assert.equal(flagAt([ev(5, 'sc', { key: 'live.event.vsc' })], 10), 'vsc');
  assert.equal(flagAt(race.events, 270_000), 'chequered');
  assert.equal(conditionAt(race, 100_000), 'dry');
  assert.equal(conditionAt(race, 130_000), 'wet');
});

test('the transcript is narrated lines only, newest first, and can keep only our cars', () => {
  const all = transcriptAt(race, 190_000);
  assert.equal(all[0].kind, 'pitOut');
  assert.ok(all.every((e) => e.key));
  const mine = transcriptAt(race, 300_000, true);
  assert.deepEqual(mine.map((e) => e.kind), ['finish', 'pitOut', 'pitIn']);
});

test('pop-ups get exactly the events that became due since the last frame', () => {
  assert.deepEqual(eventsBetween(race.events, 90_000, 100_000).map((e) => e.kind), ['lap', 'lap', 'sc']);
  assert.deepEqual(eventsBetween(race.events, 100_000, 100_000), []);
});

test('a car is drawn between two frames and nowhere outside the fetched windows', () => {
  const windows = [
    {
      fromMs: 0,
      toMs: 10_000,
      cars: [{ carId: 'a', timeMs: [0, 10_000], distanceM: [0, 500], speedMps: [40, 60], inPit: [false, false], pitM: [0, 0] }],
    },
  ];
  const s = sampleAt(windows, 'a', 5_000);
  assert.equal(s.distanceM, 250);
  assert.equal(s.speedMps, 50);
  assert.equal(sampleAt(windows, 'a', 20_000), null);
  assert.equal(sampleAt(windows, 'b', 5_000), null);
  assert.ok(covered([{ fromMs: 0, toMs: 5 }, { fromMs: 5, toMs: 12 }], 1, 10));
  assert.ok(!covered([{ fromMs: 0, toMs: 5 }, { fromMs: 6, toMs: 12 }], 1, 10));
});

test('a viewer moves the host time on at its speed until the next reading', () => {
  const clock = { raceTimeMs: 1_000, durationMs: 10_000, speed: 10, paused: false, finished: false };
  assert.equal(clockNow(clock, 0, 200), 3_000);
  assert.equal(clockNow(clock, 0, 5_000), 10_000);
  assert.equal(clockNow({ ...clock, paused: true }, 0, 5_000), 1_000);
  assert.equal(clockNow(null, 0, 1), 0);
});

test('clock and gap text', () => {
  assert.equal(formatClock(3_723_000), '1:02:03');
  assert.equal(formatTowerGap(4_340), '+4.3');
  assert.equal(formatTowerGap(64_300), '+1:04.3');
  assert.equal(formatTowerGap(null), '');
});
