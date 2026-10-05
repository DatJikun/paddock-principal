import assert from 'node:assert/strict';
import test from 'node:test';
import { addDays, daysBetween, formatDate, formatDay, formatWeekday, weekdayIndex } from './date.mjs';

test('the opening career date stays 1 January 1955', () => {
  assert.equal(formatDate('1955-01-01', 'en'), '1 January 1955');
  assert.match(formatDate('1955-01-01', 'pl'), /1955/);
  assert.match(formatDate('1955-01-01', 'pl'), /1/);
});

test('days to the first race count calendar days, not the local zone', () => {
  assert.equal(daysBetween('1955-01-01', '1955-01-16'), 15);
  assert.equal(daysBetween('1955-03-30', '1955-03-27'), -3);
  assert.equal(daysBetween(null, '1955-01-16'), null);
  assert.equal(weekdayIndex('1955-01-16'), 6);
  assert.equal(addDays('1955-02-27', 2), '1955-03-01');
  assert.equal(formatDay('1955-01-16', 'en'), '16 January');
  assert.match(formatWeekday('1955-01-16', 'en'), /^Sunday/);
  assert.match(formatWeekday('1955-01-16', 'pl'), /^Niedziela/);
});
