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
