// The in-game guide ("Poradnik"): a book made from GUIDE.md by `node tools/docs/build-docs.mjs --guide-book` (#268).
// This file only decides which page is shown and what the neighbours are; the text is the generator's.

/** Id of the opening page (the text before the first chapter). */
export const INTRO_ID = 'wstep';

/**
 * The pages of the book in reading order: the introduction first, then every chapter.
 * `introTitle` comes from the translation files, so the page has a name in both languages.
 */
export function pagesOf(book, introTitle) {
  const pages = [];
  if (book?.intro) pages.push({ id: INTRO_ID, number: null, title: introTitle, html: book.intro });
  for (const chapter of book?.chapters ?? []) pages.push(chapter);
  return pages;
}

/** The page with this id, or the first one when the id is unknown (a stale link never opens an empty book). */
export function pageById(pages, id) {
  return pages.find(page => page.id === id) ?? pages[0] ?? null;
}

/** The page `delta` places after (or before, negative) this one; null at the ends of the book. */
export function pageAround(pages, id, delta) {
  const at = pages.findIndex(page => page.id === id);
  if (at < 0) return null;
  return pages[at + delta] ?? null;
}

/** "01", "02" ... for the numbered chapters; empty for the introduction. */
export function pageNumber(page) {
  return page && Number.isInteger(page.number) ? String(page.number).padStart(2, '0') : '';
}
