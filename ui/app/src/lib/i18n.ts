import { pluralCategory } from './plural.mjs';
import enCatalog from '../../../../strings/en.json';
import plCatalog from '../../../../strings/pl.json';

export type Language = 'pl' | 'en';

type Entry = string | Record<string, string>;

const catalogs: Record<Language, Record<string, Entry>> = {
  pl: plCatalog as Record<string, Entry>,
  en: enCatalog as Record<string, Entry>,
};

const storageKey = 'pp-lang';

let language: Language = 'pl';

const listeners = new Set<() => void>();

export function getLanguage(): Language {
  return language;
}

export function setLanguage(next: Language): void {
  language = next;
  try {
    localStorage.setItem(storageKey, next);
  } catch {
    /* a private window can refuse storage; the choice still applies this session */
  }
  for (const listener of listeners) listener();
}

export function subscribeLanguage(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function loadLanguage(): void {
  try {
    const stored = localStorage.getItem(storageKey);
    if (stored === 'pl' || stored === 'en') language = stored;
  } catch {
    language = 'pl';
  }
}

export function translate(
  lang: Language,
  key: string,
  parameters: Record<string, string> = {},
  count?: number,
): string {
  const entry = catalogs[lang][key];
  if (entry === undefined) return key;
  const text = typeof entry === 'string' ? entry : pick(lang, entry, count ?? Number(parameters.count ?? 0));
  return text.replace(/\{(\w+)\}/g, (_, name: string) => parameters[name] ?? `{${name}}`);
}

function pick(lang: Language, entry: Record<string, string>, count: number): string {
  const category = pluralCategory(lang, count);
  return entry[category] ?? entry.other ?? entry.many ?? entry.one ?? '';
}
