// Tests of the in-game guide book (#268). Run: node --test "tools/docs/*.test.mjs"
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { buildGuideBook } from './build-docs.mjs';

/* A repository with a GUIDE.md of seven chapters; the second one carries what the book must treat. */
function guideRoot(second) {
  const root = mkdtempSync(join(tmpdir(), 'ppguide-'));
  mkdirSync(join(root, 'src'));
  writeFileSync(join(root, 'src', 'B.cs'), ['public static class B', '{', '    public const int Days = 14;', '}'].join('\n'));
  const chapter = n => `## ${n}. Rozdział ${n}\n\nTreść ${n}.\n\n---\n`;
  const md = ['# Jak działa gra', '', 'Wstęp.', '', '---', '', chapter(1), `## 2. Drugi\n\n${second}\n\n---\n`, chapter(3), chapter(4), chapter(5), chapter(6), chapter(7)].join('\n');
  writeFileSync(join(root, 'GUIDE.md'), md);
  return root;
}

test('the chapters come out with number and title, the introduction apart', () => {
  const book = buildGuideBook(guideRoot('Tekst.'));
  assert.equal(book.lang, 'pl');
  assert.equal(book.title, 'Jak działa gra');
  assert.equal(book.intro, '<p>Wstęp.</p>');
  assert.deepEqual(book.chapters.map(c => c.number), [1, 2, 3, 4, 5, 6, 7]);
  assert.equal(book.chapters[1].title, 'Drugi');
  assert.equal(book.chapters[1].html, '<p>Tekst.</p>', 'the rule between chapters is not part of one');
  assert.equal(new Set(book.chapters.map(c => c.id)).size, 7, 'ids are unique');
});

test('constants are filled, charts left out, links to other documents become text, classes are prefixed', () => {
  const second = [
    'Zobacz [projekt](DESIGN.md) i [początek](#start).',
    '',
    '```wybory',
    'Czas | trwa {B.Days|dni}',
    '```',
    '',
    '```wykres jakis-wykres',
    'podpis',
    '```',
  ].join('\n');
  const html = buildGuideBook(guideRoot(second)).chapters[1].html;
  assert.match(html, /<ul class="g-choices"><li><span class="g-d-title">Czas<\/span><span class="g-d-text">trwa <span class="num">14 dni<\/span><\/span><\/li><\/ul>/);
  assert.doesNotMatch(html, /svg|chart|wykres/, 'a chart is dropped, not an error (the book has none)');
  assert.doesNotMatch(html, /href="(?!#)/, 'only links inside the book remain');
  assert.match(html, /<a href="#start">początek<\/a>/);
  assert.match(html, /projekt/, 'the text of a dropped link stays');
});

test('a constant that disappeared fails the build', () => {
  assert.throws(() => buildGuideBook(guideRoot('```wybory\nCzas | {B.Gone}\n```')), /Gone/);
});

test('a GUIDE.md with almost no chapters is refused', () => {
  const root = guideRoot('x');
  writeFileSync(join(root, 'GUIDE.md'), '# Jak działa gra\n\n## 1. Jedyny\n\nTekst.\n');
  assert.throws(() => buildGuideBook(root), /fewer than five chapters/);
});

test('the real GUIDE.md becomes a book of numbered chapters with no loose ends', () => {
  const book = buildGuideBook();
  assert.ok(book.chapters.length >= 10);
  book.chapters.forEach((c, i) => assert.equal(c.number, i + 1, `chapter ${i + 1} is numbered in order`));
  const all = JSON.stringify(book);
  assert.doesNotMatch(all, /<svg|class=\\"chart\\"|<hr|@@/, 'no charts, no rules, no unresolved references');
  assert.doesNotMatch(all, /\{[A-Za-z]/, 'no unresolved placeholders');
  assert.doesNotMatch(all, /href=\\"(?!#)/, 'no links out of the book');
  assert.doesNotMatch(all, / class=\\"(fields|choices|steps|compare|ask)\\"/, 'block classes carry the prefix the app stylesheet expects');
  assert.match(all, /g-choices/);
  assert.match(all, /g-ask/);
});
