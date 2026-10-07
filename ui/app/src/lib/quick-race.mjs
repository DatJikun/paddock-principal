/* The quick race form (#280) as plain data: the bridge arguments it sends. The race itself is the bridge's: the world is built
   the way a new career builds it and the round is raced by the career's own race day. */

/** The new-career wizard's default preset; the bridge starts a quick race in it (CareerBridge.QuickRacePreset). */
export const QUICK_PRESET = 'Balanced';

/** A fresh seed for one quick race: the same seed shows the same team cards and gives the same race. */
export function freshSeed(random = Math.random) {
  return Math.floor(random() * 0x1_0000_0000);
}

/** The `teams` query for the quick race: the cards of the world the race starts in. */
export function quickTeamsArgs(managerId, year, seed) {
  return {
    managerId,
    year,
    preset: QUICK_PRESET,
    people: null,
    rules: null,
    ai: null,
    history: null,
    randomness: null,
    fatality: null,
    noNumbers: null,
    seed,
  };
}

/** The `startQuickRace` command, or null until a team and a round are chosen. */
export function quickRaceArgs(managerId, year, teamId, round, seed) {
  if (!teamId || !Number.isInteger(round) || round < 1) return null;
  return { managerId, year, teamId, round, seed };
}
