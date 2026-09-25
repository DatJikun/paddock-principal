/* Ekrany prototypu. Każdy zwraca HTML; app.js wstawia go do #view. */
const S = {};
const A = UI.arrow;
const head = (title, sub, tools = '') =>
  `<div class="screen-head"><div><h1 class="screen">${title}</h1>${sub ? `<div class="sub">${sub}</div>` : ''}</div>${tools ? `<div class="tools">${tools}</div>` : ''}</div>`;
const mailRow = (m, sel) => `<a class="mail ${m.decision ? 'decision' : ''} ${m.unread ? '' : 'read'} ${sel ? 'sel' : ''}" href="#/skrzynka/${m.id}">
  <span class="av">${m.av}</span><div><div class="from">${m.from}${m.due ? `<span class="chip due">${UI.icon('<circle cx="12" cy="12" r="8"/><path d="M12 8v4l3 2"/>', 13)}${m.due}</span>` : ''}</div><div class="t">${m.title}</div></div>
  ${m.unread && !m.when ? '<span class="dot"></span>' : `<span class="when">${m.decision ? '<span class="dot"></span>' : m.when}</span>`}</a>`;
const standingsTable = (kind, n = 6) => {
  const rows = (kind === 'drv' ? DB.standings.drivers : DB.standings.constructors).slice(0, n);
  return `<table class="table tight" data-pane="${kind}" ${kind === 'con' ? 'hidden' : ''}><tbody>${rows.map((r, i) =>
    `<tr class="${r[kind === 'drv' ? 3 : 2] ? 'mine' : ''}"><td class="num" style="width:36px">${i + 1}</td><td>${r[0]}</td><td class="r num">${kind === 'drv' ? r[2] : r[1]}</td></tr>`).join('')}</tbody></table>`;
};

/* ============ PULPIT ============ */
S.pulpit = () => {
  const car = DB.car.areas.map(([n, p]) => `<div class="crow"><span>${n}</span><span class="num" style="color:${UI.rankColor(p, 16, 34)}">${p}.</span>${UI.rankBar(p)}</div>`).join('');
  const m = DB.monthly;
  return `<div class="dash">
    <section class="panel inbox"><header><h2>Skrzynka</h2><span class="meta">5 nowych</span></header>
      <div class="list">${DB.inbox.slice(0, 6).map(x => mailRow(x)).join('')}</div>
      <footer><a class="link" href="#/skrzynka">Otwórz skrzynkę${UI.icon(A, 15)}</a></footer></section>
    <div class="col">
      <section class="panel race"><div class="ring"></div>
        <h1>Brands Hatch</h1><div class="where">GP Wielkiej Brytanii · niedziela 18 lipca</div>
        <div class="facts"><div><span class="meta">Okrążenia</span><span class="num">76</span></div><div><span class="meta">Długość</span><span class="num">4,207 km</span></div>
          <div><span class="meta">Deszcz</span><span class="num">20%</span></div><div><span class="meta">Dopasowanie</span><span class="num good">4. z 16</span></div></div>
        <div class="voice"><span class="av">DG</span><div><p>„Tor nam leży: dużo hamowania i ciasnych nawrotów. Tracimy tylko na długiej prostej. Realnie walczymy o podium.”</p>
          <small>Derek Gardner · prognoza <b>P3–P6</b> · pewność średnia</small></div></div>
        <div class="strengths"><div><span class="meta">Proste</span><span class="bad">nasza słabość</span></div><div><span class="meta">Szybkie</span><span class="muted">przeciętnie</span></div>
          <div><span class="meta">Wolne</span><span class="good">nasza siła</span></div><div><span class="meta">Hamowanie</span><span class="good">nasza siła</span></div></div>
      </section>
      <section class="panel monthly"><div class="mast"><b>Paddock Monthly</b><span>lipiec 1976</span></div>
        <div class="stories"><div class="lead"><h3>${m.lead.title}</h3><p>Szwed ma dość Marcha. Jego ludzie pytają o miejsce na 1977, jeśli Scheckter odejdzie.</p><a class="link" href="#/monthly">${m.lead.link}${UI.icon(A, 15)}</a></div>
          <div class="side">${m.stories.slice(0, 3).map(s => `<a href="#/monthly"><div class="t">${s.title}</div><div class="s">${s.sec}</div></a>`).join('')}</div></div>
      </section>
    </div>
    <div class="col">
      <section class="panel car"><header><h2>Auto vs stawka</h2><span class="meta">16 aut</span></header>${car}<footer>Ocena naszego działu technicznego</footer></section>
      <section class="panel stand"><header><h2>Mistrzostwa</h2></header>
        <div class="tabs"><div class="seg wide" data-tabs><button class="on" data-tab="drv">Kierowcy</button><button data-tab="con">Konstruktorzy</button></div></div>
        ${standingsTable('drv')}${standingsTable('con')}
        <footer><a class="link" href="#/klasyfikacje">Pełne klasyfikacje${UI.icon(A, 15)}</a></footer></section>
    </div></div>`;
};
S.pulpit.fit = true;

