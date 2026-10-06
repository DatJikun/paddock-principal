import assert from 'node:assert/strict';
import test from 'node:test';
import { formatGap, formatLapTime, formatRaceTime, timeCell } from './race.mjs';

test('lap times read m:ss.mmm', () => {
  assert.equal(formatLapTime(83456), '1:23.456');
  assert.equal(formatLapTime(60005), '1:00.005');
  assert.equal(formatLapTime(null), '');
});

test('race times read h:mm:ss.mmm and drop the hour when there is none', () => {
  assert.equal(formatRaceTime(6067234), '1:41:07.234');
  assert.equal(formatRaceTime(2467234), '41:07.234');
});

test('gaps read +s.mmm and +m:ss.mmm', () => {
  assert.equal(formatGap(12345), '+12.345');
  assert.equal(formatGap(62345), '+1:02.345');
});

test('the time cell is a time for the winner, a gap, laps down, or nothing', () => {
  const base = { classified: true, position: 1, timeMs: 6067234, gapMs: null, lapsDown: 0 };
  assert.deepEqual(timeCell(base), { kind: 'time', text: '1:41:07.234' });
  assert.deepEqual(timeCell({ ...base, position: 2, timeMs: 6070000, gapMs: 2766 }), { kind: 'gap', text: '+2.766' });
  assert.deepEqual(timeCell({ ...base, position: 9, lapsDown: 2 }), { kind: 'lapsDown', laps: 2 });
  assert.deepEqual(timeCell({ ...base, classified: false, timeMs: null }), { kind: 'none' });
  assert.deepEqual(timeCell({ ...base, timeMs: null }), { kind: 'none' });
});
