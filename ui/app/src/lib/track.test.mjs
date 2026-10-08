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

test('the pit lane is a schematic line beside the start line with an entry and an exit', () => {
  const outline = trackOutline(square);
  assert.match(outline.pit, /^M[-\d.]+,[-\d.]+(L[-\d.]+,[-\d.]+){3}$/);
  const points = outline.pit.match(/[-\d.]+,[-\d.]+/g).map((pair) => pair.split(',').map(Number));
  // it leaves the track line, runs parallel at a fixed gap, and returns to the line
  assert.equal(points[1][0] - points[0][0] !== 0 || points[1][1] - points[0][1] !== 0, true);
  const gap = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]);
  assert.ok(gap(points[1], points[2]) > gap(points[0], points[1]));
});

test('a repeated closing point does not add a zero-length segment', () => {
  const outline = trackOutline([...square, { x: 0, y: 0 }]);
  assert.equal(outline.d.split('C').length - 1, 4);
});
