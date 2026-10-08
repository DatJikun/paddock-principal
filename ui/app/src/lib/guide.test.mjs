import test from 'node:test';
import assert from 'node:assert/strict';
import { INTRO_ID, pagesOf, pageById, pageAround, pageNumber } from './guide.mjs';

const book = {
  intro: '<p>Wstęp</p>',
  chapters: [
    { id: 's-1', number: 1, title: 'Start kariery', html: '<p>a</p>' },
    { id: 's-2', number: 2, title: 'Kierowcy i wiek', html: '<p>b</p>' },
    { id: 's-10', number: 10, title: 'Sponsorzy', html: '<p>c</p>' },
  ],
};

test('the introduction comes first, then the chapters in order', () => {
  const pages = pagesOf(book, 'Wstęp');
  assert.deepEqual(pages.map(p => p.id), [INTRO_ID, 's-1', 's-2', 's-10']);
  assert.equal(pages[0].title, 'Wstęp');
});

test('a book without an introduction starts at the first chapter', () => {
  const pages = pagesOf({ intro: '', chapters: book.chapters }, 'Wstęp');
  assert.equal(pages[0].id, 's-1');
});

test('an empty or missing book has no pages and no current page', () => {
  assert.deepEqual(pagesOf(null, 'x'), []);
  assert.equal(pageById([], 's-1'), null);
});

test('an unknown id falls back to the first page', () => {
  const pages = pagesOf(book, 'Wstęp');
  assert.equal(pageById(pages, 's-2').title, 'Kierowcy i wiek');
  assert.equal(pageById(pages, 'gone').id, INTRO_ID);
});

test('neighbours stop at the ends of the book', () => {
  const pages = pagesOf(book, 'Wstęp');
  assert.equal(pageAround(pages, INTRO_ID, -1), null);
  assert.equal(pageAround(pages, INTRO_ID, 1).id, 's-1');
  assert.equal(pageAround(pages, 's-2', 1).id, 's-10');
  assert.equal(pageAround(pages, 's-10', 1), null);
  assert.equal(pageAround(pages, 'gone', 1), null);
});

test('chapter numbers have two digits, the introduction has none', () => {
  const pages = pagesOf(book, 'Wstęp');
  assert.equal(pageNumber(pages[0]), '');
  assert.equal(pageNumber(pages[1]), '01');
  assert.equal(pageNumber(pages[3]), '10');
});
