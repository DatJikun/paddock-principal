// Run: node --test "ui/prototype/tests/*.test.mjs"
// The track editor (ui/track-editor) reads and writes the layout file through track-file.js. These tests pin that an
// unchanged file survives load + save: every key and every number of the v2 fields (TECH §6.5a), and the committed
// layouts keep their values. The fixture is the hand-made v2 track the .NET tests use.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import vm from 'node:vm';
import { REPO_ROOT } from '../tools/build-track-geometry.mjs';

const ctx = {};
vm.createContext(ctx);
vm.runInContext(readFileSync(`${REPO_ROOT}/ui/track-editor/track-file.js`, 'utf8'), ctx, { filename: 'track-file.js' });
const { TrackFile } = ctx;
/* values made inside the vm context have their own prototypes; compare plain copies */
const plain = v => JSON.parse(JSON.stringify(v));

const fixturePath = `${REPO_ROOT}/tests/Paddock.Tests/Fixtures/tracks/fixture_2010.json`;
const fixtureText = readFileSync(fixturePath, 'utf8');
const geometryDir = `${REPO_ROOT}/data/authored/tracks/geometry`;
const committed = readdirSync(geometryDir).filter(f => f.endsWith('.json')).sort();

const roundTrip = text => TrackFile.format(TrackFile.parse(text));

test('the v2 fixture saves back byte for byte', () => {
  assert.equal(roundTrip(fixtureText), fixtureText);
});

test('the v2 fixture keeps every field and value after load + save', () => {
  const saved = JSON.parse(roundTrip(fixtureText));
  assert.deepEqual(saved, JSON.parse(fixtureText));
  for (const k of ['width_m', 'edges', 'pit_lane', 'elevation_m', 'corners']) assert.ok(k in saved, k);
});

test('the editor model exposes the v2 fields the canvas draws', () => {
  const m = TrackFile.parse(fixtureText);
  assert.equal(TrackFile.hasV2(m.extra), true);
  assert.equal(TrackFile.widthAt(m.extra, 0.125), 13.5);
  assert.equal(TrackFile.widthAt(m.extra, 0.9), 14.5);
  assert.equal(plain(TrackFile.edgeAt(m.extra, 0.02, 'right')).runoff, 'asphalt');
  assert.equal(TrackFile.edgeAt(m.extra, 0.02, 'left'), null);
  const pit = TrackFile.pitSummary(m.extra);
  assert.equal(pit.limitLength, 1150);
  assert.equal(pit.boxes, 12);
  assert.ok(Math.abs(pit.timeAtLimit - 51.75) < 1e-9);
});

test('scaling the points scales the pit lane with them and nothing else', () => {
  const m = TrackFile.parse(fixtureText);
  const scaled = plain(TrackFile.scaleExtra(m.extra, 2));
  assert.deepEqual(scaled.pit_lane.points[0], [-700, 16]);
  assert.equal(scaled.pit_lane.limit_to, 2500);
  assert.equal(scaled.pit_lane.boxes[0], 1000);
  assert.equal(scaled.pit_lane.limit_kph, 80);
  assert.deepEqual(scaled.width_m, plain(m.extra.width_m));
  assert.deepEqual(plain(m.extra.pit_lane.points[0]), [-350, 8], 'the original model is not changed');
});

test('every committed layout keeps its values after load + save, and a second save changes nothing', () => {
  for (const f of committed) {
    const text = readFileSync(`${geometryDir}/${f}`, 'utf8');
    const once = roundTrip(text);
    assert.deepEqual(JSON.parse(once), JSON.parse(text), f);
    assert.equal(roundTrip(once), once, f);
  }
});

test('unknown keys survive in their place after the known ones', () => {
  const text = JSON.stringify({ layout_id: 'x', control_points: [[0, 0], [1, 0], [1, 1]], source: 's', notes: 'n', future_field: { a: [1, 2] }, corners: [] });
  const saved = JSON.parse(roundTrip(text));
  assert.deepEqual(saved.future_field, { a: [1, 2] });
  assert.deepEqual(Object.keys(saved), ['layout_id', 'control_points', 'source', 'notes', 'corners', 'future_field']);
});
