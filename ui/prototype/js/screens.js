/* Ekrany prototypu (część 1). Każdy zwraca HTML; app.js wstawia go do #view. */
const S = {};
const A = UI.arrow;

/* ---------- stan prototypu: decyzje i przystanki czasu ---------- */
const STATE = {
  step: 0, decisions: {},
  now() { return DB.stops[this.step]; },
  next() { return DB.stops[this.step + 1] || DB.stops[this.step]; },
  blocking() { return DB.inbox.find(m => m.decision && m.dueDay && !this.decisions[m.id] && m.dueDay <= this.next().day); },
  onArrive() {
    const pick = this.decisions[1];
    if (this.step === 1 && pick && DB.replies[pick]) {
      const r = DB.replies[pick], m = { id: 100, when: 'Dziś', unread: true, ...r };
      DB.inbox.unshift(m); return m;
    }
    return null;
  },
};

/* ---------- wspólne kawałki ---------- */
const head = (title, fields = '', tools = '') =>
  `<div class="screen-head"><h1 class="screen">${title}</h1>${fields}${tools ? `<div class="tools">${tools}</div>` : ''}</div>`;
const teamName = k => (DB.teams[k] || { name: k }).name;
const driverHref = id => DB.drivers.find(d => d.id === id) || DB.market.find(d => d.id === id) || DB.academy.pool.find(d => d.id === id) ? `#/kierowca/${id}` : null;
const nameLink = (id, name) => { const h = driverHref(id); return h ? `<a class="plain" href="${h}">${name}</a>` : name; };
const mailRow = (m, sel) => `<a class="mail ${m.decision && !STATE.decisions[m.id] ? 'decision' : ''} ${m.unread ? '' : 'read'} ${sel ? 'sel' : ''}" href="#/skrzynka/${m.id}">
  <span class="av">${m.av}</span><div><div class="from">${m.from}${m.due && !STATE.decisions[m.id] ? UI.st('do ' + m.due, 'bad', true) : ''}</div><div class="t">${m.title}</div></div>
  ${m.unread ? '<span class="unread" aria-label="nieprzeczytana"></span>' : `<span class="when">${m.when}</span>`}</a>`;

/* sylwetka toru: zamknięta krzywa Catmulla-Roma przez punkty */
function trackSvg(key, cls = '') {
  const t = DB.tracks[key]; if (!t) return '';
  const p = t.map, n = p.length;
  const xs = p.map(q => q[0]), ys = p.map(q => q[1]);
  const x0 = Math.min(...xs) - 4, y0 = Math.min(...ys) - 4, w = Math.max(...xs) - x0 + 4, h = Math.max(...ys) - y0 + 4;
  let d = `M${p[0][0]},${p[0][1]}`;
  for (let i = 0; i < n; i++) {
    const a = p[(i - 1 + n) % n], b = p[i], c = p[(i + 1) % n], e = p[(i + 2) % n];
    d += `C${(b[0] + (c[0] - a[0]) / 6).toFixed(1)},${(b[1] + (c[1] - a[1]) / 6).toFixed(1)} ${(c[0] - (e[0] - b[0]) / 6).toFixed(1)},${(c[1] - (e[1] - b[1]) / 6).toFixed(1)} ${c[0]},${c[1]}`;
  }
  const [sx, sy] = p[0], [nx, ny] = p[1], ang = Math.atan2(ny - sy, nx - sx) + Math.PI / 2;
  const tick = `M${(sx - Math.cos(ang) * 3).toFixed(1)},${(sy - Math.sin(ang) * 3).toFixed(1)}L${(sx + Math.cos(ang) * 3).toFixed(1)},${(sy + Math.sin(ang) * 3).toFixed(1)}`;
  return `<svg class="trk ${cls}" viewBox="${x0} ${y0} ${w} ${h}" aria-label="Układ toru ${t.name}"><path class="road" d="${d}"/><path class="line" d="${d}"/><path class="sf" d="${tick}"/></svg>`;
}

