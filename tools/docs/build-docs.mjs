#!/usr/bin/env node
// Builds the HTML docs from the Markdown files. Plain node, no dependencies.
//
//   node tools/docs/build-docs.mjs            write build/docs/ (open build/docs/index.html)
//   node tools/docs/build-docs.mjs --out DIR  write somewhere else
//
// The .md files stay the source (PP-056). The output is generated and not committed.
// GUIDE.md may hold custom fences that read the game's code, so the numbers never drift
// (the segmented blocks pola, kroki, wybory, pytania and porownanie are described in blocks.mjs;
// GUIDE.md no longer uses strojenie, stan or wgrze, they stay for old files):
//   ```strojenie <path to a .cs file>      a table of tunable constants: "Name | opis | format"
//   ```wykres <name>                       a chart from charts.mjs (the body is its caption)
//   ```wykres słupki                       a bar chart whose data is in the fence
// A constant or chart that cannot be read fails the build.

import { mkdirSync, readFileSync, writeFileSync, copyFileSync } from 'node:fs';
import { dirname, join, basename } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { renderMarkdown, renderInline, resolveRefs, escapeHtml, GITHUB_BLOB } from './markdown.mjs';
import { CodeValues } from './code-values.mjs';
import { namedChart, dataChart, fmt } from './charts.mjs';
import { makeBlocks, chapterize } from './blocks.mjs';

const here = dirname(fileURLToPath(import.meta.url));
export const REPO_ROOT = join(here, '..', '..');

/* Pages in navigation order. `doc` is the name other docs use in references ("DESIGN §5"). */
export const PAGES = [
  { file: 'README.md', page: 'index.html', doc: 'README', nav: 'Start', title: 'Paddock Principal' },
  { file: 'GUIDE.md', page: 'przewodnik.html', doc: 'GUIDE', nav: 'Jak działa gra', title: 'Jak działa gra' },
  { file: 'VISION.md', page: 'vision.html', doc: 'VISION', nav: 'Wizja i decyzje', title: 'Wizja i decyzje' },
  { file: 'ROADMAP.md', page: 'roadmap.html', doc: 'ROADMAP', nav: 'Plan', title: 'Plan prac' },
  { file: 'DESIGN.md', page: 'design.html', doc: 'DESIGN', nav: 'Projekt gry', title: 'Projekt gry' },
  { file: 'TECH.md', page: 'tech.html', doc: 'TECH', nav: 'Technika', title: 'Technika' },
  { file: 'ui/HANDOFF_UI.md', page: 'ui.html', doc: 'HANDOFF_UI', nav: 'UI', title: 'Prototyp UI i uwagi' },
  { file: 'AGENTS.md', page: 'agents.html', doc: 'AGENTS', nav: 'Dla agentów', title: 'Zasady dla agentów AI' },
];

const DOC_ALIASES = { HANDOFF: 'HANDOFF_UI' };

function formatValue(value, spec) {
  const auto = n => (Number.isInteger(n) ? fmt(n) : fmt(n, Math.min(6, (String(n).split('.')[1] ?? '').length)));
  if (typeof value !== 'number') return escapeHtml(String(value));
  switch ((spec ?? '').trim()) {
    case '%': return auto(Number((value * 100).toFixed(4))) + '%';
    case '%%': return auto(value) + '%';
    case 'm%': return auto(value / 10) + '%';
    case 't': return auto(value / 10) + ' pkt';
    case 'c$': return '$' + fmt(value / 100);
    case '': return auto(value);
    default: return `${auto(value)} ${escapeHtml(unitFor(value, spec.trim()))}`;
  }
}

/* Polish plural of a unit written in its "many" form in GUIDE.md: 1 rok, 2 lata, 5 lat, 2,5 roku. */
const UNIT_FORMS = {
  lat: ['rok', 'lata', 'lat', 'roku'],
  dni: ['dzień', 'dni', 'dni', 'dnia'],
  'sezonów': ['sezon', 'sezony', 'sezonów', 'sezonu'],
  'osób': ['osoba', 'osoby', 'osób', 'osoby'],
  rund: ['runda', 'rundy', 'rund', 'rundy'],
  liczb: ['liczba', 'liczby', 'liczb', 'liczby'],
};

