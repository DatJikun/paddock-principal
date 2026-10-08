/**
 * The race seats of a team card as keyed rows for its `{#each}`. The bridge gives a seat no id (TeamCardDriver is name,
 * nationality, age, seat) and two drivers can share a name, so the key is the position in the list (#303).
 */
export function seatRows(drivers) {
  return drivers.map((driver, index) => ({ key: `seat-${index}`, driver }));
}
