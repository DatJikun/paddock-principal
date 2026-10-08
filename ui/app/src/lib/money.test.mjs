import assert from 'node:assert/strict';
import test from 'node:test';
import { formatMoney } from './money.mjs';

test('money is whole dollars: cents are rounded away, never shown', () => {
  assert.equal(formatMoney(195500, 'en'), '$1,955');
  assert.equal(formatMoney(195550, 'en'), '$1,956');
  assert.equal(formatMoney(-34848, 'pl'), '−$348');
  assert.equal(formatMoney(-250, 'pl'), '−$3');
  assert.equal(formatMoney(1, 'en'), '$0');
  assert.equal(formatMoney(-1, 'en'), '$0');
  assert.equal(formatMoney(null, 'pl'), '—');
});
