/* Router, nawigacja, ustawienia i tło. */
const NAV = [
  ['pulpit','Pulpit','<path d="M4 11l8-7 8 7v9H4z"/>'],
  ['skrzynka','Skrzynka','<path d="M4 6h16v12H4z"/><path d="M4 7l8 6 8-6"/>','5'],
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
const PARENT = { kierowca: 'kierowcy', porownaj: 'kierowcy' };

function drawNav(route) {
  const act = PARENT[route] || route;
  document.getElementById('nav').innerHTML = NAV.map(n => n ? `<a href="#/${n[0]}" class="${n[0] === act ? 'on' : ''}">${UI.icon(n[2])}${n[1]}${n[3] ? `<span class="n">${n[3]}</span>` : ''}</a>` : '<div class="sep"></div>').join('');
  document.getElementById('navfoot').innerHTML = `<a href="#/ustawienia" class="${route === 'ustawienia' ? 'on' : ''}">${UI.icon('<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M2 12h3M19 12h3M4.9 19.1L7 17M17 7l2.1-2.1"/>')}Ustawienia</a>`;
}

function route() {
  const [name, arg] = (location.hash.replace(/^#\/?/, '') || 'pulpit').split('/');
  const fn = S[name] || S.pulpit;
  const view = document.getElementById('view');
  view.className = fn.fit ? 'noscroll' : '';
  view.innerHTML = fn(arg);
  view.scrollTop = 0;
  drawNav(S[name] ? name : 'pulpit');
  if (fn.after) fn.after(arg);
  syncSettings();
}

/* ---------- ustawienia (zapamiętywane w przeglądarce) ---------- */
const PREF = { style: 'era', team: 'tyrrell', air: 'on' };
try { Object.assign(PREF, JSON.parse(localStorage.getItem('pp-proto') || '{}')); } catch (e) {}
const NAMES = { tyrrell: ['Elf Team', 'Tyrrell'], lotus: ['John Player', 'Team Lotus'], ferrari: ['Scuderia', 'Ferrari'] };
function applyPrefs() {
  document.body.dataset.style = PREF.style;
  document.body.dataset.team = PREF.team;
  const n = NAMES[PREF.team]; document.getElementById('teamname').innerHTML = `${n[0]}<span>${n[1]}</span>`;
  document.getElementById('air').style.display = PREF.air === 'on' ? '' : 'none';
  try { localStorage.setItem('pp-proto', JSON.stringify(PREF)); } catch (e) {}
}
function syncSettings() {
  document.querySelectorAll('[data-set]').forEach(g => g.querySelectorAll('button').forEach(b => b.classList.toggle('on', PREF[g.dataset.set] === b.dataset.v)));
}

document.addEventListener('click', e => {
  const set = e.target.closest('[data-set] button');
  if (set) { PREF[set.parentElement.dataset.set] = set.dataset.v; applyPrefs(); syncSettings(); return; }
  const tab = e.target.closest('[data-tabs] button');
  if (tab) {
    tab.parentElement.querySelectorAll('button').forEach(x => x.classList.toggle('on', x === tab));
    tab.closest('.panel').querySelectorAll('[data-pane]').forEach(p => p.hidden = p.dataset.pane !== tab.dataset.tab);
    return;
  }
  const seg = e.target.closest('.seg:not([data-set]):not([data-tabs]):not(#mkt-stars) button');
  if (seg) seg.parentElement.querySelectorAll('button').forEach(x => x.classList.toggle('on', x === seg));
  const t = e.target.closest('[data-toast]');
  if (t) UI.toast(t.dataset.toast);
});
document.getElementById('go').onclick = () => { location.hash = '#/skrzynka/1'; UI.toast('Najpierw decyzja: umowa z Goodyear'); };

addEventListener('hashchange', route);
applyPrefs();
route();

/* ---------- tło: smugi dymu w tunelu aerodynamicznym ----------
   Lekkie: ~30 kl./s, rozdzielczość 1:1, dwa kolory rysowane jedną ścieżką,
   ruch zawsze w prawo (brak zastygłych cząstek), pauza w tle i przy ograniczeniu ruchu. */
(() => {
  const cv = document.getElementById('air'), cx = cv.getContext('2d');
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let W, H, P = [], t = 0, last = 0, running = true;
  const N = () => Math.round(innerWidth * innerHeight / 4200);
  const spawn = (x) => ({ x: x ?? -20 - Math.random() * 200, y: Math.random() * H, px: 0, py: 0, c: Math.random() < .55 ? 0 : 1, s: .35 + Math.random() * .3 });
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
    const cs = getComputedStyle(document.body), col = [cs.getPropertyValue('--t1').trim(), cs.getPropertyValue('--a1').trim()];
    cx.globalCompositeOperation = 'destination-out'; cx.fillStyle = 'rgba(0,0,0,.05)'; cx.fillRect(0, 0, W, H);
    cx.globalCompositeOperation = 'source-over'; cx.lineWidth = 1; cx.globalAlpha = .28;
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
