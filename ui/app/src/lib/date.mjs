/** An ISO game date, shown in the active language. The calendar day is the date itself, not the local zone. */
export function formatDate(iso, language) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso ?? '');
  if (!match) return iso ?? '';
  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
  return new Intl.DateTimeFormat(language === 'en' ? 'en-GB' : 'pl-PL', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(date);
}

function utc(iso) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso ?? '');
  return match ? Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
}

/** Day and month only ("16 stycznia"), for tiles where the season is already on screen. */
export function formatDay(iso, language) {
  const at = utc(iso);
  if (at === null) return iso ?? '';
  return new Intl.DateTimeFormat(language === 'en' ? 'en-GB' : 'pl-PL', {
    day: 'numeric',
    month: 'long',
    timeZone: 'UTC',
  }).format(new Date(at));
}

/** Weekday, day and month ("Niedziela, 16 stycznia"), capitalised for the start of a line. */
export function formatWeekday(iso, language) {
  const at = utc(iso);
  if (at === null) return iso ?? '';
  const text = new Intl.DateTimeFormat(language === 'en' ? 'en-GB' : 'pl-PL', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: 'UTC',
  }).format(new Date(at));
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** Whole calendar days from one ISO date to another; null when either is missing. */
export function daysBetween(fromIso, toIso) {
  const from = utc(fromIso);
  const to = utc(toIso);
  if (from === null || to === null) return null;
  return Math.round((to - from) / 86400000);
}

/** Monday = 0 … Sunday = 6. */
export function weekdayIndex(iso) {
  const at = utc(iso);
  if (at === null) return null;
  return (new Date(at).getUTCDay() + 6) % 7;
}

/** The ISO date a number of days after another. */
export function addDays(iso, days) {
  const at = utc(iso);
  if (at === null) return null;
  return new Date(at + days * 86400000).toISOString().slice(0, 10);
}
