/* Arkusz oceny twarzy: porównanie gęstości, 100 losowych osób, starzenie, epoki, zespoły, rozmiary. */
(() => {
const drafts = PF.useDrafts();
const ST = { size: 24, year: 1976, seed: 1 };
try { Object.assign(ST, JSON.parse(localStorage.getItem('pp-faces') || '{}')); } catch (e) {}
const save = () => { try { localStorage.setItem('pp-faces', JSON.stringify(ST)); } catch (e) {} };
const $ = (s) => document.querySelector(s);
const TEAMS = Object.entries(FACES.TEAM_COLORS).map(([k, [t1, t2]]) => ({ k, t1, t2 }));

/* rozmiar w pikselach ekranu: najbliższa całkowita wielokrotność siatki */
const fit = (size, crop, target) => { const c = PF.libs[size].crops[crop][2]; return c * Math.max(1, Math.round(target / c)); };
const face = (p, o = {}) => {
  const size = o.size || ST.size, crop = o.crop || 'full';
  return PF.svg(p, { size, year: o.year || ST.year, team: o.team || p.colors, crop, px: fit(size, crop, o.target || 96) });
};
const card = (inner, cap, cls = '') => `<figure class="f ${cls}">${inner}${cap ? `<figcaption>${cap}</figcaption>` : ''}</figure>`;

/* losowe osoby: narodowości ważone jak w stawce danej epoki (przybliżenie) */
const NATS = {
  1950: 'GBR GBR ITA ITA ITA FRA FRA ARG USA GER BEL SUI', 1970: 'GBR GBR GBR ITA FRA FRA BRA SWE USA GER AUT BEL SUI AUS NZL RSA ARG',
  1990: 'GBR GBR ITA ITA FRA FRA BRA GER AUT BEL JPN FIN SWE USA', 2010: 'GBR GBR GER GER ESP FIN FRA ITA BRA AUS MEX JPN NED RUS CAN THA',
};
const natsFor = (y) => NATS[Object.keys(NATS).reverse().find(k => y >= +k)].split(' ');
const randomPeople = (n, seed, year) => {
  let a = seed * 9301 + 49297;
  const r = () => (a = (a * 1103515245 + 12345) % 2147483648) / 2147483648;
  const nats = natsFor(year);
  return Array.from({ length: n }, (_, i) => {
    const staff = r() < .22, t = TEAMS[Math.floor(r() * TEAMS.length)];
    return { id: `s${seed}-${i}`, nat: nats[Math.floor(r() * nats.length)], role: staff ? (r() < .35 ? 'mechanic' : 'staff') : 'driver',
      age: staff ? 28 + Math.floor(r() * 38) : 19 + Math.floor(r() * 20), colors: { t1: t.t1, t2: t.t2 } };
  });
};

function drawCompare() {
  const ids = ['scheckter', 'depailler', 'hunt', 'lauda', 'regazzoni', 'fittipaldi', 'watson', 'peterson', 'gardner', 'hill', 'tyrrell_ken', 'manager'];
  $('#cmp-grid').innerHTML = `<div class="cmp-h"></div>${[16, 24, 32].map(s => `<div class="cmp-h meta">${s} × ${s}</div>`).join('')}` +
    ids.map(id => {
      const p = FACES.find(id);
      return `<div class="who"><b>${p.name}</b><span class="num">${p.age}</span></div>` + [16, 24, 32].map(size =>
        `<div class="pair">${face(p, { size, year: 1976, crop: 'head', target: 36 })}${face(p, { size, year: 1976, target: 96 })}</div>`).join('');
    }).join('');
}

function drawSheet() {
  const people = randomPeople(100, ST.seed, ST.year);
  const sig = new Map();
  people.forEach(p => { const s = PF.signature(p, { size: ST.size, year: ST.year }); sig.set(s, (sig.get(s) || 0) + 1); });
  const clones = [...sig.values()].filter(v => v > 1).reduce((a, v) => a + v, 0);
  const hairs = new Set(people.map(p => PF.compose(p, { size: ST.size, year: ST.year }).sel.hair?.name)).size;
  $('#sheet-stats').innerHTML = [['Klony', clones, clones ? 'bad' : 'good'], ['Fryzury', hairs], ['Gęstość', `${ST.size} px`], ['Rok', ST.year]]
    .map(([k, v, c]) => `<div class="fld"><span class="meta">${k}</span><span class="v num ${c || ''}">${v}</span></div>`).join('');
  $('#sheet').innerHTML = people.map(p => card(face(p, { target: 96 }), `${p.age}`)).join('');
}

function drawAging() {
  const who = [
    { id: 'age-a', nat: 'GBR', born: 1935, role: 'driver', colors: { t1: '#1f4f9a', t2: '#e03a3e' } },
    { id: 'age-b', nat: 'ITA', born: 1948, role: 'driver', colors: { t1: '#c4161c', t2: '#f5c518' } },
    { id: 'age-c', nat: 'BRA', born: 1952, role: 'staff', colors: { t1: '#16130e', t2: '#c9a24a' } },
    { id: 'age-d', nat: 'SWE', born: 1944, role: 'staff', colors: { t1: '#f2efe8', t2: '#d0202a' } },
  ];
  const ages = [20, 27, 34, 41, 48, 55, 62, 69, 76];
  $('#aging').innerHTML = who.map(p => `<div class="row">${ages.map(age => {
    const y = p.born + age, q = { ...p, age, role: age < 38 ? 'driver' : 'staff' };
    return card(face(q, { year: y, target: 96 }), `${age} · ${y}`);
  }).join('')}</div>`).join('');
}

function drawEras() {
  const who = ['era-a', 'era-b', 'era-c', 'era-d'].map((id, i) => ({ id, nat: ['GBR', 'FRA', 'GER', 'ARG'][i], age: 29, role: 'driver', colors: { t1: TEAMS[i * 3].t1, t2: TEAMS[i * 3].t2 } }));
  const years = [1952, 1960, 1968, 1976, 1984, 1992, 2000, 2010, 2022];
  $('#eras').innerHTML = who.map(p => `<div class="row">${years.map(y => card(face(p, { year: y, target: 96 }), `${y}`)).join('')}</div>`).join('');
}

function drawTeams() {
  const p = FACES.find('scheckter');
  $('#teams').innerHTML = TEAMS.map(t => card(face(p, { team: t, year: 1976, target: 72 }), DB.teams[t.k].name)).join('');
  const roles = [['staff', 58, 'Szef'], ['staff', 44, 'Projektant'], ['mechanic', 38, 'Mechanik'], ['staff', 35, 'Inżynier'], ['mechanic', 29, 'Mechanik']];
  $('#staff').innerHTML = roles.map(([role, age, cap], i) => card(face({ id: 'staff-' + i, nat: i % 2 ? 'GBR' : 'ITA', role, age, colors: { t1: '#1f4f9a', t2: '#e03a3e' } }, { target: 72 }), cap)).join('');
}

function drawSizes() {
  const ids = ['scheckter', 'hunt', 'regazzoni', 'gardner', 'tyrrell_ken'];
  const kinds = [['head', 36, 'Wiersz tabeli'], ['head', 48, 'Skrzynka'], ['full', 84, 'Karta'], ['full', 160, 'Profil']];
  $('#sizes').innerHTML = kinds.map(([crop, target, cap]) => `<div class="size"><span class="meta">${cap} · ${fit(ST.size, crop, target)} px</span><div class="row">${
    ids.map(id => { const p = FACES.find(id); return face(p, { crop, target, year: 1976 }); }).join('')}</div></div>`).join('');
}

function draw() {
  document.querySelectorAll('.seg[data-k="size"] button').forEach(b => b.classList.toggle('on', +b.dataset.v === ST.size));
  $('#year').value = ST.year; $('#year-v').textContent = ST.year;
  drawSheet(); drawAging(); drawEras(); drawTeams(); drawSizes();
  save();
}
document.addEventListener('click', e => {
  const b = e.target.closest('.seg button'); if (b) { ST.size = +b.dataset.v; draw(); }
});
$('#year').addEventListener('input', e => { ST.year = +e.target.value; draw(); });
$('#reroll').addEventListener('click', () => { ST.seed++; draw(); });
if (drafts.length) { $('#draft').hidden = false; $('#draft-v').textContent = drafts.map(s => `${s} px`).join(', '); }
drawCompare();
draw();
})();