/* ============ PULPIT ============ */
S.pulpit = () => {
  const car = DB.car.areas.map(([n, p]) => `<div class="crow"><span>${n}</span><span class="num" style="color:${UI.rankColor(p, 16, 34)}">${p}.</span>${UI.rankBar(p)}</div>`).join('');
  const m = DB.monthly, unread = DB.inbox.filter(x => x.unread).length;
  const mails = [...DB.inbox].sort((a, b) => (b.decision && !STATE.decisions[b.id]) - (a.decision && !STATE.decisions[a.id])).slice(0, 6);
  const std = (kind) => {
    const rows = (kind === 'drv' ? DB.standings.drivers : DB.standings.constructors).slice(0, 6);
    return `<table class="table tight" data-pane="std:${kind}" ${kind === 'con' ? 'hidden' : ''}><tbody>${rows.map((r, i) =>
      `<tr class="${(r.team === 'tyrrell') ? 'mine' : ''}"><td class="num c" style="width:40px">${i + 1}</td><td>${kind === 'drv' ? `${UI.flag(r.nat)} ${nameLink(r.id, r.name)}` : DB.teams[r.team].full}</td><td class="r num">${r.pts}</td></tr>`).join('')}</tbody></table>`;
  };
  return `<div class="dash">
    <section class="panel inbox"><header><h2>Skrzynka</h2>${unread ? `<span class="count">${unread}</span>` : ''}</header>
      <div class="list">${mails.map(x => mailRow(x)).join('')}</div>
      <footer><a class="link" href="#/skrzynka">Otwórz skrzynkę${UI.icon(A, 15)}</a></footer></section>
    <div class="col">
      <a class="panel race" href="#/wyscig/9">
        <div class="race-top"><div><h1>Brands Hatch</h1><div class="where">${UI.flag('GBR', 'md')} GP Wielkiej Brytanii · Niedziela, 18 lipca</div></div>${trackSvg('brands_hatch', 'dash-map')}</div>
        ${UI.fields([{ k: 'Okrążenia', v: '76', num: 1 }, { k: 'Długość', v: '4,206 km', num: 1 }, { k: 'Deszcz', v: '20%', num: 1 }, { k: 'Dopasowanie auta', v: '4. z 16', num: 1, cls: 'good' }], 'facts')}
        <div class="voice"><span class="av">DG</span><div><p>„Tor nam leży: dużo hamowania i ciasnych nawrotów. Tracimy tylko na długiej prostej. Realnie walczymy o podium.”</p>
          <small>Derek Gardner · prognoza <b>P3–P6</b></small></div></div>
        ${UI.fields(DB.car.sectors.map(([k, p]) => ({ k, v: `<span style="color:${UI.rankColor(p, 16, 34)}">${p}. w stawce</span>`, num: 1 })), 'sectors eq')}
      </a>
      <section class="panel monthly"><div class="mast"><b>Paddock Monthly</b><span>${m.issue}</span></div>
        <div class="stories"><div class="lead"><span class="msec">${m.lead.sec}</span><h3>${m.lead.title}</h3><p>Szwed ma dość Marcha. Jego ludzie pytają o miejsce na 1977, jeśli Scheckter odejdzie.</p><a class="link" href="${m.lead.link[0]}">${m.lead.link[1]}${UI.icon(A, 15)}</a></div>
          <div class="side">${m.stories.slice(0, 3).map(s => `<a href="${s.link[0]}"><span class="msec">${s.sec}</span><div class="t">${s.title}</div></a>`).join('')}</div></div>
      </section>
    </div>
    <div class="col">
      <section class="panel car"><header><h2>Auto vs stawka</h2><span class="meta">16 aut</span></header><div class="body">${car}</div><footer><a class="link" href="#/auto">Auto i rozwój${UI.icon(A, 15)}</a></footer></section>
      <section class="panel stand" data-scope><header><h2>Mistrzostwa</h2></header>
        <div class="tabs-wrap">${UI.tabs('std', [['drv', 'Kierowcy'], ['con', 'Konstruktorzy']], 'drv', 'fill')}</div>
        ${std('drv')}${std('con')}
        <footer><a class="link" href="#/klasyfikacje">Pełne klasyfikacje${UI.icon(A, 15)}</a></footer></section>
    </div></div>`;
};
S.pulpit.fit = true;

