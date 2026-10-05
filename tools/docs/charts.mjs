// SVG charts for the docs. Every named chart reads its numbers from the C# sources through CodeValues,
// so a chart always shows what the game does today. Colours are CSS classes (c1..c5), themed by the page.

import { escapeHtml } from './markdown.mjs';
import { linear, smooth } from './code-values.mjs';

const W = 640;
const M = { l: 56, r: 18, t: 18, b: 46 };

/* Polish number format: comma decimal, thin space thousands. */
export function fmt(n, digits = 0) {
  if (typeof n !== 'number' || !Number.isFinite(n)) return String(n);
  const fixed = n.toFixed(digits);
  const [int, dec] = fixed.split('.');
  const sign = int.startsWith('-') ? '−' : '';
  const body = int.replace('-', '');
  const grouped = body.length > 4 ? body.replace(/\B(?=(\d{3})+(?!\d))/g, ' ') : body;
  return sign + grouped + (dec ? ',' + dec : '');
}

const pct = (v, d = 0) => fmt(v * 100, d) + '%';

function frame(height, inner, title) {
  return `<svg viewBox="0 0 ${W} ${height}" role="img" aria-label="${escapeHtml(title)}" class="chart-svg">${inner}</svg>`;
}

function legend(series) {
  if (series.length < 2) return '';
  return `<div class="legend">${series.map(s => `<span><i class="sw ${s.cls}${s.dash ? ' dash' : ''}"></i>${escapeHtml(s.label)}</span>`).join('')}</div>`;
}

function figure(title, svg, series, caption) {
  return `<figure class="chart"><figcaption class="chart-title">${escapeHtml(title)}</figcaption>${svg}${legend(series)}${caption ? `<div class="chart-note">${caption}</div>` : ''}</figure>`;
}

/* Line chart. spec: { title, x:[a,b], y:[a,b], xTicks, yTicks, xFmt, yFmt, xLabel, yLabel, series:[{label,cls,pts,dash,step}],
 * bands:[{x0,x1,label}], marks:[{x,y,label,cls,dx,dy,anchor}], height } */
export function lineChart(spec, caption) {
  const H = spec.height ?? 300;
  const pw = W - M.l - M.r;
  const ph = H - M.t - M.b;
  const [x0, x1] = spec.x;
  const [y0, y1] = spec.y;
  const X = v => M.l + (v - x0) / (x1 - x0) * pw;
  const Y = v => M.t + ph - (v - y0) / (y1 - y0) * ph;
  const xf = spec.xFmt ?? (v => fmt(v));
  const yf = spec.yFmt ?? (v => fmt(v));
  let g = '';
  for (const b of spec.bands ?? []) {
    g += `<rect class="band" x="${X(b.x0)}" y="${M.t}" width="${X(b.x1) - X(b.x0)}" height="${ph}"/>`;
    if (b.label) g += `<text class="band-label" x="${(X(b.x0) + X(b.x1)) / 2}" y="${M.t + 13}" text-anchor="middle">${escapeHtml(b.label)}</text>`;
  }
  for (const t of spec.yTicks ?? []) {
    g += `<line class="grid" x1="${M.l}" x2="${M.l + pw}" y1="${Y(t)}" y2="${Y(t)}"/>`;
    g += `<text class="tick" x="${M.l - 8}" y="${Y(t) + 4}" text-anchor="end">${yf(t)}</text>`;
  }
  for (const t of spec.xTicks ?? []) {
    g += `<line class="tickline" x1="${X(t)}" x2="${X(t)}" y1="${M.t + ph}" y2="${M.t + ph + 5}"/>`;
    g += `<text class="tick" x="${X(t)}" y="${M.t + ph + 19}" text-anchor="middle">${xf(t)}</text>`;
  }
  g += `<line class="axis" x1="${M.l}" x2="${M.l + pw}" y1="${M.t + ph}" y2="${M.t + ph}"/>`;
  if (spec.xLabel) g += `<text class="axis-label" x="${M.l + pw / 2}" y="${H - 6}" text-anchor="middle">${escapeHtml(spec.xLabel)}</text>`;
  if (spec.yLabel) g += `<text class="axis-label" transform="translate(13 ${M.t + ph / 2}) rotate(-90)" text-anchor="middle">${escapeHtml(spec.yLabel)}</text>`;
  for (const s of spec.series) {
    let pts = s.pts.filter(([a, b]) => Number.isFinite(a) && Number.isFinite(b));
    if (spec.clip) {
      const over = pts.findIndex(([, b]) => b > y1);
      if (over > 0) {
        const [ax, ay] = pts[over - 1];
        const [bx, by] = pts[over];
        pts = [...pts.slice(0, over), [ax + (bx - ax) * (y1 - ay) / (by - ay), y1]];
      }
    }
    let d = '';
    pts.forEach(([a, b], k) => {
      const px = X(a).toFixed(1);
      const py = Y(Math.max(y0, Math.min(y1, b))).toFixed(1);
      if (k === 0) d += `M${px} ${py}`;
      else if (s.step) d += `H${px}V${py}`;
      else d += `L${px} ${py}`;
    });
    g += `<path class="line ${s.cls}${s.dash ? ' dash' : ''}" d="${d}"/>`;
  }
  for (const m of spec.marks ?? []) {
    g += `<circle class="dot ${m.cls ?? 'c1'}" cx="${X(m.x)}" cy="${Y(m.y)}" r="4"/>`;
    if (m.label) g += `<text class="mark-label" x="${X(m.x) + (m.dx ?? 8)}" y="${Y(m.y) + (m.dy ?? -8)}" text-anchor="${m.anchor ?? 'start'}">${escapeHtml(m.label)}</text>`;
  }
  return figure(spec.title, frame(H, g, spec.title), spec.series, caption);
}

