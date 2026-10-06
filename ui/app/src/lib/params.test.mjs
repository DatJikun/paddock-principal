import assert from 'node:assert/strict';
import test from 'node:test';
import { formatParameter, formatParameters } from './params.mjs';

test('an ISO date in a message is written the way the language writes dates', () => {
  assert.equal(formatParameter('en', 'end', '1956-12-31'), '31 December 1956');
  assert.match(formatParameter('pl', 'until', '1956-12-31'), /31 grudnia 1956/);
});

test('whole dollars in a message become money, and other numbers are left alone', () => {
  assert.equal(formatParameter('en', 'salary', '113000'), '$113,000');
  assert.equal(formatParameter('en', 'years', '3'), '3');
  assert.equal(formatParameter('en', 'cost', '$0'), '$0');
  assert.equal(formatParameter('en', 'person', 'Jean Behra'), 'Jean Behra');
});

test('every parameter of a message is formatted', () => {
  assert.deepEqual(formatParameters('en', { person: 'A B', end: '1957-01-02', salary: '5000' }), {
    person: 'A B',
    end: '2 January 1957',
    salary: '$5,000',
  });
});
