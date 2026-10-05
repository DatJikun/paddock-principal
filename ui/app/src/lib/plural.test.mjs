import assert from 'node:assert/strict';
import test from 'node:test';
import { plural, pluralCategory } from './plural.mjs';

test('Polish plural forms follow 1 / 2–4 / 5+', () => {
  assert.equal(plural(1, 'osoba', 'osoby', 'osób'), 'osoba');
  assert.equal(plural(2, 'osoba', 'osoby', 'osób'), 'osoby');
  assert.equal(plural(4, 'osoba', 'osoby', 'osób'), 'osoby');
  assert.equal(plural(5, 'osoba', 'osoby', 'osób'), 'osób');
  assert.equal(plural(12, 'osoba', 'osoby', 'osób'), 'osób');
  assert.equal(plural(22, 'osoba', 'osoby', 'osób'), 'osoby');
  assert.equal(plural(0, 'osoba', 'osoby', 'osób'), 'osób');
});

test('catalog categories match the shipped plural rules', () => {
  assert.equal(pluralCategory('pl', 1), 'one');
  assert.equal(pluralCategory('pl', 3), 'few');
  assert.equal(pluralCategory('pl', 14), 'many');
  assert.equal(pluralCategory('pl', 1.5), 'other');
  assert.equal(pluralCategory('en', 1), 'one');
  assert.equal(pluralCategory('en', 2), 'other');
});
