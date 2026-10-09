import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import { COUNTRIES } from './career.mjs';
import { DEMONYM, flagCode, flagInner, flagSprite, hasFlag } from './flags.mjs';

const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
const en = JSON.parse(readFileSync(new URL('../../../../strings/en.json', import.meta.url), 'utf8'));
const AUTHORED = new URL('../../../../data/authored/', import.meta.url).pathname;

// The name pools use OTHER as a fallback bucket for a nationality without a pool of its own
// (FixtureNameSource). A person never carries it, so it needs neither a flag nor a name.
const NOT_A_COUNTRY = new Set(['OTHER']);

function jsonFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return jsonFiles(path);
    return name.endsWith('.json') ? [path] : [];
  });
}

// Every value under a "nationality" or "country" key, wherever it sits in the authored data.
function countryValues(node, out = []) {
  if (Array.isArray(node)) {
    for (const item of node) countryValues(item, out);
  } else if (node && typeof node === 'object') {
    for (const [key, value] of Object.entries(node)) {
      if ((key === 'nationality' || key === 'country') && typeof value === 'string') out.push(value);
      else countryValues(value, out);
    }
  }
  return out;
}

function authoredValues() {
  return jsonFiles(AUTHORED).flatMap((file) => countryValues(JSON.parse(readFileSync(file, 'utf8'))));
}

test('the flag sprite keeps the era Polish flag', () => {
  const sprite = flagSprite();
  assert.match(sprite, /id="fl-POL"/);
  assert.match(sprite, /#dc143c/);
  assert.equal(flagCode('POL'), 'POL');
  assert.equal(hasFlag('DEU'), true);
});

test('a demonym from the real data reaches the same flag as its code', () => {
  assert.equal(flagCode('Italian'), 'ITA');
  assert.equal(flagCode('New Zealander'), 'NZL');
  assert.equal(hasFlag('British'), true);
  assert.equal(hasFlag('Atlantean'), false);
  assert.equal(flagCode('ITA'), 'ITA');
});

test('every country and nationality in the authored data, the name pools and the new-career list has a flag and a name', () => {
  const values = [...authoredValues(), ...COUNTRIES];
  assert.ok(values.length > 100, 'the authored data is read');
  const missing = [];
  for (const value of new Set(values)) {
    if (NOT_A_COUNTRY.has(value)) continue;
    const code = flagCode(value);
    if (!hasFlag(code)) missing.push(`flag ${value}`);
    if (!(`country.${code}` in pl)) missing.push(`pl name ${value}`);
    if (!(`country.${code}` in en)) missing.push(`en name ${value}`);
  }
  assert.deepEqual(missing, []);
});

test('every demonym the generated world can carry maps to a flag and a name', () => {
  const missing = [];
  for (const demonym of Object.keys(DEMONYM)) {
    const code = flagCode(demonym);
    if (!hasFlag(code)) missing.push(`flag ${demonym}`);
    if (!(`country.${code}` in pl)) missing.push(`pl name ${demonym}`);
    if (!(`country.${code}` in en)) missing.push(`en name ${demonym}`);
  }
  assert.deepEqual(missing, []);
});

test('every flag the UI can show is in the sprite', () => {
  const sprite = flagSprite();
  const codes = new Set([...Object.keys(DEMONYM), ...COUNTRIES, ...authoredValues().filter((value) => !NOT_A_COUNTRY.has(value))].map(flagCode));
  for (const code of codes) assert.match(sprite, new RegExp(`id="fl-${code}"`), code);
});

test('Vettel: the German demonym shows the German flag and the name Niemcy', () => {
  assert.equal(flagCode('German'), 'GER');
  assert.equal(hasFlag('German'), true);
  assert.match(flagInner('German'), /#fl-GER"/);
  assert.equal(pl['country.GER'], 'Niemcy');
  assert.equal(en['country.GER'], 'Germany');
});
