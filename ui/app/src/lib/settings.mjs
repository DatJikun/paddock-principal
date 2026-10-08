/* The player's own settings of the window, kept in the browser's storage next to the language. Nothing here reaches the
   game: they change how the page behaves (how fast Dalej runs the days, whether the menu moves). */

import { DEFAULT_SECONDS, SPEEDS } from './autoplay.mjs';

export const SETTINGS_KEY = 'pp-settings';

export const DEFAULT_SETTINGS = {
  /** Dalej keeps running days by itself until something needs the player. */
  autoAdvance: true,
  /** Seconds one day takes while it runs. */
  daySeconds: DEFAULT_SECONDS,
  /** The slow drift of the colour stripes behind the main menu. */
  menuMotion: true,
};

/** A stored value made safe: unknown fields are dropped, a seconds value off the list snaps to the nearest speed. */
export function normalizeSettings(raw) {
  const source = raw && typeof raw === 'object' ? raw : {};
  const wanted = Number(source.daySeconds);
  const seconds = Number.isFinite(wanted) ? SPEEDS.reduce((best, speed) => (Math.abs(speed - wanted) < Math.abs(best - wanted) ? speed : best), SPEEDS[0]) : DEFAULT_SETTINGS.daySeconds;
  return {
    autoAdvance: typeof source.autoAdvance === 'boolean' ? source.autoAdvance : DEFAULT_SETTINGS.autoAdvance,
    daySeconds: seconds,
    menuMotion: typeof source.menuMotion === 'boolean' ? source.menuMotion : DEFAULT_SETTINGS.menuMotion,
  };
}

export function loadSettings(storage = globalThis.localStorage) {
  try {
    const text = storage?.getItem(SETTINGS_KEY);
    return normalizeSettings(text ? JSON.parse(text) : null);
  } catch {
    return { ...DEFAULT_SETTINGS };
  }
}

export function saveSettings(settings, storage = globalThis.localStorage) {
  try {
    storage?.setItem(SETTINGS_KEY, JSON.stringify(normalizeSettings(settings)));
  } catch {
    /* a private window can refuse storage; the choice still applies this session */
  }
}
