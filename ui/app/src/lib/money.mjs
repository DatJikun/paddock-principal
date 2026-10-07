/** Nominal USD. The ledger stores integer cents; the page shows whole dollars only (#264), never cents. */
export function formatMoney(cents, language) {
  if (cents === null || cents === undefined || !Number.isFinite(cents)) return '—';
  const dollars = Math.round(Math.abs(cents) / 100);
  const locale = language === 'en' ? 'en-GB' : 'pl-PL';
  const grouped = new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(dollars);
  return `${cents < 0 && dollars > 0 ? '−' : ''}$${grouped}`;
}
