/* Edytor części twarzy: malowanie siatki, podgląd na 8 osobach, tekst do skopiowania albo pobrany plik.
   Zmiany żyją jako szkic w przeglądarce (localStorage) i są widoczne na arkuszu twarzy. */
(() => {
const $ = (s) => document.querySelector(s);
const ORIG = Object.fromEntries(Object.entries(PF.libs).map(([s, l]) => [s, l.source]));
PF.useDrafts();

const ED = { size: 24, id: null, role: 'H', M: null, meta: null, undo: [] };
try { Object.assign(ED, JSON.parse(localStorage.getItem('pp-faces-editor') || '{}'), { M: null, meta: null, undo: [] }); } catch (e) {}
const saveUi = () => { try { localStorage.setItem('pp-faces-editor', JSON.stringify({ size: ED.size, id: ED.id, role: ED.role })); } catch (e) {} };
const lib = () => PF.libs[ED.size];
const part = () => lib().parts.find(p => p.id === ED.id);
/* sloty, których lustrzane części muszą dochodzić do osi */
const AXIS = ['head', 'neck', 'body', 'hair'];
const SLOT_ORDER = ['head', 'ears', 'neck', 'eyes', 'brows', 'nose', 'mouth', 'age', 'beard', 'hair', 'hairback', 'recede', 'glasses', 'body'];
const SLOT_NAMES = { head: 'Głowa', ears: 'Uszy', neck: 'Szyja', eyes: 'Oczy', brows: 'Brwi', nose: 'Nos', mouth: 'Usta', age: 'Wiek', beard: 'Zarost', hair: 'Włosy', hairback: 'Włosy z tyłu', recede: 'Łysienie', glasses: 'Okulary', body: 'Strój' };

/* ---------- osoby do podglądu: różne karnacje i kolory włosów ---------- */
const SKINS = [1, 3, 5, 7, 0, 2, 4, 6], HAIRS = ['brown', 'blond', 'black', 'ginger', 'dark', 'dblond', 'auburn', 'light'];
function samples(p) {
  const look = { head: { head: p.name }, ears: { ears: p.name }, eyes: { eyes: p.name }, brows: { brows: p.name }, nose: { nose: p.name }, mouth: { mouth: p.name },
    hair: { hairStyle: p.name }, hairback: { hairStyle: p.name }, beard: { beard: p.name }, glasses: { glasses: p.name }, body: { body: p.name }, recede: { recede: +p.name, hairStyle: 'crew' } }[p.slot] || {};
  const year = p.era ? Math.round((p.era[0] + Math.min(p.era[1], 2026)) / 2) : 1976;
  const age = p.age ? Math.max(p.age[0] + 4, 30) : p.slot === 'recede' ? 50 : 30;
  const role = p.roles ? p.roles[0] : 'driver';
  return SKINS.map((skin, i) => ({ person: { id: `ed-${i}`, nat: 'GBR', age, role, look: { ...look, skin, hair: HAIRS[i], beard: look.beard || 'none', glasses: look.glasses || false } }, year }));
}
const TEAM = { t1: '#1f4f9a', t2: '#e03a3e' };

/* ---------- siatka części <-> macierz płótna ---------- */
function load() {
  const p = part(), S = ED.size;
  ED.M = Array.from({ length: S }, () => Array(S).fill('.'));
  p.rows.forEach((row, j) => [...row].forEach((ch, i) => { if (p.y + j < S && p.x + i < S) ED.M[p.y + j][p.x + i] = ch; }));
  ED.meta = { slot: p.slot, name: p.name, mirror: p.mirror, flip: p.flip, tags: p.opts.filter(o => !o.startsWith('@') && o !== 'mirror' && o !== 'flip').join(' ') };
  ED.undo = [];
}
function block() {
  const S = ED.size, m = ED.meta, M = ED.M, maxX = m.mirror ? S / 2 - 1 : S - 1;
  let x0 = S, y0 = S, x1 = -1, y1 = -1;
  for (let y = 0; y < S; y++) for (let x = 0; x <= maxX; x++) if (M[y][x] !== '.') { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
  const head = [`== ${m.slot}/${m.name}`];
  if (x1 < 0) return [[...head, m.mirror ? 'mirror' : '', m.flip ? 'flip' : '', m.tags].filter(Boolean).join(' ')];
  if (m.mirror && AXIS.includes(m.slot)) x1 = S / 2 - 1;
  const rows = [];
  for (let y = y0; y <= y1; y++) rows.push(M[y].slice(x0, x1 + 1).join(''));
  return [[...head, `@${x0},${y0}`, m.mirror ? 'mirror' : '', m.flip ? 'flip' : '', m.tags].filter(Boolean).join(' '), ...rows];
}
/* podmienia blok części w źródle biblioteki i przeładowuje ją */
function replaceBlock(p, lines) {
  const src = lib().source.split('\n');
  if (p) src.splice(p.line - 1, 1 + p.rows.length, ...lines);
  else {
    const last = [...lib().parts].reverse().find(q => q.slot === ED.meta.slot);
    const at = last ? last.line + last.rows.length : src.length - 1;
    src.splice(at, 0, '', ...lines);
  }
  reload(src.join('\n'));
}
function reload(text) {
  PF.library(text);
  try { localStorage.setItem(PF.DRAFT_KEY(ED.size), text); } catch (e) {}
}
const commit = () => { replaceBlock(part(), block()); ED.id = `${ED.meta.slot}/${ED.meta.name}`; drawAll(false); };

/* ---------- płótno ---------- */
const cv = $('#cv'), cx = cv.getContext('2d');
const css = (c) => c.over ? `rgba(${c.over.join(',')},${c.a})` : `rgb(${c.map(Math.round).join(',')})`;
function palette() {
  const s = samples(part())[0], l = lib(), f = PF.look(l, s.person, { year: s.year });
  const pals = PF.palette(s.person, f.id, f.ctx, TEAM);
  return ED.meta.slot === 'beard' ? pals.beard : pals.base;
}
function drawCanvas() {
  const S = ED.size, Z = Math.floor(528 / S), W = S * Z, pal = palette();
  cv.width = cv.height = W; cv.style.width = cv.style.height = W + 'px';
  cx.fillStyle = '#efe5d1'; cx.fillRect(0, 0, W, W);
  // reszta twarzy przygaszona, bez edytowanej warstwy
  const s = samples(part())[0], ctxFace = PF.compose(s.person, { size: S, year: s.year, team: TEAM, skip: ED.meta.slot === 'hairback' ? 'hairback' : ED.meta.slot });
  cx.globalAlpha = .32;
  ctxFace.px.forEach((c, i) => { if (c) { cx.fillStyle = css(c); cx.fillRect((i % S) * Z, Math.floor(i / S) * Z, Z, Z); } });
  const paint = (x, y, ch, a) => {
    cx.globalAlpha = a;
    if (ch === '_') { cx.fillStyle = '#c23a1f'; for (let k = 0; k < Z; k += 4) cx.fillRect(x * Z + k, y * Z, 2, Z); return; }
    const c = pal[ch]; if (!c) return;
    cx.fillStyle = c.over ? `rgba(${c.over.join(',')},${Math.max(.45, c.a)})` : css(c); cx.fillRect(x * Z, y * Z, Z, Z);
  };
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const ch = ED.M[y][x]; if (ch === '.') continue;
    if (ED.meta.mirror && x >= S / 2) continue;
    paint(x, y, ch, 1);
    if (ED.meta.mirror) paint(S - 1 - x, y, ch, .55);
  }
  cx.globalAlpha = 1; cx.strokeStyle = 'rgba(35,23,16,.12)'; cx.lineWidth = 1;
  for (let k = 0; k <= S; k++) { cx.beginPath(); cx.moveTo(k * Z + .5, 0); cx.lineTo(k * Z + .5, W); cx.moveTo(0, k * Z + .5); cx.lineTo(W, k * Z + .5); cx.stroke(); }
  cx.strokeStyle = ED.meta.mirror ? '#e2572b' : 'rgba(35,23,16,.35)'; cx.lineWidth = 2;
  cx.beginPath(); cx.moveTo(W / 2, 0); cx.lineTo(W / 2, W); cx.stroke();
  if (ED.meta.mirror) { cx.fillStyle = 'rgba(239,229,209,.28)'; cx.fillRect(W / 2 + 1, 0, W / 2, W); }
}
function cell(e) {
  const r = cv.getBoundingClientRect(), S = ED.size;
  let x = Math.floor((e.clientX - r.left) / r.width * S), y = Math.floor((e.clientY - r.top) / r.height * S);
  if (ED.meta.mirror && x >= S / 2) x = S - 1 - x;
  return x >= 0 && y >= 0 && x < S && y < S ? [x, y] : null;
}
let painting = null, last = null;
/* linia między kolejnymi komórkami: szybki ruch myszy nie zostawia dziur */
const stroke = (a, c) => {
  const n = Math.max(Math.abs(c[0] - a[0]), Math.abs(c[1] - a[1]));
  for (let k = 0; k <= n; k++) { const t = n ? k / n : 0; ED.M[Math.round(a[1] + (c[1] - a[1]) * t)][Math.round(a[0] + (c[0] - a[0]) * t)] = painting; }
};
cv.addEventListener('contextmenu', e => e.preventDefault());
cv.addEventListener('pointerdown', e => {
  const c = cell(e); if (!c) return;
  ED.undo.push(ED.M.map(r => r.slice())); if (ED.undo.length > 60) ED.undo.shift();
  painting = e.button === 2 ? '.' : ED.role; last = c;
  stroke(c, c); drawCanvas(); cv.setPointerCapture(e.pointerId);
});
cv.addEventListener('pointermove', e => { if (!painting) return; const c = cell(e); if (c) { stroke(last, c); last = c; drawCanvas(); } });
cv.addEventListener('pointerup', () => { if (painting) { painting = null; commit(); } });

/* ---------- panele ---------- */
function drawParts() {
  const bySlot = lib().slots;
  $('#parts').innerHTML = SLOT_ORDER.filter(s => bySlot[s]).map(s => `<div class="grp"><span class="meta">${SLOT_NAMES[s]}</span>${
    bySlot[s].map(p => `<button data-id="${p.id}" class="${p.id === ED.id ? 'on' : ''}">${p.name}${p.era ? `<small>${p.era[0]}–${p.era[1] > 2030 ? '' : p.era[1]}</small>` : ''}</button>`).join('')}</div>`).join('');
}
function drawPalette() {
  const pal = palette();
  $('#pal').innerHTML = Object.entries(PF.ROLES).filter(([k]) => k !== '.' && (k !== '_' || ED.meta.slot === 'recede')).map(([k, name]) => {
    const c = pal[k], bg = k === '_' ? 'repeating-linear-gradient(90deg,#c23a1f 0 2px,transparent 2px 4px)' : c ? css(c) : 'transparent';
    return `<button data-r="${k}" class="${k === ED.role ? 'on' : ''}" title="${name}"><i style="background:${bg}"></i><b>${k}</b><span>${name}</span></button>`;
  }).join('');
}
function drawPreview() {
  const S = ED.size, l = lib(), c = l.crops.head, list = samples(part());
  $('#prev').innerHTML = list.map(s => PF.svg(s.person, { size: S, year: s.year, team: TEAM, px: S * Math.max(1, Math.round(84 / S)) })).join('');
  $('#prev-row').innerHTML = list.map(s => PF.svg(s.person, { size: S, year: s.year, team: TEAM, crop: 'head', px: c[2] * Math.max(1, Math.round(36 / c[2])) })).join('');
}
function drawText() {
  $('#part-id').textContent = ED.id;
  $('#tags').value = ED.meta.tags; $('#mirror').checked = ED.meta.mirror; $('#flip').checked = ED.meta.flip;
  if (document.activeElement !== $('#txt')) $('#txt').value = block().join('\n');
  $('#errs').textContent = lib().errors.join('\n');
  const changed = lib().source !== ORIG[ED.size];
  $('#draft-st').textContent = changed ? 'Zmieniony, zapisany w przeglądarce' : 'Bez zmian';
  $('#draft-st').className = changed ? 'warn' : '';
}
function drawAll(reloadPart = true) {
  if (!part()) ED.id = lib().parts.find(p => p.slot === 'hair')?.id || lib().parts[0].id;
  if (reloadPart) load();
  document.querySelectorAll('#size button').forEach(b => b.classList.toggle('on', +b.dataset.v === ED.size));
  drawParts(); drawPalette(); drawCanvas(); drawPreview(); drawText(); saveUi();
}

/* ---------- zdarzenia ---------- */
$('#size').addEventListener('click', e => { const b = e.target.closest('button'); if (b) { ED.size = +b.dataset.v; drawAll(); } });
$('#parts').addEventListener('click', e => { const b = e.target.closest('button'); if (b) { ED.id = b.dataset.id; drawAll(); } });
$('#pal').addEventListener('click', e => { const b = e.target.closest('button'); if (b) { ED.role = b.dataset.r; drawPalette(); saveUi(); } });
$('#tags').addEventListener('change', e => { ED.meta.tags = e.target.value.trim(); commit(); });
$('#mirror').addEventListener('change', e => {
  const S = ED.size;
  if (!e.target.checked) for (let y = 0; y < S; y++) for (let x = 0; x < S / 2; x++) if (ED.M[y][x] !== '.') ED.M[y][S - 1 - x] = ED.M[y][x];
  ED.meta.mirror = e.target.checked; commit();
});
$('#flip').addEventListener('change', e => { ED.meta.flip = e.target.checked; commit(); });
let typing;
$('#txt').addEventListener('input', e => {
  clearTimeout(typing);
  typing = setTimeout(() => {
    const lines = e.target.value.split('\n').map(l => l.trim()).filter(Boolean);
    if (!lines[0] || !lines[0].startsWith('== ')) return;
    replaceBlock(part(), lines);
    ED.id = lines[0].slice(3).split(/\s+/)[0];
    if (part()) drawAll(); else $('#errs').textContent = lib().errors.join('\n');
  }, 350);
});
$('#copy').addEventListener('click', () => navigator.clipboard?.writeText(block().join('\n')).then(() => flash('#copy', 'Skopiowano')));
$('#dl').addEventListener('click', () => {
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([`PF.library(String.raw\`${lib().source}\`);\n`], { type: 'text/javascript' }));
  a.download = `lib${ED.size}.js`; a.click(); URL.revokeObjectURL(a.href);
});
$('#reset').addEventListener('click', () => {
  if (!confirm(`Odrzucić wszystkie zmiany w ${ED.size} × ${ED.size}?`)) return;
  try { localStorage.removeItem(PF.DRAFT_KEY(ED.size)); } catch (e) {}
  PF.library(ORIG[ED.size]); drawAll();
});
const askName = (msg, def) => { const v = prompt(msg, def); return v && /^[a-z]+\/[a-z0-9]+$/.test(v.trim()) ? v.trim() : null; };
$('#new').addEventListener('click', () => {
  const id = askName('Nowa część (slot/nazwa, np. hair/quiff):', `${ED.meta.slot}/nowa`); if (!id) return;
  const [slot, name] = id.split('/');
  ED.meta = { slot, name, mirror: true, flip: false, tags: '' }; ED.M = ED.M.map(r => r.fill('.'));
  replaceBlock(null, [`== ${id} mirror`]); ED.id = id; drawAll();
});
$('#dup').addEventListener('click', () => {
  const id = askName('Kopia jako (slot/nazwa):', `${ED.meta.slot}/${ED.meta.name}2`); if (!id) return;
  const [slot, name] = id.split('/'); ED.meta = { ...ED.meta, slot, name };
  replaceBlock(null, block()); ED.id = id; drawAll();
});
$('#del').addEventListener('click', () => {
  if (!confirm(`Usunąć ${ED.id}?`)) return;
  const p = part(), src = lib().source.split('\n');
  src.splice(p.line - 1, 1 + p.rows.length + (src[p.line + p.rows.length - 1] === '' ? 1 : 0));
  reload(src.join('\n')); ED.id = null; drawAll();
});
document.addEventListener('keydown', e => {
  if (e.target.closest('input,textarea')) return;
  if ((e.ctrlKey || e.metaKey) && e.key === 'z') { e.preventDefault(); if (ED.undo.length) { ED.M = ED.undo.pop(); commit(); } return; }
  if (e.key.length === 1 && e.key in PF.ROLES && e.key !== '.') { ED.role = e.key; drawPalette(); saveUi(); }
});
function flash(sel, text) { const b = $(sel), t = b.textContent; b.textContent = text; setTimeout(() => b.textContent = t, 1200); }

drawAll();
})();