/* ============ SKRZYNKA ============ */
/* przełączanie wiadomości i filtrów bez animacji */
S.skrzynka = (id) => {
  const cur = DB.inbox.find(m => m.id == id) || DB.inbox[0];
  cur.unread = false;
  const chosen = STATE.decisions[cur.id];
  let opts = '';
  if (cur.options) {
    opts = `<div class="choices" role="radiogroup" aria-label="Opcje">${cur.options.map(o => `<button class="choice${chosen === o.label ? ' chosen' : ''}" role="radio" aria-checked="${chosen === o.label}" data-opt="${o.label}"${chosen ? ' disabled' : ''}>
        <div class="ch"><b>${o.label}</b><span class="rd">${chosen === o.label ? UI.icon(UI.check, 14) : ''}</span></div>
        <div class="fx">${o.fx.map(([t, x]) => `<div class="row ${t}"><b>${t === 'p' ? '+' : t === 'm' ? '−' : '·'}</b><span>${x}</span></div>`).join('')}</div></button>`).join('')}</div>
      ${chosen ? `<div class="stamp">${UI.st('Decyzja podjęta', 'good')}<b>${chosen}</b><span class="muted">${STATE.now().title}</span></div>`
        : `<div class="confirm">${UI.fields([{ k: 'Termin', v: cur.due, cls: 'bad' }, { k: 'Wybór', v: '<span id="pick">—</span>' }])}<button class="btn primary" id="confirm" disabled>${UI.icon(UI.check, 17)}<span>Potwierdź</span></button></div>`}`;
  }
  const count = k => DB.inbox.filter(m => k === 'all' || m.kind === k).length;
  return head('Skrzynka', '', UI.tabs('box', [['all', `Wszystkie ${count('all')}`], ['decyzje', `Decyzje ${count('decyzje')}`], ['raporty', `Raporty ${count('raporty')}`], ['media', `Media ${count('media')}`]], 'all')) +
  `<div class="mailbox instant"><section class="panel list">${DB.inbox.map(m => `<div data-kind="${m.kind}">${mailRow(m, m.id === cur.id)}</div>`).join('')}</section>
   <section class="panel reader"><div class="rhead"><span class="av big">${cur.av}</span><div><div class="muted">${cur.from} · ${cur.when}</div><h2>${cur.title}</h2></div></div>
     <p class="letter">${cur.body}</p>${cur.link ? `<a class="btn sm" href="${cur.link[0]}" style="margin-top:16px">${cur.link[1]}${UI.icon(A, 16)}</a>` : ''}${opts}</section></div>`;
};
S.skrzynka.after = (id) => {
  const cur = DB.inbox.find(m => m.id == id) || DB.inbox[0];
  document.querySelectorAll('.choice:not([disabled])').forEach(c => c.onclick = () => {
    document.querySelectorAll('.choice').forEach(x => x.setAttribute('aria-checked', x === c));
    document.getElementById('pick').textContent = c.dataset.opt;
    document.getElementById('confirm').disabled = false;
  });
  const conf = document.getElementById('confirm');
  if (conf) conf.onclick = () => {
    const sel = document.querySelector('.choice[aria-checked="true"]'); if (!sel) return;
    STATE.decisions[cur.id] = sel.dataset.opt;
    drawTop(); render(parse());
  };
};
document.addEventListener('tab', e => {
  if (e.detail.group !== 'box') return;
  document.querySelectorAll('.mailbox .list [data-kind]').forEach(r => r.hidden = e.detail.value !== 'all' && r.dataset.kind !== e.detail.value);
});

/* ============ KALENDARZ ============ */
S.kalendarz = () => head('Kalendarz 1976', UI.fields([{ k: 'Rundy', v: '16', num: 1 }])) +
  `<div class="cal">${DB.calendar.map((r, i) => {
    const past = i < 8, next = i === 8, t = DB.tracks[r[3]];
    return `<a class="rnd ${past ? 'past' : ''} ${next ? 'next' : ''}" href="#/wyscig/${i + 1}"><span class="rno">${i + 1}</span>
      <div class="rinfo"><span class="rdate">${r[0]}</span><b>${UI.flag(r[1], 'md')}<span>${FLAGS.name(r[1])}</span></b><small>${t.name}</small></div>${trackSvg(r[3])}</a>`;
  }).join('')}</div>`;

