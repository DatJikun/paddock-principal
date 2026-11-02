import { test } from 'node:test';
import assert from 'node:assert/strict';
import { changed, formOf, nextStop, placeMove, planCall, revealed, revealOrder, STEPS } from './weekend.mjs';

const grid = [
  { position: 1, carId: 'a' },
  { position: 2, carId: 'b' },
  { position: 3, carId: 'c' },
  { position: 0, carId: 'pit' },
];

test('a watched qualifying shows the grid from the back to pole', () => {
  assert.deepEqual(revealOrder(grid).map((row) => row.carId), ['pit', 'c', 'b', 'a']);
  assert.deepEqual(revealed(grid, 2).map((row) => row.carId), ['c', 'pit']);
  assert.deepEqual(revealed(grid, 99).map((row) => row.carId), ['a', 'b', 'c', 'pit']);
  assert.deepEqual(revealed(grid, 0), []);
});

test('the weekend has four steps in order', () => {
  assert.deepEqual(STEPS, ['practice', 'qualifying', 'strategy', 'race']);
});

test('a plan view becomes a form and the form the bridge call, with the strategist where nothing is set', () => {
  const plan = { carId: 'x', startTyres: 'medium', pace: null, engine: 'standard', stops: null };
  const form = formOf(plan);
  assert.equal(form.pace, 'auto');
  assert.equal(form.stopsMode, 'auto');
  assert.deepEqual(planCall(form, 2010, 1, 'x'), { season: 2010, round: 1, carId: 'x', startTyres: 'medium', pace: null, engine: 'standard', stops: null });
  assert.equal(changed(form, plan), false);

  form.stopsMode = 'own';
  form.stops.push({ lap: '20', tyres: 'hard' });
  assert.deepEqual(planCall(form, 2010, 1, 'x').stops, [{ lap: 20, tyres: 'hard' }]);
  assert.equal(changed(form, plan), true);
});

test('a new stop comes after the last one, on other dry tyres, and never at the flag', () => {
  const form = { startTyres: 'soft', stops: [] };
  assert.deepEqual(nextStop(form, 60, ['soft', 'hard']), { lap: 20, tyres: 'hard' });
  form.stops.push({ lap: 50, tyres: 'hard' });
  assert.deepEqual(nextStop(form, 60, ['soft', 'hard']), { lap: 59, tyres: 'soft' });
});

test('a place moves up, down, stays, or is new', () => {
  assert.deepEqual(placeMove(null, 4), { kind: 'new', by: 0 });
  assert.deepEqual(placeMove(6, 4), { kind: 'up', by: 2 });
  assert.deepEqual(placeMove(3, 5), { kind: 'down', by: 2 });
  assert.deepEqual(placeMove(2, 2), { kind: 'same', by: 0 });
});

test('the weekend and the post-race summary are screens under the calendar, and a booked race opens its summary', async () => {
  const { parseRoute, navOwner } = await import('./shell-nav.mjs');
  const { readFileSync } = await import('node:fs');
  assert.deepEqual(parseRoute('#/weekend'), { name: 'weekend', args: [] });
  assert.deepEqual(parseRoute('#/podsumowanie/3'), { name: 'podsumowanie', args: ['3'] });
  assert.equal(navOwner('weekend'), 'kalendarz');
  assert.equal(navOwner('podsumowanie'), 'kalendarz');
  const app = readFileSync(new URL('../App.svelte', import.meta.url), 'utf8');
  assert.match(app, /location\.hash = `#\/podsumowanie\/\$\{round\}`/);
  assert.match(app, /href="#\/weekend"/);
});
