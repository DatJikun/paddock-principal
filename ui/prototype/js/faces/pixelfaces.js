/* =========================================================
   PixelFaces: awatary pixel-art składane z warstw.
   Każda część (głowa, oczy, fryzura, kombinezon…) to tekstowa siatka
   w bibliotece danej gęstości (lib16.js, lib24.js, lib32.js).
   Jeden znak = jedna rola koloru; kolory dobiera osoba (skóra, włosy,
   siwienie z wiekiem) i zespół (barwy stroju). Wygląd jest deterministyczny:
   ta sama osoba, rok i zespół dają zawsze ten sam obrazek.

   Format biblioteki (szczegóły i role kolorów: nagłówek lib24.js):
     size 24
     crop head 4 1 16 16
     == slot/nazwa @x,y mirror era:1950-1969 w:3 role:driver
     ..kkSS
   ========================================================= */
(() => {
const PF = { libs: {}, cache: new Map() };

/* ---------- role kolorów ---------- */
const ROLES = {
  '.': 'pusty',
  '_': 'wymazuje włosy (tylko maski łysienia)',
  K: 'kontur (atrament)',
  l: 'skóra: światło', S: 'skóra', s: 'skóra: cień', k: 'skóra: kontur',
  m: 'usta: linia', M: 'usta: warga',
  W: 'oko: białko', E: 'oko: źrenica', e: 'oko: tęczówka',
  j: 'włosy: światło', H: 'włosy', h: 'włosy: cień', d: 'włosy: kontur', b: 'brwi', u: 'zarost: cień',
  T: 'zespół: barwa 1', t: 'zespół: barwa 1, cień', R: 'zespół: barwa 2', r: 'zespół: barwa 2, cień',
  w: 'biel', x: 'biel: cień', C: 'strój cywilny', c: 'strój cywilny: cień', n: 'koszula',
  G: 'oprawka', g: 'szkło', L: 'odblask',
};
PF.ROLES = ROLES;

/* ---------- parser ---------- */
PF.parse = (text) => {
  const lib = { size: 0, crops: { full: null }, parts: [], errors: [] };
  let cur = null;
  const err = (n, msg) => lib.errors.push(`linia ${n}: ${msg}`);
  text.split('\n').forEach((raw, i) => {
    const n = i + 1, line = raw.trim();
    if (!line) { cur = null; return; }
    if (line.startsWith('#')) return;
    if (line.startsWith('size ')) { lib.size = +line.slice(5); lib.crops.full = [0, 0, lib.size, lib.size]; return; }
    if (line.startsWith('crop ')) { const [, name, ...v] = line.split(/\s+/); lib.crops[name] = v.map(Number); return; }
    if (line.startsWith('==')) {
      const [id, ...opts] = line.slice(2).trim().split(/\s+/);
      const [slot, name] = id.split('/');
      cur = { slot, name: name || slot, id, x: 0, y: 0, mirror: false, flip: false, tags: {}, opts, rows: [], line: n };
      for (const o of opts) {
        if (o.startsWith('@')) { const [x, y] = o.slice(1).split(',').map(Number); cur.x = x; cur.y = y; }
        else if (o === 'mirror') cur.mirror = true;
        else if (o === 'flip') cur.flip = true;
        else { const [k, v = '1'] = o.split(':'); cur.tags[k] = v; }
      }
      lib.parts.push(cur);
      return;
    }
    if (!cur) { err(n, 'siatka bez nagłówka =='); return; }
    for (const ch of line) if (!(ch in ROLES)) err(n, `nieznany znak „${ch}” w ${cur.id}`);
    cur.rows.push(line);
  });
  const S = lib.size;
  if (!S) lib.errors.push('brak „size”');
  for (const p of lib.parts) {
    const w = Math.max(0, ...p.rows.map(r => r.length));
    if (p.rows.some(r => r.length !== w)) lib.errors.push(`${p.id} (linia ${p.line}): wiersze mają różną długość`);
    if (p.mirror && p.x + w > S / 2) lib.errors.push(`${p.id}: część lustrzana wychodzi za połowę (${p.x + w} > ${S / 2})`);
    if (p.x + w > S || p.y + p.rows.length > S) lib.errors.push(`${p.id}: wychodzi poza płótno ${S}×${S}`);
    p.w = w; p.h = p.rows.length;
    p.weight = +(p.tags.w ?? 1);
    if (p.tags.era) { const [a, b] = p.tags.era.split('-').map(Number); p.era = [a, b || a]; }
    if (p.tags.age) { const [a, b] = p.tags.age.split('-').map(Number); p.age = [a, b || 200]; }
    p.roles = p.tags.role ? p.tags.role.split('|') : null;
  }
  lib.slots = {};
  for (const p of lib.parts) (lib.slots[p.slot] ||= []).push(p);
  return lib;
};
PF.library = (text) => {
  const lib = PF.parse(text);
  lib.source = text;
  PF.libs[lib.size] = lib;
  PF.cache.clear();
  if (lib.errors.length && typeof console !== 'undefined') console.warn(`PixelFaces ${lib.size}:`, lib.errors);
  return lib;
};

/* ---------- losowość: deterministyczne strumienie z nazwą (INV-002) ----------
   Prototyp: FNV-1a + mulberry32. Wersja w grze bierze strumień z Paddock.Core. */
const hash = (s) => { let h = 2166136261; for (const ch of String(s)) { h ^= ch.codePointAt(0); h = Math.imul(h, 16777619); } return h >>> 0; };
const stream = (...key) => {
  let a = hash(key.join('|'));
  return () => { a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
};
const pickW = (r, items, wf) => {
  const ws = items.map(wf), sum = ws.reduce((a, b) => a + b, 0);
  if (!sum) return items[0];
  let x = r() * sum;
  for (let i = 0; i < items.length; i++) { x -= ws[i]; if (x < 0) return items[i]; }
  return items[items.length - 1];
};
PF.hash = hash;

/* ---------- kolory ---------- */
const hex = (c) => '#' + c.map(v => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('');
const rgb = (h) => { h = h.replace('#', ''); if (h.length === 3) h = [...h].map(c => c + c).join(''); return [0, 2, 4].map(i => parseInt(h.slice(i, i + 2), 16)); };
const mix = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);
const R4 = (...h) => h.map(rgb);

/* karnacje: światło, baza, cień, kontur. Jedna naturalna skala, bez „kolorów etnicznych”. */
const SKIN = [
  R4('#f7dcc9', '#efc4a7', '#d9a183', '#a0634f'),
  R4('#f3d0ae', '#e6b58f', '#cc9270', '#8e5a42'),
  R4('#eec59c', '#dba77b', '#be855c', '#7f5038'),
  R4('#e3b988', '#cb9b6a', '#ab7a50', '#6c4428'),
  R4('#d6a373', '#bb8453', '#99653b', '#5c3820'),
  R4('#b98052', '#9c663c', '#7d4e2b', '#4a2b17'),
  R4('#94603a', '#7a4b2a', '#5f371d', '#37200f'),
  R4('#704529', '#5a351e', '#442615', '#27160b'),
];
/* włosy: światło, baza, cień, kontur */
const HAIR = {
  black:  R4('#4d423c', '#2c2421', '#1d1715', '#0f0b0a'),
  dark:   R4('#6e4c34', '#4f3423', '#382419', '#20140d'),
  brown:  R4('#946842', '#724c2e', '#553820', '#301f11'),
  light:  R4('#b58a5c', '#936b43', '#704f30', '#40301b'),
  dblond: R4('#d4ae70', '#b38c51', '#8c6b3b', '#523c20'),
  blond:  R4('#f0d896', '#d8b86d', '#b3924d', '#6d5629'),
  ginger: R4('#dc8c4f', '#bb6930', '#914c20', '#562b12'),
  auburn: R4('#a05e3c', '#7c4127', '#5c2d1b', '#341910'),
};
const GREY = R4('#e6e2da', '#bdb8af', '#949087', '#5a564f');
const EYES = { brown: '#5b3a22', dark: '#2e1f15', hazel: '#7d6a34', green: '#4f7b4b', blue: '#4b79aa', grey: '#7c8b97' };
const INK = rgb('#231710');
/* stroje cywilne wg epoki */
const SUITS = [
  { era: [1950, 1969], c: ['#5f5c57', '#2f3a55', '#6b5842', '#4a4a3e'] },
  { era: [1970, 1984], c: ['#6a4a32', '#9a7650', '#2f3a55', '#5a2e2a', '#4c5a3a'] },
  { era: [1985, 2100], c: ['#2b3346', '#3a3d44', '#1f2430', '#4a4f58'] },
];
const SHIRTS = ['#f4f1ea', '#dfe7ef', '#efe6d2'];

/* ---------- regiony: tylko rozkłady wag, szerokie i zachodzące na siebie ---------- */
const REGION = {};
'GBR IRL SWE NOR DEN FIN GER AUT SUI BEL NED USA CAN AUS NZL POL CZE HUN RUS'.split(' ').forEach(c => REGION[c] = 'N');
'ITA FRA ESP POR ARG BRA MEX VEN COL CHI URU MON'.split(' ').forEach(c => REGION[c] = 'S');
'JPN CHN KOR THA MAS INA HKG TPE'.split(' ').forEach(c => REGION[c] = 'E');
'IND UAE BHR MAR IRN ISR TUR'.split(' ').forEach(c => REGION[c] = 'M');
'RSA ZIM RHO'.split(' ').forEach(c => REGION[c] = 'N');
'NGA GHA KEN'.split(' ').forEach(c => REGION[c] = 'A');
const W_SKIN = { N: [3, 5, 3, 1, .3, .15, .1, .05], S: [.5, 2, 4, 4, 2, .6, .3, .1], E: [.4, 2, 4, 3, 1, .2, 0, 0], M: [0, .3, 1.5, 3, 3, 2, .8, .2], A: [0, 0, .2, .4, 1, 3, 4, 3], X: [1, 1, 1, 1, 1, 1, 1, 1] };
const W_HAIR = {
  N: { black: .5, dark: 2, brown: 3, light: 2, dblond: 2, blond: 1.5, ginger: .5, auburn: .6 },
  S: { black: 3, dark: 4, brown: 2, light: .6, dblond: .2, blond: .1, ginger: .05, auburn: .3 },
  E: { black: 6, dark: 2 }, M: { black: 4, dark: 3, brown: .5 }, A: { black: 6, dark: 1 },
  X: { black: 2, dark: 2, brown: 2, light: 1, dblond: 1, blond: 1, ginger: .3, auburn: .3 },
};
const W_EYES = { N: { blue: 3, grey: 1.5, green: 1, hazel: 1, brown: 2 }, S: { brown: 4, dark: 2, hazel: 1.5, green: .6, blue: .5 }, X: { brown: 3, dark: 3, hazel: .5 } };
const TEX = { N: { straight: 6, wavy: 3, curly: .6 }, S: { straight: 4, wavy: 4, curly: 1.2 }, E: { straight: 1 }, M: { straight: 3, wavy: 3, curly: 2 }, A: { curly: 1 }, X: { straight: 3, wavy: 2, curly: 1 } };
const fromMap = (r, m) => pickW(r, Object.keys(m), k => m[k]);

/* ---------- cechy tożsamości: stałe przez całe życie ---------- */
PF.identity = (p) => {
  const r = stream(p.id, 'identity'), region = REGION[p.nat] || 'X', look = p.look || {};
  const skinI = look.skin ?? pickW(r, [0, 1, 2, 3, 4, 5, 6, 7], i => W_SKIN[region][i]);
  const hair = look.hair ?? fromMap(r, W_HAIR[region]);
  const eyes = look.eyes ?? fromMap(r, W_EYES[region] || W_EYES.X);
  const tex = look.tex ?? fromMap(r, TEX[region]);
  return {
    region, skinI, hair, eyes, tex,
    wide: look.wide ?? (r() < .4 ? 1 : 0),
    pick: r() * 1e9 | 0,                    // ziarno wyboru kształtów twarzy
    flip: r() < .5,                          // strona przedziałka
    greyAt: look.greyAt ?? 30 + r() * 22,   // wiek pierwszej siwizny
    greyRate: .6 + r() * .9,
    recedeAt: look.recedeAt ?? (r() < .35 ? 999 : 26 + r() * 34),
    lines: r() * 8 - 4,                      // zmarszczki wcześniej lub później
    beardy: look.beardy ?? r(),              // skłonność do zarostu
    sight: r(),                              // kiedy potrzebuje okularów
    hairLen: r(),                            // skłonność do dłuższych włosów
    suit: r(), shirt: r(),
    slide: r() * 6 | 0,                      // przesunięcie okien mody (nie wszyscy zmieniają fryzurę w tym samym roku)
  };
};

const allowed = (p, ctx) =>
  (!p.era || (ctx.year >= p.era[0] && ctx.year <= p.era[1])) &&
  (!p.age || (ctx.age >= p.age[0] && ctx.age <= p.age[1])) &&
  (!p.roles || p.roles.includes(ctx.role));

/* ---------- wybór części ---------- */
PF.look = (lib, person, opts) => {
  const id = PF.identity(person), look = person.look || {};
  const year = opts.year ?? 1976, age = person.age ?? (person.born ? year - person.born : 30);
  const role = person.role || 'driver';
  const ctx = { year, age, role };
  const slots = lib.slots, sel = {};
  const choose = (slot, r, extra = () => 1, forced) => {
    const pool = (slots[slot] || []).filter(p => allowed(p, ctx));
    if (forced) { const f = pool.find(p => p.name === forced) || (slots[slot] || []).find(p => p.name === forced); if (f) return f; }
    return pool.length ? pickW(r, pool, p => p.weight * extra(p)) : null;
  };
  // twarz: z tożsamości, bez wpływu epoki
  const ri = stream(person.id, 'face', id.pick);
  for (const s of ['head', 'ears', 'eyes', 'brows', 'nose', 'mouth']) sel[s] = choose(s, ri, undefined, look[s]);
  // wygląd zależny od epoki: okno mody co 6 lat, przesunięte per osoba
  const win = Math.floor((year + id.slide) / 6);
  const rl = stream(person.id, 'look', win);
  sel.hair = choose('hair', rl, p => {
    const tex = !p.tags.tex || p.tags.tex.split('|').includes(id.tex) ? 1 : .02;
    const len = p.tags.len === 'long' ? .3 + id.hairLen * 2 : p.tags.len === 'short' ? 1.6 - id.hairLen : 1;
    return tex * len;
  }, look.hairStyle);
  sel.hairback = sel.hair && (slots.hairback || []).find(p => p.name === sel.hair.name) || null;
  sel.beard = choose('beard', rl, p => p.name === 'none' ? 1 + (1 - id.beardy) * 3 : id.beardy * 1.5, look.beard);
  const needGlasses = look.glasses ? true : look.glasses === false ? false : age > 38 + id.sight * 50 || id.sight < .07;
  sel.glasses = needGlasses ? choose('glasses', rl, undefined, typeof look.glasses === 'string' ? look.glasses : undefined) : null;
  sel.body = choose('body', stream(person.id, 'body', year), undefined, look.body);
  sel.neck = (slots.neck || [])[0] || null;
  // wiek: łysienie i zmarszczki
  const rec = age < id.recedeAt ? 0 : age < id.recedeAt + 9 ? 1 : age < id.recedeAt + 18 ? 2 : 3;
  const recede = look.recede ?? rec;
  sel.recede = recede ? (slots.recede || []).find(p => +p.name === recede) || null : null;
  const lineAge = age + id.lines;
  sel.age = (slots.age || []).filter(p => !p.age || lineAge >= p.age[0]);
  return { id, sel, ctx };
};

/* ---------- paleta osoby ---------- */
PF.palette = (person, id, ctx, team) => {
  const sk = SKIN[id.skinI], hr = HAIR[id.hair] || HAIR.brown;
  const g = Math.max(0, Math.min(.92, (ctx.age - id.greyAt) / (26 / id.greyRate)));
  const grey = (t) => hr.map((c, i) => mix(c, GREY[i], t));
  const hc = grey(g), bc = grey(Math.min(1, g * 1.25));
  const t1 = rgb(team?.t1 || '#1f4f9a'), t2 = rgb(team?.t2 || '#e03a3e');
  const era = SUITS.find(s => ctx.year >= s.era[0] && ctx.year <= s.era[1]) || SUITS[2];
  const suit = rgb(era.c[Math.floor(id.suit * era.c.length)]);
  const base = {
    K: INK, l: sk[0], S: sk[1], s: sk[2], k: sk[3],
    m: mix(sk[3], [120, 40, 40], .35), M: mix(sk[2], [190, 90, 90], .3),
    W: [238, 233, 222], E: [30, 22, 18], e: rgb(EYES[id.eyes] || EYES.brown),
    j: hc[0], H: hc[1], h: hc[2], d: hc[3], b: mix(hr[3], GREY[2], g * .6), u: mix(sk[1], hc[3], .3),
    T: t1, t: mix(t1, [0, 0, 0], .28), R: t2, r: mix(t2, [0, 0, 0], .25),
    w: [244, 240, 232], x: [196, 190, 180], C: suit, c: mix(suit, [0, 0, 0], .3), n: rgb(SHIRTS[Math.floor(id.shirt * SHIRTS.length)]),
    G: [40, 32, 28], g: { over: [120, 150, 160], a: .28 }, L: { over: [255, 255, 255], a: .75 },
  };
  const beard = { ...base, j: bc[0], H: bc[1], h: bc[2], d: bc[3], u: mix(sk[1], bc[3], .3) };
  return { base, beard };
};

/* ---------- składanie ---------- */
const LAYERS = ['hairback', 'neck', 'body', 'head', 'ears', 'age', 'eyes', 'brows', 'nose', 'mouth', 'beard', 'hair', 'glasses'];
const WIDEN = { head: 1, ears: 1, hair: 1, hairback: 1, beard: 1, age: 1, recede: 1 };

/* rysuje część na warstwę: lustro, odbicie przedziałka, poszerzenie głowy */
const stamp = (layer, S, part, pal, o) => {
  const put = (x, y, ch) => {
    if (ch === '.') return;
    if (o.flip) x = S - 1 - x;
    if (o.wide && WIDEN[part.slot]) {
      const c = S / 2;
      if (x < c - 1 || x > c) { put0(x < c ? x - 1 : x + 1, y, ch); return; }
      put0(x < c ? x - 1 : x + 1, y, ch); put0(x, y, ch); return;
    }
    put0(x, y, ch);
  };
  const put0 = (x, y, ch) => {
    if (x < 0 || y < 0 || x >= S || y >= S) return;
    layer[y * S + x] = ch === '_' ? '_' : pal[ch];
  };
  part.rows.forEach((row, j) => [...row].forEach((ch, i) => {
    const x = part.x + i, y = part.y + j;
    put(x, y, ch);
    if (part.mirror) put(S - 1 - x, y, ch);
  }));
};

PF.compose = (person, opts = {}) => {
  const lib = PF.libs[opts.size || 24];
  if (!lib) throw new Error(`PixelFaces: brak biblioteki ${opts.size}`);
  const S = lib.size, { id, sel, ctx } = PF.look(lib, person, opts);
  const { base, beard } = PF.palette(person, id, ctx, opts.team);
  const px = new Array(S * S).fill(null);
  const flipFor = (p) => p.flip && id.flip;
  for (const slot of LAYERS) {
    if (opts.skip === slot) continue;
    const parts = slot === 'age' ? sel.age : [sel[slot]];
    for (const part of parts) {
      if (!part || !part.rows.length) continue;
      const layer = new Array(S * S).fill(undefined);
      stamp(layer, S, part, slot === 'beard' ? beard : base, { flip: flipFor(part), wide: id.wide });
      if (slot === 'hair' && sel.recede) {
        const mask = new Array(S * S).fill(undefined);
        stamp(mask, S, sel.recede, base, { wide: id.wide });
        mask.forEach((m, i) => { if (m === '_') layer[i] = undefined; });
      }
      layer.forEach((c, i) => {
        if (c === undefined || c === '_') return;
        if (c.over) { if (px[i]) px[i] = mix(px[i], c.over, c.a); return; }
        px[i] = c;
      });
    }
  }
  return { size: S, px, sel, id, ctx, lib };
};

/* ---------- SVG: jedna ścieżka na kolor, piksele łączone w poziome odcinki ---------- */
PF.svg = (person, opts = {}) => {
  const key = JSON.stringify([person.id, person.age, person.born, person.role, person.look, opts.size, opts.year, opts.team, opts.crop, opts.px]);
  if (PF.cache.has(key)) return PF.cache.get(key);
  const f = PF.compose(person, opts), S = f.size;
  const [cx, cy, cw, ch] = f.lib.crops[opts.crop || 'full'] || f.lib.crops.full;
  const paths = {};
  for (let y = cy; y < cy + ch; y++) {
    let x = cx;
    while (x < cx + cw) {
      const c = f.px[y * S + x];
      if (!c) { x++; continue; }
      const h = hex(c); let n = 1;
      while (x + n < cx + cw && f.px[y * S + x + n] && hex(f.px[y * S + x + n]) === h) n++;
      (paths[h] ||= []).push(`M${x} ${y}h${n}v1h-${n}z`);
      x += n;
    }
  }
  const size = opts.px ? ` width="${opts.px}" height="${opts.px}"` : '';
  const out = `<svg class="pf" viewBox="${cx} ${cy} ${cw} ${ch}"${size} shape-rendering="crispEdges" aria-hidden="true">${Object.entries(paths).map(([c, d]) => `<path fill="${c}" d="${d.join('')}"/>`).join('')}</svg>`;
  PF.cache.set(key, out);
  return out;
};

/* szkice z edytora (avatar-editor.html) zapisane w przeglądarce; tylko arkusz i edytor, nie gra */
PF.DRAFT_KEY = (size) => `pp-faces-draft-${size}`;
PF.useDrafts = () => {
  const used = [];
  for (const size of Object.keys(PF.libs)) {
    let text = null;
    try { text = localStorage.getItem(PF.DRAFT_KEY(size)); } catch (e) {}
    if (text) { PF.library(text); used.push(+size); }
  }
  return used;
};

/* odcisk palca wyglądu: do wykrywania „klonów” na arkuszu i w teście */
PF.signature = (person, opts = {}) => {
  const f = PF.compose(person, opts);
  return ['head', 'eyes', 'brows', 'nose', 'mouth', 'hair', 'beard'].map(s => f.sel[s]?.name || '-').join('/') + `/${f.id.skinI}/${f.id.hair}/${f.id.wide}`;
};

if (typeof window !== 'undefined') window.PF = PF;
if (typeof module !== 'undefined') module.exports = PF;
})();
