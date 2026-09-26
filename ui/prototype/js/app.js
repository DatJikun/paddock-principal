/* Router, przejścia, górny pasek, nawigacja, ustawienia i tło. */
const NAV = [
  ['pulpit','Pulpit','<path d="M4 11l8-7 8 7v9H4z"/>'],
  ['skrzynka','Skrzynka','<path d="M4 6h16v12H4z"/><path d="M4 7l8 6 8-6"/>'],
  ['kalendarz','Kalendarz','<rect x="4" y="5" width="16" height="15" rx="2"/><path d="M8 3v4M16 3v4M4 10h16"/>'],
  ['klasyfikacje','Klasyfikacje','<path d="M5 20V11M12 20V5M19 20v-6"/>'],
  null,
  ['kierowcy','Kierowcy','<circle cx="12" cy="8" r="4"/><path d="M4 20c1-4 4-6 8-6s7 2 8 6"/>'],
  ['personel','Personel','<circle cx="9" cy="9" r="3"/><circle cx="17" cy="10" r="2.5"/><path d="M3 19c1-3 3-5 6-5s5 2 6 5M15 15c3 0 5 1.5 6 4"/>'],
  ['akademia','Akademia','<path d="M3 9l9-4 9 4-9 4z"/><path d="M7 11v5c3 2 7 2 10 0v-5"/>'],
  ['auto','Auto i rozwój','<path d="M8 3.5h8M12 3.5v4M10.5 7.5h3l1 5v4.5l-1.5 3h-3L8.5 17v-4.5z"/><rect x="5" y="7" width="2.6" height="4.2" rx="1"/><rect x="16.4" y="7" width="2.6" height="4.2" rx="1"/><rect x="4.6" y="14.5" width="3" height="4.8" rx="1"/><rect x="16.4" y="14.5" width="3" height="4.8" rx="1"/><path d="M7.5 21h9"/>'],
  ['infrastruktura','Infrastruktura','<path d="M4 20V9l5-3v14M9 20V4l6 3v13M15 20v-9l5 2v7M3 20h18"/>'],
  null,
  ['dostawcy','Dostawcy','<path d="M3 7h11v9H3zM14 10h4l3 3v3h-7"/><circle cx="7" cy="18" r="1.6"/><circle cx="17" cy="18" r="1.6"/>'],
  ['sponsorzy','Sponsorzy','<path d="M12 3l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.4 6.8 19.1l1-5.8L3.5 9.2l5.9-.9z"/>'],
  ['finanse','Finanse','<path d="M12 3v18M17 7H9.5a3 3 0 000 6h5a3 3 0 010 6H6"/>'],
  ['zarzad','Zarząd','<path d="M4 20h16M6 20V10M10 20V10M14 20V10M18 20V10M3 10l9-6 9 6z"/>'],
  null,
  ['rynek','Rynek','<circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/>'],
  ['monthly','Paddock Monthly','<path d="M5 4h11l3 3v13H5z"/><path d="M8 9h8M8 13h8M8 17h5"/>'],
  ['fia','FIA i regulamin','<path d="M12 3v18M5 7h14M7 7l-3 7h6zM17 7l-3 7h6z"/>'],
  ['kronika','Kronika','<path d="M6 3h12v18l-6-4-6 4z"/>'],
];
/* ekrany-dzieci podświetlają rodzica w menu */
const PARENT = { kierowca: 'kierowcy', porownaj: 'kierowcy', osoba: 'personel', wyscig: 'kalendarz', menedzer: null };
const view = document.getElementById('view');
const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;

