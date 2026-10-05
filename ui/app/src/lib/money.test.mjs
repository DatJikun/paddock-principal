import assert from 'node:assert/strict';
import test from 'node:test';
import { formatMoney } from './money.mjs';

test('cents stay a dollar amount, not a rounded thousand', () => {
  assert.equal(formatMoney(195500, 'en'), '$1,955');
  assert.equal(formatMoney(195550, 'en'), '$1,955.50');
  assert.equal(formatMoney(-250, 'pl'), '−$2,50');
  assert.equal(formatMoney(null, 'pl'), '—');
});
