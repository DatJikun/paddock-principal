/** The race seats of a team card as keyed rows for its `{#each}`. */
export function seatRows(drivers) {
  return drivers.map((driver) => ({ key: driver.name, driver }));
}