/* ---------- nawigacja: budowana raz, znacznik przesuwa się między pozycjami ---------- */
function buildNav() {
  const nav = document.getElementById('nav');
  nav.innerHTML = '<span class="nav-ind" aria-hidden="true"></span>' + NAV.map(n => n ? `<a href="#/${n[0]}" data-r="${n[0]}">${UI.icon(n[2])}<span>${n[1]}</span>${n[0] === 'skrzynka' ? '<span class="n" id="nav-unread"></span>' : ''}</a>` : '<div class="sep"></div>').join('');
  document.getElementById('navfoot').innerHTML = `<a href="#/ustawienia" data-r="ustawienia">${UI.icon('<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M2 12h3M19 12h3M4.9 19.1L7 17M17 7l2.1-2.1"/>')}Ustawienia</a>`;
}
function markNav(name, animate) {
  const act = name in PARENT ? PARENT[name] : name;
  document.querySelectorAll('aside [data-r]').forEach(a => a.classList.toggle('on', a.dataset.r === act));
  const ind = document.querySelector('.nav-ind'), on = document.querySelector('#nav a.on');
  if (!on) { ind.style.opacity = 0; return; }
  if (!animate) ind.style.transition = 'none';
  ind.style.opacity = 1; ind.style.height = on.offsetHeight + 'px'; ind.style.transform = `translateY(${on.offsetTop}px)`;
  if (!animate) { ind.offsetWidth; ind.style.transition = ''; }
  const unread = DB.inbox.filter(m => m.unread).length;
  const badge = document.getElementById('nav-unread'); badge.textContent = unread; badge.hidden = !unread;
}

/* ---------- górny pasek: strefa zespołu (pieniądze) i strefa czasu (wyścig, data, Dalej) ---------- */
function drawTop() {
  const t = STATE.now(), blocking = STATE.blocking();
  const ticks = [];
  for (let d = t.day; d <= DB.nextRace.day; d++) {
    const wd = (t.weekday + d - t.day) % 7, cls = d === t.day ? 'now' : d === DB.nextRace.day ? 'race' : blocking && d === blocking.dueDay ? 'due' : wd === 5 || wd === 6 ? 'we' : '';
    const tip = `${DB.weekdays[wd]}, ${d} lipca${d === DB.nextRace.day ? ' · GP Wielkiej Brytanii' : cls === 'due' ? ' · termin: ' + blocking.from : ''}`;
    ticks.push(`<i class="${cls}" title="${tip}"></i>`);
  }
  const left = DB.nextRace.day - t.day;
  const goSub = blocking ? `<small><i class="blk"></i>Decyzja: ${blocking.from}</small>` : t.weekend ? '<small>Weekend wyścigowy</small>' : `<small>Do ${STATE.next().short}</small>`;
  document.getElementById('top').innerHTML = `
    <div class="hud">
      <a class="cell me" href="#/menedzer"><span class="av">MW</span><span><b>M. Wojnar</b><small>Szef zespołu</small></span></a>
      <a class="cell" href="#/finanse"><span class="meta">Wolne środki</span><span class="num v good">${UI.money(DB.money.free)}</span></a>
      <a class="cell" href="#/finanse"><span class="meta">Gotówka</span><span class="num v">${UI.money(DB.money.cash)}</span></a>
    </div>
    <div class="spacer"></div>
    <div class="hud">
      <a class="cell next" href="#/wyscig/9"><span class="meta">Brands Hatch · ${left ? UI.n(left, 'dzień', 'dni', 'dni') : 'dziś'}</span><span class="ticks">${ticks.join('')}</span></a>
      <a class="cell date" href="#/kalendarz"><b>${t.title}</b></a>
    </div>
    <button class="go" id="go"><span><b>${t.weekend ? 'Wyścig' : 'Dalej'}</b>${goSub}</span><span class="arr">${UI.icon(UI.arrow)}</span></button>`;
  document.getElementById('go').onclick = advance;
}
/* Dalej: jak w FM — gdy coś blokuje czas, przycisk prowadzi do sprawy; inaczej przewija dni do następnego przystanku */
function advance() {
  const b = STATE.blocking();
  if (b) { location.hash = '#/skrzynka/' + b.id; return; }
  if (STATE.now().weekend) { location.hash = '#/wyscig/9'; return; }
  STATE.step++;
  const mail = STATE.onArrive();
  drawTop();
  document.querySelector('.hud .date').animate([{ background: 'color-mix(in oklab,var(--t2) 40%,transparent)' }, { background: 'transparent' }], { duration: 900, easing: 'ease-out' });
  markNav(cur.name, false);
  if (cur.name === 'pulpit' || cur.name === 'skrzynka') render(cur);
  if (mail) UI.toast(`Nowa wiadomość: ${mail.from}`);
}

