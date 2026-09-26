/* Ekrany prototypu (część 2). */

/* ============ PERSONEL ============ */
S.personel = () => {
  const people = DB.departments.reduce((a, d) => a + d.people, 0);
  const monthly = DB.departments.reduce((a, d) => a + d.people * d.perHead, 0) + DB.staff.reduce((a, s) => a + s.contract.salary / 12, 0);
  const staffName = id => (DB.staff.find(s => s.id === id) || {}).name;
  return head('Personel', UI.fields([{ k: 'Kluczowe osoby', v: DB.staff.length, num: 1 }, { k: 'Pracownicy działów', v: people, num: 1 }, { k: 'Koszt miesięcznie', v: UI.money(Math.round(monthly)), num: 1 }]), UI.btn('Zatrudnij', { href: '#/rynek', cls: 'primary' })) +
  `<div class="staff-grid">
  ${UI.panel('Kluczowi ludzie', `<table class="table"><thead><tr><th>Osoba</th><th>Ocena</th><th>Atrybuty</th><th class="c">Kontrakt</th></tr></thead><tbody>${DB.staff.map(s => `<tr class="go-row" data-href="#/osoba/${s.id}"><td><a class="person" href="#/osoba/${s.id}"><span class="av">${UI.initials(s.name)}</span><div><b>${s.name}</b><small>${UI.flag(s.nat)} ${s.role}</small></div></a></td><td>${UI.stars(s.stars)}</td>
    <td><div class="sattrs">${s.attrs.slice(0, 3).map(([k, v]) => `<span>${k} ${UI.attr(v)}</span>`).join('')}</div></td><td class="c num ${s.contract.to === 1976 ? 'bad' : ''}">${s.contract.to}</td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
  ${UI.panel('Działy', `<table class="table depts"><thead><tr><th>Dział</th><th class="c">Ludzie</th><th class="c">Jakość</th><th class="r">Koszt</th></tr></thead><tbody>${DB.departments.map(d => `<tr>
    <td><b>${d.name}</b><small class="dhead">${d.head ? `<a href="#/osoba/${d.head}">${staffName(d.head)}</a>` : UI.st('Brak szefa', 'warn')}</small></td>
    <td class="c"><b class="num big-n">${d.people}</b><small class="muted"> ${UI.plural(d.people, 'osoba', 'osoby', 'osób')}</small></td>
    <td class="c"><b class="num big-n">${d.quality}</b><small class="muted"> / 20</small></td>
    <td class="r num">${UI.money(Math.round(d.people * d.perHead * 10) / 10)}<small class="muted">/mies.</small></td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
  </div>`;
};

S.osoba = (id) => {
  const s = DB.staff.find(x => x.id === id) || DB.staff[0];
  const drv = s.driver ? DB.drivers.find(d => d.id === s.driver) : null;
  return `<div class="profile">
    <section class="panel hero staff"><div class="num-big"><span>${UI.initials(s.name)}</span></div>
      <div class="hero-main"><h1 class="screen">${s.name}</h1>
        ${UI.fields([{ k: 'Narodowość', v: `${UI.flag(s.nat, 'md')} ${FLAGS.name(s.nat)}` }, { k: 'Wiek', v: `${s.age} lat`, num: 1 }, { k: 'Rola', v: s.role }, { k: 'W zespole od', v: s.since, num: 1 }], 'mid')}</div>
      <div class="hero-side">${UI.fields([{ k: 'Ocena', v: UI.stars(s.stars).replace('class="stars"', 'class="stars lg"') }, { k: 'Morale', v: s.mood }, drv ? { k: 'Kierowca', v: `<a href="#/kierowca/${drv.id}">${drv.name}</a>` } : null])}
        <div class="tools">${UI.btn('Personel', { href: '#/personel', icon: UI.back })}</div></div></section>
    <div class="prof-grid s">
      ${UI.panel('Atrybuty', `<div class="body"><div class="attrs">${s.attrs.map(([k, v]) => `<div class="arow"><span>${k}</span>${UI.attr(v)}<div class="bar thin"><i style="width:${v * 5}%;background:${v >= 17 ? 'var(--good)' : 'var(--ink)'}"></i></div></div>`).join('')}</div>
        <div class="persona">${UI.fields([{ k: 'Ambicja', v: s.ambition }, { k: 'Lojalność', v: s.loyalty }, { k: 'Staż w zespole', v: UI.n(1976 - s.since, 'rok', 'lata', 'lat'), num: 1 }], 'boxed eq')}</div></div>`)}
      <div class="col">${UI.panel('Kariera', `<table class="table tight"><thead><tr><th>Lata</th><th>Zespół</th><th>Rola</th></tr></thead><tbody>${s.career.map(c => `<tr><td class="num">${c[0]}</td><td><b>${c[1]}</b></td><td class="muted wrap">${c[2]}</td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
        ${UI.panel('Kontrakt', `<div class="body">${UI.fields([{ k: 'Do końca', v: s.contract.to, num: 1, cls: s.contract.to === 1976 ? 'bad' : '' }, { k: 'Pensja', v: `${UI.money(s.contract.salary)}/rok`, num: 1 }])}</div>`)}</div>
    </div></div>`;
};

/* ============ AKADEMIA ============ */
S.akademia = () => {
  const ac = DB.academy, free = ac.slots - ac.juniors.length;
  const slot = j => `<section class="panel slot"><header><div class="slot-no meta">Miejsce ${ac.juniors.indexOf(j) + 1}</div>${UI.st(j.status[0], j.status[1])}</header>
    <div class="body"><a class="person big" href="#/kierowca/${j.id}"><span class="av">${UI.initials(j.name)}</span><div><b>${j.name}</b><small>${UI.flag(j.nat)} ${j.age} lat · ${j.series}</small></div></a>
      ${UI.fields([{ k: 'Ocena', v: UI.stars(j.stars, j.pot) }, { k: 'W akademii', v: `${j.since}–${j.to}`, num: 1 }, { k: 'Koszt', v: `${UI.money(j.cost)}/rok`, num: 1 }], 'row1')}
      ${UI.fields(j.season.map(([k, v]) => ({ k, v, num: 1 })), 'boxed center')}
      <div class="next"><span class="meta">Następny krok</span><b>${j.next}</b></div></div></section>`;
  return head('Akademia', UI.fields([{ k: 'Miejsca', v: `${ac.juniors.length} z ${ac.slots}`, num: 1 }, { k: 'Koszt roczny', v: UI.money(ac.juniors.reduce((a, j) => a + j.cost, 0)), num: 1 }])) +
  `<div class="acad-grid">${ac.juniors.map(slot).join('')}${free ? `<section class="panel slot empty-slot"><header><div class="slot-no meta">Miejsce ${ac.slots}</div>${UI.st('Wolne')}</header><div class="body"><div class="plus">+</div></div></section>` : ''}
    ${UI.panel('Pula talentów', `<table class="table tight"><thead><tr><th>Kierowca</th><th class="c">Wiek</th><th>Seria</th><th>Ocena</th><th>Potencjał</th><th class="c">Wiedza</th></tr></thead><tbody>${ac.pool.map(p => `<tr class="go-row" data-href="#/kierowca/${p.id}"><td><a class="person" href="#/kierowca/${p.id}"><span class="av">${UI.initials(p.name)}</span><div><b>${p.name}</b><small>${UI.flag(p.nat)} ${FLAGS.name(p.nat)}</small></div></a></td><td class="c num">${p.age}</td><td>${p.team}</td><td>${UI.stars(p.band[0], 0, p.band)}</td><td>${UI.stars(0, p.pot)}</td><td class="c num">${p.known}%</td></tr>`).join('')}</tbody></table>`, { cls: 'tbl pool' })}</div>`;
};

/* ============ AUTO I ROZWÓJ (bez zmian systemu: czeka na decyzję o PP) ============ */
S.auto = () => {
  const c = DB.car;
  return head('Auto i rozwój', UI.fields([{ k: 'Auto', v: c.name }, { k: 'Koncepcja', v: c.concept }])) +
  `<div class="grid" style="grid-template-columns:1fr 1.2fr 1fr">
    ${UI.panel('Na tle stawki', `<div class="body">${c.areas.map(([n, p]) => `<div class="crow"><span>${n}</span><span class="num" style="color:${UI.rankColor(p, 16, 34)}">${p}.</span>${UI.rankBar(p)}</div>`).join('')}</div>`)}
    <div class="col">${UI.panel('Projekty', `<div class="body">${c.projects.map(p => `<div class="proj"><div class="prow"><b>${p.name}</b>${UI.st(p.stream, p.stream === 'Przyszły rok' ? 'hi' : 'team')}</div><div class="bar thin" style="margin:8px 0 6px"><i style="width:${p.pct}%;background:${p.stream === 'Przyszły rok' ? 'var(--t2)' : 'var(--t1)'}"></i></div>${UI.fields([{ k: 'Postęp', v: p.pct + '%', num: 1 }, { k: 'Koniec za', v: p.eta, num: 1 }])}</div>`).join('')}</div>`)}
      ${UI.panel('Podział zasobów', `<div class="body"><div class="split"><i style="flex:${c.split[0]};background:var(--t1)">Bieżące ${c.split[0]}%</i><i style="flex:${c.split[1]};background:var(--a2)">Konto ${c.split[1]}%</i><i style="flex:${c.split[2]};background:var(--t2)">1977 ${c.split[2]}%</i></div>
        ${UI.fields([{ k: 'Konto rozwoju', v: c.bank, num: 1 }, { k: 'Utrata przy zmianie kół w 1977', v: '−30%', num: 1, cls: 'bad' }], '') .replace('class="fields ', 'style="margin-top:14px" class="fields ')}</div>`)}</div>
    <div class="col">${UI.panel('Koncepcja', `<div class="body">${c.axes.map(a => `<div class="axis"><div class="alab"><span>${a[1]}</span><b>${a[0]}</b><span>${a[2]}</span></div><div class="atrack"><i style="left:${a[3]}%"></i></div></div>`).join('')}</div>`)}
      ${UI.panel('Zrozumienie części', `<div class="body">${c.understanding.map(u => `<div class="crow"><span>${u[0]}</span><span class="num">${u[1]}%</span><div class="bar thin"><i style="width:${u[1]}%;background:var(--t1)"></i></div></div>`).join('')}</div>`)}</div>
  </div>`;
};

S.rywal = (k = 'ferrari') => {
  const r = DB.rivals[k] || DB.rivals.ferrari;
  return head(r.car, UI.fields([{ k: 'Zespół', v: r.team }, { k: 'Silnik', v: r.engine }, { k: 'Wiedza', v: r.known + '%', num: 1 }]), UI.btn('Nasze auto', { href: '#/auto' })) +
  `<div class="grid" style="grid-template-columns:minmax(0,420px) minmax(0,1fr)">
    ${UI.panel('Na tle stawki', `<div class="body">${r.areas.map(([n, p]) => `<div class="crow"><span>${n}</span><span class="num" style="color:${UI.rankColor(p, 16, 34)}">${p}.</span>${UI.rankBar(p)}</div>`).join('')}
      <div style="margin-top:14px">${UI.fields([{ k: 'Moc', v: r.power, num: 1 }, { k: 'Masa', v: r.weight, num: 1 }])}</div></div>`)}
    ${UI.panel('Zmiany w sezonie', `<table class="table"><thead><tr><th>Kiedy</th><th>Co</th><th>Szacunek</th></tr></thead><tbody>${r.news.map(n => `<tr><td class="num">${n[0]}</td><td><b>${n[1]}</b></td><td class="muted wrap">${n[2]}</td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
  </div>`;
};

/* ============ INFRASTRUKTURA ============ */
S.infrastruktura = (sel = 'tunel') => {
  const f = DB.facilities.find(x => x.id === sel) || DB.facilities[0];
  return head('Infrastruktura', UI.fields([{ k: 'Utrzymanie miesięcznie', v: UI.money(DB.facilities.reduce((a, x) => a + x.upkeep, 0)), num: 1 }])) +
  `<div class="infra">
    ${UI.panel('Obiekty', `<table class="table fac"><thead><tr><th>Obiekt</th><th class="c">Poziom</th><th>Względem lidera</th><th>Lider stawki</th></tr></thead><tbody>${DB.facilities.map(x => `<tr class="go-row ${x.id === f.id ? 'sel' : ''}" data-href="#/infrastruktura/${x.id}"><td><b>${x.name}</b></td><td class="c num"><span class="lvl">${x.level}</span></td>
      <td><div class="rel"><div class="bar"><i style="width:${x.pct}%;background:${UI.rankColor(16 - Math.round(x.pct / 100 * 15), 16)}"></i></div><span class="num">${x.pct}%</span></div></td><td>${x.leader[0]}<small class="muted"> · ${x.leader[1]}</small></td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
    <section class="panel fac-detail"><header><h2>${f.name}</h2>${UI.st('Poziom ' + f.level, 'team')}</header>
      <div class="body">${UI.fields(f.params.map(([k, v]) => ({ k, v })), 'boxed')}
        <div class="fac-cmp">${UI.fields([{ k: 'My', v: `${f.pct}%`, num: 1 }, { k: `Lider: ${f.leader[0]}`, v: f.leader[1] }, { k: 'Utrzymanie', v: `${UI.money(f.upkeep)}/mies.`, num: 1 }])}</div>
        <div class="upgrade"><h3>Rozbudowa</h3><b>${f.upgrade.what}</b>
          ${UI.fields([{ k: 'Koszt', v: UI.money(f.upgrade.cost), num: 1, cls: f.upgrade.cost > DB.money.free ? 'bad' : '' }, { k: 'Czas', v: UI.n(f.upgrade.months, 'miesiąc', 'miesiące', 'miesięcy'), num: 1 }, { k: 'Efekt', v: f.upgrade.effect }])}
          <div class="confirm">${UI.fields([{ k: 'Wolne środki po', v: UI.money(DB.money.free - f.upgrade.cost), num: 1, cls: DB.money.free - f.upgrade.cost < 0 ? 'bad' : 'good' }])}
            <button class="btn primary" data-toast="Rozbudowa zaplanowana: ${f.name}"${f.upgrade.cost > DB.money.free ? ' disabled' : ''}>${UI.icon(UI.check, 17)}<span>Potwierdź rozbudowę</span></button></div></div></div></section>
  </div>`;
};

/* ============ DOSTAWCY ============ */
S.dostawcy = () => head('Dostawcy', UI.fields([{ k: 'Umowy', v: DB.suppliers.length, num: 1 }, { k: 'Bilans roczny', v: UI.money(DB.suppliers.reduce((a, s) => a + s.cost, 0), true), num: 1, cls: 'bad' }])) +
  `<div class="sup-list">${DB.suppliers.map(s => `<section class="panel sup">
    <div class="sup-id"><span class="meta">${s.cat}</span><b>${s.name}</b></div>
    ${UI.fields([{ k: 'Umowa', v: UI.st(s.type, s.type === 'Fabryczna' ? 'team' : s.type === 'Partnerska' ? 'hi' : '') }, { k: 'Do', v: s.to, num: 1, cls: s.to === 1976 ? 'bad' : '' }, { k: 'Rocznie', v: s.cost ? UI.money(s.cost, true) : '£0', num: 1, cls: s.cost > 0 ? 'good' : '' }], 'sup-core')}
    ${UI.fields(s.params.map(([k, v]) => ({ k, v })), 'sup-params')}</section>`).join('')}</div>`;

/* ============ SPONSORZY ============ */
S.sponsorzy = () => {
  const total = DB.sponsors.reduce((a, s) => a + s.amount, 0);
  return head('Sponsorzy', UI.fields([{ k: 'Rocznie', v: UI.money(total), num: 1, cls: 'good' }, { k: 'Wolne miejsca', v: '1', num: 1 }])) +
  `<div class="spon-grid">${DB.sponsors.map(s => `<section class="panel spon ${s.slot === 'Tytularny' ? 'title' : ''}">
      <header><span class="meta">${s.slot}</span></header>
      <div class="body"><h3>${s.name}</h3><div class="muted ind">${s.ind}</div>
        ${UI.fields([{ k: 'Kwota', v: `${UI.money(s.amount)}/rok`, num: 1 }, { k: 'Do', v: s.to, num: 1, cls: s.to === 1976 ? 'bad' : '' }], 'mid')}
        <dl class="spec"><dt>Cel</dt><dd>${s.goal}${s.goalState ? ' ' + UI.st(s.goalState[0], s.goalState[1]) : ''}</dd><dt>${s.duties[0]}</dt><dd>${s.duties[1]}</dd><dt>Miejsce na aucie</dt><dd>${s.place}</dd></dl></div></section>`).join('')}
    <section class="panel spon free"><header><span class="meta">Mniejszy</span>${UI.st('Wolne miejsce', 'hi')}</header>
      <div class="body"><table class="table tight"><thead><tr><th>Firma</th><th>Branża</th><th class="r">Oferta</th></tr></thead><tbody>${DB.sponsorLeads.map(l => `<tr><td><b>${l[0]}</b></td><td class="muted">${l[1]}</td><td class="r num">${l[2]}</td></tr>`).join('')}</tbody></table></div></section></div>`;
};

/* ============ FINANSE ============ */
S.finanse = () => {
  const f = DB.finance, max = 40;
  const chart = f.months.map(([v, m]) => `<div class="mcol"><span class="num mv ${v == null ? '' : v < 0 ? 'bad' : 'good'}">${v == null ? '' : (v > 0 ? '+' : '−') + Math.abs(v)}</span><div class="mbar ${v == null ? 'fut' : v < 0 ? 'neg' : 'pos'}" style="height:${v == null ? 6 : Math.abs(v) / max * 100}%"></div><span class="meta">${m}</span></div>`).join('');
  const list = (arr, cls) => arr.map(([n, v, det]) => `<details class="fin"><summary><span class="chev">${UI.icon(UI.chev, 16)}</span><span>${n}</span><span class="num ${cls}">${UI.money(v)}</span></summary>
    <div class="fin-det">${det.map(([a, b]) => `<div><span>${a}</span><span class="num">${UI.money(b)}</span></div>`).join('')}</div></details>`).join('');
  const inc = f.income.reduce((a, x) => a + x[1], 0), cost = f.costs.reduce((a, x) => a + x[1], 0);
  return head('Finanse', UI.fields([{ k: 'Gotówka', v: UI.money(DB.money.cash), num: 1 }, { k: 'Wolne środki', v: UI.money(DB.money.free), num: 1, cls: 'good' }, { k: 'Prognoza na koniec roku', v: '+£35 tys.', num: 1, cls: 'good' }])) +
  `<div class="fin-grid">
    ${UI.panel('Wpływy', `<div class="body">${list(f.income, 'good')}<div class="fin-sum"><span>Razem</span><b class="num good">${UI.money(inc)}</b></div></div>`)}
    ${UI.panel('Koszty', `<div class="body">${list(f.costs, 'bad')}<div class="fin-sum"><span>Razem</span><b class="num bad">${UI.money(cost)}</b></div></div>`)}
    ${UI.panel('Zobowiązania do końca roku', `<div class="body">${UI.fields([{ k: 'Zobowiązania', v: '−£290 tys.', num: 1, cls: 'bad' }, { k: 'Pewne wpływy', v: '+£205 tys.', num: 1, cls: 'good' }], 'boxed eq')}</div>`)}
    ${UI.panel('Wynik miesiąc po miesiącu', `<div class="body"><div class="mchart">${chart}</div></div>`, { cls: 'wide' })}
  </div>`;
};

/* ============ ZARZĄD ============ */
S.zarzad = () => {
  const b = DB.board, o = b.owner;
  return `<div class="board">
    <section class="panel owner"><div class="portrait">${UI.initials(o.name)}</div>
      <div class="owner-main"><span class="meta">${o.role}</span><h1>${o.name}</h1>
        ${UI.fields([{ k: 'Narodowość', v: `${UI.flag(o.nat, 'md')} ${FLAGS.name(o.nat)}` }, { k: 'Wiek', v: `${o.age} lat`, num: 1 }, { k: 'Zespół od', v: o.since, num: 1 }, { k: 'Nastrój', v: UI.st(b.mood[0], b.mood[1]) }, { k: 'Zaufanie do Ciebie', v: `${b.trust}/100`, num: 1 }], 'mid')}
        <blockquote>„${o.quote}”</blockquote></div></section>
    <div class="board-grid">
      ${UI.panel('Cele na 1976', `<div class="body">${b.goals.map(g => `<div class="goal"><span class="gi ${g[1] ? 'ok' : 'open'}">${g[1] ? UI.icon(UI.check, 16) : UI.icon('<circle cx="12" cy="12" r="6"/>', 14)}</span><b>${g[0]}</b><span class="gs">${UI.st(g[2], g[1] ? 'good' : 'warn')}</span></div>`).join('')}</div>`)}
      ${UI.panel('Zasady właściciela', `<div class="body"><dl class="spec">${b.params.map(p => `<dt>${p[0]}</dt><dd>${p[1]}</dd>`).join('')}</dl></div>`)}
      ${UI.panel('Zmiany zaufania', `<table class="table tight">${b.history.map(h => `<tr><td>${h[0]}</td><td class="r num ${h[1][0] === '+' ? 'good' : 'bad'}">${h[1]}</td></tr>`).join('')}</table>`, { cls: 'tbl' })}
    </div></div>`;
};

/* ============ RYNEK ============ */
const MKT = { key: 'stars', asc: false, min: 0, q: '', sel: 'peterson' };
S.rynek = () => head('Rynek kierowców', UI.fields([{ k: 'Kierowcy', v: DB.market.length, num: 1 }]),
  UI.tabs('mkt', [['0', 'Wszyscy'], ['3', '3★ i więcej'], ['3.5', '3½★ i więcej'], ['4', '4★ i więcej']], String(MKT.min)) +
  `<label class="search">${UI.icon('<circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/>', 16)}<input id="mkt-q" placeholder="Szukaj kierowcy" value="${MKT.q}"></label>`) +
  `<div class="market-wrap"><section class="panel tbl market"><table class="table" id="mkt"><thead><tr>
    <th data-sort="name"><span>Kierowca</span></th><th data-sort="age" class="c"><span>Wiek</span></th><th data-sort="team"><span>Obecnie</span></th><th data-sort="stars"><span>Ocena</span></th><th data-sort="pot"><span>Potencjał</span></th><th data-sort="to" class="c"><span>Kontrakt do</span></th><th data-sort="salary" class="r"><span>Pensja</span></th><th data-sort="mood"><span>Nastawienie</span></th></tr></thead><tbody></tbody></table></section>
    <section class="panel mkt-prev" id="mkt-prev"></section></div>`;
S.rynek.after = () => {
  const tb = document.querySelector('#mkt tbody');
  const moodRank = { 'Bardzo zainteresowany': 4, 'Zainteresowany': 3, 'Neutralny': 2, 'Raczej nie': 1 };
  const val = (r, k) => k === 'stars' ? r.band[1] + r.band[0] / 10 : k === 'mood' ? moodRank[r.mood] : r[k];
  const prev = () => {
    const m = DB.market.find(x => x.id === MKT.sel) || DB.market[0];
    document.getElementById('mkt-prev').innerHTML = `<div class="body"><a class="person big" href="#/kierowca/${m.id}"><span class="av">${UI.initials(m.name)}</span><div><b>${m.name}</b><small>${UI.flag(m.nat)} ${m.age} lat · ${m.team}</small></div></a>
      ${UI.fields([{ k: 'Ocena', v: UI.stars(m.band[0], m.pot, m.band) }, { k: 'Wiedza skauta', v: m.known + '%', num: 1 }], 'row1')}
      ${UI.fields([{ k: 'Kontrakt do', v: m.to, num: 1 }, { k: 'Oczekiwana pensja', v: UI.money(m.salary), num: 1 }], 'boxed eq')}
      <div class="row1">${UI.st(m.mood, moodTone(m.mood))}</div>
      <div class="prev-tools">${UI.btn('Profil', { href: `#/kierowca/${m.id}` })}${UI.btn('Rozmawiaj', { cls: 'primary', attrs: ` data-talk="${m.id}"` })}</div></div>`;
  };
  const draw = () => {
    const rows = DB.market.filter(r => r.band[1] >= MKT.min && r.name.toLowerCase().includes(MKT.q))
      .sort((a, b) => { const x = val(a, MKT.key), y = val(b, MKT.key); return (x > y ? 1 : x < y ? -1 : 0) * (MKT.asc ? 1 : -1); });
    tb.innerHTML = rows.map(r => `<tr class="go-row ${r.id === MKT.sel ? 'sel' : ''}" data-id="${r.id}"><td><div class="person"><span class="av">${UI.initials(r.name)}</span><div><b>${r.name}</b><small>${UI.flag(r.nat)} ${FLAGS.name(r.nat)}</small></div></div></td><td class="c num">${r.age}</td><td>${r.team}</td>
      <td>${UI.stars(r.band[0], 0, r.band)}</td><td>${UI.stars(0, r.pot)}</td><td class="c num ${r.to === 1976 ? 'good' : ''}">${r.to}</td><td class="r num">${UI.money(r.salary)}</td><td>${UI.st(r.mood, moodTone(r.mood))}</td></tr>`).join('');
    document.querySelectorAll('#mkt th[data-sort]').forEach(th => { th.classList.toggle('sorted', th.dataset.sort === MKT.key); th.classList.toggle('asc', th.dataset.sort === MKT.key && MKT.asc); });
  };
  document.querySelectorAll('#mkt th[data-sort]').forEach(th => th.onclick = () => {
    const k = th.dataset.sort; MKT.asc = MKT.key === k ? !MKT.asc : ['name', 'team', 'age', 'to', 'salary'].includes(k); MKT.key = k; draw();
  });
  tb.onclick = e => { const tr = e.target.closest('tr[data-id]'); if (!tr) return; MKT.sel = tr.dataset.id; draw(); prev(); };
  tb.ondblclick = e => { const tr = e.target.closest('tr[data-id]'); if (tr) location.hash = `#/kierowca/${tr.dataset.id}`; };
  document.getElementById('mkt-q').oninput = e => { MKT.q = e.target.value.toLowerCase(); draw(); };
  draw(); prev();
};
document.addEventListener('tab', e => { if (e.detail.group === 'mkt') { MKT.min = +e.detail.value; S.rynek.after(); } });

/* ============ PADDOCK MONTHLY ============ */
const SECS = ['Wszystko', 'Rynek', 'Wyścigi', 'Talenty', 'Technika', 'Pieniądze', 'Z historii'];
S.monthly = () => {
  const m = DB.monthly;
  return `<div class="magazine" data-scope><header class="mhead"><b>Paddock Monthly</b><span>${m.issue}</span>
      <div class="msecs">${UI.tabs('msec', SECS.map(s => [s, s]), 'Wszystko')}</div></header>
    <div class="mgrid" id="mgrid">${monthlyGrid('Wszystko')}</div></div>`;
};
S.monthly.fit = true;
function monthlyGrid(sec) {
  const m = DB.monthly, all = [m.lead, ...m.stories];
  const list = sec === 'Wszystko' ? all : all.filter(s => s.sec === sec);
  const [lead, ...rest] = list;
  return `<article class="mlead"><span class="msec">${lead.sec}</span><h1>${lead.title}</h1><p>${lead.text}</p><a class="link" href="${lead.link[0]}">${lead.link[1]}${UI.icon(A, 15)}</a></article>
    ${rest.map(s => `<article class="mstory"><span class="msec">${s.sec}</span><h2>${s.title}</h2><p>${s.text}</p><a class="link" href="${s.link[0]}">${s.link[1]}${UI.icon(A, 15)}</a></article>`).join('')}`;
}
document.addEventListener('tab', e => { if (e.detail.group === 'msec') { const g = document.getElementById('mgrid'); g.innerHTML = monthlyGrid(e.detail.value); g.classList.remove('pane-in'); g.offsetWidth; g.classList.add('pane-in'); } });

/* ============ FIA ============ */
S.fia = () => {
  const f = DB.fia;
  return head('FIA i regulamin', UI.fields([{ k: 'Sezon', v: '1976', num: 1 }])) +
  `<div class="fia-grid">
    <div class="rules">${f.groups.map(([g, rows]) => UI.panel(g, `<div class="body"><dl class="spec">${rows.map(r => `<dt>${r[0]}</dt><dd>${r[1]}</dd>`).join('')}</dl></div>`)).join('')}</div>
    <div class="col">
      ${UI.panel('Głosowania', `<div class="body">${f.votes.map(v => `<div class="vote"><div><b>${v.title}</b><small class="muted">${v.from} · ${v.when}</small></div>
        ${v.state ? `<div class="vres">${UI.st('Nasz głos: ' + v.state[0], v.state[1])}<span class="num">${v.result}</span></div>` : STATE.decisions[v.mail] ? UI.st('Nasz głos: ' + STATE.decisions[v.mail], 'good') : UI.btn('Głosuj w skrzynce', { href: `#/skrzynka/${v.mail}`, cls: 'sm', after: A })}</div>`).join('')}</div>`)}
      ${UI.panel('Historia zmian', `<table class="table tight"><tbody>${f.changes.map(c => `<tr><td class="num">${c[0]}</td><td class="wrap">${c[1]}</td><td>${UI.st(c[2], c[2] === 'Auto' ? 'team' : 'hi')}</td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
    </div></div>`;
};

/* ============ KRONIKA ============ */
S.kronika = () => head('Kronika') +
  `<section class="panel tbl"><div class="timeline">${DB.chronicle.map(c => `<div class="tl"><span class="meta">${c[0]}</span><div><b>${c[1]}</b><p class="muted">${c[2]}</p></div></div>`).join('')}</div></section>`;

/* ============ MENEDŻER ============ */
S.menedzer = () => {
  const m = DB.manager;
  return `<div class="profile">
    <section class="panel hero staff"><div class="num-big"><span>MW</span></div>
      <div class="hero-main"><h1 class="screen">${m.name}</h1>${UI.fields([{ k: 'Narodowość', v: `${UI.flag(m.nat, 'md')} ${FLAGS.name(m.nat)}` }, { k: 'Wiek', v: `${m.age} lat`, num: 1 }, { k: 'Rola', v: 'Szef zespołu' }, { k: 'Zespół', v: 'Elf Team Tyrrell' }], 'mid')}</div>
      <div class="hero-side">${UI.fields([{ k: 'Reputacja', v: `${m.rep}/100`, num: 1 }, { k: 'Wyścigi', v: 22, num: 1 }, { k: 'Wygrane', v: 1, num: 1 }])}</div></section>
    <div class="grid" style="grid-template-columns:1fr 1fr 1fr">
      ${UI.panel('Atrybuty', `<div class="body"><div class="attrs">${m.attrs.map(([k, v]) => `<div class="arow"><span>${k}</span>${UI.attr(v)}<div class="bar thin"><i style="width:${v * 5}%;background:var(--ink)"></i></div></div>`).join('')}</div></div>`)}
      ${UI.panel('Zmiany reputacji', `<table class="table tight">${m.repLog.map(r => `<tr><td class="num muted">${r[0]}</td><td>${r[1]}</td><td class="r num ${r[2][0] === '+' ? 'good' : 'bad'}">${r[2]}</td></tr>`).join('')}</table>`, { cls: 'tbl' })}
      ${UI.panel('Oferty pracy', `<div class="body">${UI.fields([{ k: 'Oferty', v: '0', num: 1 }, { k: 'Zespoły obserwujące', v: '2', num: 1 }], 'boxed eq')}</div>`)}
    </div></div>`;
};

/* ============ USTAWIENIA ============ */
S.ustawienia = () => head('Ustawienia') +
  `<div class="grid g2 settings">${UI.panel('Wygląd', `<div class="body set">
    <div class="srow"><b>Kolory interfejsu</b>${UI.tabs('set-style', [['era', 'Barwy epoki'], ['team', 'Barwy zespołu']], PREF.style)}</div>
    <div class="srow"><b>Skórka epoki</b>${UI.tabs('set-skin', [['auto', 'Automatycznie'], ['1970', 'Lata 70.'], ['1990', 'Lata 90.', { disabled: 1 }], ['2020', 'Lata 20.', { disabled: 1 }]], PREF.skin)}</div>
    <div class="srow"><b>Podgląd zespołu</b><div class="sw">${[['tyrrell', '#1f4f9a', '#e03a3e'], ['lotus', '#16130e', '#c9a24a'], ['ferrari', '#c4161c', '#f5c518']].map(([k, a, b]) => `<button data-team="${k}" class="${PREF.team === k ? 'on' : ''}" style="background:linear-gradient(135deg,${a} 50%,${b} 50%)" aria-label="${k}"></button>`).join('')}</div></div>
    <div class="srow"><b>Animowane tło</b>${UI.tabs('set-air', [['on', 'Włączone'], ['off', 'Wyłączone']], PREF.air)}</div>
  </div>`)}
  ${UI.panel('Rozgrywka', `<div class="body set">
    <div class="srow"><b>Informacje o ludziach</b>${UI.tabs('set-numbers', [['on', 'Liczby'], ['off', 'Opinie']], PREF.numbers)}</div>
    <div class="srow"><b>Oglądanie wyścigu</b>${UI.tabs('set-speed', [['5', '×5'], ['10', '×10'], ['20', '×20'], ['res', 'Wynik']], PREF.speed)}</div>
    <div class="srow"><b>Pit-stopy</b>${UI.tabs('set-pits', [['strateg', 'Strateg'], ['manual', 'Ręcznie']], PREF.pits)}</div>
    <div class="srow"><b>Język</b>${UI.tabs('set-lang', [['pl', 'Polski'], ['en', 'English']], PREF.lang)}</div>
  </div>`)}</div>`;
document.addEventListener('click', e => {
  const b = e.target.closest('.sw button[data-team]'); if (!b) return;
  PREF.team = b.dataset.team; applyPrefs(); document.querySelectorAll('.sw button').forEach(x => x.classList.toggle('on', x === b));
  document.querySelectorAll('.tabs').forEach(t => UI.placeInd(t, false));
});
