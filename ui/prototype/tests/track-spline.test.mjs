// Run: node --test "ui/prototype/tests/*.test.mjs"
// The renderer-side TrackSpline must draw the same curve as the backend's TrackGeometry (centripetal Catmull-Rom).
// Reference samples are exported by tests/Paddock.Tests/World/TrackSplineReferenceFixtureTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { TrackSpline } = require('../js/race-map.js');
const ref = JSON.parse(readFileSync(new URL('./fixtures/track-geometry-reference.json', import.meta.url), 'utf8'));

/* Tolerance: positions agree to 2 cm on laps of 3 to 8 km (measured today: under 2 mm). The residual is the dense
   polyline in TrackSpline (one step per 1/8000 of the control polygon) against TrackGeometry's 5 cm micro-steps and
   2 m arc-length resampling. 2 cm is far below a pixel at any zoom the map offers, and loose enough to survive
   floating point differences between runtimes. */
const TOL_M = 0.02;

for (const c of ref.cases) {
  test(`TrackSpline matches TrackGeometry: ${c.name}`, () => {
    const sp = new TrackSpline(c.control_points, c.length_m / 1000);
    assert.ok(Math.abs(sp.total - c.length_m) / c.length_m < 2e-5, `lap length ${sp.total} vs ${c.length_m}`);
    let worst = 0;
    for (const [f, x, y] of c.points) {
      const p = sp.at(f);
      worst = Math.max(worst, Math.hypot(p.x - x, p.y - y));
    }
    if (process.env.SPLINE_VERBOSE) console.log(c.name, 'worst', worst.toFixed(4), 'm', 'lap', sp.total.toFixed(3), c.length_m.toFixed(3));
    assert.ok(worst < TOL_M, `worst deviation ${worst.toFixed(3)} m exceeds ${TOL_M} m`);
  });
}

test('the Bezier path is the curve that at() samples', () => {
  const c = ref.cases[1];
  const sp = new TrackSpline(c.control_points, c.length_m / 1000);
  const d = sp.svgPath(2);
  assert.match(d, /^M[-\d.,]+C/);
  assert.ok(d.endsWith('Z'));
  assert.equal((d.match(/C/g) || []).length, c.control_points.length, 'one cubic per control point');
});

test('knotFraction gives the lap position of each control point, increasing, starting at 0', () => {
  const c = ref.cases[0];
  const sp = new TrackSpline(c.control_points, c.length_m / 1000);
  assert.equal(sp.knotFraction(0), 0);
  for (let i = 1; i < c.control_points.length; i++) assert.ok(sp.knotFraction(i) > sp.knotFraction(i - 1));
  const [x, y] = c.control_points[5], p = sp.at(sp.knotFraction(5));
  assert.ok(Math.hypot(p.x - x, p.y - y) < 0.5, 'the spline passes through its control point');
});
