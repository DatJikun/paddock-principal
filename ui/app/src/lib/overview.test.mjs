import assert from 'node:assert/strict';
import test from 'node:test';
import { carsOrdered, resultText, resultTone } from './overview.mjs';

const finish = (position, points = '0', classified = true) => ({ position, classified, retirementKey: classified ? '' : 'report.retire.engine', points });

test('podium places keep their metals, then points, then the rest', () => {
  assert.equal(resultTone(finish(1, '8')), 'gold');
  assert.equal(resultTone(finish(2, '6')), 'silver');
  assert.equal(resultTone(finish(3, '4')), 'bronze');
  assert.equal(resultTone(finish(4, '3')), 'points');
  assert.equal(resultTone(finish(7, '0')), 'nopoints');
});

test('a retirement is its own band whatever slot it was given, and an empty cell has none', () => {
  assert.equal(resultTone(finish(2, '0', false)), 'ret');
  assert.equal(resultTone(undefined), 'none');
  assert.equal(resultTone(null), 'none');
});

test('a cell prints the place, the retirement word, or nothing', () => {
  assert.equal(resultText(finish(5, '2'), 'Ret'), '5');
  assert.equal(resultText(finish(9, '0', false), 'Ret'), 'Ret');
  assert.equal(resultText(undefined, 'Ret'), '');
});

test('the cars of a team are listed best first with the retirements last', () => {
  const cars = carsOrdered([finish(11, '0', false), finish(4, '3'), finish(2, '6')]);
  assert.deepEqual(cars.map((car) => car.position), [2, 4, 11]);
});
