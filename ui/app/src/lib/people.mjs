/* Plain helpers for the people screens: attribute order, bands and sorting. No game rule lives here; a band is shown as the
   bridge sent it and a table is ordered the way the player asked. */

export const DRIVER_ATTRS = [
  'cornering',
  'braking',
  'smoothness',
  'overtaking',
  'defending',
  'consistency',
  'composure',
  'adaptability',
  'wet_weather',
  'fitness',
  'feedback',
];

export const SEATS = ['NumberOne', 'Equal', 'NumberTwo', 'Reserve'];

/** "12" for a known value, "12–14" for a band. */
export function bandText(low, high) {
  return low === high ? String(low) : `${low}–${high}`;
}

/** The band of one attribute out of a list the bridge sent, or null when the team has none. */
export function bandOf(list, key) {
  return (list ?? []).find((item) => item.key === key) ?? null;
}

export function yearOf(iso) {
  const year = Number(String(iso ?? '').slice(0, 4));
  return Number.isFinite(year) ? year : 0;
}

/** True for a contract that runs out this season or earlier: the date turns red. */
export function endsThisSeason(end, today) {
  return !!end && !!today && yearOf(end) <= yearOf(today);
}

/** The season row of a driver for one year, or null before he has raced. */
export function seasonRow(profile, season) {
  return (profile?.seasons ?? []).find((row) => row.season === season) ?? null;
}

/** Sums the season rows into one career line (this career only). */
export function careerTotals(profile) {
  const total = { starts: 0, wins: 0, podiums: 0, retirements: 0, best: null };
  for (const row of profile?.seasons ?? []) {
    total.starts += row.starts;
    total.wins += row.wins;
    total.podiums += row.podiums;
    total.retirements += row.retirements;
    if (row.best !== null && (total.best === null || row.best < total.best)) total.best = row.best;
  }
  return total;
}

/**
 * One season row as the career table shows it. A zero stays 0; a missing value (no place or points) is an empty cell.
 * The place and the points come from the bridge as they are (#331).
 */
export function careerCells(row) {
  const cell = (value) => (value === null || value === undefined ? '' : String(value));
  return {
    season: cell(row.season),
    team: cell(row.teamName),
    starts: cell(row.starts),
    wins: cell(row.wins),
    podiums: cell(row.podiums),
    retirements: cell(row.retirements),
    place: cell(row.place),
    points: cell(row.points),
  };
}

/** Sorts a copy of the rows by one value, text with the locale and numbers as numbers; a missing value goes last. */
export function sortRows(rows, get, direction = 'asc') {
  const sign = direction === 'desc' ? -1 : 1;
  return [...rows].sort((a, b) => {
    const left = get(a);
    const right = get(b);
    const leftMissing = left === null || left === undefined || left === '';
    const rightMissing = right === null || right === undefined || right === '';
    if (leftMissing || rightMissing) return leftMissing === rightMissing ? 0 : leftMissing ? 1 : -1;
    if (typeof left === 'number' && typeof right === 'number') return (left - right) * sign;
    return String(left).localeCompare(String(right)) * sign;
  });
}

/** Stars (0-5, halves) for an overall on the 1-20 attribute scale: the mean attribute over 4, as DESIGN says (PP-040). */
export function starsOf(overall) {
  return Math.min(5, Math.max(0, Math.round((Number(overall) / 4) * 2) / 2));
}

/**
 * The drivers the player can compare one driver with (#318): his teammates first, then everyone else on the market (under contract
 * elsewhere or free), each person once, without the driver himself. Names only; the profiles come from the bridge.
 */
export function compareChoices(own, market, exclude) {
  const seen = new Set([exclude]);
  const choices = [];
  const add = (person, group) => {
    if (!person?.personId || seen.has(person.personId)) return;
    seen.add(person.personId);
    choices.push({ personId: person.personId, name: person.name, team: person.organizationName ?? null, group });
  };
  for (const driver of own ?? []) add(driver, 'own');
  const others = [...(market?.contracted ?? []), ...(market?.freeAgents ?? [])].filter((person) => person.kind === 'driver');
  for (const person of [...others].sort((a, b) => String(a.name).localeCompare(String(b.name)))) add(person, person.freeAgent ? 'free' : 'other');
  return choices;
}

/**
 * One kind of the market (drivers, or staff of any role), each person once. The bridge lists a person once; this keeps a stray
 * duplicate from breaking the keyed table (the Personnel tab crashed on one, #318). A free entry loses to a contract.
 */
export function marketRows(market, kind) {
  const rows = new Map();
  for (const person of [...(market?.contracted ?? []), ...(market?.freeAgents ?? [])]) {
    if ((kind === 'driver') !== (person.kind === 'driver')) continue;
    if (!rows.has(person.personId)) rows.set(person.personId, person);
  }
  return [...rows.values()];
}