export function unitFor(value, unit) {
  const forms = UNIT_FORMS[unit];
  if (!forms || typeof value !== 'number') return unit;
  if (!Number.isInteger(value)) return forms[3];
  const n = Math.abs(value);
  if (n === 1) return forms[0];
  const last = n % 10;
  const lastTwo = n % 100;
  return last >= 2 && last <= 4 && !(lastTwo >= 12 && lastTwo <= 14) ? forms[1] : forms[2];
}

function tunableTable(path, body, cv, ctx) {
  const rows = body.filter(l => l.trim()).map(line => {
    const [name, desc = '', spec = ''] = line.split('|').map(s => s.trim());
    const { value, line: at } = cv.scalar(path, name);
    const link = `${GITHUB_BLOB}${path}#L${at}`;
    return `<tr><td><span class="t-desc">${renderInline(desc, ctx)}</span><code class="t-name">${escapeHtml(name)}</code></td>`
      + `<td class="t-val">${formatValue(value, spec)}</td>`
      + `<td class="t-src"><a href="${link}">${escapeHtml(basename(path))}:${at}</a></td></tr>`;
  });
  const n = rows.length;
  return `<details class="tune"><summary><span>Liczby do strojenia</span><span class="tune-count">${n} ${unitFor(n, 'liczb')}</span></summary>`
    + `<div class="table tunables"><table><thead><tr><th>Co to jest</th><th class="al-right">Teraz</th><th>W kodzie</th></tr></thead><tbody>\n${rows.join('\n')}\n</tbody></table></div></details>`;
}

export function buildSite(root = REPO_ROOT) {
  const cv = new CodeValues(root);
  const pageOf = name => PAGES.find(p => p.doc === (DOC_ALIASES[name] ?? name))?.page;
  const pageOfFile = file => PAGES.find(p => p.file === file || basename(p.file) === file)?.page;
  const rendered = [];
  const ids = new Map();

  for (const p of PAGES) {
    const source = readFileSync(join(root, p.file), 'utf8');
    const ctx = {
      page: p.page,
      dir: dirname(p.file) === '.' ? '' : dirname(p.file),
      pageOf,
      pageOfFile,
      fence(info, body) {
        const [kind, ...rest] = info.split(/\s+/);
        const arg = rest.join(' ');
        if (kind === 'strojenie') return tunableTable(arg, body, cv, ctx);
        const blocks = makeBlocks({ cv, ctx, formatValue });
        if (kind === 'stan') return blocks.stan(arg, body);
        if (kind === 'porownanie') return blocks.porownanie(arg, body);
        if (['pola', 'kroki', 'wybory', 'wgrze', 'pytania'].includes(kind)) return blocks[kind](arg || null, body);
        if (kind === 'wykres') {
          if (arg === 'słupki') {
            const caption = body.filter(l => l.startsWith('opis:')).map(l => renderInline(l.slice(5).trim(), ctx)).join(' ');
            return dataChart('bar', body.filter(l => !l.startsWith('opis:')), caption);
          }
          const caption = body.filter(l => l.trim()).map(l => l.trim()).join(' ');
          return namedChart(arg, cv, caption ? renderInline(caption, ctx) : '');
        }
        return null;
      },
    };
    let html = renderMarkdown(source, ctx);
    if (p.doc === 'GUIDE') html = chapterize(html);
    ids.set(p.page, ctx.ids);
    rendered.push({ ...p, html, headings: ctx.headings });
  }

  const files = new Map();
  for (const r of rendered) {
    const body = r.page === 'index.html' ? indexBody(r) : r.html;
    files.set(r.page, resolveRefs(layout(r, body), ids));
  }
  return files;
}

function toc(headings, page) {
  // Subheadings that repeat in every chapter ("Jak to działa") add noise to the contents, so they are left out.
  const count = new Map();
  for (const h of headings) count.set(h.text, (count.get(h.text) ?? 0) + 1);
  const items = headings.filter(h => h.level === 2 || h.pp || (h.level === 3 && count.get(h.text) < 3));
  if (items.length < 3) return '';
  const li = items.map(h => `<li class="l${h.pp ? 4 : h.level}"><a href="#${h.id}">${escapeHtml(h.text)}</a></li>`).join('\n');
  return `<aside class="toc" aria-label="Spis treści"><details open><summary>Na tej stronie</summary><ol>\n${li}\n</ol></details></aside>`;
}

function nav(current) {
  return PAGES.map(p => `<a href="${p.page}"${p.page === current ? ' aria-current="page"' : ''}>${escapeHtml(p.nav)}</a>`).join('');
}

