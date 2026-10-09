/* The race screen's speed control (#322): one pause/resume button and a speed stepped with the arrows, from the list the clock
 * offers (x1 to x30). The clock is the host's: these functions only decide which order a press sends, so the buttons, the keys
 * and the tests read the same rules. Pausing keeps the speed; resuming returns to it. */

/** The speed one step away from `speed` in the list, clamped at the ends. A speed the list does not offer gives the slowest. */
export function stepSpeed(speeds, speed, step) {
  const at = speeds.indexOf(speed);
  if (at < 0) return speeds[0];
  return speeds[Math.min(speeds.length - 1, Math.max(0, at + step))];
}

/** Whether one step from `speed` exists in the list (false at either end). */
export function canStep(speeds, speed, step) {
  const at = speeds.indexOf(speed);
  return at >= 0 && at + step >= 0 && at + step < speeds.length;
}

/** The order a press of an arrow sends, or null when the speed cannot move or the race is over. */
export function speedOrder(clock, step) {
  if (!clock || clock.finished || !canStep(clock.speeds, clock.speed, step)) return null;
  return { action: 'setSpeed', speed: stepSpeed(clock.speeds, clock.speed, step) };
}

/** The order a press of space or of the pause button sends, or null when the race is over. */
export function pauseOrder(clock) {
  if (!clock || clock.finished) return null;
  return { action: clock.paused ? 'play' : 'pause' };
}

/** The step an arrow key asks for: right is faster, left is slower; every other key asks for nothing. */
export function stepOfKey(key) {
  return key === 'ArrowRight' ? 1 : key === 'ArrowLeft' ? -1 : 0;
}
