/* Automatic play: when the clock may keep running by itself. The loop only calls the same advanceDay command the Dalej button
   does; this file decides when to stop, from what the bridge reports (a held clock, the next race date, the newest important
   inbox item). Nothing about the game is decided here. */

export const SPEEDS = [0.25, 0.5, 1, 2];
export const DEFAULT_SECONDS = 0.5;

/** The number inside an inbox id ("inb:12" is 12), or null for an id of another shape. */
export function inboxNumber(id) {
  const match = /(\d+)$/.exec(String(id ?? ''));
  return match ? Number(match[1]) : null;
}

/**
 * True when the item is newer than the one the run started with (or the run started with none).
 * @param {string | null | undefined} id
 * @param {string | null | undefined} seenId
 */
export function isNewerItem(id, seenId) {
  if (!id) return false;
  const now = inboxNumber(id);
  const before = inboxNumber(seenId);
  if (now === null) return id !== seenId;
  return before === null || now > before;
}

/**
 * Why automatic play should stop after a day, or null to go on. `shell` and `nextRace` are the bridge's reads after that
 * day; `seenImportantId` is the newest important item when the run started (or the last one it already stopped on).
 *  - 'held'      the clock is held (a decision is open or something else blocks it);
 *  - 'race'      it is a race day: the race is run by a deliberate click;
 *  - 'inbox'     a new important item arrived;
 *  - 'raced'     a race has just been run;
 *  - 'season'    a new season began.
 * @param {{ shell: any, nextRace?: any, seenImportantId?: string | null, raced?: boolean, seasonChanged?: boolean }} state
 * @returns {'held' | 'race' | 'inbox' | 'raced' | 'season' | null}
 */
export function stopReason({ shell, nextRace, seenImportantId = null, raced = false, seasonChanged = false }) {
  if (!shell) return 'held';
  if (shell.blockingKind || shell.decisionItemId) return 'held';
  if (isNewerItem(shell.importantItemId, seenImportantId)) return 'inbox';
  if (raced) return 'raced';
  if (seasonChanged) return 'season';
  if (nextRace?.date && nextRace.date === shell.date) return 'race';
  return null;
}

/** Whether a run may start now: nothing holds the clock and it is not a race day (that day is a deliberate click). */
export function mayStart({ shell, nextRace }) {
  if (!shell || shell.blockingKind || shell.decisionItemId) return false;
  return !(nextRace?.date && nextRace.date === shell.date);
}