function layout(r, body) {
  const title = r.page === 'index.html' ? 'Paddock Principal · Dokumentacja' : `${r.title} · Paddock Principal`;
  const tocHtml = r.page === 'index.html' ? '' : toc(r.headings, r.page);
  return `<!doctype html>
<html lang="pl">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escapeHtml(title)}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Big+Shoulders+Display:wght@800;900&family=Archivo:wght@400;500;600;700&family=JetBrains+Mono:wght@400;600&display=swap" rel="stylesheet">
<link rel="stylesheet" href="assets/docs.css">
<script>try{var t=localStorage.getItem('pp-docs-theme');if(t)document.documentElement.dataset.theme=t}catch(e){}</script>
</head>
<body class="page-${r.page.replace('.html', '')}">
<a class="skip" href="#tresc">Przejdź do treści</a>
<header class="top">
  <a class="brand" href="index.html"><i class="stripes" aria-hidden="true"></i><b>Paddock Principal</b><span>dokumentacja</span></a>
  <nav class="pages" aria-label="Dokumenty">${nav(r.page)}</nav>
  <button class="theme" type="button" aria-label="Zmień motyw" title="Motyw: jasny / ciemny / systemowy"><span aria-hidden="true">◐</span></button>
</header>
<div class="layout${tocHtml ? '' : ' no-toc'}">
${tocHtml}
<main id="tresc">
<article class="doc">
${body}
</article>
<footer class="src">Źródło tej strony: <a href="${GITHUB_BLOB}${r.file}">${escapeHtml(r.file)}</a>. Strona jest generowana, zmiany wprowadza się w pliku .md.</footer>
</main>
</div>
<script src="assets/docs.js"></script>
</body>
</html>
`;
}

/* The start page: README first, then the documents as segmented rows. */
function indexBody(r) {
  const rows = PAGES.filter(p => p.page !== 'index.html').map(p => {
    const desc = INDEX_DESC[p.doc];
    return `<a class="doc-row${p.doc === 'GUIDE' ? ' featured' : ''}" href="${p.page}"><span class="doc-name">${escapeHtml(p.title)}</span><span class="doc-desc">${escapeHtml(desc)}</span><span class="doc-file">${escapeHtml(p.file)}</span></a>`;
  }).join('\n');
  const list = `<div class="doc-list">\n${rows}\n</div>`;
  // README's own table of documents is replaced by the richer list
  const section = /(<h2 id="dokumentacja">[\s\S]*?<\/h2>\s*)<div class="table">[\s\S]*?<\/table><\/div>/;
  if (section.test(r.html)) return r.html.replace(section, (_, heading) => heading + list);
  return `${r.html}\n<h2 id="dokumenty">Dokumenty</h2>\n${list}`;
}

const INDEX_DESC = {
  GUIDE: 'Przewodnik dla testerów: co wybierasz w grze, jak to działa, z wykresami i pytaniami o Twoją opinię.',
  VISION: 'Kierunek projektu, filary i wszystkie przyjęte decyzje PP-001 i dalej.',
  ROADMAP: 'Fazy z bramkami, stan prac i otwarte pytania.',
  DESIGN: 'Pełny projekt systemów: świat, historia, epoki, auto, ludzie, wyścig, AI, ekonomia.',
  TECH: 'Architektura, niezmienniki, determinizm, dane, zapis i testy.',
  HANDOFF_UI: 'Stan klikalnego prototypu i pełne uwagi właściciela do ekranów.',
  AGENTS: 'Zasady pracy dla agentów AI w repozytorium (po angielsku).',
};

export function writeSite(outDir, root = REPO_ROOT) {
  const files = buildSite(root);
  mkdirSync(join(outDir, 'assets'), { recursive: true });
  for (const [name, html] of files) writeFileSync(join(outDir, name), html);
  copyFileSync(join(here, 'assets', 'docs.css'), join(outDir, 'assets', 'docs.css'));
  copyFileSync(join(here, 'assets', 'docs.js'), join(outDir, 'assets', 'docs.js'));
  return [...files.keys()];
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const outFlag = process.argv.indexOf('--out');
  const outDir = outFlag > 0 ? process.argv[outFlag + 1] : join(REPO_ROOT, 'build', 'docs');
  try {
    const pages = writeSite(outDir);
    console.log(`docs: ${pages.length} pages written to ${outDir}`);
    console.log(`docs: open ${join(outDir, 'index.html')}`);
  } catch (e) {
    console.error(e.message);
    process.exit(1);
  }
}
