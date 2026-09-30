/* Twarze w prototypie: łączy osoby z data.js z PixelFaces.
   Gęstość wybiera się w Ustawieniach (PREF.faces); „off” = inicjały. */
(() => {
/* barwy kombinezonów 1976: przybliżone, z pamięci, do sprawdzenia przy danych zespołów */
const TEAM_COLORS = {
  ferrari: ['#c4161c', '#f5c518'], tyrrell: ['#1f4f9a', '#e03a3e'], lotus: ['#16130e', '#c9a24a'],
  brabham: ['#f2efe8', '#c4161c'], march: ['#d9462b', '#f2efe8'], mclaren: ['#f2efe8', '#d0202a'],
  shadow: ['#1b1b1b', '#f2efe8'], surtees: ['#f2efe8', '#1d5fa8'], williams: ['#2a3e7a', '#d9b44a'],
  ensign: ['#0f5c9a', '#f2efe8'], ligier: ['#1c63b8', '#f2efe8'], penske: ['#f2efe8', '#1f3f8f'],
  copersucar: ['#c9c4b5', '#e8c21a'],
};
/* kombinezony kierowców spoza F1 (juniorzy, pula talentów): losowane z ziarna */
const PLAIN = [['#3b6e4f', '#f2efe8'], ['#7a2e2e', '#f2efe8'], ['#2e4a7a', '#e8c21a'], ['#d8d2c4', '#2e4a7a'], ['#5a4a8a', '#f2efe8']];

/* prawdziwi ludzie: tylko cechy „w duchu” (kolor włosów, zarost, fryzura epoki), nigdy portret.
   Wygląd z pamięci, do weryfikacji; brak wpisu = twarz w pełni z ziarna. */
const LOOKS = {
  lauda: { hair: 'brown', hairStyle: 'mop' }, regazzoni: { hair: 'dark', beard: 'moustache' }, scheckter: { hair: 'brown', beard: 'sideburns' },
  depailler: { hair: 'dark', tex: 'curly' }, andretti: { hair: 'dark' }, nilsson: { hair: 'blond' },
  reutemann: { hair: 'dark' }, pace: { hair: 'black', beard: 'moustache' }, brambilla: { hair: 'dark' },
  peterson: { hair: 'dblond' }, hunt: { hair: 'blond', hairStyle: 'long' }, mass: { hair: 'dark', beard: 'moustache' },
  pryce: { hair: 'dark' }, jarier: { hair: 'dark' }, jones: { hair: 'brown' }, ickx: { hair: 'brown' },
  merzario: { hair: 'dark' }, amon: { hair: 'brown' }, laffite: { hair: 'dark', tex: 'curly' },
  watson: { hair: 'brown', beard: 'full' }, fittipaldi: { hair: 'dark', beard: 'sideburns', hairStyle: 'long' },
  stuck: { hair: 'dblond', hairStyle: 'long' }, rosberg: { hair: 'dblond', beard: 'moustache' },
  prost: { hair: 'dark', tex: 'curly' }, villeneuve: { hair: 'dark' }, piquet: { hair: 'dark' },
  tyrrell_ken: { hair: 'brown', hairStyle: 'crew' },
};

let index = null;
function build() {
  index = new Map();
  const teamKey = (name) => Object.keys(DB.teams).find(k => DB.teams[k].name === name) || null;
  const add = (p) => {
    const colors = TEAM_COLORS[p.team] || PLAIN[PF.hash(p.id) % PLAIN.length];
    const person = { ...p, look: LOOKS[p.id], colors: { t1: colors[0], t2: colors[1] } };
    index.set(p.id, person); index.set(p.name, person);
  };
  DB.grid.forEach(g => add({ id: g[1], name: g[2], nat: g[3], age: g[5], team: g[4], role: 'driver' }));
  DB.drivers.forEach(d => add({ id: d.id, name: d.name, nat: d.nat, age: d.age, team: 'tyrrell', role: 'driver' }));
  DB.market.forEach(m => index.has(m.id) || add({ id: m.id, name: m.name, nat: m.nat, age: m.age, team: teamKey(m.team), role: 'driver' }));
  DB.academy.juniors.forEach(j => add({ id: j.id, name: j.name, nat: j.nat, age: j.age, role: 'driver' }));
  DB.academy.pool.forEach(j => index.has(j.id) || add({ id: j.id, name: j.name, nat: j.nat, age: j.age, role: 'driver' }));
  DB.staff.forEach(s => add({ id: s.id, name: s.name, nat: s.nat, age: s.age, team: 'tyrrell', role: /mechanik/i.test(s.role) ? 'mechanic' : 'staff' }));
  const o = DB.board.owner; add({ id: 'tyrrell_ken', name: o.name, nat: o.nat, age: o.age, team: 'tyrrell', role: 'staff' });
  const m = DB.manager; add({ id: 'manager', name: m.name, nat: m.nat, age: m.age, team: 'tyrrell', role: 'staff' });
}

/* rozmiary w interfejsie: piksel gry zawsze = całkowita liczba pikseli ekranu */
const KINDS = { row: ['head', 36], big: ['head', 48], card: ['full', 84], profile: ['full', 136], hero: ['full', 160] };

window.FACES = {
  find(who) { if (!index) build(); return typeof who === 'string' ? index.get(who) || null : who; },
  density() { const d = (typeof PREF !== 'undefined' && PREF.faces) || '24'; return d === 'off' ? null : +d; },
  /* SVG twarzy albo null (inicjały zostają) */
  html(who, kind = 'row', year = 1976) {
    const p = FACES.find(who), size = FACES.density();
    if (!p || !size || !PF.libs[size]) return null;
    const [crop, target] = KINDS[kind], c = PF.libs[size].crops[crop];
    const px = c[2] * Math.max(1, Math.round(target / c[2]));
    return PF.svg(p, { size, year, team: p.colors, crop, px });
  },
  TEAM_COLORS, LOOKS,
};
})();