/* Vertical grouped bars. spec: { title, categories, series:[{label,cls,values}], y:[0,max], yTicks, yFmt, valueFmt, yLabel } */
export function barChart(spec, caption) {
  const H = spec.height ?? 280;
  const pw = W - M.l - M.r;
  const ph = H - M.t - M.b;
  const [y0, y1] = spec.y;
  const Y = v => M.t + ph - (v - y0) / (y1 - y0) * ph;
  const yf = spec.yFmt ?? (v => fmt(v));
  const vf = spec.valueFmt ?? yf;
  const n = spec.categories.length;
  const slot = pw / n;
  const gap = Math.min(18, slot * 0.25);
  const bw = (slot - gap) / spec.series.length;
  let g = '';
  for (const t of spec.yTicks ?? []) {
    g += `<line class="grid" x1="${M.l}" x2="${M.l + pw}" y1="${Y(t)}" y2="${Y(t)}"/>`;
    g += `<text class="tick" x="${M.l - 8}" y="${Y(t) + 4}" text-anchor="end">${yf(t)}</text>`;
  }
  spec.categories.forEach((c, i) => {
    const sx = M.l + i * slot + gap / 2;
    spec.series.forEach((s, k) => {
      const v = s.values[i];
      const x = sx + k * bw;
      g += `<rect class="bar ${s.cls}" x="${x + 1}" y="${Y(v)}" width="${Math.max(1, bw - 2)}" height="${Y(y0) - Y(v)}" rx="2"/>`;
      if (spec.showValues !== false && bw > 22) g += `<text class="bar-value" x="${x + bw / 2}" y="${Y(v) - 5}" text-anchor="middle">${vf(v)}</text>`;
    });
    g += `<text class="tick" x="${M.l + i * slot + slot / 2}" y="${M.t + ph + 19}" text-anchor="middle">${escapeHtml(c)}</text>`;
  });
  g += `<line class="axis" x1="${M.l}" x2="${M.l + pw}" y1="${M.t + ph}" y2="${M.t + ph}"/>`;
  if (spec.yLabel) g += `<text class="axis-label" transform="translate(13 ${M.t + ph / 2}) rotate(-90)" text-anchor="middle">${escapeHtml(spec.yLabel)}</text>`;
  return figure(spec.title, frame(H, g, spec.title), spec.series, caption);
}

/* Horizontal grouped bars, one row per category. spec: { title, categories, series, max, valueFmt } */
export function hBarChart(spec, caption) {
  const rowH = 12 * spec.series.length + 12;
  const left = 150;
  const H = spec.categories.length * rowH + 16;
  const pw = W - left - 50;
  const X = v => left + v / spec.max * pw;
  const vf = spec.valueFmt ?? (v => fmt(v));
  let g = '';
  spec.categories.forEach((c, i) => {
    const top = 8 + i * rowH;
    g += `<text class="tick" x="${left - 10}" y="${top + rowH / 2 + 2}" text-anchor="end">${escapeHtml(c)}</text>`;
    spec.series.forEach((s, k) => {
      const v = s.values[i];
      const y = top + 4 + k * 12;
      g += `<rect class="bar ${s.cls}" x="${left}" y="${y}" width="${X(v) - left}" height="10" rx="2"/>`;
      g += `<text class="bar-value" x="${X(v) + 5}" y="${y + 9}">${vf(v)}</text>`;
    });
  });
  return figure(spec.title, frame(H, g, spec.title), spec.series, caption);
}