/* ============ STRONA WYŚCIGU ============ */
S.wyscig = (n = 9) => {
  const i = Math.max(0, Math.min(15, n - 1)), c = DB.calendar[i], t = DB.tracks[c[3]], res = DB.results[i], hist = DB.trackHistory[c[3]] || { years: [] };
  const past = !!res, prev = i > 0 ? `<a class="btn sm" href="#/wyscig/${i}">${UI.icon(UI.back, 16)}Runda ${i}</a>` : '', nxt = i < 15 ? `<a class="btn sm" href="#/wyscig/${i + 2}">Runda ${i + 2}${UI.icon(A, 16)}</a>` : '';
  const prof = [['Proste', t.profile.straights], ['Szybkie zakręty', t.profile.high_speed], ['Wolne zakręty', t.profile.low_speed], ['Hamowanie', t.profile.braking]];
  const table = past ? `<table class="table tight results"><thead><tr><th class="c">Poz.</th><th class="c">Nr</th><th>Kierowca</th><th>Zespół</th><th class="c">Okr.</th><th class="r">Czas</th><th class="c">Start</th><th class="c">Pkt</th></tr></thead><tbody>
      ${res.rows.map(r => `<tr class="${r.team === 'tyrrell' ? 'mine' : ''}"><td class="c num">${r.dnf ? '<span class="bad">DNF</span>' : r.pos}</td><td class="c num muted">${r.no}</td><td>${UI.flag(r.nat)} ${nameLink(r.id, r.name)}</td><td class="muted">${DB.teams[r.team].full}</td><td class="c num">${r.laps}</td><td class="r num ${r.dnf ? 'muted' : ''}">${r.time}</td><td class="c num">${r.grid}</td><td class="c num">${r.pts || ''}</td></tr>`).join('')}</tbody></table>` : '';
  const pole = past ? DB.gridById[Object.values(DB.grid).find(g => g[0] === res.pole)[1]] : null, fl = past ? DB.grid.find(g => g[0] === res.fl.no) : null;
  const years = hist.years.length ? `<table class="table tight"><thead><tr><th class="c">Rok</th><th>Zwycięzca</th><th>Zespół</th>${hist.years[0][3] ? '<th>Pole position</th><th>Najszybsze okrążenie</th>' : ''}</tr></thead><tbody>${hist.years.map(y => `<tr><td class="c num">${y[0]}</td><td><b>${y[1]}</b></td><td class="muted">${y[2]}</td>${y[3] ? `<td>${y[3]}</td><td>${y[4]}</td>` : ''}</tr>`).join('')}</tbody></table>`
    : `<div class="empty">${UI.st('Pierwszy wyścig F1 na tym torze', 'hi')}</div>`;
  return `<div class="race-page">
    <section class="panel rp-head"><span class="rno big">${i + 1}</span>
      <div class="rp-title"><div class="rp-where">${UI.flag(c[1], 'lg')}<span class="meta">${c[0]} 1976</span></div><h1 class="screen">${c[2]}</h1>
        ${UI.fields([{ k: 'Tor', v: t.name }, { k: 'Długość okrążenia', v: `${String(t.len).replace('.', ',')} km`, num: 1 }, { k: 'Okrążenia', v: c[4], num: 1 }, { k: 'Dystans', v: `${(t.len * c[4]).toFixed(1).replace('.', ',')} km`, num: 1 }, { k: 'Charakter', v: t.tags.join(', ') }], 'mid')}</div>
      <div class="rp-tools">${prev}${nxt}</div></section>
    <div class="rp-grid">
      <div class="col">
        ${past ? UI.panel('Wyniki', `<div class="tbl-scroll">${table}</div>`, { cls: 'tbl', right: UI.fields([{ k: 'Pole position', v: pole[2] }, { k: 'Najszybsze okrążenie', v: `${fl[2]} · ${res.fl.time}` }], 'hdr') })
          : UI.panel('Poprzednie lata', `<div class="tbl-scroll">${years}</div>`, { cls: 'tbl' })}
      </div>
      <div class="col">
        <section class="panel rp-map">${trackSvg(c[3], 'big')}</section>
        ${UI.panel('Tor w liczbach', `<div class="body">${UI.fields([{ k: 'Wyścigi F1', v: hist.gps || hist.years.length, num: 1 }, { k: 'Średnio DNF', v: String(hist.dnfAvg || '—').replace('.', ','), num: 1 }, { k: 'Średnio SC', v: String(hist.scAvg ?? 0), num: 1 }], 'eq')}
          ${hist.record ? `<div class="record">${UI.fields([{ k: 'Rekord okrążenia', v: hist.record[2], num: 1 }, { k: 'Kierowca', v: `${hist.record[0]} · ${hist.record[1]}` }, { k: 'Rok', v: hist.record[3], num: 1 }])}</div>` : ''}
          <div class="profile-bars">${prof.map(([k, v]) => `<div class="pb"><span>${k}</span><span class="num">${Math.round(v * 100)}%</span><div class="bar thin"><i style="width:${v * 100 / .5}%"></i></div></div>`).join('')}</div></div>`)}
        ${past ? UI.panel('Zwycięzcy', `<div class="tbl-scroll">${years}</div>`, { cls: 'tbl' }) : ''}
      </div>
    </div></div>`;
};

