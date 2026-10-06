/* The new-career form and the save list, as plain data. Nothing here decides a game outcome: it names the options the
   bridge accepts (the same words as NewCareerCall and CareerConfig), shapes the form into bridge arguments and orders saves. */

export const TILTS = ['none', 'negotiation', 'people_management', 'politics', 'business'];

export const TILT_KEYS = {
  none: 'shell.tilt.none',
  negotiation: 'shell.tilt.negotiation',
  people_management: 'shell.tilt.peopleManagement',
  politics: 'shell.tilt.politics',
  business: 'shell.tilt.business',
};

export const PRESETS = ['MostHistorical', 'Balanced', 'Chaos'];

export const PRESET_KEYS = {
  MostHistorical: 'shell.preset.mostHistorical',
  Balanced: 'shell.preset.balanced',
  Chaos: 'shell.preset.chaos',
};

/** The words each axis takes, in the order the form lists them. */
export const AXES = {
  people: ['RealTrajectory', 'RealPotential', 'RealNamesRandomSkills', 'FullyGenerated'],
  rules: ['Historical', 'VotedEachSeason'],
  ai: ['ReplayHistory', 'ReactToSituation', 'PureRandom'],
  fatality: ['Off', 'On'],
};

export const MIN_YEAR = 1950;
export const MAX_YEAR = 2026;

/** Countries the principal can come from: the ones the catalog names. */
export const COUNTRIES = [
  'GBR', 'FRA', 'ITA', 'IRL', 'BEL', 'GER', 'AUT', 'NED', 'ARG', 'MON', 'POL', 'ESP',
  'POR', 'JPN', 'SUI', 'SWE', 'BRA', 'USA', 'CAN', 'MEX', 'MAR', 'AUS', 'NZL', 'RSA',
];

/** The form's starting point. Axes stay empty until a preset fills them. */
export function emptySetup(year = 1955, seed = '') {
  return {
    year,
    preset: 'Balanced',
    people: '',
    rules: '',
    ai: '',
    history: 0,
    randomness: 0,
    fatality: '',
    noNumbers: 'Off',
    seed,
  };
}

/** The axes of a preset as the bridge reported them. */
export function withPreset(setup, preset) {
  return {
    ...setup,
    preset: preset.name,
    people: preset.people,
    rules: preset.rules,
    ai: preset.ai,
    history: preset.history,
    randomness: preset.randomness,
    fatality: preset.fatality,
    noNumbers: preset.noNumbers ? 'On' : 'Off',
  };
}

/** True when any axis differs from the preset the form started from. */
export function isCustom(setup, preset) {
  if (!preset) return false;
  return (
    setup.people !== preset.people ||
    setup.rules !== preset.rules ||
    setup.ai !== preset.ai ||
    Number(setup.history) !== preset.history ||
    Number(setup.randomness) !== preset.randomness ||
    setup.fatality !== preset.fatality ||
    (setup.noNumbers === 'On') !== preset.noNumbers
  );
}

function seedOf(setup) {
  const text = String(setup.seed ?? '').trim();
  if (!/^\d+$/.test(text)) return null;
  const value = Number(text);
  return Number.isSafeInteger(value) ? value : null;
}

/** The axes shared by the `teams` query and the `newCareer` command, so the cards show the world the career starts in. */
export function axisArgs(setup) {
  return {
    preset: setup.preset,
    people: setup.people || null,
    rules: setup.rules || null,
    ai: setup.ai || null,
    history: setup.history === '' || setup.history === null ? null : Number(setup.history),
    randomness: setup.randomness === '' || setup.randomness === null ? null : Number(setup.randomness),
    fatality: setup.fatality || null,
    noNumbers: setup.noNumbers === 'On',
    seed: seedOf(setup),
  };
}

export function teamsArgs(managerId, setup) {
  return { managerId, year: Number(setup.year), ...axisArgs(setup) };
}

export function newCareerArgs(managerId, setup, you, teamId) {
  return {
    managerId,
    teamId,
    givenName: you.given.trim(),
    familyName: you.family.trim(),
    nationality: you.nationality,
    tilt: you.tilt,
    year: Number(setup.year),
    name: null,
    ...axisArgs(setup),
  };
}

/** The year the form may send; anything else keeps the last good one. */
export function clampYear(value, fallback = 1955) {
  const year = Math.trunc(Number(value));
  if (!Number.isFinite(year)) return fallback;
  return Math.min(MAX_YEAR, Math.max(MIN_YEAR, year));
}

/** Constructor ids that are initialisms, as the world names them (WorldInitializer): "hwm" reads HWM, not Hwm. */
const INITIALISMS = new Set(['afm', 'ags', 'ats', 'bar', 'bmw', 'brm', 'brp', 'emw', 'enb', 'era', 'hrt', 'hwm', 'jbw', 'lds', 'lec', 'mbm', 'osca']);

/** An authored id the way the world names it when no better name is known ("red_bull" -> "Red Bull", "hwm" -> "HWM"). */
export function teamLabel(id) {
  return String(id ?? '')
    .split(/[_-]/)
    .filter(Boolean)
    .map((word) => (INITIALISMS.has(word) ? word.toUpperCase() : word[0].toUpperCase() + word.slice(1)))
    .join(' ');
}

/** The file name a player sees: the folder's extension stays out of sight. */
export function saveLabel(file) {
  return String(file ?? '').replace(/\.paddock$/i, '');
}

export function sameSave(a, b) {
  return saveLabel(a).toLowerCase() === saveLabel(b).toLowerCase();
}

/** The most recently written save, or null when there are none. */
export function latestSave(saves) {
  let best = null;
  for (const save of saves ?? []) {
    if (best === null || save.savedAt > best.savedAt || (save.savedAt === best.savedAt && save.name > best.name)) best = save;
  }
  return best;
}

/** Saves newest first. */
export function newestFirst(saves) {
  return [...(saves ?? [])].sort((a, b) => (a.savedAt === b.savedAt ? a.name.localeCompare(b.name) : a.savedAt < b.savedAt ? 1 : -1));
}