const range = (a, b, step = 1) => {
  const out = [];
  for (let v = a; v <= b + 1e-9; v += step) out.push(Number(v.toFixed(6)));
  return out;
};

/* ---------- named charts: numbers come from the code ---------- */

const P = {
  gen: 'src/Paddock.Domain/People/GenerationEstimates.cs',
  arc: 'tools/Paddock.DataPipeline/Ratings/RatingsCareerArc.cs',
  scale: 'tools/Paddock.DataPipeline/Ratings/RatingsEraScale.cs',
  map: 'tools/Paddock.DataPipeline/Ratings/RatingsMapping.cs',
  career: 'src/Paddock.Domain/Career/CareerDayEstimates.cs',
  pool: 'src/Paddock.Domain/Pool/PoolEstimates.cs',
  cars: 'src/Paddock.Domain/Cars/CarNumbers.cs',
  dev: 'src/Paddock.Domain/Development/DevelopmentEstimates.cs',
  sponsor: 'src/Paddock.Domain/Sponsors/SponsorEstimates.cs',
  pace: 'src/Paddock.Simulation/Racing/Pace/PaceConstants.cs',
  weather: 'src/Paddock.Simulation/Racing/Weather/WeatherConstants.cs',
  tyres: 'src/Paddock.Simulation/Racing/Tyres/TyreFuelConstants.cs',
  catalog: 'src/Paddock.Simulation/Racing/Tyres/TyreCompoundCatalog.cs',
  incidents: 'src/Paddock.Simulation/Racing/Incidents/IncidentConstants.cs',
  pits: 'src/Paddock.Simulation/Racing/Pits/PitConstants.cs',
  board: 'src/Paddock.Domain/Board/BoardEstimates.cs',
  finance: 'src/Paddock.Domain/Finance/FinanceEstimates.cs',
};

