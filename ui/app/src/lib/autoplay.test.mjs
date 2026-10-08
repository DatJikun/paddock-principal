import assert from 'node:assert/strict';
import test from 'node:test';
import { inboxNumber, isNewerItem, mayStart, stopReason } from './autoplay.mjs';
import { DEFAULT_SETTINGS, loadSettings, normalizeSettings, saveSettings } from './settings.mjs';

const quiet = { blockingKind: null, decisionItemId: null, importantItemId: 'inb:3', date: '1955-03-01' };
const race = { date: '1955-03-20' };

test('inbox numbers come from the id and a newer item is a higher number', () => {
  assert.equal(inboxNumber('inb:12'), 12);
  assert.equal(inboxNumber(null), null);
  assert.equal(isNewerItem('inb:12', 'inb:9'), true);
  assert.equal(isNewerItem('inb:9', 'inb:9'), false);
  assert.equal(isNewerItem('inb:2', 'inb:9'), false);
  assert.equal(isNewerItem('inb:1', null), true);
  assert.equal(isNewerItem(null, 'inb:9'), false);
});

test('a quiet day does not stop play', () => {
  assert.equal(stopReason({ shell: quiet, nextRace: race, seenImportantId: 'inb:3' }), null);
});

test('a held clock stops play, by decision or by any other blocking item', () => {
  assert.equal(stopReason({ shell: { ...quiet, decisionItemId: 'inb:4' }, nextRace: race, seenImportantId: 'inb:3' }), 'held');
  assert.equal(stopReason({ shell: { ...quiet, blockingKind: 'inbox.decision' }, nextRace: race, seenImportantId: 'inb:3' }), 'held');
  assert.equal(stopReason({ shell: null, nextRace: race }), 'held');
});

test('a race day stops play before the race is run', () => {
  assert.equal(stopReason({ shell: { ...quiet, date: '1955-03-20' }, nextRace: race, seenImportantId: 'inb:3' }), 'race');
});

test('a new important item stops play and an old one does not', () => {
  assert.equal(stopReason({ shell: { ...quiet, importantItemId: 'inb:5' }, nextRace: race, seenImportantId: 'inb:3' }), 'inbox');
  assert.equal(stopReason({ shell: { ...quiet, importantItemId: 'inb:3' }, nextRace: race, seenImportantId: 'inb:3' }), null);
  assert.equal(stopReason({ shell: { ...quiet, importantItemId: 'inb:2' }, nextRace: race, seenImportantId: 'inb:3' }), null);
});

test('a race that has just been run and a new season stop play', () => {
  assert.equal(stopReason({ shell: quiet, nextRace: race, seenImportantId: 'inb:3', raced: true }), 'raced');
  assert.equal(stopReason({ shell: quiet, nextRace: race, seenImportantId: 'inb:3', seasonChanged: true }), 'season');
});

test('play starts only when nothing holds the clock and it is not a race day', () => {
  assert.equal(mayStart({ shell: quiet, nextRace: race }), true);
  assert.equal(mayStart({ shell: { ...quiet, decisionItemId: 'inb:4' }, nextRace: race }), false);
  assert.equal(mayStart({ shell: { ...quiet, date: '1955-03-20' }, nextRace: race }), false);
  assert.equal(mayStart({ shell: null, nextRace: null }), false);
});

test('settings fall back to the defaults and snap the speed to a known one', () => {
  assert.deepEqual(normalizeSettings(null), DEFAULT_SETTINGS);
  assert.equal(DEFAULT_SETTINGS.daySeconds, 0.5);
  assert.equal(normalizeSettings({ daySeconds: 0.6 }).daySeconds, 0.5);
  assert.equal(normalizeSettings({ daySeconds: 5 }).daySeconds, 2);
  assert.equal(normalizeSettings({ autoAdvance: 'yes' }).autoAdvance, true);
  assert.equal(normalizeSettings({ autoAdvance: false }).autoAdvance, false);
});

test('settings survive a round trip through storage and a broken store', () => {
  const memory = new Map();
  const storage = { getItem: (key) => memory.get(key) ?? null, setItem: (key, value) => memory.set(key, value) };
  saveSettings({ autoAdvance: false, daySeconds: 1, menuMotion: false }, storage);
  assert.deepEqual(loadSettings(storage), { autoAdvance: false, daySeconds: 1, menuMotion: false });
  const broken = { getItem: () => { throw new Error('blocked'); }, setItem: () => { throw new Error('blocked'); } };
  assert.deepEqual(loadSettings(broken), DEFAULT_SETTINGS);
  saveSettings(DEFAULT_SETTINGS, broken);
});
