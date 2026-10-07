import assert from 'node:assert/strict';
import test from 'node:test';
import { fallbackPoints, separate, TrackSpline } from './race-map.mjs';

test('the spline closes the loop and flips north up', () => {
  const sp = new TrackSpline([
    { x: 0, y: 0 },
    { x: 1000, y: 0 },
    { x: 1000, y: 500 },
    { x: 0, y: 500 },
  ]);
  const start = sp.at(0);
  assert.ok(Math.abs(start.x) < 1e-6 && Math.abs(start.y) < 1e-6);
  const end = sp.at(0.999999);
  assert.ok(Math.hypot(end.x - start.x, end.y - start.y) < 1);
  assert.ok(sp.bounds.minY < -400 && sp.bounds.maxY < 150, 'north (positive y) is drawn above the start');
  assert.ok(sp.total > 2900 && sp.total < 3400);
});

test('a layout without points still gets a loop of about the lap length', () => {
  const sp = new TrackSpline(fallbackPoints(5000));
  assert.ok(sp.total > 4000 && sp.total < 6000);
});

test('a lone car keeps its spot and a pack goes two-wide in order', () => {
  const lone = separate([{ id: 'a', s: 100 }, { id: 'b', s: 300 }], 10);
  assert.deepEqual(lone.get('a'), { s: 100, lane: 0 });
  const pack = separate([{ id: 'a', s: 100 }, { id: 'b', s: 98 }, { id: 'c', s: 97 }], 10);
  assert.equal(pack.get('a').lane, -1);
  assert.equal(pack.get('b').lane, 1);
  assert.equal(pack.get('c').lane, -1);
  assert.ok(pack.get('a').s > pack.get('b').s && pack.get('b').s > pack.get('c').s);
});
