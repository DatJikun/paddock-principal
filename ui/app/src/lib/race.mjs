/**
 * A result row carries the report sentence key of its retirement ("report.retire.engine": "Lap {lap}: {driver}
 * retires with an engine failure"). A table cell wants the short status of the same reason; anything else is
 * shown through its own key.
 */
export function retirementLabel(key) {
  if (!key) return 'race.status.finished';
  const prefix = 'report.retire.';
  return key.startsWith(prefix) ? `race.retired.${key.slice(prefix.length)}` : key;
}

const two = (n) => String(n).padStart(2, '0');
const three = (n) => String(n).padStart(3, '0');

/** A lap time in milliseconds as "1:23.456" (the decimal mark is the game's, not the locale's: timing boards are fixed). */
export function formatLapTime(ms) {
  if (ms === null || ms === undefined || !Number.isFinite(ms)) return '';
  const minutes = Math.floor(ms / 60000);
  const seconds = Math.floor((ms % 60000) / 1000);
  return `${minutes}:${two(seconds)}.${three(ms % 1000)}`;
}

/** A race time in milliseconds as "1:41:07.234", or "41:07.234" when under an hour. */
export function formatRaceTime(ms) {
  if (ms === null || ms === undefined || !Number.isFinite(ms)) return '';
  const hours = Math.floor(ms / 3600000);
  const rest = ms % 3600000;
  const minutes = Math.floor(rest / 60000);
  const seconds = Math.floor((rest % 60000) / 1000);
  const tail = `${two(seconds)}.${three(rest % 1000)}`;
  return hours > 0 ? `${hours}:${two(minutes)}:${tail}` : `${minutes}:${tail}`;
}

/** A gap to the winner in milliseconds as "+12.345", or "+1:02.345" from a minute up. */
export function formatGap(ms) {
  if (ms === null || ms === undefined || !Number.isFinite(ms)) return '';
  if (ms < 60000) return `+${Math.floor(ms / 1000)}.${three(ms % 1000)}`;
  return `+${Math.floor(ms / 60000)}:${two(Math.floor((ms % 60000) / 1000))}.${three(ms % 1000)}`;
}

/**
 * What the time column of a result row shows, as a shape the screen turns into text:
 * the winner's race time, a gap, laps down, or nothing (a retirement shows its reason instead).
 */
export function timeCell(row) {
  if (!row.classified || row.timeMs === null) return { kind: 'none' };
  if (row.lapsDown > 0) return { kind: 'lapsDown', laps: row.lapsDown };
  if (row.position === 1) return { kind: 'time', text: formatRaceTime(row.timeMs) };
  if (row.gapMs !== null) return { kind: 'gap', text: formatGap(row.gapMs) };
  return { kind: 'none' };
}
