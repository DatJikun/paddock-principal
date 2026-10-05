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
