import assert from 'node:assert/strict';
import test from 'node:test';
import { flagCode, flagSprite, hasFlag } from './flags.mjs';

test('the flag sprite keeps the era Polish flag', () => {
  const sprite = flagSprite();
  assert.match(sprite, /id="fl-POL"/);
  assert.match(sprite, /#dc143c/);
  assert.equal(flagCode('POL'), 'POL');
  assert.equal(hasFlag('DEU'), true);
});