/* ============ KLASYFIKACJE ============ */
S.klasyfikacje = (series = 'f1') => {
  const d = DB.standings.drivers, c = DB.standings.constructors;
  const sc = DB.scoring;
  const scoring = `<div class="scoring"><span class="meta">Punktacja</span><div class="pts">${sc.points.map((p, i) => `<div><span class="meta">${i + 1}.</span><b class="num">${p}</b></div>`).join('')}</div>
    ${UI.fields([{ k: 'Liczone z rund 1–8', v: '7 najlepszych', num: 1 }, { k: 'Liczone z rund 9–16', v: '7 najlepszych', num: 1 }, { k: 'Konstruktorzy', v: 'Najlepsze auto' }])}</div>`;
  const f1 = `<div class="stand-grid" data-pane="ser:f1" ${series !== 'f1' ? 'hidden' : ''}>
    ${UI.panel('Kierowcy', `<div class="tbl-scroll"><table class="table tight"><thead><tr><th class="c">Poz.</th><th>Kierowca</th><th>Zespół</th><th class="c">Wygrane</th><th class="c">Pkt</th></tr></thead><tbody>${d.map((r, i) => `<tr class="${r.team === 'tyrrell' ? 'mine' : ''}"><td class="c num">${i + 1}</td><td>${UI.flag(r.nat)} ${nameLink(r.id, r.name)}</td><td class="muted">${teamName(r.team)}</td><td class="c num">${r.wins || ''}</td><td class="c num"><b>${r.pts}</b></td></tr>`).join('')}</tbody></table></div>`, { cls: 'tbl' })}
    <div class="col">${UI.panel('Konstruktorzy', `<table class="table tight"><thead><tr><th class="c">Poz.</th><th>Konstruktor</th><th class="c">Wygrane</th><th class="c">Pkt</th></tr></thead><tbody>${c.map((r, i) => `<tr class="${r.team === 'tyrrell' ? 'mine' : ''}"><td class="c num">${i + 1}</td><td>${DB.teams[r.team].full}</td><td class="c num">${r.wins || ''}</td><td class="c num"><b>${r.pts}</b></td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}
      ${UI.panel('', `<div class="body" style="padding-top:18px">${scoring}</div>`)}</div></div>`;
  const wsc = `<div class="stand-grid one" data-pane="ser:wsc" ${series !== 'wsc' ? 'hidden' : ''}>${UI.panel('Marki', `<table class="table tight"><thead><tr><th class="c">Poz.</th><th>Marka</th><th class="c">Pkt</th></tr></thead><tbody>${DB.wsc.map((r, i) => `<tr><td class="c num">${i + 1}</td><td>${r[0]}</td><td class="c num"><b>${r[1]}</b></td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}</div>`;
  const f2 = `<div class="stand-grid one" data-pane="ser:f2" ${series !== 'f2' ? 'hidden' : ''}>${UI.panel('Kierowcy', `<table class="table tight"><thead><tr><th class="c">Poz.</th><th>Kierowca</th><th>Zespół</th><th class="c">Pkt</th></tr></thead><tbody>${DB.f2.map((r, i) => `<tr><td class="c num">${i + 1}</td><td>${UI.flag(r[1])} ${r[0]}</td><td class="muted">${r[2]}</td><td class="c num"><b>${r[3]}</b></td></tr>`).join('')}</tbody></table>`, { cls: 'tbl' })}</div>`;
  return head('Klasyfikacje 1976', UI.fields([{ k: 'Po rundzie', v: '8 z 16', num: 1 }]), UI.tabs('ser', [['f1', 'Formuła 1'], ['wsc', 'Samochody sportowe'], ['f2', 'Formuła 2']], series)) + f1 + wsc + f2;
};

/* ============ KIEROWCY ============ */
const AT = DB.attrs;
S.kierowcy = () => head('Kierowcy', UI.fields([{ k: 'Wyścigowi', v: '2', num: 1 }, { k: 'Testowi', v: '1', num: 1 }]), UI.btn('Porównaj', { href: '#/porownaj/scheckter/depailler' }) + UI.btn('Szukaj na rynku', { href: '#/rynek', cls: 'primary' })) +
  UI.panel('Skład', `<table class="table squad"><thead><tr><th>Kierowca</th><th>Ocena</th>${AT.map(a => `<th class="c" title="${a[1]}">${a[2]}</th>`).join('')}<th>Forma</th><th class="c">Kontrakt</th></tr></thead><tbody>
  ${DB.drivers.map(d => `<tr class="go-row" data-href="#/kierowca/${d.id}"><td><div class="person"><span class="av">${d.no}</span><div><b>${d.name}</b><small>${UI.flag(d.nat)} ${d.age} l. · ${d.role}</small></div></div></td>
    <td>${UI.stars(d.stars, d.pot)}</td>${AT.map(a => `<td class="c">${UI.attr(d.attrs[a[0]])}</td>`).join('')}<td>${d.form}</td><td class="c num ${d.contract.to === 1976 ? 'bad' : ''}">${d.contract.to}</td></tr>`).join('')}
  </tbody></table>`, { cls: 'tbl' });

/* profil kierowcy: nasz (pełna wiedza) albo obcy (pasma skauta) */
S.kierowca = (id) => {
  const own = DB.drivers.find(x => x.id === id);
  if (!own) return foreignProfile(id);
  const d = own, words = PREF.numbers === 'off';
  const season = DB.seasonOf(d.id);
  const attrs = words
    ? `<div class="voices">${(d.voices || [['TW', 'Tom Walsh', 'Inżynier wyścigowy', 'Za wcześnie, żeby coś powiedzieć. Dajcie mu kilka testów.']]).map(v => `<div class="vq"><span class="av">${v[0]}</span><div><p>„${v[3]}”</p><small>${v[1]} · ${v[2]}</small></div></div>`).join('')}</div>`
    : `<div class="attrs">${AT.map(a => { const v = d.attrs[a[0]]; return `<div class="arow"><span>${a[1]}</span>${UI.attr(v)}<div class="bar thin"><i style="width:${v * 5}%;background:${v >= 17 ? 'var(--good)' : 'var(--ink)'}"></i></div></div>`; }).join('')}</div>`;
  const strip = `<div class="season-strip"><span class="meta">Sezon 1976</span><div class="rs">${season.map((p, i) => `<a href="#/wyscig/${i + 1}" class="r ${p === 1 ? 'win' : p === 'DNF' ? 'dnf' : p && p <= 3 ? 'pod' : p && p <= 6 ? 'pts' : ''}" title="${DB.calendar[i][2]}"><span class="meta">${DB.calendar[i][1]}</span><b class="num">${p === null ? '—' : p === 'DNF' ? 'DNF' : p}</b></a>`).join('')}${DB.calendar.slice(8).map((c, k) => `<span class="r fut" title="${c[2]}"><span class="meta">${c[1]}</span><b>·</b></span>`).join('')}</div></div>`;
  const c = d.career;
  return `<div class="profile">
    <section class="panel hero"><div class="num-big"><span>${d.no}</span></div>
      <div class="hero-main"><h1 class="screen">${d.name}</h1>
        ${UI.fields([{ k: 'Narodowość', v: `${UI.flag(d.nat, 'md')} ${FLAGS.name(d.nat)}` }, { k: 'Wiek', v: `${d.age} lat`, num: 1 }, { k: 'Rola', v: d.role }, { k: 'Zespół', v: 'Elf Team Tyrrell' }], 'mid')}</div>
      <div class="hero-side">${UI.fields([{ k: 'Ocena', v: words ? '<span class="muted">—</span>' : UI.stars(d.stars, d.pot).replace('class="stars"', 'class="stars lg"') }, { k: 'Forma', v: d.form }, { k: 'Morale', v: d.morale }, { k: 'Zaufanie', v: `${d.trust}/100`, num: 1 }])}
        <div class="tools">${UI.btn('Porównaj', { href: `#/porownaj/${d.id}/${d.id === 'scheckter' ? 'depailler' : 'scheckter'}` })}${UI.btn('Skład', { href: '#/kierowcy', icon: UI.back })}</div></div></section>
    <div class="prof-grid">
      ${UI.panel(words ? 'Co mówią ludzie' : 'Atrybuty', `<div class="body">${attrs}${strip}</div>`)}
      <div class="col">${UI.panel('Styl i dopasowanie do P34', `<div class="body"><table class="table tight fit"><thead><tr><th></th><th>Lubi</th><th>P34</th></tr></thead><tbody>${d.prefs.map(p => `<tr><td class="muted">${p[0]}</td><td><b>${p[1]}</b></td><td>${p[1] === p[2] ? `<span class="good match">${UI.icon(UI.check, 16)}Zgodne</span>` : `<span class="warn">${p[2]}</span>`}</td></tr>`).join('')}</tbody></table>
          <div class="tags" style="margin-top:12px">${d.traits.map(t => `<span class="tag">${t}</span>`).join('') || UI.st('Cechy nieznane')}</div></div>`)}
        ${UI.panel('Kariera', `<div class="body">${UI.fields([{ k: 'Starty', v: c.starts, num: 1 }, { k: 'Wygrane', v: c.wins, num: 1 }, { k: 'Pole', v: c.poles, num: 1 }, { k: 'Podia', v: c.podiums, num: 1 }, { k: 'Punkty', v: c.points, num: 1 }], 'boxed center')}
          <table class="table tight" style="margin-top:8px"><thead><tr><th class="c">Sezon</th><th>Zespół</th><th class="c">Starty</th><th class="c">Pkt</th><th class="c">Wygrane</th></tr></thead><tbody>${d.seasons.slice(-3).map(s => `<tr><td class="c num">${s[0]}</td><td>${s[1]}</td><td class="c num">${s[2]}</td><td class="c num">${s[3]}</td><td class="c num">${s[4]}</td></tr>`).join('')}</tbody></table></div>`)}</div>
      <div class="col">${UI.panel('Kontrakt', `<div class="body">${UI.fields([{ k: 'Do końca', v: d.contract.to, num: 1, cls: d.contract.to === 1976 ? 'bad' : '' }, { k: 'Pensja', v: UI.money(d.contract.salary) + '/rok', num: 1 }, { k: 'Premie', v: d.contract.bonus }])}
          ${d.clauses.length ? `<div class="clauses">${d.clauses.map(k => `<div class="cl"><span class="cl-type">${k.type}</span><div class="cl-txt"><span class="meta">Warunek</span><span>${k.cond}</span><span class="meta">Skutek</span><b>${k.effect}</b></div></div>`).join('')}</div>` : ''}</div>`)}
        ${d.promise ? UI.panel('Obietnica', `<div class="body"><b class="promise-t">${d.promise.text}</b>
          <div class="bar" style="margin:10px 0 12px"><i style="width:${d.promise.pct}%;background:var(--t2)"></i></div>
          ${UI.fields([{ k: 'Postęp prac', v: d.promise.pct + '%', num: 1 }, { k: 'Termin', v: d.promise.due }, { k: 'Zostało', v: UI.n(d.promise.races, 'wyścig', 'wyścigi', 'wyścigów'), num: 1 }])}
          <div class="miss"><span class="meta">Niedotrzymanie</span>${d.promise.miss.map(m => `<span>${m[0]}: <b class="bad">${m[1]}</b></span>`).join('')}</div></div>`, { cls: 'promise' }) : ''}</div>
    </div></div>`;
};

/* obcy kierowca: wiedza skauta, pasma zamiast liczb */
function foreignProfile(id) {
  const m = DB.market.find(x => x.id === id) || DB.academy.pool.find(x => x.id === id) || DB.market[0];
  const attrs = DB.scouted(m.id, (m.band[0] + m.band[1]) / 2, m.known);
  const inMarket = DB.market.includes(m);
  return `<div class="profile">
    <section class="panel hero foreign"><div class="num-big"><span>${UI.initials(m.name)}</span></div>
      <div class="hero-main"><h1 class="screen">${m.name}</h1>
        ${UI.fields([{ k: 'Narodowość', v: `${UI.flag(m.nat, 'md')} ${FLAGS.name(m.nat)}` }, { k: 'Wiek', v: `${m.age} lat`, num: 1 }, { k: 'Obecnie', v: m.team }, { k: 'Wiedza skauta', v: `${m.known}%`, num: 1 }], 'mid')}</div>
      <div class="hero-side">${UI.fields([{ k: 'Ocena', v: UI.stars(m.band[0], m.pot, m.band).replace('class="stars"', 'class="stars lg"') }, inMarket ? { k: 'Nastawienie', v: UI.st(m.mood, moodTone(m.mood)) } : { k: 'Seria', v: m.team }])}
        <div class="tools">${inMarket ? UI.btn('Rozmawiaj', { cls: 'primary', attrs: ` data-talk="${m.id}"` }) : ''}${UI.btn(inMarket ? 'Rynek' : 'Akademia', { href: inMarket ? '#/rynek' : '#/akademia', icon: UI.back })}</div></div></section>
    <div class="prof-grid f">
      ${UI.panel('Atrybuty', `<div class="body"><div class="attrs">${AT.map(a => { const v = attrs[a[0]], lo = Array.isArray(v) ? v[0] : v, hi = Array.isArray(v) ? v[1] : v; return `<div class="arow"><span>${a[1]}</span>${UI.attr(v)}<div class="bar thin band"><i style="margin-left:${lo * 5 - 5}%;width:${(hi - lo + 1) * 5}%"></i></div></div>`; }).join('')}</div></div>`)}
      <div class="col">${UI.panel('Kontrakt i oczekiwania', `<div class="body">${UI.fields(inMarket ? [{ k: 'Kontrakt do', v: m.to, num: 1 }, { k: 'Oczekiwana pensja', v: `${UI.money(m.salary)}/rok`, num: 1 }, { k: 'Zespół', v: m.team }] : [{ k: 'Seria', v: m.team }, { k: 'Potencjał', v: UI.stars(0, m.pot) }])}</div>`)}
        ${UI.panel('Co wiemy', `<div class="body"><div class="voices"><div class="vq"><span class="av">SK</span><div><p>„${m.known < 40 ? 'Widziałem go dwa razy. Szybki, ale to za mało, żeby ocenić, jak radzi sobie pod presją.' : 'Znamy go dobrze z wyścigów. Pasma są wąskie, niespodzianek raczej nie będzie.'}”</p><small>Skaut · obserwacje: ${Math.max(1, Math.round(m.known / 12))}</small></div></div></div></div>`)}</div>
    </div></div>`;
}
const moodTone = m => ({ 'Bardzo zainteresowany': 'good', 'Zainteresowany': 'good', 'Neutralny': '', 'Raczej nie': 'warn' })[m] || '';
document.addEventListener('click', e => {
  const t = e.target.closest('[data-talk]'); if (!t) return;
  const m = DB.market.find(x => x.id === t.dataset.talk);
  const id = 200 + DB.inbox.length;
  DB.inbox.unshift({ id, kind: 'raporty', from: m.name, av: UI.initials(m.name), title: 'Rozmowy rozpoczęte: pierwsze warunki', when: 'Dziś', unread: true, link: [`#/kierowca/${m.id}`, `Profil: ${m.name}`],
    body: `Menedżer kierowcy potwierdza zainteresowanie. Oczekiwania na start: ${UI.money(m.salary)} rocznie, kontrakt na dwa sezony. Odpowiedź w ciągu tygodnia.` });
  markNav(cur.name, false); location.hash = `#/skrzynka/${id}`;
});

/* ============ PORÓWNANIE ============ */
S.porownaj = (ia = 'scheckter', ib = 'depailler', tab = 'attr') => {
  const a = DB.drivers.find(x => x.id === ia) || DB.drivers[0], b = DB.drivers.find(x => x.id === ib) || DB.drivers[1];
  const sa = DB.seasonOf(a.id), sb = DB.seasonOf(b.id), ga = DB.gridOf(a.id), gb = DB.gridOf(b.id);
  const n = x => typeof x === 'number';
  let q = [0, 0], r = [0, 0];
  sa.forEach((_, i) => { if (n(ga[i]) && n(gb[i])) q[ga[i] < gb[i] ? 0 : 1]++; if (n(sa[i]) && n(sb[i])) r[sa[i] < sb[i] ? 0 : 1]++; else if (n(sa[i]) && sb[i] === 'DNF') r[0]++; else if (n(sb[i]) && sa[i] === 'DNF') r[1]++; });
  const avg = arr => { const v = arr.filter(n); return v.length ? v.reduce((x, y) => x + y, 0) / v.length : 0; };
  const pts = id => (DB.standings.drivers.find(x => x.id === id) || { pts: 0 }).pts;
  const row = (label, va, vb, better = 'hi', fmt = x => x) => {
    const w = better === 'none' ? null : better === 'hi' ? (va > vb ? 'a' : vb > va ? 'b' : null) : (va < vb ? 'a' : vb < va ? 'b' : null);
    return `<div class="cmprow"><span class="num v ${w === 'a' ? 'win' : ''}">${fmt(va)}</span><span class="lbl">${label}</span><span class="num v ${w === 'b' ? 'win' : ''}">${fmt(vb)}</span></div>`;
  };
  const bars = (label, va, vb) => `<div class="cmprow bars"><span class="num v ${va > vb ? 'win' : ''}">${va}</span><div class="bar thin rev"><i style="width:${va * 5}%"></i></div><span class="lbl">${label}</span><div class="bar thin"><i style="width:${vb * 5}%;background:var(--t2)"></i></div><span class="num v ${vb > va ? 'win' : ''}">${vb}</span></div>`;
  const f1 = x => x.toFixed(1).replace('.', ',');
  const panes = {
    attr: AT.map(x => bars(x[1], a.attrs[x[0]], b.attrs[x[0]])).join(''),
    kariera: [['Starty', 'starts'], ['Wygrane', 'wins'], ['Pole position', 'poles'], ['Podia', 'podiums'], ['Punkty', 'points'], ['Najszybsze okrążenia', 'fl'], ['Tytuły', 'titles']].map(([l, k]) => row(l, a.career[k], b.career[k])).join(''),
    sezon: [row('Punkty', pts(a.id), pts(b.id)), row('Wygrane', sa.filter(x => x === 1).length, sb.filter(x => x === 1).length), row('Podia', sa.filter(x => n(x) && x <= 3).length, sb.filter(x => n(x) && x <= 3).length),
      row('Średnia pozycja startowa', avg(ga), avg(gb), 'lo', f1), row('Średnia pozycja na mecie', avg(sa), avg(sb), 'lo', f1), row('Nieukończone', sa.filter(x => x === 'DNF').length, sb.filter(x => x === 'DNF').length, 'lo')].join(''),
    pojedynki: [row('Kwalifikacje', q[0], q[1]), row('Wyścigi', r[0], r[1]), row('Różnica w kwalifikacjach', '−0,21 s', '+0,21 s', 'none'), row('Wyścigi w deszczu', 0, 2)].join(''),
  };
  const side = (d, cls) => `<a class="cmp-side ${cls}" href="#/kierowca/${d.id}"><span class="face">${UI.initials(d.name)}<i>${d.no}</i></span><div><h2>${d.name}</h2>${UI.fields([{ k: 'Kraj', v: UI.flag(d.nat, 'md') + ' ' + d.nat }, { k: 'Wiek', v: d.age, num: 1 }, { k: 'Ocena', v: UI.stars(d.stars, d.pot) }])}</div></a>`;
  return head('Porównanie', '', UI.btn('Skład', { href: '#/kierowcy', icon: UI.back })) +
  `<section class="panel cmp" data-scope>
    <div class="cmp-top">${side(a, 'l')}<div class="cmp-mid">${UI.tabs('cmp', [['attr', 'Atrybuty'], ['kariera', 'Kariera'], ['sezon', 'Sezon 1976'], ['pojedynki', 'Pojedynki']], tab)}</div>${side(b, 'r')}</div>
    ${Object.entries(panes).map(([k, v]) => `<div class="cmp-body" data-pane="cmp:${k}" ${k !== tab ? 'hidden' : ''}>${v}</div>`).join('')}
  </section>`;
};
