// A small Markdown renderer for the project docs. Plain node, no dependencies.
// It covers what the six docs use: headings, paragraphs, nested lists, tables, block quotes, fenced code,
// horizontal rules and the inline marks (code, bold, italic, strike, links). Fenced blocks with a custom
// language ("wykres", "strojenie") are handed to the caller through ctx.fence.
//
// Cross references are resolved here as well, so the HTML pages link to each other:
//   PP-047               -> the decision in VISION
//   DESIGN §5.3, §6.5    -> the numbered section of that page (bare § means the current page)
//   [x](TECH.md#1-stack) -> the generated page instead of the .md file
//   [x](src/...)         -> the file on GitHub

export const GITHUB_BLOB = 'https://github.com/DatJikun/paddock-principal/blob/main/';
export const GITHUB_ISSUES = 'https://github.com/DatJikun/paddock-principal/issues/';

const FENCE = /^(\s*)(```+|~~~+)\s*(.*)$/;
const HEADING = /^(#{1,6})\s+(.*?)\s*#*\s*$/;
const RULE = /^\s*(?:-{3,}|\*{3,}|_{3,})\s*$/;
const MARKER = /^(\s*)([-*+]|\d+[.)])(\s+)(.*)$/;
const TABLE_SEP = /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/;

export function escapeHtml(text) {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

const indentOf = line => line.match(/^\s*/)[0].replace(/\t/g, '    ').length;
const isBlank = line => line.trim() === '';

/* Polish-aware slug for heading ids that are not numbered sections. */
export function slugify(text) {
  const map = { ą: 'a', ć: 'c', ę: 'e', ł: 'l', ń: 'n', ó: 'o', ś: 's', ź: 'z', ż: 'z' };
  return text
    .toLowerCase()
    .replace(/[ąćęłńóśźż]/g, c => map[c])
    .normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'sekcja';
}

/* Id of a heading: numbered sections ("5.3. Rozwój", "6.1a. Inicjalizator", "Faza 4: ...") get a stable id. */
export function headingId(plain) {
  const numbered = plain.match(/^(\d+(?:\.\d+)*[a-z]?)\.?\s/);
  if (numbered) return 's-' + numbered[1].replace(/\./g, '-');
  return slugify(plain);
}

/* Strips inline marks, for ids and the table of contents. */
export function plainText(md) {
  return md
    .replace(/`([^`]*)`/g, '$1')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\*\*|__|~~/g, '')
    .replace(/(^|[^*])\*([^*]+)\*/g, '$1$2')
    .trim();
}

/* ---------- inline ---------- */

