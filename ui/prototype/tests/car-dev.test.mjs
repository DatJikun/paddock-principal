// Run: node --test ui/prototype/tests/car-dev.test.mjs
// The car screen records commands from the mock view. It does not simulate development.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const js = readFileSync(new URL('../js/car-dev.js', import.meta.url), 'utf8');
const ctx = { console };
ctx.window = ctx;
vm.createContext(ctx);
vm.runInContext(js, ctx, { filename: 'car-dev.js' });
const CarDev = ctx.CarDev;

const ui = {
  fields: list => list.filter(Boolean).map(f => `${f.k}=${f.v}`).join('\n'),
  st: text => `[${text}]`,
  btn: (label, o = {}) => `<button${o.attrs || ''}>${label}</button>`,
  panel: (title, inner, o = {}) => `<section class="${o.cls || ''}"><h2>${title}</h2>${o.right || ''}${inner}</section>`,
  tabs: (_g, items) => items.map(i => i[1]).join('|'),
  check: '',
  icon: () => '',
};
const head = (title, fields) => `<h1>${title}</h1>${fields}`;
const screen = (lang, state) => CarDev.screen({ lang, ui, head, state: state || CarDev.initial() });

test('pl and en copy have the same keys', () => {
  const pl = Object.keys(CarDev.COPY.pl).sort();
  const en = Object.keys(CarDev.COPY.en).sort();
  assert.deepEqual(pl, en);
  for (const key of pl) {
    assert.ok(CarDev.COPY.pl[key].trim(), key);
    assert.ok(CarDev.COPY.en[key].trim(), key);
  }
});

test('polish and english counts', () => {
  assert.equal(CarDev.count('pl', 1, 'day'), '1 dzień');
  assert.equal(CarDev.count('pl', 2, 'day'), '2 dni');
  assert.equal(CarDev.count('pl', 5, 'day'), '5 dni');
  assert.equal(CarDev.count('pl', 12, 'race'), '12 wyścigów');
  assert.equal(CarDev.count('pl', 2, 'week'), '2 tygodnie');
  assert.equal(CarDev.count('pl', 5, 'person'), '5 osób');
  assert.equal(CarDev.count('pl', 22, 'person'), '22 osoby');
  assert.equal(CarDev.count('pl', 34, 'person'), '34 osoby');
  assert.equal(CarDev.count('en', 1, 'day'), '1 day');
  assert.equal(CarDev.count('en', 11, 'day'), '11 days');
  assert.equal(CarDev.count('en', 2, 'person'), '2 people');
});

test('split nudge keeps a total of 100 and the command is SetDevelopmentSplit', () => {
  let state = CarDev.initial();
  state = CarDev.reduce(state, { type: 'nudge-split', key: 'currentPercent', delta: 5 }).state;
  assert.equal(state.draft.currentPercent, 65);
  assert.equal(state.draft.accountPercent, 10);
  assert.equal(state.draft.nextYearPercent, 25);
  const out = CarDev.reduce(state, { type: 'confirm', group: 'split' });
  assert.equal(out.result.ok, true);
  assert.equal(JSON.stringify(out.result.command), JSON.stringify({
    command: 'SetDevelopmentSplit',
    organizationId: 'tyrrell',
    currentPercent: 65,
    accountPercent: 10,
    nextYearPercent: 25,
    aeroPriority: 4,
    chassisPriority: 8,
    reliabilityPriority: 5,
    tyresPriority: 6,
  }));
});

test('a split that does not add to 100 is refused and priorities stay in 0..10', () => {
  assert.equal(CarDev.validateSplit({ currentPercent: 50, accountPercent: 40, nextYearPercent: 20 }), 'error.badSplit');
  assert.equal(CarDev.validatePriorities({ aero: 11, chassis: 0, reliability: 0, tyres: 0 }), 'error.badPriority');
  const built = CarDev.buildSplit(CarDev.mock, { currentPercent: 10, accountPercent: 10, nextYearPercent: 10 }, CarDev.mock.priorities);
  assert.equal(built.ok, false);
  assert.equal(built.key, 'error.badSplit');
});

test('confirming the concept records CommitConcept or Wait and does not invent a new gain', () => {
  let state = CarDev.initial();
  state = CarDev.reduce(state, { type: 'pick', group: 'concept', value: 'commit' }).state;
  const committed = CarDev.reduce(state, { type: 'confirm', group: 'concept' });
  assert.equal(committed.result.command.command, 'CommitConcept');
  assert.equal(committed.result.command.projectId, 'dev:4');
  assert.equal(committed.state.production, true);
  const view = CarDev.present(CarDev.mock, committed.state, 'pl');
  assert.equal(view.conceptStatus, 'inProduction');
  assert.equal(view.ongoingGain[0], 0.4);
  assert.equal(view.ongoingGain[1], 1.1);
  assert.equal(view.productionDays, 46);
  assert.equal(view.goesLive, 'GP Holandii · 29 sierpnia');
  const upgrade = view.projects.filter(p => p.kind === 'upgrade').reduce((a, p) => a + p.gain[0], 0);
  assert.notEqual(upgrade, view.ongoingGain[0]);

  let waiting = CarDev.initial();
  waiting = CarDev.reduce(waiting, { type: 'pick', group: 'concept', value: 'wait' }).state;
  const held = CarDev.reduce(waiting, { type: 'confirm', group: 'concept' });
  assert.equal(JSON.stringify(held.result.command), JSON.stringify({ command: 'Wait', organizationId: 'tyrrell', projectId: 'dev:4' }));
  assert.equal(held.state.production, false);
  assert.equal(CarDev.present(CarDev.mock, held.state, 'en').conceptStatus, 'ready');
});

