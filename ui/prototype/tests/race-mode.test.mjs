// Run: node --test ui/prototype/tests
// Pure helpers of the race mode: legibility layout of the map and the mock feed contract.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const js = f => readFileSync(new URL(`../js/${f}`, import.meta.url), 'utf8');
function load() {
  const ctx = { console };
  ctx.window = ctx;
  vm.createContext(ctx);
  for (const f of ['data.js', 'race-map.js', 'race-sim.js']) vm.runInContext(js(f), ctx, { filename: f });
  return ctx;
}
const G = load();

test('separate: a lone car keeps its exact position', () => {
  const m = G.RaceLayout.separate([{ id: 'a', s: 100 }, { id: 'b', s: 300 }], 14);
  assert.equal(m.get('a').s, 100); assert.equal(m.get('a').lane, 0);
  assert.equal(m.get('b').s, 300); assert.equal(m.get('b').lane, 0);
});

test('separate: a packed grid never draws two dots on top of each other and keeps order', () => {
  const items = Array.from({ length: 23 }, (_, i) => ({ id: 'c' + i, s: 1000 - i * 3 }));
  const sp = 14, m = G.RaceLayout.separate(items, sp);
  const pts = items.map(it => ({ id: it.id, s: m.get(it.id).s, lane: m.get(it.id).lane }));
  for (let i = 0; i < pts.length; i++)
    for (let j = i + 1; j < pts.length; j++) {
      const a = pts[i], b = pts[j];
      const dx = a.s - b.s, dy = (a.lane - b.lane) * (sp / 2);
      assert.ok(Math.hypot(dx, dy) >= sp / 2 - 1e-9, `${a.id} vs ${b.id}`);
    }
  for (let i = 1; i < pts.length; i++) assert.ok(pts[i].s <= pts[i - 1].s, 'order along the track kept');
});

test('placeLabels: labels never overlap each other or leave the area', () => {
  const anchors = Array.from({ length: 9 }, (_, i) => ({ key: 'L' + i, x: 200 + i * 6, y: 200, ox: 0, oy: -1, w: 120, h: 15 }));
  const out = G.RaceLayout.placeLabels(anchors, [], { x0: 0, y0: 0, x1: 800, y1: 600 });
  assert.ok(out.length > 0);
  for (const l of out) assert.ok(l.cx - l.w / 2 >= 0 && l.cx + l.w / 2 <= 800 && l.cy - l.h / 2 >= 0 && l.cy + l.h / 2 <= 600);
  for (let i = 0; i < out.length; i++)
    for (let j = i + 1; j < out.length; j++) {
      const a = out[i], b = out[j];
      assert.ok(!(Math.abs(a.cx - b.cx) * 2 < a.w + b.w && Math.abs(a.cy - b.cy) * 2 < a.h + b.h), `${a.key} overlaps ${b.key}`);
    }
});

test('placeLabels: a label is dropped rather than drawn over an obstacle', () => {
  const obstacles = [];
  for (let x = 0; x <= 400; x += 4) for (let y = 0; y <= 400; y += 4) obstacles.push({ x, y });
  const out = G.RaceLayout.placeLabels([{ key: 'X', x: 200, y: 200, ox: 1, oy: 0, w: 40, h: 12 }], obstacles, { x0: 0, y0: 0, x1: 400, y1: 400 });
  assert.equal(out.length, 0);
});

test('mock feed emits CarFrames with the R-FRAMES fields, is deterministic and finishes', () => {
  const run = () => {
    const f = new G.MockRaceFeed({ trackKey: 'brands_hatch', laps: 3, seed: 7 });
    let last;
    for (let i = 0; i < 400; i++) last = f.step(1000);
    return last;
  };
  const a = run(), b = run();
  assert.equal(JSON.stringify(a.frame), JSON.stringify(b.frame));
  assert.deepEqual(Object.keys(a.frame.cars[0]).sort(), ['carId', 'distanceM', 'inPitLane', 'pitDistanceM', 'raceTimeMs', 'speedMps']);
  assert.equal(a.timing.flag, 'chequered');
});
