import assert from 'node:assert/strict';
import test from 'node:test';
import { EDGES, fitPanels, loadPanels, MAP_ROOM, normalizePanels, PANELS_KEY, savePanels, SIDE, TOWER } from './race-panels.mjs';

const memory = () => {
  const map = new Map();
  return { getItem: (key) => map.get(key) ?? null, setItem: (key, value) => map.set(key, String(value)) };
};

test('widths stay inside their ranges and junk falls back to the defaults', () => {
  assert.deepEqual(normalizePanels(null), { tower: TOWER.base, side: SIDE.base });
  assert.deepEqual(normalizePanels({ tower: 10, side: 9999 }), { tower: TOWER.min, side: SIDE.max });
  assert.deepEqual(normalizePanels({ tower: 'wide', side: 300.4 }), { tower: TOWER.base, side: 300 });
});

test('a wide window draws the panels as asked', () => {
  assert.deepEqual(fitPanels({ tower: 400, side: 480 }, 1920), { tower: 400, side: 480 });
});

test('a narrow window shrinks both panels so the map keeps its room, never below their minimum', () => {
  const fitted = fitPanels({ tower: 400, side: 480 }, 1100);
  assert.ok(fitted.tower + fitted.side <= 1100 - MAP_ROOM - EDGES);
  assert.ok(fitted.tower >= TOWER.min && fitted.side >= SIDE.min);
  assert.ok(fitted.tower < 400 && fitted.side < 480);
  assert.deepEqual(fitPanels({ tower: 400, side: 480 }, 800), { tower: TOWER.min, side: SIDE.min });
});

test('the widths survive a reload and a broken store gives the defaults', () => {
  const storage = memory();
  savePanels({ tower: 300, side: 400 }, storage);
  assert.deepEqual(loadPanels(storage), { tower: 300, side: 400 });
  storage.setItem(PANELS_KEY, '{oops');
  assert.deepEqual(loadPanels(storage), { tower: TOWER.base, side: SIDE.base });
  assert.deepEqual(loadPanels(undefined), { tower: TOWER.base, side: SIDE.base });
});