/* ---------- router ---------- */
let cur = null, busy = false, queued = false;
function parse() {
  const parts = (location.hash.replace(/^#\/?/, '') || 'pulpit').split('/');
  const name = S[parts[0]] ? parts[0] : 'pulpit';
  return { name, args: parts.slice(1).map(decodeURIComponent) };
}
function render(r) {
  const fn = S[r.name];
  view.className = fn.fit ? 'noscroll' : '';
  view.innerHTML = fn(...r.args);
  view.scrollTop = 0;
  if (fn.after) fn.after(...r.args);
  UI.initTabs(view);
  syncSettings();
}
function route() {
  if (busy) { queued = true; return; }
  const r = parse(), prev = cur;
  cur = r;
  markNav(r.name, !!prev);
  /* skrzynka to skrzynka: przełączanie wiadomości jest natychmiastowe */
  if (!prev || (prev.name === 'skrzynka' && r.name === 'skrzynka')) {
    render(r);
    if (!prev) view.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 500, easing: 'ease-out' });
    return;
  }
  if (reduce) { render(r); view.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 220, easing: 'ease-out' }); return; }
  sweep(r);
}

/* „Przejazd barw”: trzy pochylone pasy przejeżdżają przez obszar ekranu.
   Na prawo od pasów zostaje stary ekran, na lewo odsłania się nowy. ~0,8 s. */
function sweep(r) {
  busy = true;
  const content = document.querySelector('.content');
  const vr = view.getBoundingClientRect(), cr = content.getBoundingClientRect();
  const W = view.clientWidth, H = view.clientHeight, x0 = vr.left - cr.left, y0 = vr.top - cr.top;
  const old = document.createElement('div'); old.className = 'view-old';
  Object.assign(old.style, { left: x0 + 'px', top: y0 + 'px', width: W + 'px', height: H + 'px' });
  const inner = document.createElement('div'); inner.className = 'vo-in';
  const cs = getComputedStyle(view);
  Object.assign(inner.style, { padding: cs.padding, width: W + 'px', height: H + 'px', transform: `translateY(${-view.scrollTop}px)` });
  while (view.firstChild) inner.appendChild(view.firstChild);
  inner.querySelectorAll('[id]').forEach(e => e.removeAttribute('id'));
  old.appendChild(inner); content.appendChild(old);
  content.classList.add('wiping');
  render(r);

  const S = Math.round(H * .2), B = 90, far = W + S + B + 40, D = 820, ease = 'cubic-bezier(.7,0,.2,1)';
  const oldPoly = x => `polygon(${x + S}px 0px, ${far}px 0px, ${far}px ${H}px, ${x}px ${H}px)`;
  const newPoly = x => `polygon(-40px 0px, ${x - B + S}px 0px, ${x - B}px ${H}px, -40px ${H}px)`;
  const wipe = document.getElementById('wipe');
  Object.assign(wipe.style, { display: 'block', left: x0 + 'px', top: y0 + 'px', height: H + 'px', width: B + S + 'px' });
  wipe.querySelectorAll('i').forEach((s, k) => { s.style.left = k * 30 + 'px'; s.style.transformOrigin = '0 100%'; s.style.transform = `skewX(${-Math.atan(S / H) * 180 / Math.PI}deg)`; });
  const opt = { duration: D, easing: ease, fill: 'forwards' };
  const a1 = old.animate([{ clipPath: oldPoly(-S) }, { clipPath: oldPoly(W + B) }], opt);
  view.animate([{ clipPath: newPoly(-S) }, { clipPath: newPoly(W + B) }], opt);
  wipe.animate([{ transform: `translateX(${-S - B}px)` }, { transform: `translateX(${W}px)` }], opt);
  a1.onfinish = () => {
    old.remove(); wipe.style.display = 'none';
    view.getAnimations().forEach(a => a.cancel()); wipe.getAnimations().forEach(a => a.cancel());
    content.classList.remove('wiping');
    busy = false;
    if (queued) { queued = false; const n = parse(); if (n.name !== cur.name || n.args.join('/') !== cur.args.join('/')) route(); }
  };
}

