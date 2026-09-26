/* Dane przykładowe: alternatywny sezon 1976, gracz prowadzi Tyrrella.
   Wszystkie liczby to atrapa do prototypu wyglądu, nie wynik symulacji.
   Długości torów: data/authored/tracks (R2). Przepisy: data/authored/regulations (R1).
   Wyniki rund 1–8: czołowa szóstka ustalona ręcznie, reszta stawki losowana z ziarnem;
   klasyfikacje są liczone z wyników, więc zawsze się zgadzają. */
(() => {
const rng = (seed) => () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };

/* ---------- stawka 1976 ---------- */
const TEAMS = {
  ferrari:{name:'Ferrari', full:'Ferrari', engine:'Ferrari'}, tyrrell:{name:'Tyrrell', full:'Tyrrell-Ford', mine:true}, lotus:{name:'Lotus', full:'Lotus-Ford'},
  brabham:{name:'Brabham', full:'Brabham-Alfa Romeo'}, march:{name:'March', full:'March-Ford'}, mclaren:{name:'McLaren', full:'McLaren-Ford'},
  shadow:{name:'Shadow', full:'Shadow-Ford'}, surtees:{name:'Surtees', full:'Surtees-Ford'}, williams:{name:'Williams', full:'Wolf-Williams-Ford'},
  ensign:{name:'Ensign', full:'Ensign-Ford'}, ligier:{name:'Ligier', full:'Ligier-Matra'}, penske:{name:'Penske', full:'Penske-Ford'}, copersucar:{name:'Copersucar', full:'Copersucar-Ford'},
};
/* nr, id, imię i nazwisko, kraj, zespół, wiek, siła (do losowania kolejności) */
const GRID = [
  [1,'lauda','Niki Lauda','AUT','ferrari',27,98],[2,'regazzoni','Clay Regazzoni','SUI','ferrari',36,86],[3,'scheckter','Jody Scheckter','RSA','tyrrell',26,90],[4,'depailler','Patrick Depailler','FRA','tyrrell',31,86],
  [5,'andretti','Mario Andretti','USA','lotus',36,84],[6,'nilsson','Gunnar Nilsson','SWE','lotus',27,74],[7,'reutemann','Carlos Reutemann','ARG','brabham',34,80],[8,'pace','Carlos Pace','BRA','brabham',31,77],
  [9,'brambilla','Vittorio Brambilla','ITA','march',38,70],[10,'peterson','Ronnie Peterson','SWE','march',32,85],[11,'hunt','James Hunt','GBR','mclaren',28,95],[12,'mass','Jochen Mass','GER','mclaren',30,79],
  [16,'pryce','Tom Pryce','GBR','shadow',26,74],[17,'jarier','Jean-Pierre Jarier','FRA','shadow',29,72],[18,'lunger','Brett Lunger','USA','surtees',30,52],[19,'jones','Alan Jones','AUS','surtees',29,70],
  [20,'ickx','Jacky Ickx','BEL','williams',31,66],[21,'merzario','Arturo Merzario','ITA','williams',33,58],[22,'amon','Chris Amon','NZL','ensign',32,68],[26,'laffite','Jacques Laffite','FRA','ligier',32,80],
  [28,'watson','John Watson','GBR','penske',30,79],[30,'fittipaldi','Emerson Fittipaldi','BRA','copersucar',29,72],[34,'stuck','Hans-Joachim Stuck','GER','march',25,73],
];
const byNo = Object.fromEntries(GRID.map(g => [g[0], g]));
const byId = Object.fromEntries(GRID.map(g => [g[1], g]));

/* ---------- tory i kalendarz ----------
   Sylwetki torów narysowane ręcznie z pamięci: przybliżone, do zastąpienia prawdziwymi obrysami. */
const TRACKS = {
  interlagos:{name:'Interlagos', len:7.96, profile:{straights:.25,high_speed:.35,low_speed:.15,braking:.25}, tags:['Wyboisty','Długi'],
    map:[[80,60],[82,45],[82,30],[80,18],[72,10],[60,7],[48,8],[38,12],[30,18],[24,26],[20,36],[18,48],[19,57],[24,63],[32,64],[38,60],[40,53],[37,47],[42,41],[49,42],[53,47],[59,47],[63,41],[60,35],[65,30],[71,34],[73,43],[75,53]]},
  kyalami:{name:'Kyalami', len:4.104, profile:{straights:.3,high_speed:.35,low_speed:.15,braking:.2}, tags:['Szybki','Wysokość n.p.m.'],
    map:[[15,15],[45,13],[72,12],[80,13],[83,19],[80,25],[76,30],[78,36],[74,42],[67,45],[59,46],[52,44],[45,45],[40,49],[34,48],[30,44],[25,46],[19,45],[15,40],[14,32],[13,24]]},
  long_beach:{name:'Long Beach', len:3.251, profile:{straights:.15,high_speed:.15,low_speed:.35,braking:.35}, tags:['Uliczny','Techniczny'],
    map:[[80,50],[62,54],[42,55],[24,52],[15,50],[12,44],[18,41],[26,41],[30,36],[30,28],[31,20],[36,15],[46,14],[58,14],[67,15],[72,19],[74,25],[79,30],[83,37],[84,44]]},
  jarama:{name:'Jarama', len:3.404, profile:{straights:.15,high_speed:.2,low_speed:.35,braking:.3}, tags:['Techniczny','Wyboisty'],
    map:[[15,50],[30,50],[45,50],[52,48],[55,43],[52,37],[55,32],[61,29],[67,30],[71,34],[75,38],[79,35],[78,29],[73,24],[65,21],[57,18],[50,19],[45,23],[39,21],[31,18],[23,20],[16,24],[14,31],[16,38],[12,44]]},
  zolder:{name:'Zolder', len:4.262, profile:{straights:.18,high_speed:.28,low_speed:.28,braking:.26}, tags:['Techniczny'],
    map:[[60,52],[40,52],[20,52],[12,50],[10,44],[14,40],[22,38],[27,34],[33,33],[39,30],[45,30],[48,27],[51,30],[54,27],[60,24],[66,22],[70,24],[73,21],[80,22],[86,26],[90,32],[89,38],[84,41],[77,42],[71,46],[66,50]]},
  monaco:{name:'Monte Carlo', len:3.312, profile:{straights:.08,high_speed:.1,low_speed:.45,braking:.37}, tags:['Uliczny','Techniczny'],
    map:[[25,40],[35,38],[45,36],[50,36],[52,32],[56,25],[60,19],[64,15],[68,15],[70,18],[73,22],[76,21],[79,18],[82,18],[83,21],[80,24],[81,28],[84,31],[80,36],[72,40],[64,44],[60,46],[57,45],[54,47],[48,48],[44,50],[40,51],[37,49],[33,50],[29,52],[24,52],[21,49],[22,44]]},
  anderstorp:{name:'Anderstorp', len:4.018, profile:{straights:.3,high_speed:.4,low_speed:.1,braking:.2}, tags:['Szybki','Płynny'],
    map:[[30,55],[45,55],[58,55],[68,53],[74,47],[75,39],[72,31],[70,22],[66,15],[60,14],[56,18],[48,28],[38,37],[26,45],[19,49],[20,54]]},
  ricard:{name:'Paul Ricard', len:5.81, profile:{straights:.35,high_speed:.35,low_speed:.1,braking:.2}, tags:['Szybki','Płynny'],
    map:[[88,20],[93,22],[94,28],[91,34],[86,38],[80,39],[75,42],[71,40],[66,43],[62,46],[57,45],[53,48],[48,47],[44,50],[35,51],[22,51],[12,50],[7,47],[8,42],[12,39],[10,34],[13,30],[17,27],[22,23],[30,20],[50,19],[70,19]]},
  brands_hatch:{name:'Brands Hatch', len:4.206, profile:{straights:.15,high_speed:.35,low_speed:.25,braking:.25}, tags:['Techniczny','Płynny'],
    map:[[38,14],[50,13],[60,12],[66,13],[70,17],[73,22],[77,24],[80,27],[78,31],[73,30],[69,31],[65,34],[58,35],[50,35],[44,36],[40,38],[34,42],[28,46],[21,49],[15,49],[12,46],[10,40],[9,34],[11,29],[15,26],[18,22],[22,20],[24,15],[27,10],[31,9],[35,11]]},
  nurburgring:{name:'Nürburgring', len:22.835, profile:{straights:.25,high_speed:.35,low_speed:.2,braking:.2}, tags:['Długi','Wyboisty','Niebezpieczny'],
    map:[[84,56],[74,57],[66,56],[60,54],[57,50],[53,51],[49,48],[45,50],[41,50],[37,48],[33,49],[31,53],[28,58],[24,60],[20,58],[17,60],[13,58],[10,54],[7,55],[5,51],[6,46],[9,43],[8,38],[11,34],[15,30],[20,26],[25,22],[29,21],[32,17],[35,14],[38,12],[40,15],[43,11],[47,9],[51,11],[55,9],[58,12],[62,11],[66,14],[69,12],[73,15],[75,19],[78,23],[82,27],[86,34],[88,42],[88,50]]},
  osterreichring:{name:'Österreichring', len:5.911, profile:{straights:.25,high_speed:.45,low_speed:.1,braking:.2}, tags:['Szybki','Niebezpieczny'],
    map:[[20,55],[32,45],[44,35],[52,26],[56,18],[61,14],[67,14],[75,16],[83,20],[88,26],[90,34],[86,42],[80,48],[75,53],[72,58],[65,61],[54,61],[42,62],[30,62],[22,60]]},
  zandvoort:{name:'Zandvoort', len:4.226, profile:{straights:.2,high_speed:.35,low_speed:.2,braking:.25}, tags:['Techniczny','Wyboisty'],
    map:[[80,55],[80,30],[80,15],[80,8],[76,4],[72,5],[71,10],[70,15],[66,18],[60,19],[56,16],[54,11],[48,11],[42,12],[38,16],[34,22],[30,25],[26,31],[24,38],[22,44],[25,50],[31,54],[41,56],[53,57],[64,59],[74,60],[79,58]]},
  monza:{name:'Monza', len:5.8, profile:{straights:.4,high_speed:.25,low_speed:.1,braking:.25}, tags:['Szybki'],
    map:[[15,55],[15,35],[15,15],[15,12],[17,10],[15,8],[18,5],[26,3],[34,4],[40,5],[42,7],[44,5],[52,6],[56,9],[58,14],[58,18],[57,26],[55,34],[54,39],[56,42],[54,45],[51,49],[48,55],[45,62],[40,66],[31,68],[23,66],[17,62]]},
  mosport:{name:'Mosport', len:3.957, profile:{straights:.2,high_speed:.4,low_speed:.15,braking:.25}, tags:['Szybki','Wyboisty'],
    map:[[15,22],[30,21],[40,22],[44,27],[44,34],[48,39],[55,41],[60,46],[66,50],[72,50],[78,47],[79,41],[78,33],[78,24],[77,15],[72,9],[64,8],[54,10],[46,9],[34,9],[22,10],[13,13],[11,18]]},
  watkins_glen:{name:'Watkins Glen', len:5.435, profile:{straights:.25,high_speed:.35,low_speed:.18,braking:.22}, tags:['Techniczny','Długi'],
    map:[[22,12],[45,11],[68,10],[77,13],[79,19],[76,25],[80,31],[77,37],[70,43],[60,48],[50,52],[43,56],[37,61],[30,64],[23,63],[19,58],[21,52],[17,47],[12,42],[11,34],[13,25],[16,17]]},
  fuji:{name:'Fuji', len:4.359, profile:{straights:.35,high_speed:.35,low_speed:.1,braking:.2}, tags:['Szybki'],
    map:[[10,20],[40,19],[70,18],[80,18],[86,22],[86,28],[80,34],[72,38],[64,40],[58,44],[51,48],[45,50],[40,47],[32,44],[24,42],[16,40],[8,36],[5,30],[6,24]]},
};
/* runda: data, dzień tyg., GP, kraj (flaga), tor, okrążenia, czas zwycięzcy */
const CAL = [
  ['25 stycznia','BRA','GP Brazylii','interlagos',40,'1:45:16,8'],['6 marca','RSA','GP RPA','kyalami',78,'1:42:18,4'],['28 marca','USA','GP USA Zachód','long_beach',80,'1:53:18,5'],
  ['2 maja','ESP','GP Hiszpanii','jarama',75,'1:42:20,4'],['16 maja','BEL','GP Belgii','zolder',70,'1:42:53,2'],['30 maja','MON','GP Monako','monaco',78,'1:59:51,5'],
  ['13 czerwca','SWE','GP Szwecji','anderstorp',72,'1:46:53,7'],['4 lipca','FRA','GP Francji','ricard',54,'1:40:58,6'],['18 lipca','GBR','GP Wielkiej Brytanii','brands_hatch',76,null],
  ['1 sierpnia','GER','GP Niemiec','nurburgring',14,null],['15 sierpnia','AUT','GP Austrii','osterreichring',54,null],['29 sierpnia','NED','GP Holandii','zandvoort',75,null],
  ['12 września','ITA','GP Włoch','monza',52,null],['3 października','CAN','GP Kanady','mosport',80,null],['10 października','USA','GP USA Wschód','watkins_glen',59,null],
  ['24 października','JPN','GP Japonii','fuji',73,null],
];
/* czołowa szóstka (numery), pole position, najszybsze okrążenie, pola startowe naszych, wycofani na stałe */
const TOP = [
  {top:[1,11,16,34,3,4], pole:11, fl:17, grid:{3:5,4:3}},
  {top:[1,3,11,12,28,9], pole:11, fl:1, grid:{3:3,4:9}, dnf:{4:'Silnik'}},
  {top:[2,1,3,26,4,12], pole:2, fl:2, grid:{3:4,4:6}},
  {top:[11,1,6,7,22,8], pole:11, fl:12, grid:{3:7,4:10}, dnf:{3:'Zawieszenie',4:'Hamulce'}},
  {top:[1,2,26,3,19,4], pole:1, fl:1, grid:{3:6,4:8}},
  {top:[1,3,4,34,12,30], pole:1, fl:2, grid:{3:2,4:5}},
  {top:[3,4,1,26,11,2], pole:3, fl:5, grid:{3:1,4:2}},
  {top:[11,4,28,8,1,3], pole:11, fl:1, grid:{3:4,4:3}},
];
const PTS = [9, 6, 4, 3, 2, 1];
const REASONS = ['Silnik','Skrzynia biegów','Wypadek','Zawieszenie','Obrót','Kolizja','Przegrzanie','Wyciek oleju','Hamulce','Układ paliwowy'];
const secs = s => { const [h, m, x] = s.replace(',', '.').split(':').map(Number); return h * 3600 + m * 60 + x; };
const fmtGap = s => s < 60 ? `+${s.toFixed(1).replace('.', ',')} s` : `+${Math.floor(s / 60)}:${(s % 60).toFixed(1).replace('.', ',').padStart(4, '0')}`;
const fmtLap = s => `${Math.floor(s / 60)}:${(s % 60).toFixed(2).replace('.', ',').padStart(5, '0')}`;

function makeResults(i) {
  const c = CAL[i], spec = TOP[i], R = rng(1976 * 100 + i), laps = c[4], total = secs(c[5]), lap = total / laps;
  const fixedDnf = spec.dnf || {};
  const rest = GRID.filter(g => !spec.top.includes(g[0]) && !fixedDnf[g[0]])
    .map(g => ({ g, k: g[6] + R() * 30 })).sort((a, b) => b.k - a.k).map(x => x.g);
  const nDnf = 5 + Math.floor(R() * 4) - Object.keys(fixedDnf).length;
  const dnfPick = [];
  for (let k = 0; k < nDnf && rest.length > 4; k++) dnfPick.push(rest.splice(Math.floor(R() * rest.length), 1)[0]);
  /* pola startowe: pole sitter, nasi na stałych polach, reszta wg siły z szumem */
  const gridOrder = GRID.map(g => ({ g, k: g[6] + R() * 26 })).sort((a, b) => b.k - a.k).map(x => x.g[0]);
  const slots = {}; slots[spec.pole] = 1; Object.entries(spec.grid).forEach(([no, p]) => slots[no] = p);
  let p = 1; const used = new Set(Object.values(slots));
  gridOrder.forEach(no => { if (slots[no]) return; while (used.has(p)) p++; slots[no] = p; used.add(p); });
  const rows = []; let gap = 0;
  [...spec.top.map(n => byNo[n]), ...rest].forEach((g, k) => {
    gap += k === 0 ? 0 : 2 + R() * (k < 6 ? 18 : 30);
    const down = Math.floor(gap / lap);
    rows.push({ pos: k + 1, no: g[0], id: g[1], name: g[2], nat: g[3], team: g[4], grid: slots[g[0]], laps: laps - down,
      time: k === 0 ? c[5] : down ? `+${UI_n(down)}` : fmtGap(gap), pts: PTS[k] || 0 });
  });
  const dnfAll = [...Object.entries(fixedDnf).map(([no, r]) => [byNo[no], r]), ...dnfPick.map(g => [g, REASONS[Math.floor(R() * REASONS.length)]])];
  dnfAll.forEach(([g, reason]) => rows.push({ pos: null, no: g[0], id: g[1], name: g[2], nat: g[3], team: g[4], grid: slots[g[0]], laps: Math.floor(R() * laps * .85) + 1, time: reason, dnf: true, pts: 0 }));
  const flTime = lap * (.955 + R() * .01);
  return { rows, pole: spec.pole, fl: { no: spec.fl, time: fmtLap(flTime) }, laps };
}
const UI_n = n => `${n} ${n === 1 ? 'okr.' : 'okr.'}`;

const results = CAL.slice(0, 8).map((_, i) => makeResults(i));

/* klasyfikacje liczone z wyników (konstruktorzy: tylko najlepsze auto zespołu) */
const drvPts = {}, conPts = {};
results.forEach(r => {
  const seen = new Set();
  r.rows.forEach(x => {
    drvPts[x.id] = (drvPts[x.id] || 0) + x.pts;
    if (!seen.has(x.team)) { conPts[x.team] = (conPts[x.team] || 0) + x.pts; if (x.pts) seen.add(x.team); }
  });
});
const drivers = GRID.map(g => ({ id: g[1], name: g[2], nat: g[3], team: g[4], pts: drvPts[g[1]] || 0, wins: results.filter(r => r.rows[0].id === g[1]).length }))
  .sort((a, b) => b.pts - a.pts || b.wins - a.wins).filter(d => d.pts > 0);
const constructors = Object.keys(TEAMS).map(k => ({ team: k, pts: conPts[k] || 0, wins: results.filter(r => r.rows[0].team === k).length }))
  .sort((a, b) => b.pts - a.pts || b.wins - a.wins).filter(c => c.pts > 0);

/* wyniki naszego kierowcy runda po rundzie */
const seasonOf = id => results.map(r => { const x = r.rows.find(y => y.id === id); return x ? (x.dnf ? 'DNF' : x.pos) : null; });
const gridOf = id => results.map(r => { const x = r.rows.find(y => y.id === id); return x ? x.grid : null; });

/* ---------- atrybuty ---------- */
const ATTR = [['zakr','Zakręty','Zakręty'],['ham','Hamowanie','Hamow.'],['pl','Płynność','Płynność'],['wyp','Wyprzedzanie','Wyprzedz.'],['obr','Obrona','Obrona'],
  ['reg','Regularność','Regular.'],['opa','Opanowanie','Opanow.'],['ada','Adaptacja','Adaptacja'],['desz','Deszcz','Deszcz'],['kon','Kondycja','Kondycja'],['inf','Informacja zwrotna','Inf. zwrotna']];

/* obcy kierowcy: pasma ze scoutingu (szerokość zależy od wiedzy o kierowcy), wyliczane z ziarnem */
function scouted(id, stars, known) {
  const R = rng(id.split('').reduce((a, c) => a * 31 + c.charCodeAt(0), 7));
  const w = Math.max(1, Math.round((100 - known) / 18));
  return Object.fromEntries(ATTR.map(([k]) => {
    const mid = Math.max(4, Math.min(19, Math.round(6 + stars * 2.3 + (R() - .5) * 6)));
    const lo = Math.max(1, mid - Math.floor(w / 2) - Math.round(R())), hi = Math.min(20, lo + w);
    return [k, known >= 100 ? mid : [lo, hi]];
  }));
}

window.DB = {
  weekdays: ['Poniedziałek','Wtorek','Środa','Czwartek','Piątek','Sobota','Niedziela'],
  /* przystanki przycisku Dalej */
  stops: [
    { day: 7, weekday: 2, title: 'Środa, 7 lipca 1976', short: 'Śr 7 lipca' },
    { day: 9, weekday: 4, title: 'Piątek, 9 lipca 1976', short: 'Pt 9 lipca' },
    { day: 16, weekday: 4, title: 'Piątek, 16 lipca 1976', short: 'Pt 16 lipca', weekend: true },
  ],
  nextRace: { day: 18, round: 9 },
  money: { free: 120, cash: 410 },
  teams: TEAMS, grid: GRID, gridById: byId, tracks: TRACKS, calendar: CAL, results, attrs: ATTR,
  standings: { drivers, constructors },
  seasonOf, gridOf, scouted,
  scoring: { points: PTS, counted: [7, 7], halves: [[1, 8], [9, 16]] },

  /* ---------- nasi kierowcy ---------- */
  drivers: [
    { id:'scheckter', name:'Jody Scheckter', nat:'RSA', age:26, born:'29 stycznia 1950', role:'Kierowca #1', no:3, stars:4, pot:4.5, form:'Wysoka', morale:'Dobre', trust:78,
      attrs:{zakr:17,ham:16,pl:13,wyp:15,obr:15,reg:14,opa:14,ada:15,desz:13,kon:16,inf:13},
      prefs:[['Balans','Lekka nadsterowność','Lekka nadsterowność'],['Trakcja','Ostra','Ostra'],['Hamowanie','Późne','Późne']],
      traits:['Mistrz kwalifikacji','Twardy w obronie'],
      contract:{to:1976, salary:95, bonus:'£3 tys. za zwycięstwo'},
      clauses:[{type:'Wyjście', cond:'Tyrrell poza top 3 konstruktorów na koniec 1976', effect:'Odchodzi bez odstępnego'},
               {type:'Status', cond:'Cały kontrakt', effect:'Kierowca #1: pierwszeństwo w strategii i nowych częściach'},
               {type:'Zakaz', cond:'Cały kontrakt', effect:'Bez startów w wyścigach samochodów sportowych'}],
      promise:{text:'Lżejsza skrzynia biegów', due:'GP Niemiec, 1 sierpnia', races:2, pct:35, miss:[['Zaufanie','−15'],['Klauzula wyjścia','Aktywna od razu']]},
      career:{starts:43, wins:4, poles:1, podiums:11, points:96, fl:2, titles:0},
      seasons:[[1972,'McLaren',1,0,0,'9.'],[1973,'McLaren',5,0,0,'DNF'],[1974,'Tyrrell',15,45,2,'1.'],[1975,'Tyrrell',14,20,1,'1.'],[1976,'Tyrrell',8,31,1,'1.']],
      voices:[['TW','Tom Walsh','Inżynier wyścigowy','W szybkich zakrętach jest w pierwszej trójce stawki. Na hamowaniu nie odpuszcza nikomu.'],
              ['TW','Tom Walsh','Inżynier wyścigowy','Z autem się nie patyczkuje. Skrzynia i hamulce cierpią, zwłaszcza w drugiej połowie wyścigu.'],
              ['DG','Derek Gardner','Główny projektant','Jego uwagi po testach są krótkie. Wiem, że coś jest nie tak, ale rzadko wiem co.'],
              ['RH','Roger Hill','Szef mechaników','Pod presją walki o tytuł jeszcze go nie widzieliśmy. W kwalifikacjach jest zimny jak lód.']] },
    { id:'depailler', name:'Patrick Depailler', nat:'FRA', age:31, born:'9 sierpnia 1944', role:'Równy status', no:4, stars:3.5, pot:3.5, form:'Dobra', morale:'Bardzo dobre', trust:85,
      attrs:{zakr:16,ham:17,pl:14,wyp:14,obr:13,reg:12,opa:12,ada:14,desz:15,kon:14,inf:17},
      prefs:[['Balans','Neutralny','Lekka nadsterowność'],['Trakcja','Miękka','Ostra'],['Hamowanie','Późne','Późne']],
      traits:['Znakomita informacja zwrotna'],
      contract:{to:1977, salary:60, bonus:'£2 tys. za podium'},
      clauses:[{type:'Opcja', cond:'Decyzja zespołu do 30 września 1977', effect:'Przedłużenie na 1978 na tych samych warunkach'}],
      promise:null, career:{starts:39, wins:0, poles:1, podiums:5, points:47, fl:1, titles:0},
      seasons:[[1972,'Tyrrell',2,1,0,'6.'],[1974,'Tyrrell',15,14,0,'2.'],[1975,'Tyrrell',14,12,0,'3.'],[1976,'Tyrrell',8,20,0,'2.']] },
    { id:'hoffmann', name:'Ingo Hoffmann', nat:'BRA', age:23, born:'28 lutego 1953', role:'Kierowca testowy', no:'T', stars:2.5, pot:3, form:'—', morale:'Dobre', trust:60,
      attrs:{zakr:13,ham:12,pl:13,wyp:11,obr:11,reg:12,opa:11,ada:13,desz:12,kon:14,inf:12},
      prefs:[['Balans','Neutralny','Lekka nadsterowność'],['Trakcja','Miękka','Ostra'],['Hamowanie','Normalne','Późne']], traits:[],
      contract:{to:1976, salary:12, bonus:'—'}, clauses:[], promise:null,
      career:{starts:3, wins:0, poles:0, podiums:0, points:0, fl:0, titles:0}, seasons:[[1976,'Copersucar',3,0,0,'11.']] },
  ],

  /* ---------- rynek: obcy kierowcy (wiedza skauta w %, pasmo gwiazdek) ---------- */
  market: [
    ['peterson','Ronnie Peterson','SWE',32,'March',[3.5,4],4,1976,'Zainteresowany',160,70],['andretti','Mario Andretti','USA',36,'Lotus',[4,4],4,1977,'Raczej nie',210,65],
    ['watson','John Watson','GBR',30,'Penske',[3,3.5],3.5,1976,'Zainteresowany',70,55],['laffite','Jacques Laffite','FRA',32,'Ligier',[3,4],3.5,1977,'Raczej nie',90,50],
    ['pironi','Didier Pironi','FRA',24,'Formuła 2',[2.5,3.5],4.5,1977,'Bardzo zainteresowany',25,35],['villeneuve','Gilles Villeneuve','CAN',26,'Formuła Atlantic',[2.5,3.5],5,1976,'Bardzo zainteresowany',20,30],
    ['jarier','Jean-Pierre Jarier','FRA',29,'Shadow',[3,3],3,1976,'Zainteresowany',45,60],['brambilla','Vittorio Brambilla','ITA',38,'March',[2.5,3],3,1976,'Zainteresowany',35,60],
    ['stuck','Hans-Joachim Stuck','GER',25,'March',[3,3.5],3.5,1977,'Neutralny',55,45],['reutemann','Carlos Reutemann','ARG',34,'Brabham',[3.5,4],4,1976,'Neutralny',140,60],
    ['jones','Alan Jones','AUS',29,'Surtees',[3,3.5],3.5,1976,'Zainteresowany',40,50],['patrese','Riccardo Patrese','ITA',22,'Formuła 3',[2,3],4,1976,'Bardzo zainteresowany',15,30],
    ['cheever','Eddie Cheever','USA',18,'Formuła 3',[2,2.5],4,1977,'Bardzo zainteresowany',12,25],['fittipaldi','Emerson Fittipaldi','BRA',29,'Copersucar',[3.5,3.5],3.5,1977,'Raczej nie',180,75],
    ['pryce','Tom Pryce','GBR',26,'Shadow',[3,3.5],3.5,1976,'Zainteresowany',40,55],['merzario','Arturo Merzario','ITA',33,'Williams',[2.5,2.5],2.5,1976,'Zainteresowany',30,60],
  ].map(([id, name, nat, age, team, band, pot, to, mood, salary, known]) => ({ id, name, nat, age, team, band, pot, to, mood, salary, known })),

  /* ---------- personel: kluczowi ludzie (właściciel jest w Zarządzie, nie tutaj) ---------- */
  staff: [
    { id:'gardner', name:'Derek Gardner', nat:'GBR', age:45, role:'Główny projektant', since:1970, stars:4,
      attrs:[['Wizja',17],['Precyzja',16],['Innowacyjność',18],['Zarządzanie projektem',13]], contract:{to:1977, salary:28},
      career:[['1964–1969','Ferguson Research','Projektant (napęd 4×4)'],['1969','Matra','Konsultant (MS84)'],['1970–','Tyrrell','Główny projektant: 001, 003, 005, 006, 007, P34']],
      mood:'Dobre', ambition:'Wysoka', loyalty:'Wysoka' },
    { id:'philippe', name:'Maurice Philippe', nat:'GBR', age:44, role:'Projektant', since:1976, stars:3.5,
      attrs:[['Podwozie',15],['Integracja',14],['Precyzja',13]], contract:{to:1978, salary:18},
      career:[['1965–1972','Lotus','Projektant: 49, 56, 72'],['1973–1975','Parnelli','Główny projektant: VPJ4'],['1976–','Tyrrell','Projektant']],
      mood:'Dobre', ambition:'Wysoka', loyalty:'Średnia' },
    { id:'hill', name:'Roger Hill', nat:'GBR', age:38, role:'Szef mechaników', since:1969, stars:3.5,
      attrs:[['Pit-stopy',14],['Jakość montażu',16],['Organizacja',15]], contract:{to:1977, salary:9},
      career:[['1966–1968','Matra International','Mechanik'],['1969–1972','Tyrrell','Mechanik'],['1973–','Tyrrell','Szef mechaników']],
      mood:'Bardzo dobre', ambition:'Średnia', loyalty:'Bardzo wysoka' },
    { id:'walsh', name:'Tom Walsh', nat:'GBR', age:33, role:'Inżynier wyścigowy', since:1974, stars:3, driver:'scheckter',
      attrs:[['Ustawienia',14],['Relacja z kierowcą',15],['Analiza danych',12]], contract:{to:1976, salary:8},
      career:[['1968–1973','Lola','Inżynier'],['1974–','Tyrrell','Inżynier wyścigowy Schecktera']],
      mood:'Dobre', ambition:'Wysoka', loyalty:'Średnia' },
    { id:'morel', name:'Pierre Morel', nat:'FRA', age:35, role:'Inżynier wyścigowy', since:1972, stars:3, driver:'depailler',
      attrs:[['Ustawienia',13],['Relacja z kierowcą',16],['Analiza danych',13]], contract:{to:1977, salary:8},
      career:[['1966–1971','Matra','Mechanik, potem inżynier'],['1972–','Tyrrell','Inżynier wyścigowy Depailliera']],
      mood:'Dobre', ambition:'Średnia', loyalty:'Wysoka' },
  ],
  departments: [
    { name:'Biuro projektowe', people:6, quality:14, head:'gardner', perHead:1.6 },
    { name:'Produkcja', people:14, quality:13, head:'hill', perHead:0.9 },
    { name:'Zespół wyścigowy', people:11, quality:15, head:'hill', perHead:1.0 },
    { name:'Dział komercyjny', people:2, quality:12, head:null, perHead:1.1 },
    { name:'Scouting', people:1, quality:11, head:null, perHead:1.2 },
  ],

  /* ---------- skrzynka ---------- */
  inbox: [
    { id:1, kind:'decyzje', from:'Goodyear', av:'GY', title:'Umowa na lata 1977–79', when:'Dziś', due:'Pt 9 lipca', dueDay:9, decision:true, unread:true,
      body:'Goodyear chce przedłużyć umowę fabryczną na trzy sezony. W zamian proponuje dalszy wspólny rozwój małych przednich opon 10″, bez których P34 traci sens. Warunek: wyłączność do końca 1979.',
      options:[{label:'Podpisz', fx:[['p','Rozwój przednich opon 10″ do 1979'],['p','Premia +£40 tys. rocznie'],['m','Wyłączność Goodyear do końca 1979']]},
               {label:'Negocjuj', fx:[['p','Szansa na umowę do 1978'],['m','Ok. 3 tygodnie rozmów'],['m','Goodyear może wycofać ofertę']]},
               {label:'Odmów', fx:[['p','Wolny wybór dostawcy na 1977'],['m','Koniec prac nad oponami 10″ po sezonie']]}] },
    { id:8, kind:'decyzje', from:'FIA', av:'FI', title:'Głosowanie: limit szerokości tylnych opon', when:'Dziś', due:'1 października', dueDay:null, decision:true, unread:true,
      body:'CSI proponuje od 1977 ograniczyć szerokość tylnych opon do 21″. Każdy zespół ma jeden głos, a zmiana wymaga większości dwóch trzecich. Ferrari i McLaren są przeciw, Brabham i Ligier za.',
      options:[{label:'Za', fx:[['m','−3% docisku mechanicznego dla wszystkich'],['p','P34 traci mniej niż rywale: przód bez zmian'],['o','Ferrari: relacje −5']]},
               {label:'Wstrzymaj się', fx:[['o','Bez wpływu na relacje'],['o','Wynik zależy od pozostałych 12 głosów']]},
               {label:'Przeciw', fx:[['p','Obecne opony bez zmian'],['p','Ferrari: relacje +3'],['m','Ligier: relacje −3']]}] },
    { id:2, kind:'raporty', from:'Derek Gardner', av:'DG', title:'Przednie opony zużywają się szybciej', when:'Dziś', unread:true,
      body:'Dane z Paul Ricard pokazują, że małe przednie opony tracą przyczepność szybciej, niż zakładaliśmy. Na Brands Hatch stint może być krótszy o ok. 8 okrążeń. Strateg rozważa późniejszy postój, ale to zależy od temperatury.' },
    { id:3, kind:'raporty', from:'Skaut', av:'SK', title:'Didier Pironi, Formuła 2: raport', when:'Dziś', unread:true, link:['#/kierowca/pironi','Profil: Didier Pironi'],
      body:'Pironi (24 l.) jest najszybszym kierowcą w F2 w tym sezonie. Zakręty 14–17, opanowanie 12–16, cechy nieznane. Kontrakt w F2 do końca 1977. Warto obejrzeć go na żywo w Rouen.' },
    { id:4, kind:'raporty', from:'Jody Scheckter', av:'JS', title:'Pytanie o lżejszą skrzynię', when:'Pon', unread:true, link:['#/kierowca/scheckter','Profil: Jody Scheckter'],
      body:'Scheckter pyta, czy obietnica lżejszej skrzyni do GP Niemiec jest aktualna. Mówi wprost: jeśli jej nie będzie, zacznie rozmawiać z innymi zespołami o sezonie 1977.' },
    { id:5, kind:'raporty', from:'Ken Tyrrell', av:'KT', title:'Budżet na drugą połowę sezonu', when:'5 lipca', unread:true, link:['#/zarzad','Zarząd'],
      body:'Po sześciu wyścigach jesteśmy £30 tys. poniżej planu wydatków. Ken zgadza się przesunąć część na rozwój P34B, jeśli utrzymamy 2. miejsce wśród konstruktorów.' },
    { id:6, kind:'media', from:'Elf', av:'EL', title:'Kampania przed GP Wielkiej Brytanii', when:'3 lipca', unread:false,
      body:'Elf przygotowuje kampanię z udziałem Schecktera. Potrzebują jednego dnia zdjęciowego przed wyścigiem.' },
    { id:7, kind:'media', from:'FIA', av:'FI', title:'Wyjaśnienie: wysokość wlotów powietrza', when:'28 czerwca', unread:false, link:['#/fia','FIA i regulamin'],
      body:'Przypomnienie: od GP Hiszpanii obowiązuje zakaz wysokich wlotów powietrza nad kokpitem. Kontrola techniczna na Brands Hatch sprawdzi wymiary.' },
  ],
  /* odpowiedzi po decyzji w sprawie Goodyear (przychodzą przy następnym „Dalej”) */
  replies: {
    'Podpisz': { from:'Goodyear', av:'GY', kind:'raporty', title:'Umowa podpisana do 1979', body:'Goodyear potwierdza umowę fabryczną do końca 1979. Pierwsza partia nowych przednich opon 10″ trafi do Ockham przed GP Niemiec.' },
    'Negocjuj': { from:'Goodyear', av:'GY', kind:'raporty', title:'Kontroferta: umowa do 1978', body:'Goodyear zgadza się na dwa sezony, ale bez premii £40 tys. w 1977. Odpowiedź do końca lipca.' },
    'Odmów': { from:'Goodyear', av:'GY', kind:'raporty', title:'Współpraca kończy się po sezonie', body:'Goodyear przyjmuje decyzję. Dostawy opon do końca 1976 bez zmian, prace nad oponami 10″ zostają wstrzymane w październiku.' },
  },

  /* ---------- historia torów (atrapa do sprawdzenia w R2) ---------- */
  trackHistory: {
    brands_hatch: { first:1964, gps:6, dnfAvg:12.3, scAvg:0, record:['Niki Lauda','Ferrari','1:21,1',1974],
      years:[[1974,'Jody Scheckter','Tyrrell-Ford','Niki Lauda','Niki Lauda'],[1972,'Emerson Fittipaldi','Lotus-Ford','Jacky Ickx','Jackie Stewart'],[1970,'Jochen Rindt','Lotus-Ford','Jochen Rindt','Jack Brabham'],
             [1968,'Jo Siffert','Lotus-Ford','Graham Hill','Jo Siffert'],[1966,'Jack Brabham','Brabham-Repco','Jack Brabham','Jack Brabham'],[1964,'Jim Clark','Lotus-Climax','Jim Clark','Jim Clark']] },
    interlagos:{ years:[[1975,'Carlos Pace','Brabham-Ford'],[1974,'Emerson Fittipaldi','McLaren-Ford'],[1973,'Emerson Fittipaldi','Lotus-Ford']] },
    kyalami:{ years:[[1975,'Jody Scheckter','Tyrrell-Ford'],[1974,'Carlos Reutemann','Brabham-Ford'],[1973,'Jackie Stewart','Tyrrell-Ford'],[1972,'Denny Hulme','McLaren-Ford'],[1971,'Mario Andretti','Ferrari']] },
    long_beach:{ years:[] },
    jarama:{ years:[[1974,'Niki Lauda','Ferrari'],[1972,'Emerson Fittipaldi','Lotus-Ford'],[1970,'Jackie Stewart','March-Ford'],[1968,'Graham Hill','Lotus-Ford']] },
    zolder:{ years:[[1975,'Niki Lauda','Ferrari'],[1973,'Jackie Stewart','Tyrrell-Ford']] },
    monaco:{ years:[[1975,'Niki Lauda','Ferrari'],[1974,'Ronnie Peterson','Lotus-Ford'],[1973,'Jackie Stewart','Tyrrell-Ford'],[1972,'Jean-Pierre Beltoise','BRM'],[1971,'Jackie Stewart','Tyrrell-Ford']] },
    anderstorp:{ years:[[1975,'Niki Lauda','Ferrari'],[1974,'Jody Scheckter','Tyrrell-Ford'],[1973,'Denny Hulme','McLaren-Ford']] },
    ricard:{ years:[[1975,'Niki Lauda','Ferrari'],[1973,'Ronnie Peterson','Lotus-Ford'],[1971,'Jackie Stewart','Tyrrell-Ford']] },
    nurburgring:{ years:[[1975,'Carlos Reutemann','Brabham-Ford'],[1974,'Clay Regazzoni','Ferrari'],[1973,'Jackie Stewart','Tyrrell-Ford'],[1972,'Jacky Ickx','Ferrari'],[1971,'Jackie Stewart','Tyrrell-Ford']] },
    osterreichring:{ years:[[1975,'Vittorio Brambilla','March-Ford'],[1974,'Carlos Reutemann','Brabham-Ford'],[1973,'Ronnie Peterson','Lotus-Ford'],[1972,'Emerson Fittipaldi','Lotus-Ford'],[1971,'Jo Siffert','BRM']] },
    zandvoort:{ years:[[1975,'James Hunt','Hesketh-Ford'],[1974,'Niki Lauda','Ferrari'],[1973,'Jackie Stewart','Tyrrell-Ford'],[1971,'Jacky Ickx','Ferrari']] },
    monza:{ years:[[1975,'Clay Regazzoni','Ferrari'],[1974,'Ronnie Peterson','Lotus-Ford'],[1973,'Ronnie Peterson','Lotus-Ford'],[1972,'Emerson Fittipaldi','Lotus-Ford'],[1971,'Peter Gethin','BRM']] },
    mosport:{ years:[[1974,'Emerson Fittipaldi','McLaren-Ford'],[1973,'Peter Revson','McLaren-Ford'],[1972,'Jackie Stewart','Tyrrell-Ford'],[1971,'Jackie Stewart','Tyrrell-Ford']] },
    watkins_glen:{ years:[[1975,'Niki Lauda','Ferrari'],[1974,'Carlos Reutemann','Brabham-Ford'],[1973,'Ronnie Peterson','Lotus-Ford'],[1972,'Jackie Stewart','Tyrrell-Ford'],[1971,'François Cevert','Tyrrell-Ford']] },
    fuji:{ years:[] },
  },

  /* ---------- auto ---------- */
  car: {
    name:'Tyrrell P34', concept:'Sześć kół, małe przednie koła 10″',
    areas:[['Przyczepność mechaniczna',1],['Hamowanie',2],['Docisk',6],['Niezawodność',8],['Moc',10]],
    /* miejsce w stawce na typach odcinków toru */
    sectors:[['Proste',12],['Szybkie zakręty',7],['Wolne zakręty',2],['Hamowanie',1]],
    axes:[['Aero','Mały opór','Duży docisk',62],['Filozofia','Ewolucja','Rewolucja',88],['Okno pracy','Szerokie','Wąskie',70],['Chłodzenie','Zapas','Na krawędzi',45],['Opony','Łagodne','Agresywne',58]],
    projects:[{name:'Przednie zawieszenie', stream:'Bieżące auto', pct:62, eta:'3 tyg.'},{name:'Lżejsza skrzynia biegów', stream:'Bieżące auto', pct:35, eta:'5 tyg.'},{name:'Koncepcja 1977: P34B', stream:'Przyszły rok', pct:28, eta:'5 tyg.'}],
    bank:'+1,8 pkt', split:[60,15,25],
    understanding:[['Podwozie P34/2',92],['Przednie skrzydło B',74],['Nowe tylne zawieszenie',41]],
  },
  rivals: {
    ferrari: { car:'Ferrari 312T2', team:'Ferrari', engine:'Ferrari 015, 12 cyl. płaski', power:'ok. 500 KM', weight:'ok. 590 kg', known:55,
      areas:[['Moc',1],['Docisk',2],['Niezawodność',1],['Przyczepność mechaniczna',4],['Hamowanie',5]],
      news:[['Fiorano, 29 czerwca','Test szerszego tylnego skrzydła','Szacunek Gardnera: 0,2–0,4 s na okrążeniu na szybkich torach'],['GP Hiszpanii','Nowy wlot powietrza po zakazie wysokich airboxów','Bez utraty mocy według pomiarów prędkości na prostej']] },
  },

  /* ---------- infrastruktura: wartości konkretne, porównanie z liderem stawki ---------- */
  facilities: [
    { id:'fabryka', name:'Fabryka w Ockham', level:3, max:null, params:[['Powierzchnia','1 900 m²'],['Stanowiska montażu','4'],['Czas budowy podwozia','9 tyg.']],
      leader:['McLaren','3 400 m²'], pct:56, upkeep:3.5, upgrade:{what:'+800 m², 2 stanowiska montażu', cost:95, months:8, effect:'Czas budowy podwozia: 7 tyg.'} },
    { id:'tunel', name:'Tunel aerodynamiczny', level:1, params:[['Dostęp','Wynajem: Imperial College'],['Skala modelu','25%'],['Godziny w miesiącu','40']],
      leader:['Ferrari','Własny tunel, 160 h/mies.'], pct:25, upkeep:2.2, upgrade:{what:'Własny tunel, skala 25%, 160 h/mies.', cost:180, months:14, effect:'Godziny w miesiącu: 160'} },
    { id:'hamownia', name:'Hamownia silników', level:0, params:[['Dostęp','Brak'],['Silniki','Ford Cosworth DFV (klient)'],['Testy silnika','U Cosworth']],
      leader:['Ferrari','2 stanowiska'], pct:0, upkeep:0, upgrade:{what:'1 stanowisko hamowni', cost:60, months:5, effect:'Testy integracji silnika na miejscu'} },
    { id:'kompozyty', name:'Warsztat kompozytów', level:2, params:[['Materiały','Włókno szklane'],['Piec do utwardzania','Brak'],['Części w tygodniu','6']],
      leader:['Lotus','Włókno węglowe (próby)'], pct:48, upkeep:1.1, upgrade:{what:'Piec do utwardzania', cost:45, months:4, effect:'Części w tygodniu: 10'} },
    { id:'biuro', name:'Biuro projektowe', level:3, params:[['Deski kreślarskie','6'],['Projektanci','6'],['Projekty naraz','2']],
      leader:['McLaren','10 desek'], pct:60, upkeep:0.6, upgrade:{what:'+2 deski kreślarskie', cost:12, months:1, effect:'Projekty naraz: 3'} },
  ],

  /* ---------- dostawcy: wspólne pola + 3 parametry kategorii ---------- */
  suppliers: [
    { cat:'Silnik', name:'Ford Cosworth DFV', type:'Kliencka', to:1977, cost:-84, params:[['Moc','465 KM'],['Masa','161 kg'],['Awarie w 1976','2 na 16 startów']] },
    { cat:'Opony', name:'Goodyear', type:'Fabryczna', to:1976, cost:15, params:[['Przednie','10″ (tylko Tyrrell)'],['Mieszanki na weekend','3'],['Komplety na weekend','8']] },
    { cat:'Paliwo i olej', name:'Elf', type:'Partnerska', to:1978, cost:0, params:[['Paliwo','Bezpłatne'],['Moc (paliwo wyścigowe)','+4 KM'],['Dni marketingowe','6 w roku']] },
    { cat:'Skrzynia biegów', name:'Hewland FG400', type:'Kliencka', to:1976, cost:-22, params:[['Masa','52 kg'],['Przełożenia','5'],['Awarie w 1976','1 na 16 startów']] },
    { cat:'Hamulce', name:'Lockheed', type:'Kliencka', to:1977, cost:-9, params:[['Tarcze','Wentylowane, 4 na koło przód'],['Masa zestawu','21 kg'],['Awarie w 1976','0']] },
  ],

  sponsors: [
    { slot:'Tytularny', name:'Elf', ind:'Paliwa', amount:180, to:1978, goal:'Top 3 konstruktorów', goalState:['2. miejsce','good'], duties:['Dni PR','6 w roku, 4 wykorzystane'], place:'Nazwa zespołu, boki, skrzydło' },
    { slot:'Główny', name:'First National City', ind:'Czeki podróżne', amount:70, to:1977, goal:'Zwycięstwo w sezonie', goalState:['Spełniony: Szwecja','good'], duties:['Dni PR','3 w roku, 1 wykorzystany'], place:'Nos, kombinezony' },
    { slot:'Mniejszy', name:'Goodyear', ind:'Opony', amount:15, to:1976, goal:'—', goalState:null, duties:['Dni PR','—'], place:'Przednie skrzydło' },
  ],
  sponsorLeads: [['Marlboro','Tytoń','£120–160 tys.'],['Candy','AGD','£40–60 tys.'],['Citibank','Finanse','£30–50 tys.'],['Olympus','Aparaty','£20–30 tys.']],

  finance: {
    income:[['Sponsorzy',265,[['Elf',180],['First National City',70],['Goodyear',15]]],['Pieniądze startowe',120,[['FOCA: 8 rund po £15 tys.',120]]],['Nagrody',38,[['GP Szwecji: zwycięstwo',12],['GP Monako: 2. i 3. miejsce',11],['Pozostałe rundy',15]]],['Sprzedaż części',6,[['Stare podwozie 007 (Lec)',6]]]],
    costs:[['Pensje kierowców',167,[['Jody Scheckter',95],['Patrick Depailler',60],['Ingo Hoffmann',12]]],['Personel',97,[['Kluczowi ludzie',71],['Działy (34 osoby)',26]]],
      ['Silniki i części',118,[['Cosworth: dzierżawa i przeglądy',84],['Hewland: skrzynie',22],['Lockheed: hamulce',9],['Drobne części',3]]],['Rozwój',64,[['Przednie zawieszenie',28],['Lżejsza skrzynia',21],['P34B',15]]],
      ['Podróże',41,[['Frachty lotnicze (Brazylia, RPA, USA)',26],['Transport w Europie',15]]],['Infrastruktura',18,[['Wynajem tunelu',9],['Utrzymanie fabryki',9]]]],
    months:[[-12,'sty'],[18,'lut'],[-8,'mar'],[22,'kwi'],[15,'maj'],[31,'cze'],[-6,'lip'],[null,'sie'],[null,'wrz'],[null,'paź'],[null,'lis'],[null,'gru']],
  },

  board: {
    owner:{ name:'Ken Tyrrell', nat:'GBR', age:52, since:1958, role:'Właściciel', quote:'Jody ma zostać. Zrób, co trzeba, ale nie wydawaj pieniędzy, których nie mamy.' },
    mood:['Zadowolony','good'], trust:72,
    params:[['Cierpliwość','2 sezony bez celu'],['Budżet 1977','£1,1 mln'],['Kontrakty kierowców','Wymagają zgody'],['Ryzyko finansowe','Zakaz deficytu']],
    goals:[['Top 3 konstruktorów w 1976',true,'2. miejsce'],['Zwycięstwo w wyścigu',true,'GP Szwecji'],['Budżet bez deficytu',true,'+£30 tys.'],['Utrzymać Schecktera na 1977',null,'Obietnica: skrzynia do GP Niemiec']],
    history:[['GP Szwecji: zwycięstwo','+6'],['GP Monako: 2. i 3. miejsce','+3'],['GP Hiszpanii: oba auta DNF','−2']],
  },

  academy: {
    slots: 3,
    juniors: [
      { id:'daly', name:'Derek Daly', nat:'IRL', age:23, series:'Formuła Ford 2000', stars:2, pot:3.5, since:1976, to:1978, cost:18, status:['W programie','good'],
        season:[['Starty','9'],['Zwycięstwa','3'],['Miejsce','2.']], next:'Sezon w F3: 1977 (opłacony)' },
      { id:'south', name:'Stephen South', nat:'GBR', age:23, series:'Brytyjska F3', stars:2.5, pot:3.5, since:1975, to:1977, cost:25, status:['Test F1 zaplanowany','hi'],
        season:[['Starty','11'],['Zwycięstwa','2'],['Miejsce','4.']], next:'Test w P34: Silverstone, 21 lipca' },
    ],
    pool: [
      ['villeneuve','Gilles Villeneuve','CAN',26,'Formuła Atlantic',[2.5,3.5],5,30],['patrese','Riccardo Patrese','ITA',22,'Formuła 3',[2,3],4,30],
      ['prost','Alain Prost','FRA',21,'Formuła Renault',[1.5,2.5],4.5,15],['piquet','Nelson Piquet','BRA',23,'Formuła Vee',[1.5,2.5],4.5,10],
      ['mansell','Nigel Mansell','GBR',22,'Formuła Ford',[1,2],4,10],['cheever','Eddie Cheever','USA',18,'Formuła 3',[2,2.5],4,25],
      ['rosberg','Keke Rosberg','FIN',27,'Formuła 2',[2,3],3.5,20],['warwick','Derek Warwick','GBR',22,'Formuła 3',[1.5,2.5],3.5,15],
    ].map(([id, name, nat, age, team, band, pot, known]) => ({ id, name, nat, age, team, band, pot, known })),
  },

  monthly: {
    issue:'Nr 7 · lipiec 1976',
    lead:{sec:'Rynek', title:'Peterson rozmawia z Tyrrellem?', text:'Szwed ma dość Marcha. Jego ludzie pytają o miejsce na 1977, jeśli Scheckter odejdzie. W padoku mówi się też o zainteresowaniu Lotusa, ale Chapman ma już Andrettiego i Nilssona.', link:['#/kierowca/peterson','Ronnie Peterson: profil i kontrakt']},
    stories:[
      {sec:'Wyścigi', title:'Porsche 936 wygrywa Le Mans', text:'Ickx i van Lennep na szczycie. Renault prowadziło przez noc, ale turbo nie wytrzymało. Porsche umacnia się na prowadzeniu w mistrzostwach samochodów sportowych.', link:['#/klasyfikacje/wsc','Klasyfikacja samochodów sportowych']},
      {sec:'Talenty', title:'Villeneuve rozbija Formułę Atlantic', text:'Kanadyjczyk wygrał pięć z sześciu wyścigów. Kto pierwszy da mu test w F1?', link:['#/kierowca/villeneuve','Gilles Villeneuve: profil']},
      {sec:'Technika', title:'Ferrari testuje nowy tylny spojler', text:'Na Fiorano widziano 312T2 z szerszym tylnym skrzydłem. Ile to daje na szybkich torach?', link:['#/rywal/ferrari','Co wiemy o Ferrari 312T2']},
      {sec:'Pieniądze', title:'Tytoń kupuje F1', text:'Kolejne zespoły zmieniają barwy dla sponsorów tytoniowych. Marlboro szuka drugiego zespołu na 1977.', link:['#/sponsorzy','Wolne miejsca sponsorskie']},
      {sec:'Z historii', title:'Dziesięć lat temu: Brabham mistrzem we własnym aucie', text:'Jedyny taki przypadek w historii. Czy ktoś to powtórzy?', link:['#/kronika','Kronika']},
    ],
  },

  /* ---------- FIA: przepisy 1976 z data/authored/regulations (R1) ---------- */
  fia: {
    groups:[
      ['Silnik i paliwo',[['Formuła silnika','3,0 l wolnossący lub 1,5 l doładowany'],['Limit silników','Brak'],['Paliwo','Handlowa benzyna'],['Tankowanie w wyścigu','Dozwolone']]],
      ['Auto',[['Masa minimalna','575 kg'],['Aerodynamika','Elementy stałe, na masie resorowanej'],['Wloty powietrza','Zakaz wysokich wlotów nad kokpitem'],['Opony','Slicki, wielu dostawców']]],
      ['Sport',[['Punktacja','9-6-4-3-2-1'],['Liczone wyniki','7 najlepszych z rund 1–8 i 7 z rund 9–16'],['Konstruktorzy','Punktuje tylko najlepsze auto'],['Wyścig skrócony','Poniżej 30%: 0 pkt · 30–60%: połowa']]],
      ['Weekend',[['Kwalifikacje','2 sesje, liczy się najlepszy czas'],['Polecenia zespołowe','Dozwolone'],['Samochód bezpieczeństwa','Brak'],['Testy w sezonie','Bez limitu']]],
    ],
    changes:[['GP Hiszpanii 1976','Zakaz wysokich wlotów powietrza nad kokpitem','Auto'],['1976','Liczone wyniki: 7 + 7 (było 6 + 6)','Sport'],['1975','Połowa punktów za wyścig skrócony (30–60% dystansu)','Sport'],
      ['1973','Masa minimalna 575 kg (było 550 kg)','Auto'],['1972','Masa minimalna 550 kg (było 530 kg)','Auto'],['1971','Pierwsze opony typu slick','Auto']],
    votes:[{title:'Limit szerokości tylnych opon', from:'CSI', when:'Głosowanie do 1 października', mail:8, state:null},
           {title:'Obowiązkowa gaśnica pokładowa', from:'GPDA', when:'Przyjęte 12 marca 1976, od 1977', state:['Za','good'], result:'12 : 1'}],
  },

  chronicle: [
    ['4 lipca 1976','Hunt wygrywa we Francji, Lauda dopiero piąty','W rzeczywistości Hunt też wygrał, ale Lauda nie ukończył.'],
    ['13 czerwca 1976','Scheckter wygrywa w Szwecji, dublet Tyrrella','Tak jak w rzeczywistości.'],
    ['Styczeń 1976','Przejmujesz Tyrrella po Kenie jako szefie zespołu','W rzeczywistości Ken Tyrrell prowadził zespół sam do 1998.'],
  ],

  manager: { name:'M. Wojnar', nat:'POL', age:34, since:1975, rep:68,
    attrs:[['Negocjacje',13],['Zarządzanie ludźmi',15],['Polityka',9],['Biznes',12]],
    repLog:[['13 czerwca','GP Szwecji: zwycięstwo','+4'],['30 maja','GP Monako: 2. i 3. miejsce','+2'],['2 maja','GP Hiszpanii: oba auta DNF','−1'],['Styczeń','Objęcie Tyrrella','+0']],
    career:[['1975–','Tyrrell','Szef zespołu',22,1]],
  },
  wsc: [['Porsche',80],['Alpine-Renault',38],['Lola-Ford',14],['Chevron-Ford',11],['Osella-BMW',8]],
  f2: [['Jean-Pierre Jabouille','FRA','Elf-Renault',38],['René Arnoux','FRA','Martini-Renault',34],['Patrick Tambay','FRA','Martini-Renault',24],['Michel Leclère','FRA','Elf-Renault',20],['Eddie Cheever','USA','Ralt-BMW',17],['Didier Pironi','FRA','Martini-Renault',12]],
};
})();
