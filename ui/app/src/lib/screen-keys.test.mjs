import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import test from 'node:test';
import { INBOX_AREAS } from './protocol.mjs';
import { retirementLabel } from './race.mjs';

const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
const en = JSON.parse(readFileSync(new URL('../../../../strings/en.json', import.meta.url), 'utf8'));

function sources(dir) {
  const out = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = new URL(entry.name + (entry.isDirectory() ? '/' : ''), dir);
    if (entry.isDirectory()) out.push(...sources(path));
    else if (/\.(svelte|ts)$/.test(entry.name) && !entry.name.endsWith('.generated.ts')) out.push(path);
  }
  return out;
}

test('every literal key a screen translates exists in Polish and English', () => {
  const keys = new Set();
  for (const file of sources(new URL('../', import.meta.url))) {
    const text = readFileSync(file, 'utf8');
    for (const match of text.matchAll(/\bt(?:Count)?\(\s*'([a-z][\w.]+)'/g)) keys.add(match[1]);
  }
  assert.ok(keys.size > 40, `only ${keys.size} keys found`);
  for (const key of keys) {
    assert.ok(key in pl, `pl: ${key}`);
    assert.ok(key in en, `en: ${key}`);
  }
});

test('keys built from bridge values exist for every value the bridge can send', () => {
  const families = [
    ...INBOX_AREAS.map((area) => `inbox.area.${area}`),
    'inbox.area.other',
    ...['Resolved', 'Dismissed', 'Expired'].map((status) => `inbox.status.${status}`),
    ...['None', 'OnePointSharedIfTied', 'OnePointIfTopTen', 'OnePointIfTopTenAndHalfDistance'].map((rule) => `standings.fastestLap.${rule}`),
    ...['NoChampionship', 'BestFinishingCarOnly', 'AllCars'].map((rule) => `standings.constructors.${rule}`),
  ];
  const circuits = JSON.parse(readFileSync(new URL('../../../../data/authored/tracks/circuits.json', import.meta.url), 'utf8'));
  for (const tag of circuits.character_tags) families.push(`track.character.${tag.id}`);
  for (const key of Object.keys(pl).filter((name) => name.startsWith('report.retire.'))) families.push(retirementLabel(key));
  assert.equal(retirementLabel(''), 'race.status.finished');
  assert.equal(retirementLabel('report.retire.engine'), 'race.retired.engine');
  for (const key of families) {
    assert.ok(key in pl, `pl: ${key}`);
    assert.ok(key in en, `en: ${key}`);
  }
});
