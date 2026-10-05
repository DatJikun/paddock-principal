// Tests of the docs generator. Run: node --test "tools/docs/*.test.mjs"
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { renderMarkdown, resolveRefs, headingId } from './markdown.mjs';
import { CodeValues, smooth, linear } from './code-values.mjs';
import { buildSite, unitFor } from './build-docs.mjs';
import { makeBlocks } from './blocks.mjs';
import { fmt } from './charts.mjs';

const ctx = (page = 'design.html', extra = {}) => ({
  page,
  dir: '',
  pageOf: name => ({ VISION: 'vision.html', DESIGN: 'design.html', TECH: 'tech.html' })[name],
  pageOfFile: file => ({ 'VISION.md': 'vision.html', 'TECH.md': 'tech.html' })[file],
  ...extra,
});

test('numbered headings get stable section ids', () => {
  assert.equal(headingId('5.3. Rozwój: potencjał koncepcji'), 's-5-3');
  assert.equal(headingId('6.1a. Inicjalizator świata (T20)'), 's-6-1a');
  assert.equal(headingId('Faza 4: Pętla kariery'), 'faza-4-petla-kariery');
});

test('nested lists, tables and quotes render', () => {
  const html = renderMarkdown([
    '- jeden',
    '  - zagnieżdżony',
    '- dwa',
    '',
    '| A | B |',
    '|---|--:|',
    '| x | 1 |',
    '',
    '> **Stan:** działa',
  ].join('\n'), ctx());
  assert.match(html, /<ul>\n<li>jeden\n<ul>\n<li>zagnieżdżony<\/li>\n<\/ul><\/li>\n<li>dwa<\/li>\n<\/ul>/);
  assert.match(html, /<td class="al-right">1<\/td>/);
  assert.match(html, /<blockquote class="status">/);
});

test('decisions, sections and issues become links', () => {
  const c = ctx();
  const html = renderMarkdown('Zob. PP-047, TECH §6.5, §5.3 i #122.', c);
  assert.match(html, /href="@@vision.html#pp-047"/);
  assert.match(html, /href="@@tech.html#s-6-5"/);
  assert.match(html, /href="@@design.html#s-5-3"/);
  assert.match(html, /issues\/122/);
  const pages = new Map([['tech.html', new Set(['s-6'])], ['design.html', new Set(['s-5-3'])], ['vision.html', new Set(['pp-047'])]]);
  const resolved = resolveRefs(html, pages);
  assert.match(resolved, /href="tech.html#s-6"/, 'a missing subsection falls back to its parent');
  assert.match(resolved, /href="design.html#s-5-3"/);
});

