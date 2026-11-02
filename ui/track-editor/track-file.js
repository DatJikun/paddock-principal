'use strict';
/* Plik układu toru (TECH §6.5, §6.5a): wczytanie, zapis i odczyt pól v2 do rysowania.
   Czyste funkcje bez DOM, żeby test (ui/prototype/tests/track-editor-file.test.mjs) mógł je uruchomić w Node.
   Zasada: wszystko, czego edytor nie zmienia, wraca do pliku bez zmian (te same klucze i te same liczby).
   Wartości domyślne epoki (szerokość, pobocza) zna tylko kod C# (TrackEraDefaults); edytor ich nie kopiuje. */
(function (root) {
  const BASE_KEYS = ['layout_id', 'control_points', 'source', 'notes'];
  /** Kolejność zapisu znanych pól; nieznane idą na koniec w kolejności z pliku. */
  const KNOWN_ORDER = ['corners', 'width_m', 'edges', 'pit_lane', 'elevation_m'];

  /** Punkty kontrolne: zaokrąglenie do 1 mm, jak dotąd (przeciąganie daje długie ułamki). */
  function fmtPoint(x) {
    let r = Math.round(x * 1000) / 1000; if (r === 0) r = 0;
    let s = String(r); if (!/[.e]/.test(s)) s += '.0'; return s;
  }
  /** Pozostałe liczby bez zaokrąglania, żeby zapis był bezstratny. Całkowite zostają całkowite
      (indeks zakrętu `point` jest w C# liczbą całkowitą i nie przyjmie "6.0"). */
  function fmtNum(x) { return Object.is(x, -0) ? '0' : String(x); }
  function inline(v) {
    if (typeof v === 'number') return fmtNum(v);
    if (Array.isArray(v)) return '[' + v.map(inline).join(', ') + ']';
    if (v && typeof v === 'object') {
      const keys = Object.keys(v);
      return keys.length ? '{ ' + keys.map((k) => `${JSON.stringify(k)}: ${inline(v[k])}`).join(', ') + ' }' : '{}';
    }
    return JSON.stringify(v);
  }
  /** Lista obiektów lub list: jeden element w linii. Lista liczb i obiekt płaski: w jednej linii. */
  function block(v, indent) {
    const pad = ' '.repeat(indent), inner = ' '.repeat(indent + 2);
    if (Array.isArray(v)) {
      if (!v.length || v.every((x) => typeof x !== 'object' || x === null)) return inline(v);
      return '[\n' + v.map((x) => inner + inline(x)).join(',\n') + '\n' + pad + ']';
    }
    if (v && typeof v === 'object') {
      const keys = Object.keys(v);
      if (!keys.length) return '{}';
      return '{\n' + keys.map((k) => `${inner}${JSON.stringify(k)}: ${block(v[k], indent + 2)}`).join(',\n') + '\n' + pad + '}';
    }
    return inline(v);
  }

  /** Tekst JSON -> model edytora. Rzuca Error z czytelnym komunikatem dla złych punktów kontrolnych. */
  function parse(text) {
    const o = JSON.parse(text);
    if (!o || typeof o !== 'object' || Array.isArray(o)) throw new Error('Plik toru musi być obiektem JSON.');
    if (!Array.isArray(o.control_points) || !o.control_points.every((p) => Array.isArray(p) && p.length === 2 && isFinite(p[0]) && isFinite(p[1])))
      throw new Error('control_points musi być listą par [x, y].');
    const extra = {};
    for (const k of Object.keys(o)) if (!BASE_KEYS.includes(k)) extra[k] = o[k];
    return {
      layoutId: o.layout_id || 'new_track',
      points: o.control_points.map((p) => [Number(p[0]), Number(p[1])]),
      source: o.source || '',
      notes: o.notes || '',
      extra,
    };
  }

  /** Model edytora -> tekst JSON w stałym układzie (ten sam, co pliki w repo i fixture). */
  function format(m) {
    const lines = ['{', `  "layout_id": ${JSON.stringify(m.layoutId)},`, '  "control_points": ['];
    m.points.forEach((p, i) => lines.push(`    [${fmtPoint(p[0])}, ${fmtPoint(p[1])}]${i < m.points.length - 1 ? ',' : ''}`));
    lines.push('  ],');
    const tail = [`  "source": ${JSON.stringify(m.source)}`, `  "notes": ${JSON.stringify(m.notes)}`];
    const extra = m.extra || {};
    const keys = KNOWN_ORDER.filter((k) => k in extra).concat(Object.keys(extra).filter((k) => !KNOWN_ORDER.includes(k)));
    for (const k of keys) tail.push(`  ${JSON.stringify(k)}: ${block(extra[k], 2)}`);
    lines.push(tail.join(',\n'), '}');
    return lines.join('\n') + '\n';
  }

  /** Czy plik ma którekolwiek pole v2 (opisane ułamkiem okrążenia albo osobną ścieżką). */
  function hasV2(extra) {
    return !!extra && ['width_m', 'edges', 'pit_lane', 'elevation_m'].some((k) => k in extra);
  }

  /** Interpolacja liniowa po pętli, jak TrackDetail.Interpolate w C#. */
  function profileAt(points, key, s) {
    if (!Array.isArray(points) || !points.length) return null;
    s -= Math.floor(s);
    if (points.length === 1) return points[0][key];
    for (let i = 0; i < points.length - 1; i++) {
      const a = points[i], b = points[i + 1];
      if (s >= a.s && s < b.s) return a[key] + (b[key] - a[key]) * (s - a.s) / (b.s - a.s);
    }
    const last = points[points.length - 1], first = points[0];
    const span = first.s + 1 - last.s, along = s >= last.s ? s - last.s : s + 1 - last.s;
    return last[key] + (first[key] - last[key]) * along / span;
  }
  /** Szerokość w metrach w danym ułamku okrążenia; null, gdy plik jej nie opisuje (wtedy działa wartość epoki). */
  function widthAt(extra, s) { return profileAt(extra && extra.width_m, 'w', s); }

  /** Odcinek pobocza dla strony w danym ułamku okrążenia; null poza opisanymi odcinkami (wartość epoki). */
  function edgeAt(extra, s, side) {
    const edges = extra && extra.edges;
    if (!Array.isArray(edges)) return null;
    s -= Math.floor(s);
    for (const e of edges) {
      if (e.side !== side) continue;
      const inside = e.from <= e.to ? s >= e.from && s < e.to : s >= e.from || s < e.to;
      if (inside) return e;
    }
    return null;
  }

  /** Długość alei, odcinka z limitem i czas przejazdu odcinka z limitem (s). */
  function pitSummary(extra) {
    const pit = extra && extra.pit_lane;
    if (!pit || !Array.isArray(pit.points) || pit.points.length < 2) return null;
    let len = 0;
    for (let i = 1; i < pit.points.length; i++) {
      const a = pit.points[i - 1], b = pit.points[i];
      len += Math.sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
    }
    const limitLen = pit.limit_to - pit.limit_from;
    return { length: len, limitLength: limitLen, limitKph: pit.limit_kph, timeAtLimit: limitLen / (pit.limit_kph / 3.6), boxes: (pit.boxes || []).length };
  }

  /** Punkt na alei w odległości d (m) od jej początku. */
  function pitPointAt(pit, d) {
    let acc = 0;
    for (let i = 1; i < pit.points.length; i++) {
      const a = pit.points[i - 1], b = pit.points[i];
      const seg = Math.sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
      if (acc + seg >= d || i === pit.points.length - 1) {
        const f = seg > 0 ? Math.min(1, Math.max(0, (d - acc) / seg)) : 0;
        return [a[0] + f * (b[0] - a[0]), a[1] + f * (b[1] - a[1])];
      }
      acc += seg;
    }
    return pit.points[0].slice();
  }

  /** Skalowanie wraz z punktami kontrolnymi: aleja to metry w tym samym układzie, więc skaluje się razem z nimi. */
  function scaleExtra(extra, f) {
    const pit = extra && extra.pit_lane;
    if (!pit) return extra;
    const out = Object.assign({}, extra);
    const p = Object.assign({}, pit);
    if (Array.isArray(p.points)) p.points = p.points.map((q) => [q[0] * f, q[1] * f]);
    if (typeof p.limit_from === 'number') p.limit_from *= f;
    if (typeof p.limit_to === 'number') p.limit_to *= f;
    if (Array.isArray(p.boxes)) p.boxes = p.boxes.map((b) => b * f);
    out.pit_lane = p;
    return out;
  }

  root.TrackFile = { parse, format, hasV2, widthAt, edgeAt, pitSummary, pitPointAt, scaleExtra };
})(typeof window !== 'undefined' ? window : globalThis);
