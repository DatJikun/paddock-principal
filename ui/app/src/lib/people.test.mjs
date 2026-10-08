import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { bandOf, bandText, careerTotals, DRIVER_ATTRS, endsThisSeason, SEATS, seasonRow, sortRows, starsOf } from './people.mjs';

const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
const en = JSON.parse(readFileSync(new URL('../../../../strings/en.json', import.meta.url), 'utf8'));

test('a band shows as one value when the team knows it exactly', () => {
  assert.equal(bandText(12, 12), '12');
  assert.equal(bandText(12, 14), '12–14');
  assert.deepEqual(bandOf([{ key: 'a', low: 1, high: 2 }], 'a'), { key: 'a', low: 1, high: 2 });
  assert.equal(bandOf([], 'a'), null);
  assert.equal(bandOf(undefined, 'a'), null);
});

test('a contract that ends this season turns red, a later one does not', () => {
  assert.equal(endsThisSeason('1955-12-31', '1955-03-04'), true);
  assert.equal(endsThisSeason('1956-12-31', '1955-03-04'), false);
  assert.equal(endsThisSeason(null, '1955-03-04'), false);
});

test('the career line adds up the seasons of this career', () => {
  const profile = {
    seasons: [
      { season: 1955, starts: 5, wins: 1, podiums: 2, retirements: 1, best: 1 },
      { season: 1956, starts: 7, wins: 0, podiums: 1, retirements: 2, best: 3 },
    ],
  };
  assert.deepEqual(careerTotals(profile), { starts: 12, wins: 1, podiums: 3, retirements: 3, best: 1 });
  assert.equal(seasonRow(profile, 1956).starts, 7);
  assert.equal(seasonRow(profile, 1950), null);
  assert.deepEqual(careerTotals({ seasons: [] }), { starts: 0, wins: 0, podiums: 0, retirements: 0, best: null });
});

test('sorting puts missing values last in either direction', () => {
  const rows = [{ n: 3 }, { n: null }, { n: 1 }, { n: 2 }];
  assert.deepEqual(sortRows(rows, (row) => row.n).map((row) => row.n), [1, 2, 3, null]);
  assert.deepEqual(sortRows(rows, (row) => row.n, 'desc').map((row) => row.n), [3, 2, 1, null]);
  assert.deepEqual(sortRows([{ s: 'b' }, { s: 'a' }], (row) => row.s).map((row) => row.s), ['a', 'b']);
});

test('every driver attribute and seat has text in both languages', () => {
  for (const key of DRIVER_ATTRS) {
    assert.ok(`attr.${key}` in pl, `pl: attr.${key}`);
    assert.ok(`attr.${key}` in en, `en: attr.${key}`);
  }
  for (const seat of SEATS) {
    assert.ok(`seat.${seat}` in pl, `pl: seat.${seat}`);
    assert.ok(`seat.${seat}` in en, `en: seat.${seat}`);
  }
});

test('starsOf turns the 1-20 overall into half stars', () => {
  assert.equal(starsOf(20), 5);
  assert.equal(starsOf(10), 2.5);
  assert.equal(starsOf(13), 3.5);
  assert.equal(starsOf(1), 0.5);
});
