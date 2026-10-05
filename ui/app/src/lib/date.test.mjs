import assert from 'node:assert/strict';
import test from 'node:test';
import { formatDate } from './date.mjs';

test('the opening career date stays 1 January 1955', () => {
  assert.equal(formatDate('1955-01-01', 'en'), '1 January 1955');
  assert.match(formatDate('1955-01-01', 'pl'), /1955/);
  assert.match(formatDate('1955-01-01', 'pl'), /1/);
});