/* ============ SKRZYNKA ============ */
S.skrzynka = (id) => {
  const cur = DB.inbox.find(m => m.id == id) || DB.inbox[0];
  const opts = cur.options ? `<div class="choices">${cur.options.map(o => `<button class="choice" data-toast="Wybrano: ${o.label}"><div><b>${o.label}</b><ul>${o.plus.map(p => `<li class="p">+ ${p}</li>`).join('')}${o.minus.map(p => `<li class="m">− ${p}</li>`).join('')}</ul></div><span class="arr">${UI.icon(A, 16)}</span></button>`).join('')}</div>` : '';
  return head('Skrzynka', '5 nowych · 1 decyzja czeka', `<div class="seg"><button class="on">Wszystkie</button><button>Decyzje</button><button>Raporty</button><button>Media</button></div>`) +
  `<div class="mailbox"><section class="panel list">${DB.inbox.map(m => mailRow(m, m.id === cur.id)).join('')}</section>
   <section class="panel reader"><div class="rhead"><span class="av big">${cur.av}</span><div><div class="muted">${cur.from} · ${cur.when}</div><h2>${cur.title}</h2></div>${cur.due ? `<span class="chip due" style="margin-left:auto">${cur.due}</span>` : ''}</div>
     <p class="letter">${cur.body}</p>${opts}
     ${cur.options ? '<p class="muted hint">Skutki każdej opcji widać przed wyborem. Decyzję można podjąć do terminu, a do tego czasu „Dalej” się tu zatrzyma.</p>' : ''}</section></div>`;
};

/* ============ KALENDARZ ============ */
S.kalendarz = () => head('Kalendarz 1976', '16 rund · 8 za nami') +
  `<div class="cal">${DB.calendar.map((r, i) => {
    const past = !!r[3], next = i === 8;
    return `<div class="rnd ${past ? 'past' : ''} ${next ? 'next' : ''}"><span class="num r">${i + 1}</span><div><span class="meta">${r[0]}</span><b>${r[1]}</b><small>${r[2]}</small></div>
      ${past ? `<div class="res"><span class="muted">wygrał ${r[3]}</span><span class="num">${r[4]}</span></div>` : next ? '<span class="chip team">następny</span>' : ''}</div>`;
  }).join('')}</div>`;