export function renderInline(text, ctx, opts = {}) {
  const slots = [];
  const hold = html => `\u0000${slots.push(html) - 1}\u0000`;

  let s = text.replace(/(`+)([\s\S]*?[^`])\1(?!`)/g, (_, __, code) => hold(`<code>${escapeHtml(code.trim())}</code>`));
  s = escapeHtml(s);
  s = s.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_, label, href) =>
    hold(`<a href="${escapeHtml(resolveHref(href.replace(/&amp;/g, '&'), ctx))}">${renderMarks(label)}</a>`));
  s = s.replace(/(^|[\s(])(https?:\/\/[^\s<)]+[^\s<).,;:])/g, (_, pre, url) => pre + hold(`<a href="${url}">${url}</a>`));
  s = renderMarks(s);
  if (!opts.noRefs) s = linkRefs(s, ctx, hold);
  return s.replace(/\u0000(\d+)\u0000/g, (_, i) => slots[Number(i)]);
}

function renderMarks(s) {
  return s
    .replace(/\*\*([^*]+?)\*\*/g, '<strong>$1</strong>')
    .replace(/(^|[^*\w])\*([^*\s][^*]*?)\*(?!\w)/g, '$1<em>$2</em>')
    .replace(/~~([^~]+?)~~/g, '<del>$1</del>');
}

const DOC_NAMES = 'VISION|DESIGN|TECH|ROADMAP|README|AGENTS|GUIDE|HANDOFF_UI|HANDOFF';

function linkRefs(s, ctx, hold) {
  if (!ctx) return s;
  // "DESIGN §5.3" or "TECH.md §6.5" or a bare "§5.3" (current page). A list "§6.1, §6.3" links each.
  s = s.replace(new RegExp(`\\b(${DOC_NAMES})(?:\\.md)?(\\s+)§(\\d+(?:\\.\\d+)*[a-z]?)`, 'g'), (m, doc, sp, num) => {
    const page = ctx.pageOf(doc);
    return page ? hold(`<a class="ref" href="@@${page}#s-${num.replace(/\./g, '-')}">${doc}${sp}§${num}</a>`) : m;
  });
  s = s.replace(/(^|[^\w@#-])§(\d+(?:\.\d+)*[a-z]?)/g, (m, pre, num) =>
    pre + hold(`<a class="ref" href="@@${ctx.page}#s-${num.replace(/\./g, '-')}">§${num}</a>`));
  s = s.replace(/(^|[\s(])#(\d{2,4})\b/g, (m, pre, n) => pre + hold(`<a class="ref" href="${GITHUB_ISSUES}${n}">#${n}</a>`));
  s = s.replace(/\bPP-(\d{3})\b/g, (m, n) => {
    if (ctx.ownPp === n) return m;
    return hold(`<a class="ref pp" href="@@vision.html#pp-${n}">${m}</a>`);
  });
  return s;
}

function resolveHref(href, ctx) {
  if (/^(https?:|mailto:|#)/.test(href)) return href;
  const [path, anchor] = href.split('#');
  const base = path.replace(/^\.\//, '');
  const page = ctx && ctx.pageOfFile(base.replace(/^\.\.\//, ''));
  if (page) {
    if (!anchor) return page;
    const num = anchor.match(/^(\d+(?:-\d+)*)/);
    return `@@${page}#${num ? 's-' + num[1] : anchor}`;
  }
  const fromRoot = ctx && ctx.dir ? normalizePath(ctx.dir + '/' + base) : base;
  return GITHUB_BLOB + fromRoot + (anchor ? '#' + anchor : '');
}

function normalizePath(p) {
  const out = [];
  for (const part of p.split('/')) {
    if (part === '' || part === '.') continue;
    if (part === '..') out.pop();
    else out.push(part);
  }
  return out.join('/');
}

/* ---------- blocks ---------- */

/* Renders Markdown to HTML. ctx collects headings (ctx.headings) and handles custom fences (ctx.fence). */
export function renderMarkdown(source, ctx) {
  ctx.headings ??= [];
  ctx.ids ??= new Set();
  const lines = source.replace(/\r\n?/g, '\n').split('\n');
  return renderBlocks(lines, ctx);
}

function renderBlocks(lines, ctx) {
  const out = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    if (isBlank(line)) { i++; continue; }

    const fence = line.match(FENCE);
    if (fence) {
      const close = new RegExp(`^\\s*${fence[2][0] === '`' ? '`' : '~'}{${fence[2].length},}\\s*$`);
      const body = [];
      i++;
      while (i < lines.length && !close.test(lines[i])) body.push(lines[i++]);
      i++;
      const info = fence[3].trim();
      const custom = info && ctx.fence ? ctx.fence(info, body) : null;
      if (custom != null) out.push(custom);
      else out.push(`<pre><code>${escapeHtml(body.join('\n'))}</code></pre>`);
      continue;
    }

    const heading = line.match(HEADING);
    if (heading) {
      out.push(renderHeading(heading[1].length, heading[2], ctx));
      i++;
      continue;
    }

    if (RULE.test(line)) {
      out.push('<hr>');
      i++;
      continue;
    }

    if (line.trimStart().startsWith('|') && i + 1 < lines.length && TABLE_SEP.test(lines[i + 1])) {
      const rows = [];
      while (i < lines.length && lines[i].trimStart().startsWith('|')) rows.push(lines[i++]);
      out.push(renderTable(rows, ctx));
      continue;
    }

    if (/^\s*>/.test(line)) {
      const inner = [];
      while (i < lines.length && /^\s*>/.test(lines[i])) inner.push(lines[i++].replace(/^\s*>\s?/, ''));
      out.push(renderQuote(inner, ctx));
      continue;
    }

    if (MARKER.test(line)) {
      const { html, next } = renderList(lines, i, ctx);
      out.push(html);
      i = next;
      continue;
    }

    const para = [];
    while (i < lines.length && !isBlank(lines[i]) && !startsBlock(lines, i)) {
      const text = lines[i++].trim();
      // a new "**Label:**" line keeps its own line, as in the header of every doc
      para.push(para.length && /^\*\*[^*]{1,40}:\*\*/.test(text) ? '\u0001' + text : text);
    }
    if (para.length === 0) { para.push(lines[i++].trim()); }
    out.push(renderParagraph(para.join(' '), ctx).replace(/ ?\u0001/g, '<br>'));
  }
  return out.join('\n');
}

function startsBlock(lines, i) {
  const line = lines[i];
  return FENCE.test(line) || HEADING.test(line) || /^\s*>/.test(line) || MARKER.test(line)
    || (line.trimStart().startsWith('|') && i + 1 < lines.length && TABLE_SEP.test(lines[i + 1]));
}

function renderParagraph(text, ctx) {
  const pp = text.match(/^\*\*PP-(\d{3})\b/);
  if (pp && ctx.page === 'vision.html') {
    const id = `pp-${pp[1]}`;
    ctx.ids.add(id);
    const title = (text.match(/^\*\*([^*]+)\*\*/) || [, `PP-${pp[1]}`])[1];
    ctx.headings.push({ level: 4, id, text: plainText(title).replace(/[.:]\s*$/, ''), pp: true });
    const html = renderInline(text, { ...ctx, ownPp: pp[1] });
    return `<p id="${id}" class="decision">${html}</p>`;
  }
  return `<p>${renderInline(text, ctx)}</p>`;
}

function renderHeading(level, text, ctx) {
  const plain = plainText(text);
  let id = headingId(plain);
  while (ctx.ids.has(id)) id += '-2';
  ctx.ids.add(id);
  ctx.headings.push({ level, id, text: plain });
  const anchor = level > 1 ? `<a class="anchor" href="#${id}" aria-label="Link do sekcji">#</a>` : '';
  return `<h${level} id="${id}">${renderInline(text, ctx)}${anchor}</h${level}>`;
}

function splitCells(row) {
  let s = row.trim();
  if (s.startsWith('|')) s = s.slice(1);
  if (s.endsWith('|') && !s.endsWith('\\|')) s = s.slice(0, -1);
  const cells = [];
  let cur = '';
  let inCode = false;
  for (let k = 0; k < s.length; k++) {
    const c = s[k];
    if (c === '`') inCode = !inCode;
    if (c === '\\' && s[k + 1] === '|') { cur += '|'; k++; continue; }
    if (c === '|' && !inCode) { cells.push(cur.trim()); cur = ''; continue; }
    cur += c;
  }
  cells.push(cur.trim());
  return cells;
}

function renderTable(rows, ctx) {
  const head = splitCells(rows[0]);
  const align = splitCells(rows[1]).map(c => (c.startsWith(':') && c.endsWith(':') ? 'center' : c.endsWith(':') ? 'right' : ''));
  const cell = (tag, text, k) => {
    const a = align[k] ? ` class="al-${align[k]}"` : '';
    return `<${tag}${a}>${renderInline(text, ctx)}</${tag}>`;
  };
  const body = rows.slice(2).map(r => `<tr>${splitCells(r).map((c, k) => cell('td', c, k)).join('')}</tr>`).join('\n');
  return `<div class="table"><table>\n<thead><tr>${head.map((c, k) => cell('th', c, k)).join('')}</tr></thead>\n<tbody>\n${body}\n</tbody></table></div>`;
}

/* A quote that starts with "**Stan:**" is a status note; "**Uwaga:**" a warning; "**Strojenie:**" a feedback hint. */
function renderQuote(lines, ctx) {
  const first = (lines.find(l => !isBlank(l)) || '').trim();
  const kind = /^\*\*Stan:?\*\*/.test(first) ? 'status' : /^\*\*Uwaga:?\*\*/.test(first) ? 'note' : /^\*\*W grze:?\*\*/.test(first) ? 'feel' : '';
  const cls = kind ? ` class="${kind}"` : '';
  return `<blockquote${cls}>${renderBlocks(lines, ctx)}</blockquote>`;
}

function renderList(lines, start, ctx) {
  const first = lines[start].match(MARKER);
  const baseIndent = indentOf(first[1]);
  const ordered = /\d/.test(first[2]);
  const items = [];
  let i = start;
  while (i < lines.length) {
    const m = lines[i].match(MARKER);
    if (!m || indentOf(m[1]) !== baseIndent || /\d/.test(m[2]) !== ordered) break;
    const contentIndent = baseIndent + m[2].length + m[3].length;
    const body = [m[4]];
    let loose = false;
    i++;
    while (i < lines.length) {
      const line = lines[i];
      if (isBlank(line)) {
        let j = i + 1;
        while (j < lines.length && isBlank(lines[j])) j++;
        if (j < lines.length && indentOf(lines[j]) >= contentIndent) {
          loose = true;
          for (; i < j; i++) body.push('');
          continue;
        }
        break;
      }
      const ind = indentOf(line);
      if (ind <= baseIndent) {
        if (MARKER.test(line) || startsBlock(lines, i)) break;
        if (body.length && !isBlank(body[body.length - 1])) { body.push(line.trim()); i++; continue; }
        break;
      }
      body.push(line.slice(Math.min(ind, contentIndent)));
      i++;
    }
    items.push({ body, loose, start: ordered ? Number(m[2].replace(/\D/g, '')) : null });
    // a blank line between items makes the next item start after it
    if (i < lines.length && isBlank(lines[i])) {
      let j = i;
      while (j < lines.length && isBlank(lines[j])) j++;
      const nm = j < lines.length ? lines[j].match(MARKER) : null;
      if (nm && indentOf(nm[1]) === baseIndent && /\d/.test(nm[2]) === ordered) { i = j; continue; }
      break;
    }
  }
  const tag = ordered ? 'ol' : 'ul';
  const startAttr = ordered && items[0].start !== 1 ? ` start="${items[0].start}"` : '';
  const lis = items.map(item => {
    const html = renderBlocks(item.body, ctx);
    const tight = !item.loose ? html.replace(/^<p>([\s\S]*?)<\/p>/, '$1') : html;
    return `<li>${tight}</li>`;
  });
  return { html: `<${tag}${startAttr}>\n${lis.join('\n')}\n</${tag}>`, next: i };
}

/* Replaces the @@page#id placeholders. An id the target page lacks falls back to its parent section, then the page. */
export function resolveRefs(html, pages) {
  return html.replace(/@@([\w-]+\.html)(#[\w-]+)?/g, (_, page, hash) => {
    if (!hash) return page;
    const ids = pages.get(page);
    let id = hash.slice(1);
    if (ids) {
      while (!ids.has(id) && /-[^-]+$/.test(id) && id.startsWith('s-')) id = id.replace(/-[^-]+$/, '');
      if (!ids.has(id)) return page;
    }
    return `${page}#${id}`;
  });
}
