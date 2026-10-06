import { formatDate } from './date.mjs';
import { formatMoney } from './money.mjs';

/** Parameters the host sends as whole nominal dollars in invariant text. */
const MONEY = new Set(['salary', 'partial', 'amount', 'bonus', 'severance', 'minimum']);

/**
 * One message parameter as the player reads it. The host sends invariant text (ISO dates, whole dollars) because it does not
 * know the language; the page shows dates and money the way the active language writes them. Anything else stays as sent.
 */
export function formatParameter(language, name, value) {
  const text = String(value);
  if (/^\d{4}-\d{2}-\d{2}$/.test(text)) return formatDate(text, language);
  if (MONEY.has(name) && /^\d+$/.test(text)) return formatMoney(Number(text) * 100, language);
  return text;
}

export function formatParameters(language, parameters) {
  const out = {};
  for (const [name, value] of Object.entries(parameters ?? {})) out[name] = formatParameter(language, name, value);
  return out;
}
