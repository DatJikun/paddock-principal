/* Test biblioteki twarzy: node ui/prototype/js/faces/check.js
   Bez zależności. Kończy się kodem 1, jeśli którykolwiek warunek nie jest spełniony. */
const vm = require('vm'), fs = require('fs'), path = require('path');
const DIR = __dirname, SIZES = [16, 24, 32];

function load() {
  const ctx = { console: { warn() {}, log: console.log } };
  ctx.window = ctx;
  vm.createContext(ctx);
  for (const f of ['pixelfaces.js', ...SIZES.map(s => `lib${s}.js`), 'people.js']) vm.runInContext(fs.readFileSync(path.join(DIR, f), 'utf8'), ctx, { filename: f });
  return ctx;
}

const fails = [];
const check = (ok, msg) => { if (!ok) fails.push(msg); };
const { PF, FACES } = load();

/* osoby testowe: przekrój narodowości, ról i wieku */
const NATS = ['GBR', 'ITA', 'FRA', 'BRA', 'GER', 'SWE', 'ARG', 'USA', 'JPN', 'RSA', 'AUT', 'FIN', 'ESP', 'IND', 'NGA', 'POL'];
const people = (n, tag) => Array.from({ length: n }, (_, i) => ({
  id: `${tag}-${i}`, nat: NATS[i % NATS.length], role: ['driver', 'driver', 'staff', 'mechanic'][i % 4], age: 19 + (i * 7) % 55,
}));

/* 1. biblioteki bez błędów i z kompletem slotów */
const REQUIRED = ['neck', 'head', 'ears', 'eyes', 'brows', 'nose', 'mouth', 'hair', 'beard', 'body', 'glasses', 'recede', 'age'];
for (const s of SIZES) {
  const lib = PF.libs[s];
  check(lib && lib.size === s, `lib${s}: brak biblioteki`);
  if (!lib) continue;
  lib.errors.forEach(e => check(false, `lib${s}: ${e}`));
  REQUIRED.forEach(slot => check(lib.slots[slot]?.length, `lib${s}: brak slotu ${slot}`));
  ['1', '2', '3'].forEach(n => check(lib.slots.recede?.some(p => p.name === n), `lib${s}: brak recede/${n}`));
  /* 2. części lustrzane, które tworzą sylwetkę, muszą dochodzić do osi (inaczej szpara na środku) */
  for (const p of lib.parts) {
    if (p.mirror && ['head', 'neck', 'body', 'hair'].includes(p.slot)) check(p.x + p.w === s / 2, `lib${s}: ${p.id} nie dochodzi do osi (${p.x + p.w} ≠ ${s / 2})`);
  }
}

/* 3. te same nazwy części zależnych od epoki w każdej gęstości: przełączenie gęstości nie zmienia stylu osoby */
for (const slot of ['hair', 'beard', 'body', 'glasses', 'head', 'recede']) {
  const names = SIZES.map(s => (PF.libs[s].slots[slot] || []).map(p => p.name).sort().join(','));
  check(names.every(n => n === names[0]), `slot ${slot}: różne nazwy części między gęstościami: ${names.join(' | ')}`);
}

/* 4. wskazówki wyglądu prawdziwych osób wskazują na istniejące części */
for (const [id, look] of Object.entries(FACES.LOOKS)) {
  for (const [key, slot] of [['hairStyle', 'hair'], ['beard', 'beard']]) {
    if (look[key]) SIZES.forEach(s => check(PF.libs[s].slots[slot].some(p => p.name === look[key]), `LOOKS.${id}.${key}: brak ${slot}/${look[key]} w lib${s}`));
  }
}

/* 5. determinizm: dwa niezależne załadowania dają identyczne SVG */
{
  const other = load().PF;
  for (const s of SIZES) for (const p of people(150, 'det')) for (const year of [1955, 1976, 2015]) {
    const opts = { size: s, year, team: { t1: '#1f4f9a', t2: '#e03a3e' } };
    check(PF.svg(p, opts) === other.svg(p, opts), `determinizm: ${p.id} ${s}px ${year}`);
  }
}

/* 6. klony: wśród 100 losowych osób z jednej epoki najwyżej 1 powtórzony wygląd */
const cloneReport = [];
for (const s of SIZES) for (const year of [1955, 1976, 1995, 2020]) {
  const seen = new Map();
  for (const p of people(100, `clone${year}`)) { const sig = PF.signature(p, { size: s, year }); seen.set(sig, (seen.get(sig) || 0) + 1); }
  const dup = [...seen.values()].filter(v => v > 1).reduce((a, v) => a + v - 1, 0);
  cloneReport.push(`${s}px ${year}: ${dup}`);
  check(dup <= 1, `klony: ${dup} powtórzeń wśród 100 osób (${s}px, ${year})`);
}

/* 7. każda część jest osiągalna: pojawia się u kogoś w przekroju lat 1950–2026 */
for (const s of SIZES) {
  const used = new Set();
  for (let year = 1950; year <= 2026; year += 4) for (const p of people(160, `use${year}`)) {
    const f = PF.compose(p, { size: s, year });
    Object.values(f.sel).flat().forEach(part => part && used.add(part.id));
  }
  PF.libs[s].parts.forEach(p => check(used.has(p.id), `lib${s}: część ${p.id} nigdy nie jest wybierana`));
}

/* 8. tożsamość przeżywa starzenie: kształt twarzy, oczy, nos i usta nie zmieniają się z wiekiem */
for (const s of SIZES) for (const p of people(60, 'aging')) {
  const a = PF.compose({ ...p, age: 22 }, { size: s, year: 1970 }), b = PF.compose({ ...p, age: 64 }, { size: s, year: 2012 });
  for (const slot of ['head', 'eyes', 'brows', 'nose', 'mouth', 'ears']) check(a.sel[slot] === b.sel[slot], `starzenie: ${p.id} zmienia ${slot} (${s}px)`);
  check(a.id.skinI === b.id.skinI && a.id.hair === b.id.hair, `starzenie: ${p.id} zmienia karnację lub kolor włosów`);
}

console.log(`klony na 100 osób: ${cloneReport.join(' · ')}`);
if (fails.length) { console.log(`\nBŁĘDY (${fails.length}):\n` + [...new Set(fails)].slice(0, 60).join('\n')); process.exit(1); }
console.log('OK: biblioteki 16/24/32, osie, nazwy, determinizm, klony, osiągalność, starzenie');
