import assert from 'node:assert/strict';
import test from 'node:test';
import { afterAdvance, classify, nextAction } from './protocol.mjs';

test('a reply and a push stay different messages', () => {
  const reply = classify('{"id":"1","ok":true,"data":{"date":"1955-01-02"}}');
  assert.equal(reply.kind, 'reply');
  assert.equal(reply.ok, true);
  const push = classify('{"type":"dayAdvanced","data":{"date":"1955-01-02"}}');
  assert.equal(push.kind, 'event');
  assert.equal(push.type, 'dayAdvanced');
});

test('Dalej moves the date when the day is free', () => {
  assert.deepEqual(nextAction({ decisionItemId: null }), { type: 'advance' });
  assert.deepEqual(afterAdvance({ ok: true, data: { date: '1955-01-02' } }), {
    type: 'advanced',
    date: '1955-01-02',
  });
});

test('an inbox decision stops the clock and names the screen that shows it', () => {
  assert.equal(nextAction({ decisionItemId: 'item-4' }).type, 'show');
  const refused = afterAdvance({
    ok: false,
    error: { key: 'ready.blockingItem', parameters: {} },
  });
  assert.deepEqual(refused, { type: 'show', screen: 'skrzynka' });
});
