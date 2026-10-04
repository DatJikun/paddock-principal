// Run: node --test ui/prototype/tests
// "Auto i rozwój" (PP-043, path A): the mock data keeps the player on bands (truth vs knowledge) and the screen renders the
// concept commitment flow (status, band of further gain, days to the race, production time, "Wdrażamy teraz" / "Czekamy").
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const js = f => readFileSync(new URL(`../js/${f}`, import.meta.url), 'utf8');
function load() {
  const noop = () => {};
  const ctx = { console, document: { body: { insertAdjacentHTML: noop }, addEventListener: noop }, setTimeout };
  ctx.window = ctx;
  vm.createContext(ctx);
  for (const f of ['data.js', 'flags.js', 'ui.js', 'screens.js', 'screens-more.js', 'screens-dev.js']) vm.runInContext(js(f), ctx, { filename: f });
  return ctx;
}
const G = load();
const DEV = vm.runInContext('DB.car.dev', G);
const STATE = vm.runInContext('STATE', G);
const render = () => vm.runInContext('S.auto()', G);
const isBand = b => Array.isArray(b) && b.length === 2 && b[0] < b[1];

test('mock plan: three streams sum to 100 in steps of 5, priorities stay in 0..10', () => {
  const p = DEV.plan;
  assert.equal(p.current + p.account + p.nextYear, 100);
  for (const v of Object.values(p)) assert.equal(v % 5, 0);
  for (const a of DEV.priorities) assert.ok(a.value >= 0 && a.value <= 10);
});

test('truth vs knowledge: the player is only given bands, never a single hidden value', () => {
  assert.ok(isBand(DEV.account.band));
  assert.ok(isBand(DEV.account.ruleLoss));
  assert.ok(isBand(DEV.concept.furtherGain));
  for (const p of DEV.projects) { assert.ok(isBand(p.forecast.gain), p.name); assert.ok(isBand(p.forecast.risk), p.name); }
  for (const f of DEV.finished) assert.ok(isBand(f.effect), f.name);
});

test('every project is run by a real staff member and the decision mails carry the expected options', () => {
  const staff = vm.runInContext('DB.staff', G), inbox = vm.runInContext('DB.inbox', G);
  for (const p of DEV.projects) assert.ok(staff.some(s => s.id === p.engineer), p.name);
  const labels = id => [...inbox.find(m => m.id === id).options.map(o => o.label)];
  assert.deepEqual(labels(DEV.concept.decisionMail), ['Wdrażamy teraz', 'Czekamy']);
  assert.deepEqual(labels(DEV.projects.find(p => p.replyMail).replyMail), ['Trzymamy plan', 'Tniemy projekt']);
});

test('ready concept: status, band, days to the race, production time and both decisions, confirmed with "Potwierdź"', () => {
  STATE.decisions = {};
  const html = render();
  for (const t of ['Gotowa do decyzji', '+0,05–0,20 s/okr.', '11 dni', '63 dni', '4 wyścigi', 'Wdrażamy teraz', 'Czekamy', 'Trzymamy plan', 'Tniemy projekt']) assert.ok(html.includes(t), t);
  assert.equal((html.match(/do-confirm" disabled/g) || []).length, 2, 'both decisions wait for an explicit confirm');
  assert.ok(html.includes('plan-go" disabled'), 'an unchanged plan cannot be confirmed');
  for (const h of ['Stan', 'Dlaczego', 'Prognoza']) assert.ok(html.includes(`<th>${h}</th>`), h);
});

test('committing starts production: the concept goes live after the production date, not on a timer', () => {
  STATE.decisions = { [DEV.concept.decisionMail]: 'Wdrażamy teraz' };
  const html = render();
  assert.ok(html.includes('W produkcji'));
  assert.ok(html.includes('8 września'));
  assert.ok(html.includes('GP Włoch'));
  assert.ok(!html.includes('po N wyścigach'));
});

test('waiting keeps developing; cutting a project is shown on its row', () => {
  STATE.decisions = { [DEV.concept.decisionMail]: 'Czekamy', [DEV.projects.find(p => p.replyMail).replyMail]: 'Tniemy projekt' };
  const html = render();
  assert.ok(html.includes('Rozwijana dalej'));
  assert.ok(html.includes('Zamknięty przed czasem'));
  STATE.decisions = {};
});