export function namedChart(name, cv, caption) {
  const v = (file, key) => cv.value(P[file], key);
  const attrNames = {
    cornering: 'Zakręty', braking: 'Hamowanie', smoothness: 'Płynność', overtaking: 'Wyprzedzanie',
    defending: 'Obrona', consistency: 'Regularność', composure: 'Opanowanie', adaptability: 'Adaptacja',
    wet_weather: 'Deszcz', fitness: 'Kondycja', feedback: 'Informacja zwrotna',
  };

  switch (name) {
    case 'wagi-oceny': {
      const early = cv.weights(P.gen, 'EarlyWeights');
      const classic = cv.weights(P.gen, 'ClassicWeights');
      const modern = cv.weights(P.gen, 'ModernWeights');
      const share = list => { const t = list.reduce((a, [, w]) => a + w, 0); return list.map(([, w]) => w / t); };
      const lastEarly = v('gen', 'EarlyEraLastYear');
      const firstModern = v('gen', 'ModernEraFirstYear');
      return hBarChart({
        title: 'Waga atrybutu w ocenie ogólnej, według epoki',
        categories: early.map(([k]) => attrNames[k] ?? k),
        series: [
          { label: `do ${lastEarly}`, cls: 'c3', values: share(early) },
          { label: `${lastEarly + 1}–${firstModern - 1}`, cls: 'c2', values: share(classic) },
          { label: `od ${firstModern}`, cls: 'c1', values: share(modern) },
        ],
        max: 0.2,
        valueFmt: x => pct(x, 0),
      }, caption);
    }

    case 'luk-kariery': {
      const before = v('arc', 'YearsOfGrowthBeforeDebut');
      const minPeak = v('arc', 'MinPeakAge');
      const maxPeak = v('arc', 'MaxPeakAge');
      const dMin = v('arc', 'DeclineStartMin');
      const dMax = v('arc', 'DeclineStartMax');
      const rate = v('arc', 'DeclineLevelsPerYear');
      // Example: debut at 21, peak at 27 at level 18, decline from 38. Growth shape is drawn smooth (schematic).
      const debut = 21, peakAge = 27, peak = 18, decline = 38, start = debut - before, low = 11;
      const typical = [];
      for (let a = start; a <= 46; a += 0.25) {
        let y;
        if (a <= peakAge) { const t = (a - start) / (peakAge - start); y = low + (peak - low) * (1 - (1 - t) ** 2); }
        else if (a <= decline) y = peak;
        else y = peak - rate * (a - decline);
        typical.push([a, y]);
      }
      // Retired at the peak at 46 (a Fangio-like career): decline only after the last real season.
      const late = [];
      for (let a = 33; a <= 52; a += 0.25) late.push([a, a <= 46 ? 19.5 : 19.5 - rate * (a - 46)]);
      return lineChart({
        title: 'Łuk kariery kierowcy (kształt wspólny dla wszystkich)',
        x: [16, 52], y: [8, 20.5],
        xTicks: range(16, 52, 4), yTicks: [8, 10, 12, 14, 16, 18, 20],
        xLabel: 'wiek', yLabel: 'poziom 1–20',
        bands: [
          { x0: minPeak, x1: maxPeak, label: `szczyt zwykle ${minPeak}–${maxPeak}` },
          { x0: dMin, x1: dMax, label: `spadek od ${dMin}–${dMax}` },
        ],
        series: [
          { label: `typowy: debiut w wieku ${debut}, szczyt w ${peakAge}`, cls: 'c1', pts: typical },
          { label: 'odszedł na szczycie w wieku 46 (jak Fangio)', cls: 'c2', pts: late, dash: true },
        ],
        marks: [
          { x: start, y: low, label: `rozwój od ${start} lat`, cls: 'c1', dy: 16 },
          { x: debut, y: low + (peak - low) * (1 - (1 - (debut - start) / (peakAge - start)) ** 2), label: 'debiut w F1', cls: 'c1', dx: 8, dy: 14 },
        ],
        height: 320,
      }, caption);
    }

    case 'emerytura': {
      const from = v('career', 'DriverRetirementFromAge');
      const certain = v('career', 'DriverRetirementCertainAge');
      const steps = certain - from + 1;
      const yearly = [];
      const stillRacing = [];
      let alive = 1;
      for (let a = from - 2; a <= certain + 1; a++) {
        const p = a < from ? 0 : a >= certain ? 1 : (a - from + 1) / steps;
        yearly.push([a, p]);
        alive *= 1 - p;
        stillRacing.push([a, alive]);
      }
      return lineChart({
        title: 'Kiedy kierowcy bez zaplanowanego końca kariery odchodzą na emeryturę',
        x: [from - 2, certain + 1], y: [0, 1],
        xTicks: range(from - 2, certain + 1, 2), yTicks: [0, 0.25, 0.5, 0.75, 1],
        yFmt: x => pct(x), xLabel: 'wiek (urodziny)',
        series: [
          { label: 'szansa odejścia w danych urodzinach', cls: 'c1', pts: yearly, step: true },
          { label: 'odsetek, który nadal się ściga', cls: 'c3', pts: stillRacing, step: true },
        ],
      }, caption);
    }

    case 'skala-ocen': {
      const mean = v('map', 'LevelAtFieldMean');
      const below = v('map', 'LevelsPerSd');
      const sat = v('map', 'SaturationSd');
      const level = z => (z < 0 ? Math.max(1, mean + below * z) : mean + 8 * Math.tanh(z / sat));
      const pts = range(-2.5, 3, 0.05).map(z => [z, level(z)]);
      return lineChart({
        title: 'Od przewagi nad stawką epoki do poziomu i gwiazdek',
        x: [-2.5, 3], y: [0, 20],
        xTicks: [-2, -1, 0, 1, 2, 3], yTicks: [0, 4, 8, 12, 16, 20],
        xFmt: z => (z > 0 ? '+' : '') + fmt(z), yFmt: y => `${fmt(y)} (${fmt(y / 4)}★)`,
        xLabel: 'odchylenia od średniej stawki swojej epoki (z)',
        series: [{ label: 'poziom', cls: 'c1', pts }],
        marks: [
          { x: 0, y: level(0), label: `przeciętny kierowca epoki: ${fmt(level(0))} (${fmt(level(0) / 4, 1)}★)`, cls: 'c3', dy: 18 },
          { x: 1.2, y: level(1.2), label: `z = 1,2 (np. Lauda): ${fmt(level(1.2), 1)}`, cls: 'c2', dx: 10, dy: 18 },
          { x: 2, y: level(2), label: `z = 2 (Senna, Fangio): ${fmt(level(2), 1)}`, cls: 'c2', dx: -8, dy: -10, anchor: 'end' },
        ],
      }, caption);
    }

    case 'przyciaganie': {
      const half = v('scale', 'ShrinkHalfDuels');
      const pts = range(0, 600, 5).map(d => [d, d / (d + half)]);
      return lineChart({
        title: 'Ile przewagi nad stawką zostaje kierowcy, zależnie od liczby pojedynków',
        x: [0, 600], y: [0, 1], xTicks: range(0, 600, 100), yTicks: [0, 0.25, 0.5, 0.75, 1],
        yFmt: x => pct(x), xLabel: 'pojedynki z partnerem z zespołu (cała kariera)',
        series: [{ label: 'zachowana część przewagi', cls: 'c1', pts }],
        marks: [
          { x: 40, y: 40 / (40 + half), label: `40 pojedynków: ${pct(40 / (40 + half))}`, cls: 'c2', dx: 10, dy: 16 },
          { x: half, y: 0.5, label: `${fmt(half)} pojedynków: połowa`, cls: 'c3', dx: -8, dy: -10, anchor: 'end' },
          { x: 400, y: 400 / (400 + half), label: `400: ${pct(400 / (400 + half))}`, cls: 'c2', dy: 18 },
        ],
      }, caption);
    }

    case 'pula-rozwoj': {
      const rate = v('pool', 'DevelopmentRatePercent') / 100;
      const step = v('pool', 'MaxAnnualStep');
      const cheap = v('pool', 'CheapSlowSpeedPercent') / 100;
      const fast = v('pool', 'ExpensiveFastSpeedPercent') / 100;
      const run = speed => {
        let a = 8; const out = [[0, a]];
        for (let s = 1; s <= 6; s++) { a = Math.min(16, a + Math.min(step, (16 - a) * rate * speed)); out.push([s, a]); }
        return out;
      };
      return lineChart({
        title: 'Rozwój juniora w puli: atrybut 8, ukryty potencjał 16',
        x: [0, 6], y: [8, 16.5], xTicks: range(0, 6), yTicks: [8, 10, 12, 14, 16],
        xLabel: 'sezony w puli', yLabel: 'atrybut 1–20',
        series: [
          { label: 'sam (bez programu)', cls: 'c3', pts: run(1) },
          { label: `tani i wolny program (${pct(cheap)})`, cls: 'c2', pts: run(cheap) },
          { label: `drogi i szybki program (${pct(fast)})`, cls: 'c1', pts: run(fast) },
        ],
      }, caption);
    }

    case 'pasmo-skauta': {
      const start = v('pool', 'StartHalfWidth');
      const min = v('pool', 'MinHalfWidth');
      const tau = v('pool', 'HalfWidthTauPoints');
      const person = v('pool', 'PersonFocusMilliPerMonth') / 1000;
      const whole = v('pool', 'PoolFocusMilliPerMonth') / 1000;
      const net = (v('pool', 'NetworkBasePercent') + v('pool', 'NetworkPercentPerPoint') * 10) / 100;
      const width = pts => 2 * (min + (start - min) * tau / (tau + pts));
      const months = range(0, 24, 0.5);
      return lineChart({
        title: 'Szerokość widełek atrybutu u skauta (sieć kontaktów 10/20)',
        x: [0, 24], y: [0, 2 * start], xTicks: range(0, 24, 3), yTicks: [0, 2, 4, 6, 8, 10],
        xLabel: 'miesiące obserwacji', yLabel: 'szerokość widełek (punkty)',
        series: [
          { label: 'skupienie na jednej osobie', cls: 'c1', pts: months.map(m => [m, width(m * person * net)]) },
          { label: 'obserwacja całej puli', cls: 'c3', pts: months.map(m => [m, width(m * whole * net)]) },
        ],
        marks: [{ x: 24, y: 2 * min, label: `najwęższe możliwe: ${fmt(2 * min)} pkt`, cls: 'c2', dx: -6, dy: -8, anchor: 'end' }],
      }, caption);
    }

    case 'koncepcja': {
      const em = v('cars', 'EvolutionCeilingMean'), es = v('cars', 'EvolutionCeilingSd');
      const rm = v('cars', 'RevolutionCeilingMean'), rs = v('cars', 'RevolutionCeilingSd');
      const ef = v('cars', 'EvolutionStartFraction'), rf = v('cars', 'RevolutionStartFraction');
      const pdf = (x, m, s) => Math.exp(-0.5 * ((x - m) / s) ** 2) / (s * Math.sqrt(2 * Math.PI));
      const xs = range(30, 110, 0.5);
      return lineChart({
        title: 'Ewolucja czy rewolucja: gdzie wypada sufit koncepcji',
        x: [30, 110], y: [0, 0.12], xTicks: range(30, 110, 10), yTicks: [],
        xLabel: 'sufit osiągów koncepcji (skala 0–100)',
        series: [
          { label: `ewolucja: sufit ~${fmt(em)}, start ${pct(ef)} sufitu`, cls: 'c3', pts: xs.map(x => [x, pdf(x, em, es)]) },
          { label: `rewolucja: sufit ~${fmt(rm)}, start ${pct(rf)} sufitu`, cls: 'c1', pts: xs.map(x => [x, pdf(x, rm, rs)]) },
        ],
        marks: [
          { x: em * ef, y: 0.005, label: `start ewolucji ≈ ${fmt(em * ef)}`, cls: 'c3', dy: -10 },
          { x: rm * rf, y: 0.005, label: `start rewolucji ≈ ${fmt(rm * rf)}`, cls: 'c1', dy: -26 },
        ],
      }, caption);
    }

    case 'konto-rozwoju': {
      const decay = v('dev', 'AccountDailyDecay');
      const pts = range(0, 730, 5).map(d => [d, 100 * (1 - decay) ** d]);
      return lineChart({
        title: 'Konto rozwoju traci wartość, bo rywale idą do przodu',
        x: [0, 730], y: [0, 100], xTicks: [0, 90, 180, 365, 545, 730], yTicks: [0, 25, 50, 75, 100],
        yFmt: y => fmt(y) + '%', xLabel: 'dni bez użycia', yLabel: 'wartość odłożonej wiedzy',
        series: [{ label: 'wartość konta', cls: 'c1', pts }],
        marks: [
          { x: 365, y: 100 * (1 - decay) ** 365, label: `po roku: ${fmt(100 * (1 - decay) ** 365)}%`, cls: 'c1' },
          { x: 730, y: 100 * (1 - decay) ** 730, label: `po 2 latach: ${fmt(100 * (1 - decay) ** 730)}%`, cls: 'c1', dx: -6, anchor: 'end' },
        ],
      }, caption);
    }

    case 'czas-produkcji': {
      const base = v('dev', 'ConceptProductionBaseDays');
      const hc = cv.anchors(P.dev, 'HeadcountAnchors');
      const days = y => Math.round(base * Math.sqrt(linear(hc, y) / linear(hc, 1950)));
      const pts = range(1950, 2026).map(y => [y, days(y)]);
      return lineChart({
        title: 'Ile dni trwa produkcja zatwierdzonej koncepcji (przeciętny zespół epoki)',
        x: [1950, 2026], y: [0, 200], xTicks: range(1950, 2020, 10), yTicks: [0, 50, 100, 150, 200],
        xFmt: y => String(y), xLabel: 'sezon', yLabel: 'dni produkcji',
        series: [{ label: 'dni', cls: 'c1', pts }],
        marks: [
          { x: 1955, y: days(1955), label: `1955: ${days(1955)} dni`, cls: 'c1', dy: -10 },
          { x: 1976, y: days(1976), label: `1976: ${days(1976)} dni`, cls: 'c1', dy: -10 },
          { x: 2025, y: days(2025), label: `2025: ${days(2025)} dni`, cls: 'c1', dx: -8, anchor: 'end' },
        ],
      }, caption);
    }

    case 'sponsor-czekanie': {
      const open = v('sponsor', 'OpeningTermsMilli');
      const gain = v('sponsor', 'WaitingGainMilliPerDay');
      const cap = v('sponsor', 'BaseCapMilli') + v('sponsor', 'CapMilliPerSkill') * 10;
      const presence = v('sponsor', 'RivalPresenceChance');
      const daily = v('sponsor', 'RivalSignChance');
      const terms = d => Math.min(cap, open + gain * d) / 1000;
      const free = d => (1 - presence) + presence * (1 - daily) ** d;
      const xs = range(0, 90);
      const best = xs.reduce((b, d) => (terms(d) * free(d) > terms(b) * free(b) ? d : b), 0);
      return lineChart({
        title: 'Podpisać od razu czy czekać? Rozmowa ze sponsorem (negocjator 10/20)',
        x: [0, 90], y: [0.4, 1.15], xTicks: range(0, 90, 15), yTicks: [0.4, 0.6, 0.8, 1, 1.1],
        yFmt: y => pct(y), xLabel: 'dni rozmów',
        series: [
          { label: 'warunki (część pełnej ceny)', cls: 'c1', pts: xs.map(d => [d, terms(d)]) },
          { label: 'szansa, że sponsor jeszcze nie podpisał z rywalem', cls: 'c3', pts: xs.map(d => [d, free(d)]) },
          { label: 'średnio do zdobycia (warunki × szansa)', cls: 'c2', pts: xs.map(d => [d, terms(d) * free(d)]), dash: true },
        ],
        marks: [{ x: best, y: terms(best) * free(best), label: best === 0 ? 'średnio najwięcej daje podpis od razu' : `średnio najwięcej: ok. ${best} dni`, cls: 'c2', dx: 14, dy: 44 }],
        height: 320,
      }, caption);
    }

    case 'predkosc-epoki': {
      const anchors = cv.anchors(P.pace, 'EraAverageSpeedKph');
      const pts = range(1950, 2026).map(y => [y, smooth(anchors, y)]);
      return lineChart({
        title: 'Średnia prędkość okrążenia na neutralnym torze',
        x: [1950, 2026], y: [140, 230], xTicks: range(1950, 2020, 10), yTicks: [140, 160, 180, 200, 220],
        xFmt: y => String(y), yFmt: y => `${fmt(y)}`, xLabel: 'sezon', yLabel: 'km/h',
        series: [{ label: 'km/h', cls: 'c1', pts }],
        marks: anchors.map(([y, s]) => ({ x: y, y: s, cls: 'c3' })),
      }, caption);
    }

    case 'dopasowanie-toru': {
      const m = cv.dict(P.pace, 'CarFitMatrix');
      const keys = ['straights', 'high_speed', 'low_speed', 'braking'].filter(k => m[k]);
      const names = { straights: 'proste', high_speed: 'szybkie zakręty', low_speed: 'wolne zakręty', braking: 'strefy hamowania' };
      const attrs = ['Moc', 'Docisk', 'Przyczepność mech.', 'Hamowanie'];
      const cls = ['c1', 'c2', 'c3', 'c4'];
      return barChart({
        title: 'Co nagradza każdy fragment toru (udział parametru auta)',
        categories: keys.map(k => names[k]),
        series: attrs.map((label, a) => ({ label, cls: cls[a], values: keys.map(k => m[k][a]) })),
        y: [0, 1], yTicks: [0, 0.25, 0.5, 0.75, 1], yFmt: x => pct(x), showValues: false,
      }, caption);
    }

    case 'auto-kontra-kierowca': {
      const sens = cv.anchors(P.pace, 'CarFitSensitivity');
      const driver = v('pace', 'DriverSensitivity');
      const years = range(1950, 2026);
      return lineChart({
        title: 'Ile daje +10 punktów oceny (0–100) na okrążeniu trwającym 100 s',
        x: [1950, 2026], y: [0, 1.4], xTicks: range(1950, 2020, 10), yTicks: [0, 0.25, 0.5, 0.75, 1, 1.25],
        xFmt: y => String(y), yFmt: s => fmt(s, 2) + ' s', xLabel: 'sezon',
        series: [
          { label: 'auto lepiej dopasowane do toru', cls: 'c1', pts: years.map(y => [y, smooth(sens, y) * 100 * 10]) },
          { label: 'szybszy kierowca (+2 punkty atrybutu)', cls: 'c3', pts: years.map(y => [y, driver * 100 * 10]) },
        ],
      }, caption);
    }

    case 'opony': {
      const all = cv.calls(P.catalog, 'Dry');
      const growth = v('tyres', 'WearGrowthShare');
      const drop = v('tyres', 'CliffDropSeconds');
      const slope = v('tyres', 'CliffSlopeMultiplier');
      const loss = ([, grip, wear, cliff], e) => grip + wear * e * (1 + growth * e / cliff) + (e > cliff ? drop + slope * wear * (e - cliff) : 0);
      const pick = id => all.find(c => c.id === id);
      const sets = [
        ['treaded.hard', 'lata 50.–60.: jedna twarda opona', 'c3'],
        ['slick2009.soft', 'era 2009+: miękka', 'c1'],
        ['slick2009.medium', 'era 2009+: pośrednia', 'c2'],
        ['slick2009.hard', 'era 2009+: twarda', 'c4'],
      ].filter(([id]) => pick(id));
      const xs = range(0, 60, 0.5);
      return lineChart({
        title: 'Strata czasu na okrążeniu wraz ze zużyciem opon',
        x: [0, 60], y: [0, 4], xTicks: range(0, 60, 10), yTicks: [0, 1, 2, 3, 4], clip: true,
        yFmt: s => fmt(s) + ' s', xLabel: 'okrążenia na komplecie (typowe warunki)',
        series: sets.map(([id, label, cls]) => ({ label, cls, pts: xs.map(e => [e, loss(pick(id).args, e)]) })),
      }, caption);
    }

    case 'incydenty': {
      const ae = v('incidents', 'AggressionExponent');
      const ce = v('incidents', 'ComposureExponent');
      const xs = range(0, 100, 2);
      return lineChart({
        title: 'Jak charakter kierowcy zmienia ryzyko incydentu',
        x: [0, 100], y: [0.4, 2], xTicks: range(0, 100, 20), yTicks: [0.5, 1, 1.5, 2],
        yFmt: x => '×' + fmt(x, x % 1 ? 1 : 0), xLabel: 'wartość cechy (0–100)', yLabel: 'mnożnik ryzyka',
        series: [
          { label: 'agresja', cls: 'c1', pts: xs.map(a => [a, Math.exp(ae * (a - 50) / 50)]) },
          { label: 'opanowanie', cls: 'c3', pts: xs.map(c => [c, Math.exp(-ce * (c - 50) / 50)]) },
        ],
      }, caption);
    }

    case 'deszcz': {
      return barChart({
        title: 'Szansa deszczu w trakcie wyścigu, według klimatu toru',
        categories: ['tor suchy', 'tor umiarkowany', 'tor deszczowy'],
        series: [{ label: 'szansa', cls: 'c2', values: [v('weather', 'RaceRainProbabilityLow'), v('weather', 'RaceRainProbabilityMedium'), v('weather', 'RaceRainProbabilityHigh')] }],
        y: [0, 0.3], yTicks: [0, 0.1, 0.2, 0.3], yFmt: x => pct(x), height: 240,
      }, caption);
    }

    case 'mechanicy': {
      const q0 = v('pits', 'FaultProbabilityAtQuality0');
      const q1 = v('pits', 'FaultProbabilityAtQuality100');
      return lineChart({
        title: 'Szansa wolnego postoju albo błędu w boksie',
        x: [0, 100], y: [0, 0.16], xTicks: range(0, 100, 20), yTicks: [0, 0.05, 0.1, 0.15],
        yFmt: x => pct(x), xLabel: 'jakość mechaników (0–100)',
        series: [{ label: 'szansa', cls: 'c1', pts: [[0, q0], [100, q1]] }],
        height: 240,
      }, caption);
    }

    case 'zarzad-prog': {
      const base = v('board', 'DismissBelowAtPatience50Tenths') / 10;
      const per = v('board', 'DismissThresholdTenthsPerPatiencePoint') / 10;
      const pBase = v('board', 'PatienceBase');
      const pAge = v('board', 'PatienceMaxAge');
      const xs = range(0, 100, 1);
      return lineChart({
        title: 'Próg zaufania, poniżej którego zarząd zaczyna liczyć słabe oceny',
        x: [0, 100], y: [20, 60], xTicks: range(0, 100, 20), yTicks: [20, 30, 40, 50, 60],
        xLabel: `cierpliwość zarządu (${pBase} + wiek zespołu w latach, najwyżej ${pBase + pAge})`, yLabel: 'zaufanie (punkty)',
        series: [{ label: 'próg', cls: 'c1', pts: xs.map(p => [p, base - per * (p - 50)]) }],
        height: 240,
      }, caption);
    }

    default:
      throw new Error(`docs: unknown chart "${name}"`);
  }
}