/* ---------- ustawienia (zapamiętywane w przeglądarce) ---------- */
const PREF = { style: 'era', team: 'tyrrell', air: 'on', numbers: 'on', skin: 'auto', speed: '10', pits: 'strateg', lang: 'pl' };
try { Object.assign(PREF, JSON.parse(localStorage.getItem('pp-proto') || '{}')); } catch (e) {}
const NAMES = { tyrrell: ['Elf Team', 'Tyrrell'], lotus: ['John Player', 'Team Lotus'], ferrari: ['Scuderia', 'Ferrari'] };
function applyPrefs() {
  document.body.dataset.style = PREF.style;
  document.body.dataset.team = PREF.team;
  const n = NAMES[PREF.team]; document.getElementById('teamname').innerHTML = `${n[0]}<span>${n[1]}</span>`;
  document.getElementById('air').style.display = PREF.air === 'on' ? '' : 'none';
  try { localStorage.setItem('pp-proto', JSON.stringify(PREF)); } catch (e) {}
}
function syncSettings() {}

document.addEventListener('tab', e => {
  const { group, value } = e.detail;
  if (group.startsWith('set-')) { PREF[group.slice(4)] = value; applyPrefs(); }
});
document.addEventListener('click', e => {
  const tab = e.target.closest('.tabs button');
  if (tab && !tab.disabled) { UI.selectTab(tab); return; }
  const row = e.target.closest('[data-href]');
  if (row && !e.target.closest('a,button')) { location.hash = row.dataset.href; return; }
  const t = e.target.closest('[data-toast]');
  if (t) UI.toast(t.dataset.toast);
});
addEventListener('resize', () => { UI.initTabs(); if (cur) markNav(cur.name, false); });
document.fonts && document.fonts.ready.then(() => { UI.initTabs(); if (cur) markNav(cur.name, false); });

addEventListener('hashchange', route);
applyPrefs();
buildNav();
drawTop();
route();

/* ---------- tło: smugi dymu w tunelu aerodynamicznym ----------
   ~30 kl./s, dwa kolory rysowane jedną ścieżką, ruch zawsze w prawo,
   pauza w tle i przy ograniczeniu ruchu. */
(() => {
  const cv = document.getElementById('air'), cx = cv.getContext('2d');
  let W, H, P = [], t = 0, last = 0, running = true;
  const N = () => Math.round(innerWidth * innerHeight / 3900);
  const spawn = (x) => ({ x: x ?? -20 - Math.random() * 200, y: Math.random() * H, c: Math.random() < .55 ? 0 : 1, s: .35 + Math.random() * .3 });
  function size() { W = cv.width = innerWidth; H = cv.height = innerHeight; P = Array.from({ length: N() }, () => spawn(Math.random() * W)); }
  function angle(x, y) {
    const nx = x / W, ny = y / H;
    const a = Math.sin(nx * 2.4 + t * .00018) * .28 + Math.sin(ny * 4.2 - t * .00012 + nx * 1.6) * .18;
    const dy = ny - .5 - Math.sin(t * .0001) * .05, dx = nx - .64, bump = Math.exp(-(dx * dx) * 30) * (dy > 0 ? .5 : -.5) * Math.exp(-dy * dy * 8);
    return a + bump;
  }
  function frame(now) {
    if (!running) return;
    requestAnimationFrame(frame);
    if (now - last < 33) return;
    last = now; t += 33;
    const cs = getComputedStyle(document.body), col = [cs.getPropertyValue('--t1').trim(), cs.getPropertyValue('--smoke2').trim()];
    cx.globalCompositeOperation = 'destination-out'; cx.fillStyle = 'rgba(0,0,0,.042)'; cx.fillRect(0, 0, W, H);
    cx.globalCompositeOperation = 'source-over'; cx.lineWidth = 1.25; cx.globalAlpha = .42;
    for (let k = 0; k < 2; k++) {
      cx.strokeStyle = col[k]; cx.beginPath();
      for (const p of P) {
        if (p.c !== k) continue;
        const a = angle(p.x, p.y), v = p.s;
        const nx = p.x + Math.cos(a) * v * 1.4, ny = p.y + Math.sin(a) * v;
        cx.moveTo(p.x, p.y); cx.lineTo(nx, ny);
        p.x = nx; p.y = ny;
        if (p.x > W + 10 || p.y < -10 || p.y > H + 10) Object.assign(p, spawn());
      }
      cx.stroke();
    }
    cx.globalAlpha = 1;
  }
  size(); addEventListener('resize', size);
  document.addEventListener('visibilitychange', () => { running = !document.hidden && !reduce; if (running) requestAnimationFrame(frame); });
  if (reduce) { for (let i = 0; i < 400; i++) { running = true; frame(i * 40); } running = false; }
  else requestAnimationFrame(frame);
})();
