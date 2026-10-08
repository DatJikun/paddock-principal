/*
 * The season grid's cells, as plain data. Display only (TECH §3): the bridge sends the stored finish of each car, and
 * this file only names how a cell is read and coloured. Nothing here decides a result or a point.
 */

/** The colour band of one result: podium places keep their metals, then points, then the rest, then a retirement. */
export function resultTone(result) {
  if (!result) return 'none';
  if (!result.classified) return 'ret';
  if (result.position === 1) return 'gold';
  if (result.position === 2) return 'silver';
  if (result.position === 3) return 'bronze';
  return Number(result.points) > 0 ? 'points' : 'nopoints';
}

/** What a cell prints: the finishing place, or the retirement word the caller passes (nothing when the car did not race). */
export function resultText(result, retired) {
  if (!result) return '';
  return result.classified ? String(result.position) : retired;
}

/** A cell of a constructors grid lists its cars best place first; the retirements follow the classified cars. */
export function carsOrdered(results) {
  return [...results].sort((a, b) => Number(b.classified) - Number(a.classified) || a.position - b.position);
}
