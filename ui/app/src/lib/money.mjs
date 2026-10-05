/** Nominal USD. The ledger stores integer cents; the page shows whole dollars and leftover cents. */
export function formatMoney(cents, language) {
  if (cents === null || cents === undefined || !Number.isFinite(cents)) return '—';
  const negative = cents < 0;
  const abs = Math.abs(Math.trunc(cents));
  const whole = Math.trunc(abs / 100);
  const frac = abs % 100;
  const locale = language === 'en' ? 'en-GB' : 'pl-PL';
  const grouped = new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(whole);
  const body = frac === 0 ? grouped : `${grouped}${language === 'en' ? '.' : ','}${String(frac).padStart(2, '0')}`;
  return `${negative ? '−' : ''}$${body}`;
}
