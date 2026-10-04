// Segmented blocks for GUIDE.md (owner's rule, HANDOFF_UI §3: segments, fields, no walls of text).
// Each fence holds rows separated by "|". Values may name a constant from the code:
//   {Name}                 a constant of the fence's default file (```pola src/...cs)
//   {ClassName.Name}       a constant of any static class in src/ or tools/
//   {Name|dni}             the same with a format (see formatValue in build-docs.mjs)
//
//   ```stan dziala|toku|projekt|pozniej   status of the chapter; the body is one line of detail
//   ```pola [path]          fields: Etykieta | wartość | dopisek
//   ```kroki                numbered flow: Tytuł | opis
//   ```wybory               player decisions: Decyzja | co zmienia
//   ```wgrze                feedback map: Jeśli w grze… | sprawdź
//   ```porownanie A | B     two columns side by side: Wiersz | A | B

import { renderInline, escapeHtml } from './markdown.mjs';

const STATUS = {
  dziala: ['Działa', 'ok'],
  toku: ['Działa, podłączanie do kariery', 'mid'],
  czesciowo: ['Częściowo', 'mid'],
  projekt: ['Zaprojektowane', 'plan'],
  pozniej: ['Później', 'later'],
};

// split on "|" outside {placeholders}, so {Name|dni} stays one cell
const cells = line => line.split(/\|(?![^{]*\})/).map(s => s.trim());
const rows = body => body.filter(l => l.trim()).map(cells);

export function makeBlocks({ cv, ctx, formatValue }) {
  const value = (text, path) => {
    const filled = text.replace(/\{([A-Za-z_][\w.]*)(?:\|([^}]*))?\}/g, (_, ref, spec) => {
      let file = path;
      let name = ref;
      if (ref.includes('.')) {
        const [cls, n] = ref.split('.');
        file = cv.classPath(cls);
        name = n;
      }
      if (!file) throw new Error(`docs: {${ref}} needs a file (add one to the fence)`);
      return `\u0002${formatValue(cv.value(file, name), spec ?? '')}\u0003`;
    });
    return renderInline(filled, ctx).replace(/\u0002/g, '<span class="num">').replace(/\u0003/g, '</span>');
  };

  return {
    stan(kind, body) {
      const [label, tone] = STATUS[kind] ?? (() => { throw new Error(`docs: unknown status "${kind}"`); })();
      const detail = body.filter(l => l.trim()).join(' ');
      return `<!--stan--><div class="status-line"><span class="badge ${tone}">${label}</span>${detail ? `<span class="status-detail">${renderInline(detail, ctx)}</span>` : ''}</div><!--/stan-->`;
    },

    pola(path, body) {
      const items = rows(body).map(([label, val = '', note = '']) =>
        `<div class="field"><span class="f-label">${renderInline(label, ctx)}</span><span class="f-value">${value(val, path)}</span>${note ? `<span class="f-note">${value(note, path)}</span>` : ''}</div>`);
      return `<div class="fields">${items.join('')}</div>`;
    },

    kroki(path, body) {
      const items = rows(body).map(([title, text = '']) =>
        `<li><span class="s-title">${value(title, path)}</span>${text ? `<span class="s-text">${value(text, path)}</span>` : ''}</li>`);
      return `<ol class="steps">${items.join('')}</ol>`;
    },

    wybory(path, body) {
      const items = rows(body).map(([title, text = '']) =>
        `<li><span class="d-title">${value(title, path)}</span><span class="d-text">${value(text, path)}</span></li>`);
      return `<ul class="choices">${items.join('')}</ul>`;
    },

    wgrze(path, body) {
      const items = rows(body).map(([feel, check = '']) =>
        `<tr><td>${value(feel, path)}</td><td class="arrow" aria-hidden="true">→</td><td>${value(check, path)}</td></tr>`);
      return `<div class="feel"><div class="feel-head"><span>Jeśli w grze</span><span>Sprawdź</span></div><table>${items.join('')}</table></div>`;
    },

    porownanie(header, body) {
      const [a = '', b = ''] = cells(header);
      const items = rows(body).map(([label, x = '', y = '']) =>
        `<div class="cmp-row"><span class="cmp-label">${renderInline(label, ctx)}</span><span>${value(x)}</span><span>${value(y)}</span></div>`);
      return `<div class="compare"><div class="cmp-row cmp-head"><span></span><span>${renderInline(a, ctx)}</span><span>${renderInline(b, ctx)}</span></div>${items.join('')}</div>`;
    },
  };
}

/* Wraps every h2 of the guide in a chapter with a header (number, title, status) and puts an index on top. */
export function chapterize(html) {
  const parts = html.split(/(?=<h2 id=")/);
  const intro = parts.shift();
  const index = [];
  const chapters = parts.map(part => {
    const h = part.match(/^<h2 id="([^"]+)">([\s\S]*?)<\/h2>/);
    const id = h[1];
    const inner = h[2].replace(/<a class="anchor"[\s\S]*?<\/a>/, '');
    const numbered = inner.match(/^(\d+)\.\s*([\s\S]*)$/);
    const num = numbered ? numbered[1].padStart(2, '0') : '';
    const title = numbered ? numbered[2] : inner;
    let rest = part.slice(h[0].length);
    let status = '';
    rest = rest.replace(/<!--stan-->([\s\S]*?)<!--\/stan-->/, (_, s) => { status = s; return ''; });
    const tone = (status.match(/badge (\w+)/) || [])[1] ?? '';
    const label = (status.match(/class="badge \w+">([^<]*)</) || [])[1] ?? '';
    index.push(`<a href="#${id}">${num ? `<span class="ci-num">${num}</span>` : ''}<span class="ci-title">${title.replace(/<[^>]+>/g, '')}</span>${tone ? `<i class="dot ${tone}" title="${escapeHtml(label)}"></i>` : ''}</a>`);
    return `<section class="chapter" aria-labelledby="${id}"><header class="ch-head">${num ? `<span class="ch-num" aria-hidden="true">${num}</span>` : ''}<div class="ch-title"><h2 id="${id}">${title}</h2>${status}</div></header>${rest}</section>`;
  });
  return `${intro}<nav class="chapter-index" aria-label="Rozdziały">${index.join('')}</nav>${chapters.join('\n')}`;
}