/* ============ KLASYFIKACJE ============ */
S.klasyfikacje = () => head('Klasyfikacje', 'Mistrzostwa świata F1 1976 · po 8 rundach',
  '<div class="seg"><button class="on">F1</button><button>Samochody sportowe</button><button>Formuła 2</button></div>') +
  `<div class="grid g2">
    ${UI.panel('Kierowcy', `<table class="table"><thead><tr><th>#</th><th>Kierowca</th><th>Zespół</th><th class="r">Pkt</th></tr></thead><tbody>${DB.standings.drivers.map((r, i) => `<tr class="${r[3] ? 'mine' : ''}"><td class="num">${i + 1}</td><td>${r[0]}</td><td class="muted">${r[1]}</td><td class="r num">${r[2]}</td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
    ${UI.panel('Konstruktorzy', `<table class="table"><thead><tr><th>#</th><th>Zespół</th><th class="r">Pkt</th></tr></thead><tbody>${DB.standings.constructors.map((r, i) => `<tr class="${r[2] ? 'mine' : ''}"><td class="num">${i + 1}</td><td>${r[0]}</td><td class="r num">${r[1]}</td></tr>`).join('')}</tbody></table>
      <footer>Punktacja 9-6-4-3-2-1 · liczy się 7 najlepszych wyników z każdej połowy sezonu</footer>`, { cls: 'tbl' })}
  </div>`;

/* ============ KIEROWCY ============ */
const AT = [['zakr','Zakręty'],['ham','Hamow.'],['pl','Płynność'],['wyp','Wyprz.'],['obr','Obrona'],['reg','Regul.'],['opa','Opan.'],['ada','Adapt.'],['desz','Deszcz'],['kon','Kond.'],['inf','Inf. zwr.']];
S.kierowcy = () => head('Kierowcy', 'Skład na 1976 · 2 kierowców wyścigowych i tester', '<a class="btn" href="#/porownaj">Porównaj</a><a class="btn primary" href="#/rynek">Szukaj na rynku</a>') +
  UI.panel('Skład', `<table class="table"><thead><tr><th>Kierowca</th><th>Ocena</th>${AT.map(a => `<th class="c">${a[1]}</th>`).join('')}<th>Forma</th><th>Kontrakt</th></tr></thead><tbody>
  ${DB.drivers.map(d => `<tr onclick="location.hash='#/kierowca/${d.id}'" style="cursor:pointer"><td><div class="person"><span class="av">${d.no}</span><div><b>${d.name}</b><small>${UI.flag(d.nat)} ${d.age} l. · ${d.role}</small></div></div></td>
    <td>${UI.stars(d.stars, d.pot)}</td>${AT.map(a => `<td class="c">${UI.attr(d.attrs[a[0]])}</td>`).join('')}<td>${d.form}</td><td class="muted">do ${d.contract.to}</td></tr>`).join('')}
  </tbody></table><footer>Kliknij kierowcę, żeby zobaczyć profil, kontrakt i obietnice.</footer>`, { cls: 'tbl' });

S.kierowca = (id) => {
  const d = DB.drivers.find(x => x.id === id) || DB.drivers[0];
  const attrs = AT.map(a => `<div class="arow"><span>${a[1]}</span>${UI.attr(d.attrs[a[0]])}<div class="bar thin"><i style="width:${d.attrs[a[0]] * 5}%;background:${d.attrs[a[0]] >= 17 ? 'var(--good)' : 'var(--ink)'}"></i></div></div>`).join('');
  return `<div class="profile">
    <section class="panel hero"><div class="num-big">${d.no}</div><div><h1 class="screen">${d.name}</h1><div class="sub">${UI.flag(d.nat)} ${d.age} l. · ${d.role} · Elf Team Tyrrell</div>
      <div class="stats" style="margin-top:14px"><div class="stat"><span class="meta">Ocena</span>${UI.stars(d.stars, d.pot)}</div><div class="stat"><span class="meta">Forma</span><b>${d.form}</b></div><div class="stat"><span class="meta">Morale</span><b>${d.morale}</b></div><div class="stat"><span class="meta">Zaufanie do zespołu</span><b class="num">${d.trust}/100</b></div></div></div>
      <div class="tools"><a class="btn" href="#/porownaj">Porównaj</a><a class="btn primary" href="#/kierowcy">Wróć do składu</a></div></section>
    <div class="grid" style="grid-template-columns:1.1fr 1fr 1fr">
      ${UI.panel('Atrybuty', `<div class="body attrs">${attrs}</div>`)}
      <div class="col">${UI.panel('Styl i cechy', `<div class="body"><dl class="kv"><dt>Balans</dt><dd>${d.prefs.balans}</dd><dt>Trakcja</dt><dd>${d.prefs.trakcja}</dd><dt>Hamowanie</dt><dd>${d.prefs.hamowanie}</dd></dl>
        <div class="chips">${d.traits.map(t => `<span class="chip">${t}</span>`).join('') || '<span class="muted">Cechy nieznane</span>'}</div>
        <p class="muted" style="margin-top:12px">Dopasowanie do P34: <b class="good">dobre</b>. Auto lekko nadsterowne, jak lubi.</p></div>`)}
        ${UI.panel('Kariera', `<div class="body"><div class="stats">${Object.entries({Starty:d.career.starts, Zwycięstwa:d.career.wins, 'Pole position':d.career.poles, Podia:d.career.podiums, Punkty:d.career.points}).map(([k, v]) => `<div class="stat"><span class="meta">${k}</span><span class="num">${v}</span></div>`).join('')}</div>
          <table class="table tight" style="margin-top:12px"><thead><tr><th>Sezon</th><th>Zespół</th><th class="r">Pkt</th><th class="r">Wygrane</th></tr></thead><tbody>${d.seasons.map(s => `<tr><td class="num">${s[0]}</td><td>${s[1]}</td><td class="r num">${s[2]}</td><td class="r num">${s[3]}</td></tr>`).join('')}</tbody></table></div>`)}</div>
      <div class="col">${UI.panel('Kontrakt', `<div class="body"><dl class="kv"><dt>Do końca</dt><dd>${d.contract.to}</dd><dt>Pensja</dt><dd>${d.contract.salary}</dd><dt>Premie</dt><dd>${d.contract.bonus}</dd></dl>
        <div class="meta" style="margin:14px 0 6px">Klauzule</div><ul class="clauses">${d.clauses.map(c => `<li>${c}</li>`).join('') || '<li class="muted">Brak</li>'}</ul></div>`)}
        ${d.promise ? UI.panel('Obietnica', `<div class="body"><b style="font-size:16px">${d.promise.text}</b><div class="bar" style="margin:10px 0 6px"><i style="width:${d.promise.pct}%;background:var(--t2)"></i></div><span class="muted">Postęp prac ${d.promise.pct}% · termin za ${d.promise.left}</span>
          <p class="muted" style="margin-top:10px">Jeśli jej nie dotrzymamy, zaufanie spadnie, a klauzula wyjścia stanie się realna.</p></div>`, { cls: 'promise' }) : ''}</div>
    </div></div>`;
};

S.porownaj = () => {
  const [a, b] = DB.drivers;
  return head('Porównanie', `${a.name} vs ${b.name}`, '<div class="seg"><button class="on">Kariera</button><button>Sezon 1976</button><button>Pojedynki</button></div>') +
  `<div class="grid g2">${UI.panel('Atrybuty', `<div class="body cmp">${AT.map(x => { const va = a.attrs[x[0]], vb = b.attrs[x[0]];
    return `<div class="cmprow"><span class="num ${va > vb ? 'good' : ''}">${va}</span><div class="bar thin rev"><i style="width:${va * 5}%"></i></div><span class="lbl">${x[1]}</span><div class="bar thin"><i style="width:${vb * 5}%;background:var(--t2)"></i></div><span class="num ${vb > va ? 'good' : ''}">${vb}</span></div>`; }).join('')}</div>`)}
    ${UI.panel('Pojedynki w 1976', `<div class="body"><div class="duel"><div><span class="num big">6</span><span class="muted">kwalifikacje</span></div><span class="vs">:</span><div><span class="num big">2</span><span class="muted">kwalifikacje</span></div></div>
      <div class="duel"><div><span class="num big">5</span><span class="muted">wyścigi</span></div><span class="vs">:</span><div><span class="num big">3</span><span class="muted">wyścigi</span></div></div>
      <p class="muted">Średnia różnica w kwalifikacjach: <b>0,21 s</b> na korzyść Schecktera. Depailler szybszy w deszczu (2 z 2).</p></div>`)}</div>`;
};

/* ============ PERSONEL ============ */
S.personel = () => head('Personel', '6 kluczowych osób · 34 pracowników w działach', '<a class="btn primary" href="#/rynek">Zatrudnij</a>') +
  `<div class="grid" style="grid-template-columns:1.4fr 1fr">
  ${UI.panel('Kluczowi ludzie', `<table class="table"><thead><tr><th>Osoba</th><th>Ocena</th><th>Atrybuty</th><th>Kontrakt</th></tr></thead><tbody>${DB.staff.map(s => `<tr><td><div class="person"><span class="av">${UI.initials(s.name)}</span><div><b>${s.name}</b><small>${s.role}${s.note ? ' · ' + s.note : ''}</small></div></div></td><td>${UI.stars(s.stars)}</td>
    <td>${Object.entries(s.attrs).map(([k, v]) => `<span class="sattr">${k} ${UI.attr(v)}</span>`).join('')}</td><td class="muted">${s.contract}</td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
  ${UI.panel('Działy', `<div class="body">${DB.departments.map(d => `<div class="dept"><div><b>${d.name}</b><small class="muted">szef: ${d.head} · ${d.cost}/mies.</small></div><div class="dnum"><span class="num">${d.people}</span><span class="meta">osób</span></div><div class="dq">${UI.attr(d.quality)}<span class="meta">jakość</span></div></div>`).join('')}
    <p class="muted" style="margin-top:12px">Więcej ludzi = szybciej ukończone projekty. Jakość wyniku zależy od kluczowych osób i infrastruktury.</p></div>`)}
  </div>`;

/* ============ AKADEMIA ============ */
S.akademia = () => head('Akademia', 'Juniorzy, których finansujesz poza F1', '<a class="btn primary" href="#/rynek">Znajdź talent</a>') +
  `<div class="grid g2">${DB.academy.map(j => UI.panel(j.name, `<div class="body"><div class="stats"><div class="stat"><span class="meta">Obecnie</span>${UI.stars(j.stars, j.pot)}</div><div class="stat"><span class="meta">Seria</span><b>${j.series}</b></div><div class="stat"><span class="meta">Wiek</span><span class="num">${j.age}</span></div></div>
    <div class="meta" style="margin:16px 0 6px">Program</div><b>${j.program}</b><div class="bar" style="margin-top:8px"><i style="width:${j.progress}%"></i></div>
    <p class="muted" style="margin-top:10px">Potencjał (gwiazdki przerywane) to ocena skauta, nie pewnik.</p></div>`, { right: UI.flag(j.nat) })).join('')}
  ${UI.panel('Pula talentów', `<div class="body"><p class="muted">Kierowcy spoza F1, którzy mogą trafić do akademii: Formuła 2, F3, Formuła Atlantic. Najciekawsi według skauta:</p>
    <div class="chips" style="margin-top:12px"><span class="chip">Gilles Villeneuve · Atlantic</span><span class="chip">Riccardo Patrese · F3</span><span class="chip">Eddie Cheever · F3</span><span class="chip">Didier Pironi · F2</span></div></div>`, { style: 'grid-column:1/3' })}</div>`;

/* ============ AUTO I ROZWÓJ ============ */
S.auto = () => {
  const c = DB.car;
  return head('Auto i rozwój', `${c.name} · ${c.concept}`, '<a class="btn primary">Nowy projekt</a>') +
  `<div class="grid" style="grid-template-columns:1fr 1.2fr 1fr">
    ${UI.panel('Na tle stawki', `<div class="body">${c.areas.map(([n, p]) => `<div class="crow"><span>${n}</span><span class="num" style="color:${UI.rankColor(p, 16, 34)}">${p}.</span>${UI.rankBar(p)}</div>`).join('')}</div><footer>Ocena działu technicznego · pewność średnia</footer>`)}
    <div class="col">${UI.panel('Projekty', `<div class="body">${c.projects.map(p => `<div class="proj"><div class="prow"><b>${p.name}</b><span class="chip">${p.stream}</span></div><div class="bar thin" style="margin:8px 0 5px"><i style="width:${p.pct}%;background:${p.stream === 'Przyszły rok' ? 'var(--t2)' : 'var(--t1)'}"></i></div><span class="muted">${p.pct}% · ${p.eta} · ${p.note}</span></div>`).join('')}</div>`)}
      ${UI.panel('Podział zasobów', `<div class="body"><div class="split"><i style="flex:${c.split[0]};background:var(--t1)">Bieżące ${c.split[0]}%</i><i style="flex:${c.split[1]};background:var(--a2)">Konto ${c.split[1]}%</i><i style="flex:${c.split[2]};background:var(--t2)">1977 ${c.split[2]}%</i></div>
        <p class="muted" style="margin-top:10px">Na koncie rozwoju: <b>${c.bank}</b>. Straci ok. 30% wartości, jeśli regulamin 1977 zmieni wymiary kół.</p></div>`)}</div>
    <div class="col">${UI.panel('Koncepcja', `<div class="body">${c.axes.map(a => `<div class="axis"><div class="alab"><span>${a[1]}</span><b>${a[0]}</b><span>${a[2]}</span></div><div class="atrack"><i style="left:${a[3]}%"></i></div></div>`).join('')}</div>`)}
      ${UI.panel('Zrozumienie części', `<div class="body">${c.understanding.map(u => `<div class="crow"><span>${u[0]}</span><span class="num">${u[1]}%</span><div class="bar thin"><i style="width:${u[1]}%;background:var(--t1)"></i></div></div>`).join('')}</div>`)}</div>
  </div>`;
};

/* ============ INFRASTRUKTURA ============ */
S.infrastruktura = () => head('Infrastruktura', 'Jakość liczona względem stanu techniki w 1976 roku') +
  `<div class="grid g3">${DB.facilities.map(f => UI.panel(f.name, `<div class="body"><div class="meta">Obecnie</div><b style="font-size:17px">${f.level}</b>
    <div class="meta" style="margin:14px 0 6px">Względem najlepszych w stawce</div><div class="bar"><i style="width:${f.frontier}%;background:${UI.rankColor(16 - Math.round(f.frontier / 100 * 15), 16)}"></i></div>
    <p class="muted" style="margin-top:10px">${f.note}</p></div>${f.cost ? `<footer><button class="btn" data-toast="Projekt dodany do planu">Rozbuduj · ${f.cost}</button></footer>` : ''}`)).join('')}
    ${UI.panel('Dlaczego to nigdy się nie kończy', `<div class="body"><p class="muted">Stan techniki przesuwa się co roku razem z całą stawką. Tunel, który dziś jest najlepszy, za dziesięć lat będzie przeciętny. Nowe rodzaje obiektów (CFD, symulator) pojawią się z kolejnymi epokami.</p></div>`)}</div>`;

/* ============ DOSTAWCY ============ */
S.dostawcy = () => head('Dostawcy', 'Umowy na silniki, opony, paliwo i części', '<a class="btn primary">Szukaj dostawcy</a>') +
  UI.panel('Umowy', `<table class="table"><thead><tr><th>Kategoria</th><th>Dostawca</th><th>Typ umowy</th><th>Do</th><th>Plusy</th><th>Minusy</th></tr></thead><tbody>${DB.suppliers.map(s => `<tr><td class="muted">${s.cat}</td><td><b>${s.name}</b></td><td><span class="chip ${s.type === 'Fabryczna' ? 'team' : s.type === 'Partner' ? 'ok' : ''}">${s.type}</span></td><td class="num">${s.to}</td><td class="good">${s.pros}</td><td class="bad">${s.cons}</td></tr>`).join('')}</tbody></table>
  <footer>Umowa fabryczna z Goodyear kończy się w tym roku. Decyzja czeka w skrzynce.</footer>`, { cls: 'tbl' });

/* ============ SPONSORZY ============ */
S.sponsorzy = () => head('Sponsorzy', '£265 tys. rocznie · 1 wolne miejsce na aucie', '<a class="btn primary">Szukaj sponsora</a>') +
  `<div class="grid g2">${DB.sponsors.map(s => UI.panel(s.slot, `<div class="body"><h3 style="font-size:24px">${s.name}</h3><div class="muted">${s.ind}</div>
    <dl class="kv" style="margin-top:14px"><dt>Kwota</dt><dd>${s.amount}</dd><dt>Umowa do</dt><dd>${s.to || '—'}</dd><dt>Cel</dt><dd class="${s.goalOk ? 'good' : ''}">${s.goal}</dd></dl></div>`, { right: s.slot === 'Tytularny' ? '<span class="chip team">nazwa zespołu</span>' : '' })).join('')}</div>`;

/* ============ FINANSE ============ */
S.finanse = () => {
  const f = DB.finance, max = 40;
  const chart = f.months.map(([v, m]) => `<div class="mcol"><div class="mbar ${v == null ? 'fut' : v < 0 ? 'neg' : 'pos'}" style="height:${v == null ? 6 : Math.abs(v) / max * 100}%"></div><span class="meta">${m}</span></div>`).join('');
  const list = (arr) => arr.map(([n, v]) => `<div class="crow"><span>${n}</span><span class="num">£${v} tys.</span><div class="bar thin"><i style="width:${v / 2.7}%"></i></div></div>`).join('');
  return head('Finanse', 'Sezon 1976 · gotówka to nie budżet') +
  `<div class="grid" style="grid-template-columns:1fr 1fr 1fr">
    ${UI.panel('Stan', `<div class="body"><dl class="kv big"><dt>Gotówka</dt><dd>£410 tys.</dd><dt>Zobowiązania do końca roku</dt><dd class="bad">−£290 tys.</dd><dt>Pewne wpływy</dt><dd class="good">+£205 tys.</dd><dt>Wolne środki</dt><dd class="good">£120 tys.</dd></dl>
      <p class="muted" style="margin-top:12px">Prognoza na koniec sezonu: <b>+£35 tys.</b>, jeśli utrzymamy 2. miejsce.</p></div>`)}
    ${UI.panel('Wpływy', `<div class="body">${list(f.income)}</div>`)}
    ${UI.panel('Koszty', `<div class="body">${list(f.costs)}</div>`)}
    ${UI.panel('Wynik miesiąc po miesiącu', `<div class="body"><div class="mchart">${chart}</div></div>`, { style: 'grid-column:1/4' })}
  </div>`;
};

/* ============ ZARZĄD ============ */
S.zarzad = () => {
  const b = DB.board;
  return head('Zarząd', `Właściciel: ${b.owner}`) +
  `<div class="grid" style="grid-template-columns:1fr 1.4fr">
    ${UI.panel('Nastrój', `<div class="body"><h3 style="font-size:30px" class="good">${b.mood}</h3><dl class="kv" style="margin-top:14px"><dt>Cierpliwość</dt><dd>${b.patience}</dd><dt>Twoja reputacja</dt><dd class="num">${b.rep}/100</dd></dl>
      <div class="bar" style="margin-top:14px"><i style="width:${b.rep}%"></i></div></div>`)}
    ${UI.panel('Cele', `<div class="body">${b.goals.map(g => `<div class="goal"><span class="gi ${g[1] ? 'ok' : 'open'}">${g[1] ? UI.icon('<path d="M5 12l5 5 9-10"/>', 16) : UI.icon('<circle cx="12" cy="12" r="7"/>', 16)}</span><b>${g[0]}</b><span class="muted" style="margin-left:auto">${g[2]}</span></div>`).join('')}</div>`)}
  </div>`;
};

/* ============ RYNEK ============ */
S.rynek = () => head('Rynek kierowców', `${DB.market.length} kierowców · sortuj, klikając nagłówek`,
  `<div class="seg" id="mkt-stars"><button class="on" data-min="0">Wszyscy</button><button data-min="3">3★+</button><button data-min="3.5">3½★+</button><button data-min="4">4★+</button></div>
   <label class="search">${UI.icon('<circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/>', 16)}<input id="mkt-q" placeholder="Szukaj kierowcy"></label>`) +
  `<section class="panel tbl market"><table class="table" id="mkt"><thead><tr>
    <th data-sort="0">Kierowca</th><th data-sort="2" class="r">Wiek</th><th data-sort="3">Obecnie</th><th data-sort="4" class="sorted">Ocena</th><th data-sort="5">Potencjał</th><th data-sort="6" class="r">Kontrakt do</th><th data-sort="7">Nastawienie</th><th></th></tr></thead><tbody></tbody></table></section>`;
S.rynek.after = () => {
  let key = 4, asc = false, min = 0, q = '';
  const tb = document.querySelector('#mkt tbody');
  const mood = { 'bardzo zainteresowany':'ok', 'zainteresowany':'ok', 'neutralny':'', 'raczej nie':'warn' };
  const draw = () => {
    const rows = DB.market.filter(r => r[4] >= min && r[0].toLowerCase().includes(q)).sort((a, b) => (a[key] > b[key] ? 1 : a[key] < b[key] ? -1 : 0) * (asc ? 1 : -1));
    tb.innerHTML = rows.map(r => `<tr><td><div class="person"><span class="av">${UI.initials(r[0])}</span><div><b>${r[0]}</b><small>${UI.flag(r[1])} ${r[1]}</small></div></div></td><td class="r num">${r[2]}</td><td>${r[3]}</td><td>${UI.stars(r[4])}</td><td>${UI.stars(r[4], r[5])}</td><td class="r num">${r[6]}</td><td><span class="chip ${mood[r[7]]}">${r[7]}</span></td><td class="r"><button class="btn" data-toast="Rozpoczęto rozmowy z: ${r[0]}">Rozmawiaj</button></td></tr>`).join('');
  };
  document.querySelectorAll('#mkt th[data-sort]').forEach(th => th.onclick = () => {
    const k = +th.dataset.sort; asc = key === k ? !asc : k === 0 || k === 3; key = k;
    document.querySelectorAll('#mkt th').forEach(x => x.classList.remove('sorted', 'asc')); th.classList.add('sorted'); if (asc) th.classList.add('asc'); draw();
  });
  document.querySelectorAll('#mkt-stars button').forEach(b => b.onclick = () => { min = +b.dataset.min; document.querySelectorAll('#mkt-stars button').forEach(x => x.classList.toggle('on', x === b)); draw(); });
  document.getElementById('mkt-q').oninput = e => { q = e.target.value.toLowerCase(); draw(); };
  draw();
};

/* ============ PADDOCK MONTHLY ============ */
S.monthly = () => {
  const m = DB.monthly;
  return `<div class="magazine"><header class="mhead"><b>Paddock Monthly</b><span>${m.issue}</span>
      <nav class="msecs">${['Wszystko','Rynek','Wyścigi','Talenty','Technika','Pieniądze','Z historii'].map((x, i) => `<a class="${i ? '' : 'on'}">${x}</a>`).join('')}</nav></header>
    <div class="mgrid"><article class="mlead"><span class="msec">${m.lead.sec}</span><h1>${m.lead.title}</h1><p>${m.lead.text}</p><a class="link" href="#/rynek">${m.lead.link}${UI.icon(A, 15)}</a></article>
      ${m.stories.map(s => `<article class="mstory"><span class="msec">${s.sec}</span><h2>${s.title}</h2><p>${s.text}</p><a class="link" href="#/klasyfikacje">${s.link}${UI.icon(A, 15)}</a></article>`).join('')}</div></div>`;
};

/* ============ FIA ============ */
S.fia = () => head('FIA i regulamin', 'Przepisy sezonu 1976 i propozycje zmian') +
  `<div class="grid g2">${UI.panel('Obowiązuje teraz', `<div class="body"><dl class="kv">${DB.fia.rules.map(r => `<dt>${r[0]}</dt><dd>${r[1]}</dd>`).join('')}</dl></div>`)}
  <div class="col">${DB.fia.proposals.map(p => UI.panel(p.title, `<div class="body"><div class="muted">${p.from} · ${p.vote}</div><p style="margin:10px 0 14px">Wpływ na nas: <b>${p.effect}</b></p>
    <div class="seg"><button class="${p.stance === 'za' ? 'on' : ''}" data-toast="Głos: za">Za</button><button data-toast="Wstrzymano się">Wstrzymaj się</button><button data-toast="Głos: przeciw">Przeciw</button></div></div>`)).join('')}</div></div>`;

/* ============ KRONIKA ============ */
S.kronika = () => head('Kronika', 'Twoja historia obok prawdziwej') +
  `<section class="panel tbl"><div class="timeline">${DB.chronicle.map(c => `<div class="tl"><span class="meta">${c[0]}</span><div><b>${c[1]}</b><p class="muted">${c[2]}</p></div></div>`).join('')}</div></section>`;

/* ============ MENEDŻER ============ */
S.menedzer = () => head('M. Wojnar', 'Szef zespołu · 2. sezon w F1') +
  `<div class="grid g3">${UI.panel('Reputacja', `<div class="body"><span class="num" style="font-size:44px">68</span><span class="muted"> / 100</span><div class="bar" style="margin-top:10px"><i style="width:68%"></i></div><p class="muted" style="margin-top:10px">Rośnie: zwycięstwo w Szwecji, 2. miejsce wśród konstruktorów.</p></div>`)}
   ${UI.panel('Kariera', `<div class="body"><dl class="kv"><dt>Zespoły</dt><dd>Tyrrell (1975–)</dd><dt>Wyścigi</dt><dd class="num">22</dd><dt>Zwycięstwa</dt><dd class="num">1</dd><dt>Tytuły</dt><dd class="num">0</dd></dl></div>`)}
   ${UI.panel('Oferty pracy', `<div class="body"><p class="muted">Brak ofert. Przy obecnej reputacji mógłbyś dostać propozycję od zespołu ze środka stawki.</p></div>`)}</div>`;

/* ============ USTAWIENIA ============ */
S.ustawienia = () => head('Ustawienia') +
  `<div class="grid g2">${UI.panel('Wygląd', `<div class="body set">
    <div class="srow"><div><b>Kolory interfejsu</b><small class="muted">Barwy epoki albo barwy zespołu</small></div><div class="seg" data-set="style"><button data-v="era">Era</button><button data-v="team">Zespół</button></div></div>
    <div class="srow"><div><b>Skórka epoki</b><small class="muted">Zmienia się z dekadą albo jest stała</small></div><div class="seg"><button class="on">Automatycznie</button><button>1970s</button><button>1990s</button><button>2020s</button></div></div>
    <div class="srow"><div><b>Podgląd zespołu</b><small class="muted">Tylko w prototypie</small></div><div class="sw" data-set="team"><button data-v="tyrrell" style="background:linear-gradient(135deg,#1f4f9a 50%,#e03a3e 50%)"></button><button data-v="lotus" style="background:linear-gradient(135deg,#16130e 50%,#c9a24a 50%)"></button><button data-v="ferrari" style="background:linear-gradient(135deg,#c4161c 50%,#f5c518 50%)"></button></div></div>
    <div class="srow"><div><b>Animowane tło</b><small class="muted">Smugi dymu jak w tunelu aerodynamicznym</small></div><div class="seg" data-set="air"><button data-v="on">Włączone</button><button data-v="off">Wyłączone</button></div></div>
  </div>`)}
  ${UI.panel('Rozgrywka', `<div class="body set">
    <div class="srow"><div><b>Tryb bez liczb</b><small class="muted">Wszystko z opinii Twoich ludzi</small></div><div class="seg"><button class="on">Liczby</button><button>Opinie</button></div></div>
    <div class="srow"><div><b>Oglądanie wyścigu</b><small class="muted">Domyślne tempo relacji</small></div><div class="seg"><button>×5</button><button class="on">×10</button><button>×20</button><button>Wynik</button></div></div>
    <div class="srow"><div><b>Pit-stopy</b><small class="muted">Moduł ręcznej kontroli</small></div><div class="seg"><button class="on">Strateg</button><button>Ręcznie</button></div></div>
    <div class="srow"><div><b>Język</b></div><div class="seg"><button class="on">Polski</button><button>English</button></div></div>
  </div>`)}</div>`;
