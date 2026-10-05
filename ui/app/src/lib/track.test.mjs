import assert from 'node:assert/strict';
import test from 'node:test';
import { trackOutline } from './track.mjs';

const square = [
  { x: 0, y: 0 },
  { x: 1000, y: 0 },
  { x: 1000, y: 500 },
  { x: 0, y: 500 },
];

test('a layout without enough points draws nothing', () => {
  assert.equal(trackOutline([]), null);
  assert.equal(trackOutline(null), null);
  assert.equal(trackOutline(square.slice(0, 2)), null);
});

test('a closed outline keeps the longest side at 80 units with north up', () => {
  const outline = trackOutline(square);
  assert.ok(outline);
  assert.match(outline.d, /^M0,40C/);
  assert.ok(outline.d.endsWith('Z'));
  assert.equal(outline.d.split('C').length - 1, 4);
  assert.equal(outline.viewBox, '-4 -4 88 48');
  assert.match(outline.tick, /^M[-\d.]+,[-\d.]+L[-\d.]+,[-\d.]+$/);
});

test('a repeated closing point does not add a zero-length segment', () => {
  const outline = trackOutline([...square, { x: 0, y: 0 }]);
  assert.equal(outline.d.split('C').length - 1, 4);
});
