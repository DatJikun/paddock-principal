import type { TranslationMessage } from './api/types.generated';
import { flagCode } from './flags.mjs';
import { translate, type Language } from './i18n';
import { formatParameters } from './params.mjs';

/** Translation helpers a screen receives from the shell, bound to the active language. */
export type Tr = {
  lang: Language;
  t: (key: string, parameters?: Record<string, string>) => string;
  tCount: (key: string, count: number, parameters?: Record<string, string>) => string;
  tMsg: (message: TranslationMessage | null | undefined) => string;
};

export function translator(lang: Language): Tr {
  return {
    lang,
    t: (key, parameters = {}) => translate(lang, key, parameters),
    tCount: (key, count, parameters = {}) => translate(lang, key, { ...parameters, count: String(count) }, count),
    tMsg: (message) => (message ? translate(lang, message.key, (formatParameters(lang, message.parameters ?? {}) as Record<string, string>)) : ''),
  };
}

export const ICON = {
  arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  back: '<path d="M19 12H5M11 6l-6 6 6 6"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7"/>',
  board: '<path d="M4 20h16M6 20V10M10 20V10M14 20V10M18 20V10M3 10l9-6 9 6z"/>',
  person: '<circle cx="12" cy="8" r="4"/><path d="M4 20c1-4 4-6 8-6s7 2 8 6"/>',
  car: '<path d="M8 3.5h8M12 3.5v4M10.5 7.5h3l1 5v4.5l-1.5 3h-3L8.5 17v-4.5z"/><path d="M7.5 21h9"/>',
  sponsor: '<path d="M12 3l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.4 6.8 19.1l1-5.8L3.5 9.2l5.9-.9z"/>',
  supply: '<path d="M3 7h11v9H3zM14 10h4l3 3v3h-7"/><circle cx="7" cy="18" r="1.6"/><circle cx="17" cy="18" r="1.6"/>',
  scout: '<circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/>',
  mail: '<path d="M4 6h16v12H4z"/><path d="M4 7l8 6 8-6"/>',
  menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
  play: '<path d="M7 5l12 7-12 7z"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  folder: '<path d="M3 7h6l2 2h10v10H3z"/>',
  exit: '<path d="M10 4H5v16h5M15 8l4 4-4 4M19 12H9"/>',
  flag: '<path d="M5 21V4M5 4h13l-2.5 4.5L18 13H5"/>',
  gear: '<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M2 12h3M19 12h3M4.9 19.1L7 17M17 7l2.1-2.1"/>',
};

export function icon(path: string, size?: number) {
  const style = size ? ` style="width:${size}px;height:${size}px"` : '';
  return `<svg class="i" viewBox="0 0 24 24" aria-hidden="true"${style}>${path}</svg>`;
}

/** The icon of an inbox area ("inbox.area.board" -> the board's pillars). */
export function areaIcon(area: string) {
  const name = area.replace('inbox.area.', '');
  if (name === 'board') return ICON.board;
  if (name === 'contract' || name === 'negotiation' || name === 'market') return ICON.person;
  if (name === 'development') return ICON.car;
  if (name === 'sponsor') return ICON.sponsor;
  if (name === 'supply') return ICON.supply;
  if (name === 'scouting') return ICON.scout;
  return ICON.mail;
}

export function initials(name: string) {
  const words = name.split(/\s+/).filter(Boolean);
  if (words.length >= 2) return `${words[0]?.[0] ?? ''}${words[words.length - 1]?.[0] ?? ''}`.toUpperCase();
  return name.slice(0, 2).toUpperCase();
}

/** Country name through the catalog; an unknown code stays a code. */
export function countryName(tr: Tr, code: string | null | undefined) {
  if (!code) return '';
  const key = `country.${flagCode(code)}`;
  const text = tr.t(key);
  return text === key ? code : text;
}

/** A layout length in the active language, two decimals ("3,91"). */
export function km(tr: Tr, value: number | null | undefined) {
  if (value === null || value === undefined) return '—';
  const text = new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 3,
  }).format(value);
  return tr.t('race.km', { km: text });
}

/** Facility quality in the active language: stored in milli-units, shown with one decimal ("107,5"). */
export function quality(tr: Tr, milli: number) {
  return new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(milli / 1000);
}

/** A share as a whole percent ("79%"). */
export function percent(tr: Tr, ratio: number) {
  return `${new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', { maximumFractionDigits: 0 }).format(ratio * 100)}%`;
}

/** Points as stored (invariant text, maybe "3.5"), shown with the language's decimal mark. */
export function points(tr: Tr, value: string) {
  const number = Number(value);
  if (!Number.isFinite(number)) return value;
  return new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', { maximumFractionDigits: 2 }).format(number);
}
