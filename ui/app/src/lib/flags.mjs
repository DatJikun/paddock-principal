/* Flagi jako jeden sprite SVG (symbole 3:2, ostre w każdym rozmiarze).
   Flagi są z epoki kariery: RPA 1928–1994, RFN, Hiszpania bez herbu (bandera cywilna).
   Kody: 3-literowe jak w danych prototypu; aliasy ISO z data/authored/tracks. */
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
    /* Meksyk, Portugalia i Maroko: tory kalendarza od 1958 i 1962. Godła uproszczone do jednego znaku. */
    MEX: v3('#006847', '#fff', '#ce1126') + '<circle cx="15" cy="10" r="2.4" fill="#8c6b2f"/><circle cx="15" cy="10" r="1.3" fill="#006847"/>',
    POR: '<rect width="30" height="20" fill="#da291c"/><rect width="12" height="20" fill="#046a38"/><circle cx="12" cy="10" r="3.6" fill="#ffe000"/><circle cx="12" cy="10" r="2.1" fill="#da291c"/><rect x="10.9" y="8.9" width="2.2" height="2.2" fill="#fff"/>',
    MAR: '<rect width="30" height="20" fill="#c1272d"/><polygon points="' + star(15, 10.4, 5.2, 5, .382) + '" fill="none" stroke="#006233" stroke-width=".9"/>',
    /* RPA 1928–1994: pomarańcz–biel–błękit, w środku flagi Wielkiej Brytanii, Wolnego Państwa Orania i Transwalu */
    RSA: '<rect width="30" height="6.67" fill="#f17f29"/><rect y="6.67" width="30" height="6.67" fill="#fff"/><rect y="13.33" width="30" height="6.67" fill="#1c3f94"/>' +
      '<svg x="10.4" y="8.4" width="3.2" height="2" viewBox="0 0 60 30" preserveAspectRatio="none"><use href="#ukj"/></svg>' +
      '<rect x="14.1" y="7.6" width="1.8" height="3.6" fill="#f17f29"/><rect x="14.1" y="8.3" width="1.8" height=".5" fill="#fff"/><rect x="14.1" y="9.3" width="1.8" height=".5" fill="#fff"/><rect x="14.1" y="10.3" width="1.8" height=".5" fill="#fff"/>' +
      '<rect x="16.4" y="8.4" width="3.2" height=".67" fill="#c8102e"/><rect x="16.4" y="9.07" width="3.2" height=".67" fill="#fff"/><rect x="16.4" y="9.73" width="3.2" height=".67" fill="#1c3f94"/><rect x="16.4" y="8.4" width="1" height="2" fill="#007a3d"/>',
    FIN: '<rect width="30" height="20" fill="#fff"/><rect x="8" width="4" height="20" fill="#003580"/><rect y="8" width="30" height="4" fill="#003580"/>',
    DEN: '<rect width="30" height="20" fill="#c8102e"/><rect x="8" width="4" height="20" fill="#fff"/><rect y="8" width="30" height="4" fill="#fff"/>',
    /* Uruguay: nine stripes, a sun on the white canton (simplified, no rays) */
    URU: Array.from({ length: 9 }, (_, i) => `<rect y="${(i * 20 / 9).toFixed(3)}" width="30" height="${(20 / 9 + .02).toFixed(3)}" fill="${i % 2 ? '#0038a8' : '#fff'}"/>`).join('') +
      '<rect width="9" height="11.11" fill="#fff"/><circle cx="4.5" cy="5.6" r="2.4" fill="#fcd116"/>',
    RUS: h3('#fff', '#0039a6', '#d52b1e'),
    COL: '<rect width="30" height="10" fill="#fcd116"/><rect y="10" width="30" height="5" fill="#003893"/><rect y="15" width="30" height="5" fill="#ce1126"/>',
    VEN: h3('#fcd116', '#003893', '#ce1126') + Array.from({ length: 8 }, (_, i) => `<polygon points="${star(9 + i * 12 / 7, 10, .9, 5, .45)}" fill="#fff"/>`).join(''),
    /* East Germany: black-red-gold, the emblem reduced to a gold ring */
    GDR: h3('#111', '#dd0000', '#ffce00') + '<circle cx="15" cy="10" r="3" fill="#ffce00"/><circle cx="15" cy="10" r="2.2" fill="#111"/>',
    /* Rhodesia 1968-1979, approximation: Union Jack in the canton, the arms reduced to a roundel */
    RHO: '<rect width="30" height="20" fill="#00843d"/>' + canton + '<circle cx="22" cy="10" r="4.5" fill="#fcd116"/><circle cx="22" cy="10" r="2.8" fill="#00843d"/>',
    CZE: '<rect width="30" height="10" fill="#fff"/><rect y="10" width="30" height="10" fill="#d7141a"/><polygon points="0,0 15,10 0,20" fill="#11457e"/>',
    IDN: '<rect width="30" height="10" fill="#e70011"/><rect y="10" width="30" height="10" fill="#fff"/>',
    LIE: '<rect width="30" height="10" fill="#002b7f"/><rect y="10" width="30" height="10" fill="#ce1126"/><path d="M6 5.2l1.2-2.6 1.8 1.6 1.2-2.4 1.2 2.4 1.8-1.6 1.2 2.6z" fill="#ffd700"/><rect x="6" y="5.4" width="7.2" height="1" fill="#ffd700"/>',
    CHI: '<rect width="30" height="10" fill="#fff"/><rect y="10" width="30" height="10" fill="#d52b1e"/><rect width="10" height="10" fill="#0039a6"/><polygon points="' + star(5, 5, 2.8) + '" fill="#fff"/>',
    MYS: Array.from({ length: 14 }, (_, i) => `<rect y="${(i * 20 / 14).toFixed(3)}" width="30" height="${(20 / 14 + .02).toFixed(3)}" fill="${i % 2 ? '#fff' : '#cc0001'}"/>`).join('') +
      '<rect width="15" height="10" fill="#010066"/><circle cx="6.5" cy="5" r="3.4" fill="#ffcc00"/><circle cx="7.7" cy="5" r="2.9" fill="#010066"/><polygon points="' + star(10.6, 5, 1.8) + '" fill="#ffcc00"/>',
    CHN: '<rect width="30" height="20" fill="#de2910"/><polygon points="' + star(5, 5, 3.8) + '" fill="#ffde00"/>' +
      [[10, 2], [12, 4], [12, 7], [10, 9]].map(([x, y]) => `<polygon points="${star(x, y, 1.2)}" fill="#ffde00"/>`).join(''),
    /* Hong Kong: white bauhinia on red, petals simplified to ellipses */
    HKG: '<rect width="30" height="20" fill="#de2910"/><g transform="translate(15 10)" fill="#fff">' +
      [0, 1, 2, 3, 4].map((k) => `<ellipse cx="0" cy="-3.6" rx="2.3" ry="3.6" transform="rotate(${k * 72})"/>`).join('') + '</g><circle cx="15" cy="10" r=".9" fill="#de2910"/>',
    LUX: h3('#ed2939', '#fff', '#00a1de'),
    GRC: Array.from({ length: 9 }, (_, i) => `<rect y="${(i * 20 / 9).toFixed(3)}" width="30" height="${(20 / 9 + .02).toFixed(3)}" fill="${i % 2 ? '#fff' : '#0d5eaf'}"/>`).join('') +
      '<rect width="9" height="11.11" fill="#0d5eaf"/><rect x="3.7" width="1.6" height="11.11" fill="#fff"/><rect y="4.76" width="9" height="1.6" fill="#fff"/>',
    CYP: '<rect width="30" height="20" fill="#fff"/><path d="M8 11c2-3.5 6-3 7-5 2 2 6 2.5 7 5.5-3 1-6 1-8 1.5-3 .5-5 0-6-2z" fill="#d57800"/><path d="M9 15q6 4 12 0" stroke="#4e7d3f" stroke-width="1.2" fill="none"/><path d="M10 16.4q5 3 10 0" stroke="#4e7d3f" stroke-width="1" fill="none"/>',
    TUR: '<rect width="30" height="20" fill="#e30a17"/><circle cx="11.5" cy="10" r="5" fill="#fff"/><circle cx="12.9" cy="10" r="4.1" fill="#e30a17"/><polygon points="' + star(17.3, 10, 2.2) + '" fill="#fff"/>',
    SGP: '<rect width="30" height="10" fill="#ef3340"/><rect y="10" width="30" height="10" fill="#fff"/><circle cx="7.5" cy="5.2" r="3.6" fill="#fff"/><circle cx="8.8" cy="5.2" r="3" fill="#ef3340"/>' +
      [[13, 2.8], [15.4, 3.9], [16.6, 6.5], [15.4, 9.1], [13, 10.2]].map(([x, y]) => `<polygon points="${star(x, y, .9)}" fill="#fff"/>`).join(''),
    SAU: '<rect width="30" height="20" fill="#006c35"/><rect x="7.5" y="6" width="15" height="1.6" fill="#fff"/><rect x="9" y="12" width="12" height="1.2" fill="#fff"/><rect x="19.5" y="10.8" width="1.2" height="3.6" fill="#fff"/>',
    QAT: '<rect width="30" height="20" fill="#8a1538"/><polygon points="0,0 8,2.5 0,5 8,7.5 0,10 8,12.5 0,15 8,17.5 0,20" fill="#fff"/>',
    BHR: '<rect width="30" height="20" fill="#ce1126"/><polygon points="0,0 6,2 0,4 6,6 0,8 6,10 0,12 6,14 0,16 6,18 0,20" fill="#fff"/>',
    /* South Korea: taegeuk reduced to two half discs, the four trigrams as bars */
    KOR: '<rect width="30" height="20" fill="#fff"/><circle cx="15" cy="10" r="5" fill="#cd2e3a"/><path d="M10 10A5 5 0 0 0 20 10A2.5 2.5 0 0 1 15 10A2.5 2.5 0 0 0 10 10Z" fill="#0047a0"/>' +
      [[6, 5], [24, 5], [6, 15], [24, 15]].map(([x, y]) => `<g fill="#000">${[-1.2, 0, 1.2].map((dy) => `<rect x="${x - 2}" y="${(y + dy - .3).toFixed(2)}" width="4" height=".6"/>`).join('')}</g>`).join(''),
    ARE: h3('#00732f', '#fff', '#000') + '<rect width="7.5" height="20" fill="#ff0000"/>',
    AZE: h3('#0092d2', '#e4002b', '#00b33c') + '<circle cx="14.2" cy="10" r="3.2" fill="#fff"/><circle cx="15.2" cy="10" r="2.6" fill="#e4002b"/><polygon points="' + star(18.4, 10, 1.9) + '" fill="#fff"/>',
    IND: h3('#ff9933', '#fff', '#138808') + '<circle cx="15" cy="10" r="2.6" fill="none" stroke="#000080" stroke-width=".5"/><circle cx="15" cy="10" r=".6" fill="#000080"/>',
    HUN: h3('#ce2939', '#fff', '#477050'),
    THA: '<rect width="30" height="3.33" fill="#a51931"/><rect y="3.33" width="30" height="3.33" fill="#fff"/><rect y="6.67" width="30" height="6.67" fill="#2d2a4a"/><rect y="13.33" width="30" height="3.33" fill="#fff"/><rect y="16.67" width="30" height="3.33" fill="#a51931"/>',
  };
  /* The world names real people by demonym ("Italian"), generated ones by code ("ITA"); both reach the same flag. */
  /* Nationality strings as the Jolpica history gives them; the generated world keeps them as they are. Names come from strings/. */
  export const DEMONYM = { British: 'GBR', English: 'GBR', Italian: 'ITA', German: 'GER', French: 'FRA', American: 'USA', Swiss: 'SUI', Austrian: 'AUT',
    Japanese: 'JPN', Brazilian: 'BRA', Spanish: 'ESP', Australian: 'AUS', Dutch: 'NED', Canadian: 'CAN', Irish: 'IRL', Belgian: 'BEL',
    Monegasque: 'MON', Swedish: 'SWE', Argentine: 'ARG', 'New Zealander': 'NZL', 'South African': 'RSA', Polish: 'POL', Portuguese: 'POR',
    Mexican: 'MEX', Moroccan: 'MAR', Finnish: 'FIN', Danish: 'DEN', Uruguayan: 'URU', Rhodesian: 'RHO', Russian: 'RUS', Venezuelan: 'VEN',
    'East German': 'GDR', Colombian: 'COL', Thai: 'THA', Indian: 'IND', Hungarian: 'HUN', Czech: 'CZE', Indonesian: 'IDN',
    Liechtensteiner: 'LIE', Chilean: 'CHI', Malaysian: 'MYS', Chinese: 'CHN', 'Hong Kong': 'HKG' };
  const ALIAS = { ZAF: 'RSA', DEU: 'GER', MCO: 'MON', NLD: 'NED', CHE: 'SUI', PRT: 'POR', ...DEMONYM };

  const ukj = `<symbol id="ukj" viewBox="0 0 60 30" preserveAspectRatio="xMidYMid slice">
    <clipPath id="ukj-c"><path d="M0,0v30h60v-30z"/></clipPath><clipPath id="ukj-t"><path d="M30,15h30v15zv15h-30zh-30v-15zv-15h30z"/></clipPath>
    <g clip-path="url(#ukj-c)"><path d="M0,0v30h60v-30z" fill="#012169"/><path d="M0,0L60,30M60,0L0,30" stroke="#fff" stroke-width="6"/>
    <path d="M0,0L60,30M60,0L0,30" clip-path="url(#ukj-t)" stroke="#c8102e" stroke-width="4"/><path d="M30,0v30M0,15h60" stroke="#fff" stroke-width="10"/>
    <path d="M30,0v30M0,15h60" stroke="#c8102e" stroke-width="6"/></g></symbol>`;
export function flagSprite() {
  return `<svg id="flag-sprite" aria-hidden="true" style="position:absolute;width:0;height:0;overflow:hidden">${ukj}${Object.entries(F).map(([k, v]) => `<symbol id="fl-${k}" viewBox="0 0 30 20">${v}</symbol>`).join('')}</svg>`;
}

export function flagCode(code) {
  return ALIAS[code] || code;
}

export function hasFlag(code) {
  return !!F[flagCode(code)];
}

export function flagInner(code) {
  return `<svg viewBox="0 0 30 20" aria-hidden="true"><use href="#fl-${flagCode(code)}"/></svg>`;
}
