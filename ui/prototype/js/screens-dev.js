/* Ekran „Auto i rozwój” (PP-043, ścieżka A: autonomiczni inżynierowie).
   Gracz ustala podział zasobów i priorytety obszarów i zatwierdza przyciskiem „Potwierdź”. Projekty wybierają inżynierowie:
   ekran pokazuje je jako stan / dlaczego / prognoza. Gotowa koncepcja nie wchodzi do auta po upływie zegara: szef ją
   zatwierdza („Wdrażamy teraz”, potem produkcja) albo czeka („Czekamy”). Odpowiedź inżyniera jest wiadomością ze skrzynki.
   UI tylko wyświetla dane z DB.car.dev: gracz widzi przedziały i szacunki, nigdy ukrytych wartości (TECH §3). */
{
  const DEV = DB.car.dev;
  const COMMIT = 'Wdrażamy teraz', WAIT = 'Czekamy', CUT = 'Tniemy projekt';
  const STREAMS = [['current', 'Bieżące auto', 'var(--t1)'], ['account', 'Konto rozwoju', 'var(--a2)'], ['nextYear', 'Przyszły rok', 'var(--t2)']];
  const streamOf = id => STREAMS.find(s => s[0] === id);
  const streamTag = id => `<span class="st" style="--tone:${streamOf(id)[2]}">${streamOf(id)[1]}</span>`;
  const dec = (v, d = 2) => v.toFixed(d).replace('.', ',');
  const bandTxt = ([a, b], unit = 's') => unit === 'pkt' ? `${dec(a, 1)}–${dec(b, 1)} pkt` : `${dec(a)}–${dec(b)} s`;
  const days = n => UI.n(n, 'dzień', 'dni', 'dni');
  const mailOf = id => DB.inbox.find(m => m.id === id);
  const person = id => DB.staff.find(s => s.id === id);

  /* plan zatwierdzony (STATE.dev) i szkic, który gracz właśnie układa; nic nie działa przed „Potwierdź” */
  STATE.dev = { plan: { ...DEV.plan }, prio: Object.fromEntries(DEV.priorities.map(p => [p.id, p.value])) };
  let draft = null;
  const fresh = () => ({ plan: { ...STATE.dev.plan }, prio: { ...STATE.dev.prio } });
  const sum = () => STREAMS.reduce((a, s) => a + draft.plan[s[0]], 0);
  const changed = () => STREAMS.some(s => draft.plan[s[0]] !== STATE.dev.plan[s[0]]) || DEV.priorities.some(p => draft.prio[p.id] !== STATE.dev.prio[p.id]);

  const conceptState = () => { const d = STATE.decisions[DEV.concept.decisionMail]; return d === COMMIT ? 'production' : d === WAIT ? 'waiting' : 'ready'; };

  /* ---------- decyzja: wybór opcji, osobny przycisk „Potwierdź” ---------- */
  function decisionFoot(m) {
    const chosen = STATE.decisions[m.id];
    return chosen
      ? `<div class="stamp">${UI.st('Decyzja podjęta', 'good')}<b>${chosen}</b></div>`
      : `<div class="confirm">${UI.fields([{ k: 'Termin', v: m.due, cls: 'bad' }, { k: 'Wybór', v: '<span class="pick">—</span>' }])}<button class="btn primary do-confirm" disabled>${UI.icon(UI.check, 17)}<span>Potwierdź</span></button></div>`;
  }
  const decisionBlock = m => choiceCards(m) + decisionFoot(m);

  /* ---------- koncepcja: status, przedział dalszego zysku, czas do wyścigu, czas produkcji ---------- */
  function conceptPanel() {
    const k = DEV.concept, m = mailOf(k.decisionMail), s = conceptState(), pr = k.production;
    const toRace = DB.nextRace.day - STATE.now().day;
    const race = `${UI.flag(DB.calendar[DB.nextRace.round - 1][1])} ${toRace ? days(toRace) : 'Dziś'}`;
    const tag = { ready: UI.st('Gotowa do decyzji', 'hi', true), production: UI.st('W produkcji', 'team', true), waiting: UI.st('Rozwijana dalej', 'ink') }[s];
    const fields = s === 'production'
      ? [{ k: 'Koniec produkcji', v: pr.ends, num: 1 }, { k: 'Pierwszy wyścig z ' + k.name, v: `${k.firstRace.name} · ${k.firstRace.round}` }, { k: 'Do wyścigu', v: race, num: 1 }, { k: 'Koszt produkcji', v: UI.money(pr.cost), num: 1 }]
      : [{ k: 'Dalszy zysk', v: `+${bandTxt(k.furtherGain)}/okr.`, num: 1 }, { k: 'Do wyścigu', v: race, num: 1 }, { k: 'Produkcja', v: `${days(pr.days)} · ${UI.n(k.racesOnOldCar, 'wyścig', 'wyścigi', 'wyścigów')}`, num: 1 }, { k: 'Koszt produkcji', v: UI.money(pr.cost), num: 1 }];
    const body = s === 'ready' ? decisionBlock(m) : s === 'production' ? timeline(k.schedule, pr.days, `Koniec produkcji · ${pr.ends}`) + decisionFoot(m) : timeline(k.wait.schedule, k.wait.days, `Kolejna decyzja · ${m.due}`) + decisionFoot(m);
    return `<section class="panel concept ${s}" id="dev-concept"><header><h2>Koncepcja ${k.name}</h2>${tag}</header>
      <div class="body">${UI.fields(fields, 'boxed eq')}${body}</div></section>`;
  }
  /* oś czasu: wyścigi do końca okresu (produkcji albo oczekiwania); wyścig po końcu produkcji to pierwszy z nowym autem */
  function timeline(schedule, end, endLabel) {
    const total = Math.max(end, ...schedule.map(x => x[2]));
    const mk = schedule.map(([round, c, d]) => `<span class="mk ${d > end ? 'new' : ''}" style="left:${d / total * 100}%">${UI.flag(c)}<b class="num">${round}</b></span>`).join('');
    return `<div class="ptl"><div class="ptl-track"><i style="width:${end / total * 100}%"></i>${mk}</div>
      <div class="ptl-lab"><span class="meta">Dziś</span><span class="meta" style="left:${end / total * 100}%">${endLabel}</span></div></div>`;
  }

  /* ---------- odpowiedź inżyniera, jak wiadomość ze skrzynki ---------- */
  function replyPanel() {
    const p = DEV.projects.find(x => x.replyMail), m = mailOf(p.replyMail), e = person(p.engineer), chosen = STATE.decisions[m.id];
    const tag = chosen ? UI.st('Decyzja podjęta', 'good') : UI.st('do ' + m.due, 'bad', true);
    return `<section class="panel reply" id="dev-reply"><header><a class="person" href="#/osoba/${e.id}"><span class="av">${UI.initials(e.name)}</span><div><b>${e.name}</b><small>${p.name}</small></div></a>${tag}</header>
      <div class="body"><p class="letter">${m.body}</p>${decisionBlock(m)}</div></section>`;
  }

  /* ---------- projekty: stan / dlaczego / prognoza ---------- */
  function projectRow(p) {
    const e = person(p.engineer), a = DEV.priorities.find(x => x.id === p.area), f = p.forecast, m = p.replyMail && mailOf(p.replyMail), pick = m && STATE.decisions[m.id];
    const cut = pick === CUT;
    const flag = !m ? '' : !pick ? UI.st('Prośba inżyniera', 'warn') : cut ? UI.st('Zamknięty przed czasem', 'bad') : UI.st('Plan utrzymany', 'good');
    const gain = `+${bandTxt(f.gain, f.unit)}${f.unit === 'pkt' ? '' : '/okr.'}`;
    return `<tr><td><b>${p.name}</b><div class="pc-sub">${streamTag(p.stream)}${flag}</div></td>
      <td><b class="num pct">${p.progress}%</b><div class="bar thin"><i style="width:${p.progress}%;background:${cut ? 'var(--ink3)' : streamOf(p.stream)[2]}"></i></div></td>
      <td><p class="why">„${p.why}”</p><small class="muted"><a class="plain" href="#/osoba/${e.id}">${e.name.split(' ').pop()}</a> · ${a.name} <span class="num">${STATE.dev.prio[a.id]}/10</span></small></td>
      <td><b class="num">${cut ? `Efekt za ${p.progress}% prac` : gain}</b><small class="${f.late && !cut ? 'bad' : 'muted'}">${cut ? 'zamknięty' : 'do ' + f.finish}</small>${cut ? '' : `<small class="muted">ryzyko ${f.risk[0]}–${f.risk[1]}%</small>`}</td></tr>`;
  }
  function projectsPanel(tab = 'now') {
    const now = `<table class="table dev-proj" data-pane="proj:now" ${tab !== 'now' ? 'hidden' : ''}><thead><tr><th>Projekt</th><th>Stan</th><th>Dlaczego</th><th>Prognoza</th></tr></thead><tbody>${DEV.projects.map(projectRow).join('')}</tbody></table>`;
    const done = `<table class="table dev-proj done" data-pane="proj:done" ${tab !== 'done' ? 'hidden' : ''}><thead><tr><th>Projekt</th><th>Wdrożono</th><th>Efekt, s/okr.</th><th>Zrozumienie</th></tr></thead><tbody>${DEV.finished.map(f => `<tr><td><b>${f.name}</b><div class="pc-sub">${streamTag(f.stream)}</div></td><td>${f.done}</td><td class="num">+${bandTxt(f.effect)}</td>
      <td><b class="num pct">${f.understanding}%</b><div class="bar thin"><i style="width:${f.understanding}%;background:var(--t1)"></i></div></td></tr>`).join('')}</tbody></table>`;
    const axes = `<div class="body axes" data-pane="proj:axes" ${tab !== 'axes' ? 'hidden' : ''}>${DB.car.axes.map(a => `<div class="axis"><div class="alab"><span>${a[1]}</span><b>${a[0]}</b><span>${a[2]}</span></div><div class="atrack"><i style="left:${a[3]}%"></i></div></div>`).join('')}</div>`;
    return `<section class="panel projects tbl" id="dev-projects"><header><h2>Projekty</h2>${UI.tabs('proj', [['now', 'W toku'], ['done', 'Zakończone'], ['axes', 'Koncepcja auta']], tab)}</header>${now}${done}${axes}</section>`;
  }

  /* ---------- podział zasobów i priorytety ---------- */
  function splitPanel() {
    const streams = STREAMS.map(([id, name, col]) => `<div class="stream" data-s="${id}"><span class="meta"><i style="background:${col}"></i>${name}</span>
        <div class="step"><button class="btn sm" data-d="-5" aria-label="Mniej: ${name}">−</button><span class="val"><b class="num"></b><small class="delta muted"></small></span><button class="btn sm" data-d="5" aria-label="Więcej: ${name}">+</button></div></div>`).join('');
    const prios = DEV.priorities.map(a => `<div class="prio" data-a="${a.id}"><div class="pn"><span>${a.name}</span><span class="num rk" title="Miejsce w stawce" style="color:${UI.rankColor(a.rank, 16, 34)}">${a.rank}.</span></div>
        <div class="pr"><div class="pips" role="radiogroup" aria-label="Priorytet: ${a.name}">${Array.from({ length: 10 }, (_, i) => `<button class="pip" role="radio" data-v="${i + 1}" aria-label="${i + 1}"></button>`).join('')}</div><b class="num pv"></b></div></div>`).join('');
    return `<section class="panel plan" id="dev-plan"><header><h2>Podział zasobów</h2><span class="plan-st"></span></header>
      <div class="body"><div class="splitbar">${STREAMS.map(([id, , col]) => `<i data-s="${id}" style="background:${col}"></i>`).join('')}</div>
        <div class="streams">${streams}</div>
        <div class="prios-h"><span class="meta">Priorytet obszarów</span><span class="meta">Miejsce w stawce</span></div><div class="prios">${prios}</div>
        <div class="confirm"><div class="fields"><div class="fld"><span class="meta">Suma</span><span class="v num total"></span></div></div><button class="btn primary plan-go" disabled>${UI.icon(UI.check, 17)}<span>Potwierdź</span></button></div></div></section>`;
  }
  function paintPlan(root) {
    const total = sum(), dirty = changed();
    STREAMS.forEach(([id]) => {
      const v = draft.plan[id], was = STATE.dev.plan[id], row = root.querySelector(`.stream[data-s="${id}"]`);
      row.querySelector('.val b').textContent = v + '%';
      row.querySelector('.delta').textContent = v !== was ? `z ${was}%` : '';
      row.classList.toggle('chg', v !== was);
      row.querySelector('[data-d="-5"]').disabled = v <= 0;
      row.querySelector('[data-d="5"]').disabled = v >= 100;
      root.querySelector(`.splitbar [data-s="${id}"]`).style.flex = v;
    });
    DEV.priorities.forEach(a => {
      const v = draft.prio[a.id], row = root.querySelector(`.prio[data-a="${a.id}"]`);
      row.querySelectorAll('.pip').forEach(p => { p.classList.toggle('on', +p.dataset.v <= v); p.setAttribute('aria-checked', +p.dataset.v === v); });
      row.querySelector('.pv').textContent = v;
      row.classList.toggle('chg', v !== STATE.dev.prio[a.id]);
    });
    const t = root.querySelector('.total'); t.textContent = total + '%'; t.classList.toggle('bad', total !== 100);
    root.querySelector('.plan-st').innerHTML = dirty ? UI.st('Niezatwierdzony', 'warn') : UI.st('Obowiązuje', 'good');
    root.querySelector('.plan-go').disabled = !(dirty && total === 100);
  }

  /* ---------- ekran ---------- */
  S.auto = () => {
    draft = fresh();
    const c = DB.car, acc = DEV.account;
    return head('Auto i rozwój', UI.fields([{ k: 'Auto', v: c.name }, { k: 'Koncepcja', v: c.concept }, { k: 'Konto rozwoju', v: bandTxt(acc.band, acc.unit), num: 1 }, { k: 'Utrata konta w 1977', v: `${acc.ruleLoss[0]}–${acc.ruleLoss[1]}%`, num: 1, cls: 'bad' }])) +
      `<div class="dev"><div class="dev-cell">${conceptPanel()}</div><div class="dev-cell">${replyPanel()}</div><div class="dev-cell">${projectsPanel()}</div><div class="dev-cell">${splitPanel()}</div></div>`;
  };

  /* podmienia jeden panel i podpina go ponownie (bez przerysowania całego ekranu) */
  function swap(id, html, bind) {
    const old = document.getElementById(id); if (!old) return;
    const tpl = document.createElement('template'); tpl.innerHTML = html.trim();
    const el = tpl.content.firstElementChild; old.replaceWith(el);
    UI.initTabs(el); bind(el);
  }
  const activeTab = () => { const b = document.querySelector('#dev-projects .tabs button.on'); return b ? b.dataset.v : 'now'; };
  function bindDecision(root, m) {
    const go = root.querySelector('.do-confirm');
    root.querySelectorAll('.choice:not([disabled])').forEach(ch => ch.onclick = () => {
      root.querySelectorAll('.choice').forEach(x => x.setAttribute('aria-checked', x === ch));
      root.querySelector('.pick').textContent = ch.dataset.opt; go.disabled = false;
    });
    if (go) go.onclick = () => {
      const sel = root.querySelector('.choice[aria-checked="true"]'); if (!sel) return;
      STATE.decisions[m.id] = sel.dataset.opt;
      swap('dev-concept', conceptPanel(), bindConcept);
      swap('dev-reply', replyPanel(), bindReply);
      swap('dev-projects', projectsPanel(activeTab()), () => {});
      UI.toast(sel.dataset.opt === COMMIT ? `Produkcja ${DEV.concept.name} rozpoczęta` : sel.dataset.opt === WAIT ? 'Koncepcja rozwijana dalej' : sel.dataset.opt === CUT ? 'Projekt zamknięty przed czasem' : 'Plan utrzymany');
    };
  }
  const bindConcept = el => bindDecision(el, mailOf(DEV.concept.decisionMail));
  const bindReply = el => bindDecision(el, mailOf(DEV.projects.find(x => x.replyMail).replyMail));
  function bindPlan(el) {
    el.querySelectorAll('.stream [data-d]').forEach(b => b.onclick = () => {
      const id = b.closest('.stream').dataset.s, v = draft.plan[id] + +b.dataset.d;
      if (v >= 0 && v <= 100) { draft.plan[id] = v; paintPlan(el); }
    });
    el.querySelectorAll('.prio .pip').forEach(p => p.onclick = () => {
      const id = p.closest('.prio').dataset.a, v = +p.dataset.v;
      draft.prio[id] = draft.prio[id] === v ? v - 1 : v; paintPlan(el);
    });
    el.querySelector('.plan-go').onclick = () => {
      STATE.dev = { plan: { ...draft.plan }, prio: { ...draft.prio } };
      swap('dev-projects', projectsPanel(activeTab()), () => {});
      paintPlan(el); UI.toast('Podział zasobów zatwierdzony');
    };
    paintPlan(el);
  }
  S.auto.after = () => {
    bindConcept(document.getElementById('dev-concept'));
    bindReply(document.getElementById('dev-reply'));
    bindPlan(document.getElementById('dev-plan'));
  };
}
