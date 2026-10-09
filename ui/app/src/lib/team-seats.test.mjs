import assert from 'node:assert/strict';
import test from 'node:test';
import { ownCarRows, seatRows } from './team-seats.mjs';

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

test('the first driver is the first row, the left card, and the second follows (#325)', () => {
  const race = [seat('H. First'), { ...seat('N. Second'), seat: 'NumberTwo' }];
  const rows = seatRows(race);
  assert.equal(rows[0].driver.name, 'H. First');
  assert.equal(rows[1].driver.name, 'N. Second');
});

const car = (carId, own, seatOrder) => [carId, { carId, own, seatOrder }];
const row = (carId, grid) => ({ carId, grid });

test('the pit wall lists the first driver before the second, whatever the grid says (#325)', () => {
  const cars = new Map([car('first', true, 0), car('second', true, 1), car('rival', false, 0)]);
  // The second driver starts ahead of the first on the grid; the order still follows the seat.
  const rows = [row('rival', 1), row('second', 2), row('first', 5)];
  assert.deepEqual(ownCarRows(rows, cars).map((item) => item.carId), ['first', 'second']);
});

test('cars of equal seat order keep the grid order, and a car with no order counts as the first', () => {
  const cars = new Map([car('a', true, 0), car('b', true, 0), ['c', { carId: 'c', own: true }]]);
  const rows = [row('b', 4), row('a', 9), row('c', 2)];
  assert.deepEqual(ownCarRows(rows, cars).map((item) => item.carId), ['c', 'b', 'a']);
});

test('no own car gives no rows', () => {
  assert.deepEqual(ownCarRows([row('x', 1)], new Map([car('x', false, 0)])), []);
  assert.deepEqual(ownCarRows([], new Map()), []);
});
