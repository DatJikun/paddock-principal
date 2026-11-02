import assert from 'node:assert/strict';
import test from 'node:test';
import { championshipRows, hasConstructorsTitle } from './standings.mjs';

const row = (n) => ({ position: n, id: `p${n}`, name: `P${n}`, points: String(30 - n) });
const standings = {
  rules: { constructors: 'BestCar' },
  drivers: Array.from({ length: 22 }, (_, i) => row(i + 1)),
  constructors: Array.from({ length: 11 }, (_, i) => row(i + 1)),
};

test('the panel lists every driver and every constructor, not the top six', () => {
  assert.equal(championshipRows(standings, 'drivers').length, 22);
  assert.equal(championshipRows(standings, 'constructors').length, 11);
});

test('without a constructors title the drivers stay on screen', () => {
  const noTitle = { ...standings, rules: { constructors: 'NoChampionship' } };
  assert.equal(hasConstructorsTitle(noTitle), false);
  assert.equal(championshipRows(noTitle, 'constructors'), noTitle.drivers);
  assert.equal(hasConstructorsTitle({ ...standings, rules: null }), false);
});
