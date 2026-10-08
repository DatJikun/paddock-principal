/* Widths of the race screen's side panels: the timing tower on the left and the column with the car card, the pit wall
   and the radio on the right. The viewer drags their edges; the widths live in the browser's storage next to the
   settings and never reach the game. */

export const PANELS_KEY = 'pp-race-panels';

/** The tower: narrow enough for the map, wide enough for a full surname and the gap. */
export const TOWER = { min: 220, base: 266, max: 440 };
/** The right column: the five-step driver scale needs its minimum. */
export const SIDE = { min: 280, base: 320, max: 520 };
/** The map always keeps at least this much width between the panels, and the panels sit this far from the edges. */
export const MAP_ROOM = 360;
export const EDGES = 36;
/** One arrow key press on a panel edge. */
export const STEP = 16;

const clamp = (value, { min, max, base }) => (Number.isFinite(value) ? Math.min(max, Math.max(min, Math.round(value))) : base);

/** A stored or dragged pair made safe: each width inside its own range. */
export function normalizePanels(raw) {
  const source = raw && typeof raw === 'object' ? raw : {};
  return { tower: clamp(Number(source.tower), TOWER), side: clamp(Number(source.side), SIDE) };
}

/** The widths drawn in a window `width` px wide: when both panels and the map's room do not fit, both panels give
    way in proportion to how far they are above their minimum, never below it. */
export function fitPanels(want, width) {
  const { tower, side } = normalizePanels(want);
  const room = Math.max(0, width - MAP_ROOM - EDGES);
  const over = tower + side - room;
  if (!(over > 0)) return { tower, side };
  const slackT = tower - TOWER.min;
  const slackS = side - SIDE.min;
  const slack = slackT + slackS;
  if (slack <= over) return { tower: TOWER.min, side: SIDE.min };
  return { tower: Math.round(tower - (over * slackT) / slack), side: Math.round(side - (over * slackS) / slack) };
}

export function loadPanels(storage = globalThis.localStorage) {
  try {
    const text = storage?.getItem(PANELS_KEY);
    return normalizePanels(text ? JSON.parse(text) : null);
  } catch {
    return normalizePanels(null);
  }
}

export function savePanels(panels, storage = globalThis.localStorage) {
  try {
    storage?.setItem(PANELS_KEY, JSON.stringify(normalizePanels(panels)));
  } catch {
    /* a private window can refuse storage; the widths still apply this race */
  }
}
