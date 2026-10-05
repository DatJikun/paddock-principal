import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { afterAdvance, blockingLabel, classify, nextAction } from './protocol.mjs';

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

test('the Dalej bar names the decision with its parameters filled', () => {
  const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
  const fill = (message) => pl[message.key].replace(/\{(\w+)\}/g, (_, name) => message.parameters[name] ?? `{${name}}`);
  const shell = {
    decisionItemId: 'item-1',
    decisionKind: 'board.seasonTarget',
    decisionSubject: {
      key: 'board.seasonTarget.subject',
      parameters: { safeTarget: '6', expectedTarget: '4', ambitiousTarget: '2' },
    },
  };
  const label = blockingLabel(shell);
  assert.equal(label.area, 'inbox.area.board');
  const subject = fill(label.subject);
  assert.ok(subject.includes('P6') && subject.includes('P4') && subject.includes('P2'), subject);
  assert.ok(!subject.includes('{'), subject);
  assert.equal(blockingLabel({ decisionItemId: null, decisionKind: null, decisionSubject: null }), null);
  const odd = blockingLabel({ decisionItemId: 'x', decisionKind: 'mystery.kind', decisionSubject: shell.decisionSubject });
  assert.equal(odd.area, 'inbox.area.other');
});
