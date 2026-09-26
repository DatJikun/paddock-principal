/* Flagi jako jeden sprite SVG (symbole 3:2, ostre w każdym rozmiarze).
   Flagi są z epoki kariery: RPA 1928–1994, RFN, Hiszpania bez herbu (bandera cywilna).
   Kody: 3-literowe jak w danych prototypu; aliasy ISO z data/authored/tracks. */
(() => {
  const star = (cx, cy, r, n = 5, inner = .45, rot = -90) => {
    const pts = [];
    for (let i = 0; i < n * 2; i++) {
      const a = (rot + i * 180 / n) * Math.PI / 180, rr = i % 2 ? r * inner : r;
      pts.push((cx + Math.cos(a) * rr).toFixed(2) + ',' + (cy + Math.sin(a) * rr).toFixed(2));
    }
    return pts.join(' ');
  };
  const h3 = (a, b, c) => `<rect width="30" height="6.67" fill="${a}"/><rect y="6.67" width="30" height="6.67" fill="${b}"/><rect y="13.33" width="30" height="6.67" fill="${c}"/>`;
  const v3 = (a, b, c) => `<rect width="10" height="20" fill="${a}"/><rect x="10" width="10" height="20" fill="${b}"/><rect x="20" width="10" height="20" fill="${c}"/>`;
  const canton = '<svg x="0" y="0" width="15" height="10" viewBox="0 0 60 30" preserveAspectRatio="xMidYMid slice"><use href="#ukj"/></svg>';
  const usStars = (() => { let s = ''; for (let r = 0; r < 5; r++) for (let c = 0; c < 6; c++) if ((r + c) % 2 === 0 || r % 2 === 0) s += `<circle cx="${1.1 + c * 1.95}" cy="${1.1 + r * 2.1}" r=".42" fill="#fff"/>`; return s; })();
  const leaf = 'M15 3.2L16.1 5.4L17.4 4.9L16.9 8L18.9 6.3L19.3 7.6L21.2 7.2L20.6 9.1L21.6 9.6L18.3 12.3L18.7 13.4L15.4 13L15.5 16.3L14.5 16.3L14.6 13L11.3 13.4L11.7 12.3L8.4 9.6L9.4 9.1L8.8 7.2L10.7 7.6L11.1 6.3L13.1 8L12.6 4.9L13.9 5.4Z';

  const F = {
    GBR: '<use href="#ukj" width="30" height="20"/>',
    FRA: v3('#0055a4', '#fff', '#ef4135'),
    ITA: v3('#009246', '#fff', '#ce2b37'),
    IRL: v3('#169b62', '#fff', '#ff883e'),
    BEL: v3('#111', '#fae042', '#ed2939'),
    GER: h3('#111', '#dd0000', '#ffce00'),
    AUT: h3('#ed2939', '#fff', '#ed2939'),
    NED: h3('#ae1c28', '#fff', '#21468b'),
    ARG: h3('#74acdf', '#fff', '#74acdf') + '<circle cx="15" cy="10" r="2" fill="#f6b40e" stroke="#85340a" stroke-width=".3"/>',
    MON: '<rect width="30" height="10" fill="#ce1126"/><rect y="10" width="30" height="10" fill="#fff"/>',
    POL: '<rect width="30" height="10" fill="#fff"/><rect y="10" width="30" height="10" fill="#dc143c"/>',
    ESP: '<rect width="30" height="20" fill="#c60b1e"/><rect y="5" width="30" height="10" fill="#ffc400"/>',
    JPN: '<rect width="30" height="20" fill="#fff"/><circle cx="15" cy="10" r="6" fill="#bc002d"/>',
    SUI: '<rect width="30" height="20" fill="#da291c"/><rect x="13" y="4" width="4" height="12" fill="#fff"/><rect x="9" y="8" width="12" height="4" fill="#fff"/>',
    SWE: '<rect width="30" height="20" fill="#006aa7"/><rect x="9.4" width="3.75" height="20" fill="#fecc00"/><rect y="8" width="30" height="4" fill="#fecc00"/>',
    BRA: '<rect width="30" height="20" fill="#009c3b"/><polygon points="3,10 15,2.2 27,10 15,17.8" fill="#ffdf00"/><circle cx="15" cy="10" r="4.6" fill="#002776"/><path d="M10.5 9.1Q15 8 19.4 11.2" stroke="#fff" stroke-width=".9" fill="none"/>',
    USA: (() => { let s = ''; for (let i = 0; i < 13; i++) s += `<rect y="${(i * 20 / 13).toFixed(3)}" width="30" height="${(20 / 13 + .02).toFixed(3)}" fill="${i % 2 ? '#fff' : '#b22234'}"/>`; return s + `<rect width="12" height="${(7 * 20 / 13).toFixed(3)}" fill="#3c3b6e"/>` + usStars; })(),
    CAN: '<rect width="30" height="20" fill="#fff"/><rect width="7.5" height="20" fill="#d52b1e"/><rect x="22.5" width="7.5" height="20" fill="#d52b1e"/><path d="' + leaf + '" fill="#d52b1e"/>',
    AUS: '<rect width="30" height="20" fill="#012169"/>' + canton + `<polygon points="${star(7.5, 15, 2.6, 7, .45)}" fill="#fff"/>` +
      [[22.5, 16.6, 1.25], [18.3, 9.6, 1.15], [22.5, 3.8, 1.15], [26.4, 8.3, 1.15], [24.4, 11.4, .6]].map(([x, y, r]) => `<polygon points="${star(x, y, r, x === 24.4 ? 5 : 7, .45)}" fill="#fff"/>`).join(''),
    NZL: '<rect width="30" height="20" fill="#012169"/>' + canton +
      [[22.5, 16.4, 1.4], [18.6, 9.6, 1.25], [22.5, 4, 1.25], [26.2, 8.6, 1.1]].map(([x, y, r]) => `<polygon points="${star(x, y, r + .35)}" fill="#fff"/><polygon points="${star(x, y, r)}" fill="#c8102e"/>`).join(''),
    /* RPA 1928–1994: pomarańcz–biel–błękit, w środku flagi Wielkiej Brytanii, Wolnego Państwa Orania i Transwalu */
    RSA: '<rect width="30" height="6.67" fill="#f17f29"/><rect y="6.67" width="30" height="6.67" fill="#fff"/><rect y="13.33" width="30" height="6.67" fill="#1c3f94"/>' +
      '<svg x="10.4" y="8.4" width="3.2" height="2" viewBox="0 0 60 30" preserveAspectRatio="none"><use href="#ukj"/></svg>' +
      '<rect x="14.1" y="7.6" width="1.8" height="3.6" fill="#f17f29"/><rect x="14.1" y="8.3" width="1.8" height=".5" fill="#fff"/><rect x="14.1" y="9.3" width="1.8" height=".5" fill="#fff"/><rect x="14.1" y="10.3" width="1.8" height=".5" fill="#fff"/>' +
      '<rect x="16.4" y="8.4" width="3.2" height=".67" fill="#c8102e"/><rect x="16.4" y="9.07" width="3.2" height=".67" fill="#fff"/><rect x="16.4" y="9.73" width="3.2" height=".67" fill="#1c3f94"/><rect x="16.4" y="8.4" width="1" height="2" fill="#007a3d"/>',
  };
  const ALIAS = { ZAF: 'RSA', DEU: 'GER', MCO: 'MON', NLD: 'NED', CHE: 'SUI', PRT: 'POR' };
  const NAMES = { GBR: 'Wielka Brytania', FRA: 'Francja', ITA: 'Włochy', IRL: 'Irlandia', BEL: 'Belgia', GER: 'RFN', AUT: 'Austria', NED: 'Holandia',
    ARG: 'Argentyna', MON: 'Monako', POL: 'Polska', ESP: 'Hiszpania', JPN: 'Japonia', SUI: 'Szwajcaria', SWE: 'Szwecja', BRA: 'Brazylia', USA: 'USA',
    CAN: 'Kanada', AUS: 'Australia', NZL: 'Nowa Zelandia', RSA: 'RPA' };

  const ukj = `<symbol id="ukj" viewBox="0 0 60 30" preserveAspectRatio="xMidYMid slice">
    <clipPath id="ukj-c"><path d="M0,0v30h60v-30z"/></clipPath><clipPath id="ukj-t"><path d="M30,15h30v15zv15h-30zh-30v-15zv-15h30z"/></clipPath>
    <g clip-path="url(#ukj-c)"><path d="M0,0v30h60v-30z" fill="#012169"/><path d="M0,0L60,30M60,0L0,30" stroke="#fff" stroke-width="6"/>
    <path d="M0,0L60,30M60,0L0,30" clip-path="url(#ukj-t)" stroke="#c8102e" stroke-width="4"/><path d="M30,0v30M0,15h60" stroke="#fff" stroke-width="10"/>
    <path d="M30,0v30M0,15h60" stroke="#c8102e" stroke-width="6"/></g></symbol>`;
  const sprite = `<svg id="flag-sprite" aria-hidden="true" style="position:absolute;width:0;height:0;overflow:hidden">${ukj}${Object.entries(F).map(([k, v]) => `<symbol id="fl-${k}" viewBox="0 0 30 20">${v}</symbol>`).join('')}</svg>`;
  document.body.insertAdjacentHTML('afterbegin', sprite);

  window.FLAGS = {
    code: c => ALIAS[c] || c,
    name: c => NAMES[ALIAS[c] || c] || c,
    has: c => !!F[ALIAS[c] || c],
  };
})();
