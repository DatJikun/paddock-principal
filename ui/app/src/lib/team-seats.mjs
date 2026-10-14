/**
 * The race seats of a team card as keyed rows for its `{#each}`. The bridge gives a seat no id (TeamCardDriver is name,
 * nationality, age, seat) and two drivers can share a name, so the key is the position in the list (#303).
 * The bridge lists a team's drivers first driver first (#325); the first row is the left card, the second the right one,
 * and this keeps that order.
 */
export function seatRows(drivers) {
  return drivers.map((driver, index) => ({ key: `seat-${index}`, driver }));
}

/**
 * Our cars of a watched race as pit wall rows, first driver first. `cars` maps a car id to its `LiveCarView`, whose `seatOrder`
 * the bridge sets (0 for the first driver of a team, 1 for the second); equal orders keep the grid order, so a pit wall button
 * never moves under the pointer when the cars swap places.
 * @template {{ carId: string, grid: number }} Row
 * @param {Row[]} rows
 * @param {Map<string, { own?: boolean, seatOrder?: number }>} cars
 * @returns {Row[]}
 */
export function ownCarRows(rows, cars) {
  const order = (row) => cars.get(row.carId)?.seatOrder ?? 0;
  return rows
    .filter((row) => cars.get(row.carId)?.own)
    .sort((a, b) => order(a) - order(b) || a.grid - b.grid);
}
