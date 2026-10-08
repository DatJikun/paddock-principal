import assert from 'node:assert/strict';
import test from 'node:test';
import { seatRows } from './team-seats.mjs';

const seat = (name, age = 30) => ({ name, nationality: 'GBR', age, seat: 'NumberOne' });

test('two race seats with the same name get different keys, so the keyed list stays valid', () => {
  const race = [seat('J. Smith', 24), seat('J. Smith', 41)];
  const keys = seatRows(race).map((row) => row.key);
  assert.equal(new Set(keys).size, race.length);
});

test('a name listed three times still gives three distinct keys', () => {
  const race = [seat('A. Rossi'), seat('A. Rossi'), seat('A. Rossi')];
  const keys = seatRows(race).map((row) => row.key);
  assert.equal(new Set(keys).size, 3);
});

test('each row keeps its driver, in the order the bridge sent them', () => {
  const race = [seat('J. Smith', 24), seat('K. Ito', 35), seat('J. Smith', 41)];
  const rows = seatRows(race);
  assert.deepEqual(rows.map((row) => row.driver), race);
});

test('the keys of the same roster do not change between renders', () => {
  const race = [seat('J. Smith'), seat('J. Smith')];
  assert.deepEqual(
    seatRows(race).map((row) => row.key),
    seatRows(race).map((row) => row.key),
  );
});

test('an empty roster has no rows', () => {
  assert.deepEqual(seatRows([]), []);
});
