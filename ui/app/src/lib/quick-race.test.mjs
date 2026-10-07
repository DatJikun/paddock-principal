import assert from 'node:assert/strict';
import test from 'node:test';
import { freshSeed, QUICK_PRESET, quickRaceArgs, quickTeamsArgs } from './quick-race.mjs';

test('the team cards are read in the preset and with the seed the race will use', () => {
  const args = quickTeamsArgs('human:player', 1976, 42);
  assert.equal(args.preset, QUICK_PRESET);
  assert.equal(args.year, 1976);
  assert.equal(args.seed, 42);
  assert.equal(args.people, null);
});

test('a quick race needs a team and a round', () => {
  assert.equal(quickRaceArgs('human:player', 1976, '', 3, 1), null);
  assert.equal(quickRaceArgs('human:player', 1976, 'tyrrell', 0, 1), null);
  assert.deepEqual(quickRaceArgs('human:player', 1976, 'tyrrell', 3, 9), {
    managerId: 'human:player',
    year: 1976,
    teamId: 'tyrrell',
    round: 3,
    seed: 9,
  });
});

test('a fresh seed is a whole number the bridge can read', () => {
  assert.equal(freshSeed(() => 0), 0);
  const seed = freshSeed(() => 0.999999);
  assert.ok(Number.isSafeInteger(seed) && seed >= 0 && seed < 2 ** 32);
});