test('inline code is never linkified', () => {
  const html = renderMarkdown('Kod `PP-047 #12` zostaje.', ctx());
  assert.match(html, /<code>PP-047 #12<\/code>/);
});

test('a "**Label:**" line keeps its own line', () => {
  const html = renderMarkdown('**Status:** gotowe\n**Rola:** opis', ctx());
  assert.match(html, /gotowe<br><strong>Rola:<\/strong>/);
});

test('custom fences reach the caller', () => {
  const html = renderMarkdown('```wykres x\npodpis\n```', ctx('a.html', { fence: (info, body) => `[${info}|${body.join()}]` }));
  assert.equal(html, '[wykres x|podpis]');
});

test('C# constants, anchors and references are read', () => {
  const root = mkdtempSync(join(tmpdir(), 'ppdocs-'));
  mkdirSync(join(root, 'src'));
  writeFileSync(join(root, 'src', 'A.cs'), [
    'public static class A',
    '{',
    '    /// <summary>ESTIMATE</summary>',
    '    public const double Rate = 0.25d;',
    '    public const int Big = 95_000;',
    '    public const decimal Lead = 2.5m;',
    '    public const int Copy = Other.Base;',
    '    public const int Base = 7;',
    '    public const double MeanA = 11, AmpA = 8;',
    '    public static readonly (int Season, double Value)[] Curve =',
    '    [',
    '        (1950, 150d),',
    '        (2000, 200d),',
    '    ];',
    '}',
  ].join('\n'));
  const cv = new CodeValues(root);
  assert.deepEqual(cv.scalar('src/A.cs', 'Rate'), { value: 0.25, line: 4, raw: '0.25d' });
  assert.equal(cv.value('src/A.cs', 'Big'), 95000);
  assert.equal(cv.value('src/A.cs', 'Lead'), 2.5);
  assert.equal(cv.value('src/A.cs', 'Copy'), 7);
  assert.equal(cv.value('src/A.cs', 'AmpA'), 8);
  assert.deepEqual(cv.anchors('src/A.cs', 'Curve'), [[1950, 150], [2000, 200]]);
  assert.throws(() => cv.value('src/A.cs', 'Missing'), /Missing not found/);
});

test('era curves match the engine: smoothstep and linear', () => {
  const a = [[1950, 100], [2000, 200]];
  assert.equal(smooth(a, 1975), 150);
  assert.ok(Math.abs(smooth(a, 1960) - 110.4) < 1e-9);
  assert.equal(linear(a, 1960), 120);
  assert.equal(smooth(a, 1900), 100);
});

test('Polish numbers and units', () => {
  assert.equal(fmt(0.25, 2), '0,25');
  assert.equal(fmt(95000), '95 000');
  assert.equal(fmt(1960), '1960');
  assert.equal(unitFor(1, 'lat'), 'rok');
  assert.equal(unitFor(3, 'lat'), 'lata');
  assert.equal(unitFor(12, 'lat'), 'lat');
  assert.equal(unitFor(22, 'dni'), 'dni');
  assert.equal(unitFor(2.5, 'lat'), 'roku');
  assert.equal(unitFor(4, 'sezonów'), 'sezony');
});

test('pytania renders a list of questions and fills constants', () => {
  const root = mkdtempSync(join(tmpdir(), 'ppdocs-'));
  mkdirSync(join(root, 'src'));
  writeFileSync(join(root, 'src', 'B.cs'), ['public static class B', '{', '    public const int Days = 14;', '}'].join('\n'));
  const cv = new CodeValues(root);
  const blocks = makeBlocks({ cv, ctx: ctx(), formatValue: (v, spec) => `${v} ${spec}`.trim() });
  const html = blocks.pytania(null, ['Czy {B.Days|dni} to dużo?', '', 'Drugie pytanie']);
  assert.match(html, /^<div class="ask"><span class="ask-head">Twoja opinia<\/span><ul>/);
  assert.equal((html.match(/<li>/g) ?? []).length, 2, 'blank lines are skipped');
  assert.match(html, /<span class="num">14 dni<\/span>/);
});

test('the real docs build: every constant and chart in GUIDE.md resolves', () => {
  const files = buildSite();
  for (const page of ['index.html', 'przewodnik.html', 'vision.html', 'design.html', 'tech.html']) {
    assert.ok(files.get(page)?.includes('</html>'), page);
  }
  const guide = files.get('przewodnik.html');
  assert.ok((guide.match(/class="chart"/g) ?? []).length >= 10, 'charts rendered');
  assert.ok((guide.match(/class="choices"/g) ?? []).length >= 8, 'player choices rendered');
  assert.ok((guide.match(/class="ask"/g) ?? []).length >= 8, 'questions for testers rendered');
  assert.doesNotMatch(guide, /@@/, 'every cross reference resolved');
  assert.doesNotMatch(guide, /NaN|undefined/);
});

test('the guide is for outsiders: no code names, issue numbers or decision ids', () => {
  const guide = buildSite().get('przewodnik.html');
  const body = guide.slice(guide.indexOf('<main'), guide.indexOf('</main>'));
  assert.doesNotMatch(body, /t-name|class="tune"|class="badge/, 'no tuning tables or status badges');
  assert.doesNotMatch(body, /\{[A-Za-z]/, 'no unresolved placeholders');
  assert.doesNotMatch(body, /PP-\d{3}/, 'no decision ids');
  assert.doesNotMatch(body, /#\d{2,}/, 'no issue numbers');
  assert.doesNotMatch(body, /[A-Z][A-Za-z]+(Estimates|Constants)|\.cs/, 'no class or file names');
});