/* A chart whose data sits in the Markdown fence:
 *   tytuł: ...      oś: ...      format: %|liczba      serie: A | B
 *   label | value [| value]                                           */
export function dataChart(kind, body, caption) {
  const meta = {};
  const rows = [];
  for (const line of body) {
    if (!line.trim()) continue;
    const m = line.match(/^(tytuł|oś|format|serie|max):\s*(.*)$/);
    if (m) { meta[m[1]] = m[2].trim(); continue; }
    const cells = line.split('|').map(c => c.trim());
    rows.push({ label: cells[0], values: cells.slice(1).map(Number) });
  }
  const isPct = meta.format === '%';
  const f = x => (isPct ? fmt(x) + '%' : fmt(x, x % 1 ? 1 : 0));
  const names = (meta.serie ?? 'wartość').split('|').map(s => s.trim());
  const cls = ['c1', 'c3', 'c2', 'c4'];
  const top = Number(meta.max) || Math.max(...rows.flatMap(r => r.values)) * 1.15;
  const step = [1, 2, 2.5, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000].find(st => st >= top / 5) ?? top / 4;
  const max = Math.ceil(top / step) * step;
  return barChart({
    title: meta['tytuł'] ?? '',
    categories: rows.map(r => r.label),
    series: names.map((label, k) => ({ label, cls: cls[k], values: rows.map(r => r.values[k]) })),
    y: [0, max], yTicks: Array.from({ length: Math.round(max / step) + 1 }, (_, k) => k * step), yFmt: f, yLabel: meta['oś'], height: 260,
  }, caption);
}
