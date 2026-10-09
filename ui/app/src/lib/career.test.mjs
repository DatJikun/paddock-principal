import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  AXES,
  clampYear,
  contractsArg,
  COUNTRIES,
  emptySetup,
  isCustom,
  latestSave,
  newCareerArgs,
  newestFirst,
  PRESET_KEYS,
  saveLabel,
  sameSave,
  teamLabel,
  teamsArgs,
  TILT_KEYS,
  withPreset,
} from './career.mjs';
import { livery } from './livery.mjs';

const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
const en = JSON.parse(readFileSync(new URL('../../../../strings/en.json', import.meta.url), 'utf8'));

const balanced = {
  name: 'Balanced',
  people: 'RealPotential',
  rules: 'Historical',
  ai: 'ReactToSituation',
  history: 5,
  randomness: 50,
  fatality: 'Off',
  noNumbers: false,
};

test('a preset fills every axis and the form is custom only once one differs', () => {
  const setup = withPreset(emptySetup(1955, '1'), balanced);
  assert.equal(setup.preset, 'Balanced');
  assert.equal(setup.history, 5);
  assert.equal(isCustom(setup, balanced), false);
  assert.equal(isCustom({ ...setup, ai: 'PureRandom' }, balanced), true);
  assert.equal(isCustom({ ...setup, history: '7' }, balanced), true);
  assert.equal(isCustom({ ...setup, noNumbers: 'On' }, balanced), true);
});

test('the teams query and the newCareer command carry the same axes', () => {
  const setup = { ...withPreset(emptySetup(1960, '42'), balanced), ai: 'PureRandom' };
  const you = { given: ' Enzo ', family: 'Test', nationality: 'ITA', tilt: 'none' };
  const query = teamsArgs('human:player', setup);
  const command = newCareerArgs('human:player', setup, you, 'ferrari');
  for (const key of ['preset', 'people', 'rules', 'ai', 'history', 'randomness', 'fatality', 'noNumbers', 'seed']) {
    assert.deepEqual(query[key], command[key], key);
  }
  assert.equal(query.year, 1960);
  assert.equal(query.ai, 'PureRandom');
  assert.equal(query.seed, 42);
  assert.equal(command.givenName, 'Enzo');
  assert.equal(command.teamId, 'ferrari');
});

test('real contracts are the default, only newCareer carries the choice and it never makes the form custom', () => {
  const setup = withPreset(emptySetup(2010, '1'), balanced);
  const you = { given: 'Enzo', family: 'Test', nationality: 'ITA', tilt: 'none' };
  assert.equal(setup.contracts, 'Real');
  assert.equal(newCareerArgs('m', setup, you, 'ferrari').startContracts, 'Real');
  assert.equal(newCareerArgs('m', { ...setup, contracts: 'AllEndThisYear' }, you, 'ferrari').startContracts, 'AllEndThisYear');
  assert.equal(newCareerArgs('m', { ...setup, contracts: 'nonsense' }, you, 'ferrari').startContracts, 'Real');
  assert.equal(contractsArg({}), 'Real');
  assert.equal('startContracts' in teamsArgs('m', setup), false);
  assert.equal(isCustom({ ...setup, contracts: 'AllEndThisYear' }, balanced), false);
  assert.equal(withPreset({ ...setup, contracts: 'AllEndThisYear' }, balanced).contracts, 'AllEndThisYear');
});

test('a seed that is not a plain number is left to the bridge default', () => {
  assert.equal(teamsArgs('m', { ...emptySetup(1955, 'abc') }).seed, null);
  assert.equal(teamsArgs('m', { ...emptySetup(1955, '') }).seed, null);
  assert.equal(teamsArgs('m', { ...emptySetup(1955, '12345678901234567890') }).seed, null);
});

test('the year stays inside the range the career allows', () => {
  assert.equal(clampYear('1948'), 1950);
  assert.equal(clampYear(2050), 2026);
  assert.equal(clampYear('1962'), 1962);
  assert.equal(clampYear('x', 1955), 1955);
});

test('a team without a name from the data is named from its id the way the world names it', () => {
  assert.equal(teamLabel('red_bull'), 'Red Bull');
  assert.equal(teamLabel('ferrari'), 'Ferrari');
  assert.equal(teamLabel('cooper-climax'), 'Cooper Climax');
  assert.equal(teamLabel('hwm'), 'HWM');
  assert.equal(teamLabel('cooper-brm'), 'Cooper BRM');
});

test('saves: the label hides the extension and "continue" is the file written last', () => {
  assert.equal(saveLabel('career.paddock'), 'career');
  assert.equal(sameSave('Career.paddock', 'career'), true);
  const saves = [
    { name: 'a.paddock', savedAt: '2026-10-01T10:00:00Z' },
    { name: 'b.paddock', savedAt: '2026-10-03T10:00:00Z' },
    { name: 'c.paddock', savedAt: '2026-10-02T10:00:00Z' },
  ];
  assert.equal(latestSave(saves).name, 'b.paddock');
  assert.deepEqual(newestFirst(saves).map((save) => save.name), ['b.paddock', 'c.paddock', 'a.paddock']);
  assert.equal(latestSave([]), null);
});

test('every option the form offers has text in both languages', () => {
  const keys = [
    ...Object.values(TILT_KEYS),
    ...Object.values(PRESET_KEYS),
    ...COUNTRIES.map((code) => `country.${code}`),
    ...AXES.people.map((value) => `career.people.${value}`),
    ...AXES.rules.map((value) => `career.rules.${value}`),
    ...AXES.ai.map((value) => `career.ai.${value}`),
    ...AXES.fatality.map((value) => `career.toggle.${value}`),
    ...AXES.contracts.map((value) => `career.contracts.${value}`),
    'career.axis.contracts',
    ...['low', 'typical', 'top'].map((tier) => `team.budget.${tier}`),
    ...['works', 'customer', 'partner', 'badged', 'unknown'].map((kind) => `team.engine.${kind}`),
  ];
  for (const key of keys) {
    assert.ok(key in pl, `pl: ${key}`);
    assert.ok(key in en, `en: ${key}`);
  }
});

test('a team without a livery is drawn in neutral ink, a known one in its colours', () => {
  assert.equal(livery('ferrari').known, true);
  assert.equal(livery('ferrari').main, '#c4161c');
  assert.equal(livery('nobody-knows').known, false);
  assert.match(livery('nobody-knows').main, /^#[0-9a-f]{6}$/);
});