test('deployment timing is DeployConcept: when ready, after N races, or next season', () => {
  const ready = CarDev.buildDeploy(CarDev.mock, 'WhenReady', 0, 'ready');
  assert.equal(ready.command.command, 'DeployConcept');
  assert.equal(ready.command.timing, 'WhenReady');
  assert.equal(ready.command.races, 0);
  const races = CarDev.buildDeploy(CarDev.mock, 'AfterRaces', 4, 'ready');
  assert.equal(races.command.timing, 'AfterRaces');
  assert.equal(races.command.races, 4);
  assert.equal(CarDev.buildDeploy(CarDev.mock, 'AfterRaces', 0, 'ready').key, 'error.badTiming');
  assert.equal(CarDev.buildDeploy(CarDev.mock, 'AfterRaces', 31, 'ready').key, 'error.badTiming');
  assert.equal(CarDev.buildDeploy(CarDev.mock, 'NextSeason', 0, 'ready').command.timing, 'NextSeason');
  assert.equal(CarDev.buildDeploy(CarDev.mock, 'Hold', 0, 'ready').key, 'error.badTiming');

  let state = CarDev.initial();
  state = CarDev.reduce(state, { type: 'pick', group: 'timing', value: 'AfterRaces' }).state;
  state = CarDev.reduce(state, { type: 'nudge-races', key: 'races', delta: 1 }).state;
  const out = CarDev.reduce(state, { type: 'confirm', group: 'timing' });
  assert.equal(out.result.command.timing, 'AfterRaces');
  assert.equal(out.result.command.races, 4);
  assert.equal(out.state.production, false);

  let now = CarDev.initial();
  now = CarDev.reduce(now, { type: 'pick', group: 'timing', value: 'WhenReady' }).state;
  const live = CarDev.reduce(now, { type: 'confirm', group: 'timing' });
  assert.equal(live.state.production, true);
  assert.equal(CarDev.present(CarDev.mock, live.state, 'pl').productionDays, CarDev.mock.concept.productionDays);
});

test('cutting the project records CutProject and keeping the plan does not', () => {
  assert.equal(CarDev.buildCut(CarDev.mock, 'dev:4').key, 'error.notActive');
  let state = CarDev.initial();
  state = CarDev.reduce(state, { type: 'pick', group: 'reply', value: 'cut' }).state;
  const cut = CarDev.reduce(state, { type: 'confirm', group: 'reply' });
  assert.equal(cut.result.command.command, 'CutProject');
  assert.equal(cut.result.command.projectId, 'dev:1');
  const view = CarDev.present(CarDev.mock, cut.state, 'pl');
  assert.equal(view.projects.find(p => p.id === 'dev:1').status, 'cut');
  assert.equal(view.projects.find(p => p.id === 'dev:1').progressPercent, 62);
  assert.equal(CarDev.reduce(cut.state, { type: 'confirm', group: 'reply' }).result.ok, false);

  let keep = CarDev.initial();
  keep = CarDev.reduce(keep, { type: 'pick', group: 'reply', value: 'keep' }).state;
  const kept = CarDev.reduce(keep, { type: 'confirm', group: 'reply' });
  assert.equal(kept.result.command.command, 'KeepPlan');
  assert.equal(CarDev.present(CarDev.mock, kept.state, 'pl').projects.find(p => p.id === 'dev:1').status, 'active');
});

test('a concept that is not ready cannot be committed', () => {
  assert.equal(CarDev.buildCommit(CarDev.mock, 'active').key, 'error.notReady');
  assert.equal(CarDev.buildCommit(CarDev.mock, 'inProduction').key, 'error.notReady');
  const state = CarDev.initial();
  assert.equal(CarDev.reduce(state, { type: 'confirm', group: 'concept' }).result.key, 'error.noChoice');
});

test('the screen shows the decision, the estimate marker and no filler captions', () => {
  const html = screen('pl');
  for (const text of ['Wdrażamy teraz', 'Czekamy', 'Potwierdź', 'ESTIMATE', 'Trzymaj plan', 'Utnij projekt', 'Gdy gotowa', 'Po wyścigach', 'Przyszły sezon']) {
    assert.ok(html.includes(text), text);
  }
  const lower = html.toLowerCase();
  for (const banned of ['kliknij', 'sortuj', 'za nami', 'dlaczego to nigdy', 'ocena naszego']) {
    assert.equal(lower.includes(banned), false, banned);
  }
  const en = screen('en');
  assert.ok(en.includes('Commit now'));
  assert.ok(en.includes('Wait'));
  assert.ok(en.includes('Confirm'));
  assert.equal(en.includes('Wdrażamy teraz'), false);
});
