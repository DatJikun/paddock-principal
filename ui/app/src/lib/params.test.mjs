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

test('the board bonus of each season target is money (#315)', () => {
  assert.equal(formatParameter('en', 'safeBonus', '1500000'), '$1,500,000');
  assert.equal(formatParameter('en', 'expectedBonus', '4500000'), '$4,500,000');
  assert.equal(formatParameter('en', 'ambitiousBonus', '12000000'), '$12,000,000');
});
