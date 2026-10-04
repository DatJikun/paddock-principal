/* =========================================================================
 * PADDOCK PRINCIPAL · RACE MODE (PP-052)
 * -------------------------------------------------------------------------
 * A separate full-screen mode, not a screen inside the app shell. Entering it
 * takes the whole shell (sidebar, top bar, dashboard) out of the document and
 * mounts #race-root; leaving puts the very same shell node back, so the
 * dashboard comes back exactly as it was.
 *
 * The mode only displays: PositionFrames go to the map, timing goes to the
 * tower and the car card, events become radio / race-control lines. The one
 * thing it sends back is a pit call (feed.requestPit), a player decision.
 * The feed is MockRaceFeed until the race tape is wired (R-FRAMES #152).
 * ========================================================================= */

(() => {
  'use strict';

  const I18N = {
    pl: {
      lap: 'Okrążenie', flag: 'Flaga', time: 'Czas', weather: 'Pogoda', pace: 'Tempo', air: 'Powietrze', trackT: 'Tor',
      green: 'Zielona', yellow: 'Żółta', chequered: 'Szachownica', sunny: 'Słonecznie',
      tower: 'Klasyfikacja', toGap: 'Do lidera', toInt: 'Odstęp', pos: 'Poz.', no: 'Nr', driver: 'Kierowca', tyres: 'Opony',
      inLane: 'Aleja', inBox: 'Postój', fin: 'Meta', lapsDown: n => `+${n} okr.`,
      position: 'Pozycja', speed: 'Prędkość', last: 'Ostatnie okr.', best: 'Najlepsze okr.', gapLeader: 'Do lidera', gapAhead: 'Do auta przed', leader: 'Lider', stops: 'Zjazdy',
      follow: 'Śledź', following: 'Śledzisz', close: 'Zamknij',
      pitwall: 'Boks', callIn: 'Wezwij do boksu', confirm: 'Potwierdź', cancel: 'Anuluj', called: 'Zjazd na tym okrążeniu',
      S: 'Miękkie', M: 'Średnie', H: 'Twarde',
      engineer: 'Inżynier', control: 'Dyrekcja wyścigu', race: 'Wyścig',
      zoomIn: 'Przybliż', zoomOut: 'Oddal', fit: 'Cały tor', towerKey: 'Klasyfikacja (T)', pause: 'Pauza (spacja)',
      result: 'Wynik', back: 'Wróć do gry', gapCol: 'Strata', team: 'Zespół',
      ev: {
        start: () => 'Start wyścigu',
        fastestLap: (e, c, t) => `Najszybsze okrążenie: #${c.no} ${c.short}, ${t}`,
        overtake: (e, c, o) => `#${c.no} ${c.short} wyprzedza #${o.no} ${o.short} i jest P${e.value}`,
        pitIn: (e, c) => `#${c.no} ${c.short} zjeżdża do boksu`,
        pitOut: (e, c) => `#${c.no} ${c.short} wraca na tor po postoju ${fmtS(e.value)} s`,
        chequered: (e, c) => `Flaga w szachownicę. Wygrywa #${c.no} ${c.short}`,
      },
      radio: {
        boxThisLap: () => 'Box, box. Zjeżdżaj na tym okrążeniu.',
        tyresGoing: e => `Opony mają ${e.value}%. Czas okrążenia zacznie rosnąć.`,
        pitDone: e => `Postój ${fmtS(e.value)} s. Jedziesz, droga wolna.`,
        finished: () => 'Meta. Dobra robota, zjeżdżaj spokojnie.',
      },
    },
    en: {
      lap: 'Lap', flag: 'Flag', time: 'Time', weather: 'Weather', pace: 'Speed', air: 'Air', trackT: 'Track',
      green: 'Green', yellow: 'Yellow', chequered: 'Chequered', sunny: 'Sunny',
      tower: 'Classification', toGap: 'To leader', toInt: 'Interval', pos: 'Pos', no: 'No', driver: 'Driver', tyres: 'Tyres',
      inLane: 'Pit lane', inBox: 'Stopped', fin: 'Finished', lapsDown: n => `+${n} ${n === 1 ? 'lap' : 'laps'}`,
      position: 'Position', speed: 'Speed', last: 'Last lap', best: 'Best lap', gapLeader: 'To leader', gapAhead: 'To car ahead', leader: 'Leader', stops: 'Stops',
      follow: 'Follow', following: 'Following', close: 'Close',
      pitwall: 'Pit wall', callIn: 'Call in', confirm: 'Confirm', cancel: 'Cancel', called: 'Pitting this lap',
      S: 'Soft', M: 'Medium', H: 'Hard',
      engineer: 'Engineer', control: 'Race control', race: 'Race',
      zoomIn: 'Zoom in', zoomOut: 'Zoom out', fit: 'Whole track', towerKey: 'Classification (T)', pause: 'Pause (space)',
      result: 'Result', back: 'Back to the game', gapCol: 'Gap', team: 'Team',
      ev: {
        start: () => 'Race start',
        fastestLap: (e, c, t) => `Fastest lap: #${c.no} ${c.short}, ${t}`,
        overtake: (e, c, o) => `#${c.no} ${c.short} passes #${o.no} ${o.short} for P${e.value}`,
        pitIn: (e, c) => `#${c.no} ${c.short} comes into the pits`,
        pitOut: (e, c) => `#${c.no} ${c.short} rejoins after a ${fmtS(e.value)} s stop`,
        chequered: (e, c) => `Chequered flag. #${c.no} ${c.short} wins`,
      },
      radio: {
        boxThisLap: () => 'Box, box. Pit this lap.',
        tyresGoing: e => `Tyres at ${e.value}%. Lap times will start to drop off.`,
        pitDone: e => `${fmtS(e.value)} s stop. Go, the road is clear.`,
        finished: () => 'Chequered flag. Good job, bring it home easy.',
      },
    },
  };
  const lang = () => (typeof PREF !== 'undefined' && PREF.lang === 'en' ? 'en' : 'pl');
  const T = k => I18N[lang()][k];
  const dec = s => lang() === 'pl' ? s.replace('.', ',') : s;
  const fmtS = v => dec(Number(v).toFixed(1));
  const fmtLap = ms => { if (!ms) return '—'; const m = Math.floor(ms / 60000); return `${m}:${dec(((ms % 60000) / 1000).toFixed(1)).padStart(4, '0')}`; };
  const fmtClock = ms => { const s = Math.floor(ms / 1000), h = Math.floor(s / 3600), m = Math.floor(s / 60) % 60; return `${h}:${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; };
  const fmtGap = g => !g ? '' : g.laps != null ? T('lapsDown')(g.laps) : `+${fmtS(g.s)}`;
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const ICON = {
    pause: '<path d="M8 5v14M16 5v14"/>', play: '<path d="M7 5l12 7-12 7z"/>', plus: '<path d="M12 5v14M5 12h14"/>', minus: '<path d="M5 12h14"/>',
    fit: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>', follow: '<circle cx="12" cy="12" r="3"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4"/>',
    tower: '<path d="M4 6h16M4 12h10M4 18h13"/>', x: '<path d="M6 6l12 12M18 6L6 18"/>', sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
    radio: '<path d="M4 9h16v11H4zM8 9l9-5"/><circle cx="9" cy="14.5" r="2"/>',
  };
  const SPEEDS = [1, 4, 16, 64];

  /* re-render only on change, so a click is never lost to a rebuilt button */
  const put = (el, html) => { if (el._h !== html) { el._h = html; el.innerHTML = html; } };

  let st = null; /* everything that lives only while the mode is mounted */

  /* ---------- mount ---------- */
  function mount(root, round) {
    const ri = Math.max(0, Math.min(DB.calendar.length - 1, (Number(round) || 9) - 1));
    const cal = DB.calendar[ri], track = DB.tracks[cal[3]];
    const feed = new MockRaceFeed({ trackKey: cal[3], laps: cal[4], seed: 1976 + ri });
    const entries = new Map(feed.entries.map(e => [e.carId, e]));
    const ac = new AbortController();
    st = { root, feed, entries, ac, ri, cal, track, speed: 4, paused: false, towerOn: true, gapMode: 'gap', selected: null,
      timing: null, raf: 0, last: 0, domAt: 0, radio: [], pitUi: {}, finishedShown: false };

    root.innerHTML = `
      <div class="rm" data-tower="on">
        <canvas class="rm-map" aria-label="${esc(track.name)}"></canvas>
        <header class="rm-status ov">
          <div class="seg gp"><span class="rno">R${ri + 1}</span><span><b>${esc(cal[2])}</b><small>${esc(track.name)}</small></span></div>
          <div class="seg"><span class="meta">${T('lap')}</span><b class="num" data-k="lap">1<i>/${cal[4]}</i></b></div>
          <div class="seg"><span class="meta">${T('flag')}</span><b class="flagchip green" data-k="flag">${T('green')}</b></div>
          <div class="seg"><span class="meta">${T('time')}</span><b class="num" data-k="time">0:00:00</b></div>
          <div class="seg"><span class="meta">${T('weather')}</span><b class="wx">${UI.icon(ICON.sun, 16)}<span>${T('sunny')}</span><small>${T('air')} ${feed.conditions.airC}° · ${T('trackT')} ${feed.conditions.trackC}°</small></b></div>
        </header>
        <div class="rm-pace ov">
          <div class="seg"><span class="meta">${T('pace')}</span>
            <span class="speeds" role="group">
              <button class="ctl ico" data-act="pause" title="${T('pause')}" aria-label="${T('pause')}">${UI.icon(ICON.pause, 15)}</button>
              ${SPEEDS.map(s => `<button class="ctl" data-speed="${s}" aria-pressed="${s === 4}">${s}×</button>`).join('')}
            </span></div>
          <div class="seg back" hidden><button class="rm-btn primary" data-act="exit">${UI.icon(UI.back, 17)}<span>${T('back')}</span></button></div>
        </div>
        <aside class="rm-tower ov" aria-label="${T('tower')}">
          <header><b>${T('tower')}</b>
            <span class="gapsw" role="group"><button class="ctl" data-gap="gap" aria-pressed="true">${T('toGap')}</button><button class="ctl" data-gap="int" aria-pressed="false">${T('toInt')}</button></span>
            <button class="ctl ico" data-act="tower" title="${T('towerKey')}" aria-label="${T('towerKey')}">${UI.icon(ICON.tower, 15)}<kbd>T</kbd></button>
          </header>
          <table><thead><tr><th class="c">${T('pos')}</th><th class="c">${T('no')}</th><th>${T('driver')}</th><th class="r" data-k="gaphead">${T('toGap')}</th><th class="c">${T('tyres')}</th></tr></thead><tbody></tbody></table>
        </aside>
        <button class="rm-tower-tab ov ctl" data-act="tower" title="${T('towerKey')}" aria-label="${T('towerKey')}">${UI.icon(ICON.tower, 16)}<kbd>T</kbd></button>
        <div class="rm-right">
          <section class="rm-card ov" hidden></section>
          <div class="rm-zoom ov">
          <button class="ctl ico" data-act="in" title="${T('zoomIn')} (+)" aria-label="${T('zoomIn')}">${UI.icon(ICON.plus, 16)}</button>
          <button class="ctl ico" data-act="out" title="${T('zoomOut')} (−)" aria-label="${T('zoomOut')}">${UI.icon(ICON.minus, 16)}</button>
          <button class="ctl ico" data-act="fit" title="${T('fit')} (0)" aria-label="${T('fit')}">${UI.icon(ICON.fit, 16)}</button>
          <button class="ctl ico" data-act="follow" title="${T('follow')} (F)" aria-label="${T('follow')}" aria-pressed="false">${UI.icon(ICON.follow, 16)}</button>
        </div>
          <section class="rm-pit ov"><header><b>${T('pitwall')}</b></header><div class="pit-rows"></div></section>
        </div>
        <div class="rm-radio" aria-live="polite"></div>
        <div class="rm-finish" hidden></div>
      </div>`;

    const $ = s => root.querySelector(s);
    st.$ = $;
    const shape = TrackShape.resolve(track);
    const map = new RaceMap($('.rm-map'), shape.spline, entries, shape.corners);
    st.map = map;
    map.onSelect = id => select(id);
    map.onFollow = on => { $('[data-act=follow]').setAttribute('aria-pressed', on); renderCard(); };
    layoutInsets(); map.fit();

    const o = { signal: ac.signal };
    root.addEventListener('click', onClick, o);
    addEventListener('keydown', onKey, o);
    addEventListener('resize', () => { layoutInsets(); map.resize(); }, o);

    const first = feed.step(0);
    apply(first, true);
    map.render(first.frame);
    st.last = performance.now();
    st.raf = requestAnimationFrame(loop);
  }

  function unmount() {
    if (!st) return;
    cancelAnimationFrame(st.raf); st.ac.abort(); st.map.destroy();
    st.root.innerHTML = ''; st = null;
  }

  /* the map's free area is what the overlays leave uncovered */
  function layoutInsets() {
    const r = st.root.getBoundingClientRect(), q = s => st.$(s).getBoundingClientRect();
    const tower = st.towerOn ? q('.rm-tower').right - r.left + 12 : 12;
    st.map.setInset({ l: tower, t: q('.rm-status').bottom - r.top + 8, r: r.right - q('.rm-right').left + 8, b: 12 });
  }

  /* ---------- loop ---------- */
  function loop(now) {
    if (!st) return;
    const dt = Math.min(100, now - st.last); st.last = now;
    if (!st.paused) {
      const out = st.feed.step(dt * st.speed);
      st.map.render(out.frame);
      apply(out, now - st.domAt > 200);
      if (now - st.domAt > 200) st.domAt = now;
    } else st.map.draw();
    st.raf = requestAnimationFrame(loop);
  }

  function apply(out, dom) {
    st.timing = out.timing;
    out.events.forEach(onEvent);
    if (!dom) return;
    const t = out.timing, $ = st.$;
    $('[data-k=lap]').innerHTML = `${t.lap}<i>/${t.laps}</i>`;
    $('[data-k=time]').textContent = fmtClock(t.raceTimeMs);
    const fl = $('[data-k=flag]'); fl.className = `flagchip ${t.flag}`; fl.textContent = T(t.flag);
    renderTower(); renderCard(); renderPit();
    if (t.flag === 'chequered') {
      $('.seg.back').hidden = false;
      if (!st.finishedShown && t.rows.every(r => r.finished)) { st.finishedShown = true; renderFinish(); }
    }
  }

  /* ---------- tower ---------- */
  function renderTower() {
    const rows = st.timing.rows;
    put(st.$('.rm-tower tbody'), rows.map(r => {
      const e = st.entries.get(r.carId), lv = e.livery;
      const gap = r.pos === 1 ? `<span class="lead">${T('leader')}</span>` : fmtGap(st.gapMode === 'gap' ? r.gap : r.int);
      const tag = r.finished ? `<span class="chq" title="${T('fin')}" aria-label="${T('fin')}"></span>` : r.pit ? `<span class="tag pit">${r.pit === 'box' ? T('inBox') : T('inLane')}</span>` : '';
      return `<tr data-car="${r.carId}" class="${e.mine ? 'mine' : ''} ${r.carId === st.selected ? 'sel' : ''}">
        <td class="c pos">${r.pos}</td>
        <td class="c"><span class="chip" style="--p:${lv.primary};--s:${lv.secondary};--x:${lv.text}">${e.no}</span></td>
        <td class="nm">${esc(e.short)}${tag}</td>
        <td class="r num">${gap}</td>
        <td class="c tyre"><span class="tc c-${r.tyre}">${r.tyre}</span><span class="num ${r.wear < 35 ? 'low' : ''}">${r.wear}</span></td></tr>`;
    }).join(''));
  }

  /* ---------- selected car ---------- */
  function select(id) {
    st.selected = id || null;
    st.map.select(st.selected);
    if (!id) st.map.setFollow(false);
    renderCard(); renderTower();
  }
  function renderCard() {
    const el = st.$('.rm-card');
    const r = st.selected && st.timing.rows.find(x => x.carId === st.selected);
    if (!r) { el.hidden = true; el._h = ''; return; }
    const e = st.entries.get(r.carId), lv = e.livery, f = st.lastFrame?.cars.find(c => c.carId === r.carId);
    const speed = f ? Math.round(f.speedMps * 3.6) : 0;
    el.hidden = false;
    put(el, `
      <header>
        <span class="bignum" style="--p:${lv.primary};--s:${lv.secondary};--x:${lv.text}">${e.no}</span>
        <span class="who"><b>${esc(e.name)}</b><span class="sub">${UI.flag(e.nat)}<span>${esc(e.team)}</span></span></span>
        <button class="ctl ico" data-act="close" title="${T('close')} (Esc)" aria-label="${T('close')}">${UI.icon(ICON.x, 15)}</button>
      </header>
      <div class="tiles">
        <div><span class="meta">${T('position')}</span><b class="num">P${r.pos}</b></div>
        <div><span class="meta">${T('lap')}</span><b class="num">${r.lap}</b></div>
        <div><span class="meta">${T('speed')}</span><b class="num">${speed}<small> km/h</small></b></div>
        <div><span class="meta">${T('last')}</span><b class="num">${fmtLap(r.last)}</b></div>
        <div><span class="meta">${T('best')}</span><b class="num">${fmtLap(r.best)}</b></div>
        <div><span class="meta">${T('stops')}</span><b class="num">${r.pits}</b></div>
        <div><span class="meta">${T('gapLeader')}</span><b class="num">${r.pos === 1 ? '—' : fmtGap(r.gap)}</b></div>
        <div><span class="meta">${T('gapAhead')}</span><b class="num">${r.pos === 1 ? '—' : fmtGap(r.int)}</b></div>
        <div><span class="meta">${T('tyres')}</span><b class="num"><span class="tc c-${r.tyre}">${r.tyre}</span> ${r.wear}%</b></div>
      </div>
      <button class="rm-btn ${st.map.follow ? 'on' : ''}" data-act="follow" aria-pressed="${st.map.follow}">${UI.icon(ICON.follow, 16)}<span>${st.map.follow ? T('following') : T('follow')}</span><kbd>F</kbd></button>`);
  }

  /* ---------- pit wall: choose the tyre, then confirm (HANDOFF §3.12) ---------- */
  function renderPit() {
    const box = st.$('.pit-rows');
    const mine = st.timing.rows.filter(r => st.entries.get(r.carId).mine);
    put(box, mine.map(r => {
      const e = st.entries.get(r.carId), lv = e.livery, ui = st.pitUi[r.carId];
      let action;
      if (r.finished) action = `<span class="tag fin">${T('fin')}</span>`;
      else if (r.pit) action = `<span class="tag pit">${r.pit === 'box' ? T('inBox') : T('inLane')}</span>`;
      else if (r.called) action = `<span class="tag call">${T('called')}</span><button class="rm-btn sm" data-act="pitcancel" data-car="${r.carId}">${T('cancel')}</button>`;
      else if (ui) action = `
        <span class="tcsw" role="group">${['S', 'M', 'H'].map(c => `<button class="ctl" data-act="cmp" data-car="${r.carId}" data-cmp="${c}" aria-pressed="${ui.cmp === c}"><span class="tc c-${c}">${c}</span>${T(c)}</button>`).join('')}</span>
        <span class="row2"><button class="rm-btn sm" data-act="pitclose" data-car="${r.carId}">${T('cancel')}</button><button class="rm-btn sm primary" data-act="pitgo" data-car="${r.carId}" ${ui.cmp ? '' : 'disabled'}>${T('confirm')}</button></span>`;
      else action = `<button class="rm-btn sm" data-act="pitopen" data-car="${r.carId}">${T('callIn')}</button>`;
      return `<div class="pr ${ui && !r.called && !r.pit && !r.finished ? 'open' : ''}">
        <div class="pr-top" data-car="${r.carId}">
          <span class="chip" style="--p:${lv.primary};--s:${lv.secondary};--x:${lv.text}">${e.no}</span>
          <b class="nm">${esc(e.short)}</b>
          <span class="num pp">P${r.pos}</span>
          <span class="wear"><span class="tc c-${r.tyre}">${r.tyre}</span><span class="bar"><i style="width:${r.wear}%" class="${r.wear < 35 ? 'low' : ''}"></i></span><span class="num">${r.wear}%</span></span>
        </div>
        <div class="pr-act">${action}</div></div>`;
    }).join(''));
  }

  /* ---------- radio and race control ---------- */
  function onEvent(e) {
    const c = e.carId && st.entries.get(e.carId), o = e.other && st.entries.get(e.other);
    let kind, text;
    if (e.type === 'radio') { kind = 'radio'; text = I18N[lang()].radio[e.key](e); }
    else {
      if (e.type === 'overtake' && !(c.mine || o.mine)) return;
      if ((e.type === 'pitIn' || e.type === 'pitOut') && !c.mine) return;
      kind = e.type === 'overtake' || e.type === 'pitIn' || e.type === 'pitOut' ? 'race' : 'control';
      text = I18N[lang()].ev[e.type](e, c, e.type === 'fastestLap' ? fmtLap(e.value) : o);
    }
    st.radio.unshift({ kind, text, car: kind === 'radio' ? c : null, lap: e.lap, at: performance.now() });
    st.radio.length = Math.min(st.radio.length, 3);
    renderRadio();
  }
  function renderRadio() {
    st.$('.rm-radio').innerHTML = st.radio.map((m, i) => {
      const who = m.kind === 'radio' ? `${T('engineer')} · #${m.car.no} ${esc(m.car.short)}` : m.kind === 'race' ? T('race') : T('control');
      const lv = m.car && m.car.livery;
      return `<div class="msg ${m.kind}" style="${lv ? `--p:${lv.primary}` : ''};opacity:${1 - i * 0.2}">
        <span class="mh">${m.kind === 'radio' ? UI.icon(ICON.radio, 14) : ''}<b>${who}</b><span class="num">${T('lap')} ${m.lap}</span></span>
        <p>${esc(m.text)}</p></div>`;
    }).join('');
  }

  /* ---------- finish ---------- */
  function renderFinish() {
    const rows = st.timing.rows, mine = rows.filter(r => st.entries.get(r.carId).mine);
    const show = [...rows.slice(0, 6), ...mine.filter(r => r.pos > 6)];
    const el = st.$('.rm-finish');
    el.innerHTML = `<div class="fin-card ov">
      <header><span class="flagchip chequered">${T('chequered')}</span><b>${esc(st.cal[2])}</b><span class="meta">${T('result')}</span></header>
      <table><thead><tr><th class="c">${T('pos')}</th><th class="c">${T('no')}</th><th>${T('driver')}</th><th>${T('team')}</th><th class="r">${T('gapCol')}</th></tr></thead><tbody>
      ${show.map(r => { const e = st.entries.get(r.carId), lv = e.livery; return `<tr class="${e.mine ? 'mine' : ''}"><td class="c pos">${r.pos}</td><td class="c"><span class="chip" style="--p:${lv.primary};--s:${lv.secondary};--x:${lv.text}">${e.no}</span></td><td class="nm">${esc(e.name)}</td><td>${esc(e.team)}</td><td class="r num">${r.pos === 1 ? fmtClock(st.timing.raceTimeMs) : fmtGap(r.gap)}</td></tr>`; }).join('')}
      </tbody></table>
      <button class="rm-btn primary big" data-act="exit">${UI.icon(UI.back, 18)}<span>${T('back')}</span></button></div>`;
    el.hidden = false;
    el.animate([{ opacity: 0, transform: 'translateY(16px)' }, { opacity: 1, transform: 'none' }], { duration: 520, easing: 'cubic-bezier(.16,1,.3,1)' });
  }

  /* ---------- input ---------- */
  function setSpeed(s) {
    st.paused = false; st.speed = s;
    st.$('[data-act=pause]').innerHTML = UI.icon(ICON.pause, 15);
    st.$('[data-act=pause]').setAttribute('aria-pressed', 'false');
    st.root.querySelectorAll('[data-speed]').forEach(b => b.setAttribute('aria-pressed', Number(b.dataset.speed) === s));
  }
  function togglePause() {
    st.paused = !st.paused;
    const b = st.$('[data-act=pause]');
    b.innerHTML = UI.icon(st.paused ? ICON.play : ICON.pause, 15); b.setAttribute('aria-pressed', st.paused);
    st.root.querySelectorAll('[data-speed]').forEach(x => x.setAttribute('aria-pressed', !st.paused && Number(x.dataset.speed) === st.speed));
  }
  function toggleTower() {
    st.towerOn = !st.towerOn;
    st.$('.rm').dataset.tower = st.towerOn ? 'on' : 'off';
    layoutInsets();
    if (!st.map.follow) st.map.fit();
    st.map.draw();
  }
  function toggleFollow() {
    if (!st.selected) { const me = st.timing.rows.find(r => st.entries.get(r.carId).mine); if (me) select(me.carId); }
    st.map.setFollow(!st.map.follow);
  }

  function onClick(ev) {
    const b = ev.target.closest('[data-act],[data-speed],[data-gap],tr[data-car],.pr-top[data-car]');
    if (!b || !st) return;
    const a = b.dataset.act, car = b.dataset.car;
    if (b.dataset.speed) return setSpeed(Number(b.dataset.speed));
    if (b.dataset.gap) {
      st.gapMode = b.dataset.gap;
      st.root.querySelectorAll('[data-gap]').forEach(x => x.setAttribute('aria-pressed', x.dataset.gap === st.gapMode));
      st.$('[data-k=gaphead]').textContent = st.gapMode === 'gap' ? T('toGap') : T('toInt');
      return renderTower();
    }
    if (!a) { select(car); return; }
    switch (a) {
      case 'pause': return togglePause();
      case 'tower': return toggleTower();
      case 'in': return st.map.zoomBy(1.3);
      case 'out': return st.map.zoomBy(1 / 1.3);
      case 'fit': st.map.setFollow(false); return st.map.fit(), st.map.draw();
      case 'follow': return toggleFollow();
      case 'close': return select(null);
      case 'exit': return RaceMode.leave();
      case 'pitopen': st.pitUi[car] = { cmp: null }; return renderPit();
      case 'pitclose': delete st.pitUi[car]; return renderPit();
      case 'cmp': st.pitUi[car].cmp = b.dataset.cmp; return renderPit();
      case 'pitgo': if (st.pitUi[car]?.cmp && st.feed.requestPit(car, st.pitUi[car].cmp)) delete st.pitUi[car]; return renderPit();
      case 'pitcancel': st.feed.cancelPit(car); return renderPit();
    }
  }
  function onKey(e) {
    if (!st || e.ctrlKey || e.metaKey || e.altKey) return;
    const k = e.key;
    if (k === ' ') { e.preventDefault(); togglePause(); }
    else if (k === 't' || k === 'T') toggleTower();
    else if (k === 'f' || k === 'F') toggleFollow();
    else if (k === '+' || k === '=') st.map.zoomBy(1.3);
    else if (k === '-') st.map.zoomBy(1 / 1.3);
    else if (k === '0') { st.map.setFollow(false); st.map.fit(); st.map.draw(); }
    else if (k === 'Escape') select(null);
  }

  /* keep the latest frame for the car card's speed */
  const _render = RaceMap.prototype.render;
  RaceMap.prototype.render = function (frame) { if (st && st.map === this) st.lastFrame = frame; return _render.call(this, frame); };

  /* ---------- heavy transition: the team-colour curtain (HANDOFF §3.10) ----------
     Bands sweep in from the left and cover the screen, the mode swaps under the
     cover (title card shows the Grand Prix), then the bands sweep off to the right. */
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  function curtain(title, swap) {
    return new Promise(done => {
      const c = document.createElement('div');
      c.className = 'rm-curtain'; c.setAttribute('aria-hidden', 'true');
      c.innerHTML = `<div class="band"><i></i><i></i><i></i><div class="fill">${title ? `<span>${title}</span>` : ''}</div><i></i><i></i><i></i></div>`;
      document.body.appendChild(c);
      const band = c.firstChild;
      if (reduce) {
        c.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 200, fill: 'forwards' }).onfinish = () => {
          swap(); c.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 200, fill: 'forwards' }).onfinish = () => { c.remove(); done(); };
        };
        return;
      }
      const ease = 'cubic-bezier(.72,0,.18,1)';
      const inA = band.animate([{ transform: 'translateX(-135vw) skewX(-14deg)' }, { transform: 'translateX(-15vw) skewX(-14deg)' }], { duration: 640, easing: ease, fill: 'forwards' });
      let stage = 0;
      const out = () => {
        if (stage !== 1) return; stage = 2;
        const o = band.animate([{ transform: 'translateX(-15vw) skewX(-14deg)' }, { transform: 'translateX(110vw) skewX(-14deg)' }], { duration: 680, easing: ease, fill: 'forwards' });
        let fin = false; const end = () => { if (fin) return; fin = true; c.remove(); done(); };
        o.onfinish = end; setTimeout(end, 900);
      };
      const mid = () => {
        if (stage) return; stage = 1;
        swap();
        const t = band.querySelector('.fill span');
        if (t) t.animate([{ opacity: 0, transform: 'translateX(-24px)' }, { opacity: 1, transform: 'none' }], { duration: 300, easing: 'ease-out', fill: 'forwards' });
        setTimeout(out, title ? 520 : 140);
      };
      inA.onfinish = mid; setTimeout(mid, 900); /* fallback when animations do not tick (background tab) */
    });
  }

  /* ---------- public API used by the router ---------- */
  let shell = null, chain = Promise.resolve();
  const RaceMode = {
    active: false,
    returnHash: '#/pulpit',
    enter(round, returnHash) {
      if (RaceMode.active) return chain;
      RaceMode.active = true; RaceMode.returnHash = returnHash || '#/pulpit';
      const ri = Math.max(0, Math.min(DB.calendar.length - 1, (Number(round) || 9) - 1));
      const title = esc(DB.calendar[ri][2]);
      chain = chain.then(() => curtain(title, () => {
        shell = document.querySelector('.app');
        if (shell) shell.remove();                  /* the shell is not rendered at all in race mode */
        const air = document.getElementById('air'); if (air) air.style.display = 'none';
        const root = document.getElementById('race-root');
        root.hidden = false;
        mount(root, round);
      }));
      return chain;
    },
    exit() {
      if (!RaceMode.active) return chain;
      RaceMode.active = false;
      chain = chain.then(() => curtain('', () => {
        const root = document.getElementById('race-root');
        unmount(); root.hidden = true;
        if (shell) document.body.insertBefore(shell, root);   /* the very same node: dashboard state intact */
        shell = null;
        const air = document.getElementById('air'); if (air && (typeof PREF === 'undefined' || PREF.air === 'on')) air.style.display = '';
        document.dispatchEvent(new CustomEvent('race-mode-exit'));
      }));
      return chain;
    },
    /* "Back to the game": go back to where the player came from; the router calls exit() */
    leave() { location.hash = RaceMode.returnHash; },
  };
  window.RaceMode = RaceMode;
})();
