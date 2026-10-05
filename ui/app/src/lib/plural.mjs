/** Polish cardinal forms, matching ui/prototype/js/ui.js: 1 / 2–4 / 5+, and 12–14 stay with the many form. */
export function plural(n, one, few, many) {
  const abs = Math.abs(n);
  if (!Number.isInteger(abs)) return few;
  if (abs === 1) return one;
  const lastDigit = abs % 10;
  const lastTwo = abs % 100;
  return lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14) ? few : many;
}

/** CLDR categories used by the string catalogs. Fractions are "other"; English has no few/many. */
export function pluralCategory(language, count) {
  const integer = Number.isFinite(count) && Math.floor(count) === count;
  const abs = Math.abs(count);
  if (language === 'en') return integer && abs === 1 ? 'one' : 'other';
  if (!integer) return 'other';
  if (abs === 1) return 'one';
  const lastDigit = abs % 10;
  const lastTwo = abs % 100;
  return lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14) ? 'few' : 'many';
}
