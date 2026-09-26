/* Wspólne klocki UI: jedna rodzina kontrolek dla wszystkich ekranów. */
const UI = {
  icon(path, size) {
    const s = size ? ` style="width:${size}px;height:${size}px"` : '';
    return `<svg class="i" viewBox="0 0 24 24" aria-hidden="true"${s}>${path}</svg>`;
  },
  arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  back: '<path d="M19 12H5M11 6l-6 6 6 6"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7"/>',
  lock: '<rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 018 0v3"/>',
  chev: '<path d="M9 6l6 6-6 6"/>',

  /* ---------- liczby i język ---------- */
  /* polska odmiana: 1 osoba, 2–4 osoby, 5+ osób (12–14 zawsze „osób”) */
  plural(n, one, few, many) {
    const a = Math.abs(n);
    if (!Number.isInteger(a)) return few;
    if (a === 1) return one;
    const d = a % 10, h = a % 100;
    return d >= 2 && d <= 4 && (h < 12 || h > 14) ? few : many;
  },
  n(n, one, few, many) { return `${UI.fmt(n)} ${UI.plural(n, one, few, many)}`; },
  fmt(n) { return Number(n).toLocaleString('pl-PL'); },
  /* kwoty w tysiącach funtów */
  money(k, sign) {
    const s = sign && k > 0 ? '+' : k < 0 ? '−' : '';
    const a = Math.abs(k);
    return a >= 1000 ? `${s}£${UI.fmt(Math.round(a / 100) / 10)} mln` : `${s}£${UI.fmt(a)} tys.`;
  },
  cap(s) { return s ? s[0].toUpperCase() + s.slice(1) : s; },

  /* ---------- oceny ---------- */
  /* gwiazdki 0–5 z połówkami; pot = potencjał (gwiazdki „duchy”); band = pasmo wiedzy [od, do] */
  stars(v, pot, band) {
    const P = 'M12 2.8l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 16.8l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z';
    const hi = band ? band[1] : v;
    let out = `<span class="stars" role="img" aria-label="${band ? band[0] + '–' + band[1] : v} z 5 gwiazdek">`;
    for (let i = 1; i <= 5; i++) {
      if (v >= i) out += `<svg viewBox="0 0 24 24"><path class="f" d="${P}"/></svg>`;
      else if (v >= i - .5) out += `<svg viewBox="0 0 24 24"><path class="e" d="${P}"/><path class="f" d="${P}" style="clip-path:inset(0 50% 0 0)"/></svg>`;
      else if (band && hi >= i - .5) out += `<svg viewBox="0 0 24 24"><path class="e" d="${P}"/><path class="b" d="${P}"${hi >= i ? '' : ' style="clip-path:inset(0 50% 0 0)"'}/></svg>`;
      else if (pot && pot >= i - .5) out += `<svg viewBox="0 0 24 24"><path class="g" d="${P}"/></svg>`;
      else out += `<svg viewBox="0 0 24 24"><path class="e" d="${P}"/></svg>`;
    }
    return out + '</span>';
  },
  attr(v) {
    if (Array.isArray(v)) return `<span class="attr band">${v[0]}–${v[1]}</span>`;
    const c = v >= 17 ? 'a4' : v >= 13 ? 'a3' : v >= 8 ? 'a2' : 'a1';
    return `<span class="attr ${c}">${v}</span>`;
  },
  /* kolor miejsca w stawce: 1 = zielony, ostatni = czerwony */
  rankColor(pos, of = 16, l = 42) {
    const h = Math.round(128 - Math.pow((pos - 1) / (of - 1), .8) * 128);
    return `hsl(${h} 58% ${l}%)`;
  },
  rankBar(pos, of = 16) {
    const w = Math.round(100 - (pos - 1) / of * 100);
    return `<div class="bar"><i style="width:${w}%;background:${UI.rankColor(pos, of)}"></i></div>`;
  },
  initials(n) { return n.split(' ').filter(x => /^[A-ZŁŚŻ]/.test(x)).map(x => x[0]).slice(0, 2).join('').toUpperCase(); },

  /* ---------- flagi (sprite w flags.js) ---------- */
  flag(c, cls = '') {
    const k = FLAGS.code(c);
    return `<span class="flag ${cls}" title="${FLAGS.name(c)}"><svg viewBox="0 0 30 20" aria-hidden="true"><use href="#fl-${k}"/></svg></span>`;
  },

  /* ---------- kontrolki ---------- */
  /* zakładki / przełącznik: jeden komponent w całej grze.
     items: [[wartość, etykieta, {disabled}]]; group: nazwa grupy paneli (data-pane="grupa:wartość") */
  tabs(group, items, active, cls = '') {
    return `<div class="tabs ${cls}" role="tablist" data-group="${group}">${items.map(([v, l, o = {}]) =>
      `<button role="tab" data-v="${v}" aria-selected="${v === active}" class="${v === active ? 'on' : ''}"${o.disabled ? ' disabled' : ''}>${o.disabled ? UI.icon(UI.lock, 14) : ''}${l}</button>`).join('')}<i class="ind" aria-hidden="true"></i></div>`;
  },
  /* etykieta statusu: romb w kolorze tonu + tekst; solid = wybity znacznik (np. termin) */
  st(text, tone = '', solid = false) { return `<span class="st ${tone}${solid ? ' solid' : ''}">${text}</span>`; },
  /* pola informacji: etykieta nad wartością, segmenty rozdzielone linią */
  fields(list, cls = '') {
    return `<div class="fields ${cls}">${list.filter(Boolean).map(f => {
      const tag = f.href ? 'a' : 'div';
      return `<${tag} class="fld ${f.cls || ''}"${f.href ? ` href="${f.href}"` : ''}><span class="meta">${f.k}</span><span class="v${f.num ? ' num' : ''}">${f.v}</span></${tag}>`;
    }).join('')}</div>`;
  },
  btn(label, opts = {}) {
    const tag = opts.href ? 'a' : 'button';
    return `<${tag} class="btn ${opts.cls || ''}"${opts.href ? ` href="${opts.href}"` : ''}${opts.attrs || ''}>${opts.icon ? UI.icon(opts.icon, 17) : ''}<span>${label}</span>${opts.after ? UI.icon(opts.after, 17) : ''}</${tag}>`;
  },
  panel(title, inner, opts = {}) {
    return `<section class="panel ${opts.cls || ''}" ${opts.style ? `style="${opts.style}"` : ''}>
      ${title ? `<header><h2>${title}</h2>${opts.right || ''}</header>` : ''}${inner}</section>`;
  },

  toast(msg) {
    document.querySelectorAll('.toast').forEach(t => t.remove());
    const t = document.createElement('div'); t.className = 'toast'; t.textContent = msg;
    document.body.appendChild(t); setTimeout(() => t.remove(), 2400);
  },

  /* ---------- zachowanie zakładek ---------- */
  placeInd(tabs, animate) {
    const on = tabs.querySelector('button.on'), ind = tabs.querySelector('.ind');
    if (!on || !ind) return;
    if (!animate) ind.style.transition = 'none';
    ind.style.width = on.offsetWidth + 'px';
    ind.style.transform = `translateX(${on.offsetLeft}px)`;
    if (!animate) { ind.offsetWidth; ind.style.transition = ''; }
    tabs.classList.add('ready');
  },
  initTabs(root = document) { root.querySelectorAll('.tabs').forEach(t => UI.placeInd(t, false)); },
  selectTab(btn) {
    const tabs = btn.closest('.tabs'), group = tabs.dataset.group, v = btn.dataset.v;
    if (btn.classList.contains('on')) return;
    tabs.querySelectorAll('button').forEach(b => { const on = b === btn; b.classList.toggle('on', on); b.setAttribute('aria-selected', on); });
    UI.placeInd(tabs, true);
    const scope = tabs.closest('[data-scope]') || document.getElementById('view');
    scope.querySelectorAll(`[data-pane^="${group}:"]`).forEach(p => {
      const show = p.dataset.pane === `${group}:${v}`;
      p.hidden = !show;
      if (show && !p.closest('.instant')) { p.classList.remove('pane-in'); p.offsetWidth; p.classList.add('pane-in'); }
    });
    document.dispatchEvent(new CustomEvent('tab', { detail: { group, value: v, tabs } }));
  },
};
