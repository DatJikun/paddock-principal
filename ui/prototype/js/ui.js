/* Wspólne klocki UI. */
const UI = {
  icon(path, size) {
    const s = size ? ` style="width:${size}px;height:${size}px"` : '';
    return `<svg class="i" viewBox="0 0 24 24"${s}>${path}</svg>`;
  },
  arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',

  /* gwiazdki 0–5 z połówkami; pot = potencjał (gwiazdki „duchy”) */
  stars(v, pot) {
    const P = 'M12 2.8l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 16.8l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z';
    let out = '<span class="stars" aria-label="' + v + ' z 5 gwiazdek">';
    for (let i = 1; i <= 5; i++) {
      if (v >= i) out += `<svg viewBox="0 0 24 24"><path class="f" d="${P}"/></svg>`;
      else if (v >= i - .5) out += `<svg viewBox="0 0 24 24"><defs><clipPath id="h${i}"><rect width="12" height="24"/></clipPath></defs><path class="e" d="${P}"/><path class="f" d="${P}" clip-path="url(#h${i})"/></svg>`;
      else if (pot && pot >= i - .5) out += `<svg viewBox="0 0 24 24"><path class="g" d="${P}"/></svg>`;
      else out += `<svg viewBox="0 0 24 24"><path class="e" d="${P}"/></svg>`;
    }
    return out + '</span>';
  },

  /* flaga jako prosty pasek barw kraju (prototyp) */
  flag(c) {
    const F = { RSA:'#007a4d,#ffb612,#de3831', FRA:'#0055a4 33%,#fff 0 66%,#ef4135 0', BRA:'#009b3a,#fedf00', SWE:'#006aa7 40%,#fecc00 0 55%,#006aa7 0',
      USA:'#b22234 50%,#fff 0 60%,#3c3b6e 0', GBR:'#012169 40%,#c8102e 0 60%,#012169 0', CAN:'#d52b1e 25%,#fff 0 75%,#d52b1e 0',
      ITA:'#009246 33%,#fff 0 66%,#ce2b37 0', GER:'#000 33%,#dd0000 0 66%,#ffce00 0', ARG:'#74acdf 33%,#fff 0 66%,#74acdf 0',
      AUS:'#012169 60%,#e4002b 0', IRL:'#169b62 33%,#fff 0 66%,#ff883e 0' };
    return `<span class="flag" title="${c}" style="background:linear-gradient(90deg,${F[c] || '#999,#ccc'})"></span>`;
  },

  attr(v) {
    const c = v >= 17 ? 'a4' : v >= 13 ? 'a3' : v >= 8 ? 'a2' : 'a1';
    return `<span class="attr ${c}">${v}</span>`;
  },

  /* kolor miejsca w stawce: 1 = zielony, 16 = czerwony */
  rankColor(pos, of = 16, l = 42) {
    const h = Math.round(128 - Math.pow((pos - 1) / (of - 1), .8) * 128);
    return `hsl(${h} 58% ${l}%)`;
  },
  rankBar(pos, of = 16) {
    const w = Math.round(100 - (pos - 1) / of * 100);
    return `<div class="bar"><i style="width:${w}%;background:${UI.rankColor(pos, of)}"></i></div>`;
  },

  initials(n) { return n.split(' ').map(x => x[0]).slice(0, 2).join('').toUpperCase(); },

  panel(title, inner, opts = {}) {
    const right = opts.right || '';
    return `<section class="panel ${opts.cls || ''}" ${opts.style ? `style="${opts.style}"` : ''}>
      <header><h2>${title}</h2>${right}</header>${inner}</section>`;
  },

  toast(msg) {
    document.querySelectorAll('.toast').forEach(t => t.remove());
    const t = document.createElement('div'); t.className = 'toast'; t.textContent = msg;
    document.body.appendChild(t); setTimeout(() => t.remove(), 2200);
  },
};
