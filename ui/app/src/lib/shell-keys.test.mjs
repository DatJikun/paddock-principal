import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { SHELL_KEYS } from './shell-nav.mjs';

const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
const en = JSON.parse(readFileSync(new URL('../../../../strings/en.json', import.meta.url), 'utf8'));

test('every shell key exists in Polish and English', () => {
  for (const key of SHELL_KEYS) {
    assert.ok(key in pl, key);
    assert.ok(key in en, key);
  }
});
