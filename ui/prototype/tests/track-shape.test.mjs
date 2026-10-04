// Run: node --test "ui/prototype/tests/*.test.mjs"
// The UI draws tracks from the authored geometry (data/authored/tracks/geometry), through the generated JS file and
// TrackShape. These tests pin the chain: file -> generated data -> resolver -> spline, and the fallback rules.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import vm from 'node:vm';
import { build, REPO_ROOT } from '../tools/build-track-geometry.mjs';

const read = rel => readFileSync(new URL(`../${rel}`, import.meta.url), 'utf8');
const readRepo = rel => JSON.parse(readFileSync(`${REPO_ROOT}/${rel}`, 'utf8'));

function load() {
  const ctx = { console };
  ctx.window = ctx;
  vm.createContext(ctx);
  for (const f of ['js/track-geometry.generated.js', 'js/data.js', 'js/race-map.js', 'js/track-shape.js', 'js/race-sim.js']) vm.runInContext(read(f), ctx, { filename: f });
  return ctx;
}
/* values made inside the vm context have their own Array prototype; compare plain copies */
const plain = v => JSON.parse(JSON.stringify(v));
const G = load();
const geometryDir = `${REPO_ROOT}/data/authored/tracks/geometry`;
const geometryFiles = readdirSync(geometryDir).filter(f => f.endsWith('.json')).sort();

test('the committed generated file is what the build tool writes from the JSON today', () => {
  assert.equal(read('js/track-geometry.generated.js'), build(),
    'stale: run node ui/prototype/tools/build-track-geometry.mjs and commit the result');
});

test('every geometry file is in the generated data with the same control points', () => {
  assert.deepEqual([...Object.keys(G.TRACK_GEOMETRY)].sort(), geometryFiles.map(f => f.replace('.json', '')));
  for (const f of geometryFiles) {
    const file = readRepo(`data/authored/tracks/geometry/${f}`), ui = plain(G.TRACK_GEOMETRY[file.layout_id]);
    assert.deepEqual(ui.points, file.control_points, file.layout_id);
    assert.deepEqual(ui.corners, (file.corners || []).map(c => [c.point, c.name]), file.layout_id);
  }
});

test('every layout of the 1955 calendar has a geometry the UI can draw', () => {
  const layouts = readRepo('data/authored/tracks/race_layout_map.json').filter(e => e.season === 1955).map(e => e.layout_id);
  assert.ok(layouts.length >= 7);
  for (const id of layouts) {
    const s = G.TrackShape.resolve({ layout: id, len: 1 });
    assert.equal(s.source, 'geometry', `${id} falls back`);
    assert.ok(s.spline.total > 0 && Number.isFinite(s.spline.bounds.w));
  }
});

test('every layout id used by the prototype calendar exists in circuits.json', () => {
  const ids = new Set(readRepo('data/authored/tracks/circuits.json').circuits.flatMap(c => c.layouts.map(l => l.layout_id)));
  for (const [key, t] of Object.entries(G.DB.tracks)) assert.ok(ids.has(t.layout), `${key}: unknown layout ${t.layout}`);
});

test('a track with a geometry file has no hand-made map or corner list left in data.js', () => {
  for (const [key, t] of Object.entries(G.DB.tracks)) {
    if (!G.TRACK_GEOMETRY[t.layout]) continue;
    assert.equal(t.map, undefined, `${key}: delete the legacy map`);
    assert.equal(t.corners, undefined, `${key}: corner names come from the geometry file`);
  }
});

test('Monza is drawn from monza_1972.json, with its corner names placed along the lap', () => {
  const s = G.TrackShape.resolve(G.DB.tracks.monza);
  assert.equal(s.source, 'geometry');
  assert.equal(s.layout, 'monza_1972');
  const names = s.corners.map(c => c[1]);
  assert.ok(names.includes('Parabolica') && names.includes('Curva Grande'));
  const fr = s.corners.map(c => c[0]);
  assert.ok(fr.every(f => f >= 0 && f < 1) && fr.every((f, i) => !i || f > fr[i - 1]), 'fractions increasing inside the lap');
  /* normalised to the screen: longest side SIZE units, y down */
  const b = s.spline.bounds;
  assert.ok(Math.abs(Math.max(b.w, b.h) - G.TrackShape.SIZE) < 2, `longest side ${Math.max(b.w, b.h)}`);
});

test('north stays up: a point with larger y (north) is drawn higher on the screen', () => {
  const pts = G.TrackShape.normalise([[0, 0], [100, 0], [100, 50]]);
  assert.ok(pts[2][1] < pts[1][1]);
  assert.ok(pts[1][0] > pts[0][0]);
});

test('legacy: a layout without a geometry file still draws its hand-made map', () => {
  const t = G.DB.tracks.kyalami;
  assert.equal(G.TRACK_GEOMETRY[t.layout], undefined);
  assert.equal(G.TrackShape.resolve(t).source, 'legacy');
});

test('fallback: no geometry and no map gives a neutral stadium, never a throw', () => {
  const t = { layout: 'no_such_layout_1900', len: 5 };
  const s = G.TrackShape.resolve(t);
  assert.equal(s.source, 'fallback');
  assert.equal(s.corners.length, 0);
  assert.equal(G.TrackShape.resolve(t), s, 'cached');
  const b = s.spline.bounds;
  assert.ok(Math.abs(b.w / b.h - 2.5) < 0.08, `stadium is 5 r by 2 r, got ${b.w / b.h}`);
  assert.ok(Math.abs(s.spline.lengthM - 5000) < 1e-9);
  assert.doesNotThrow(() => G.TrackShape.resolve({ layout: null, len: 3.2 }));
  assert.doesNotThrow(() => G.TrackShape.resolve({ layout: 'monza_1972', len: 5.8, map: [] }));
});

test('fallback stadium has the length the backend fallback has (2 pi r + 6 r)', () => {
  const pts = G.TrackShape.fallbackPoints(4);
  const sp = new G.TrackSpline(pts, 4);
  assert.ok(Math.abs(sp.total - 4000) / 4000 < 2e-3, `lap ${sp.total}`);
});

test('the mock race feed runs on a geometry-backed track', () => {
  const feed = new G.MockRaceFeed({ trackKey: 'monza', laps: 3 });
  assert.equal(feed.sp, G.TrackShape.resolve(G.DB.tracks.monza).spline);
});
