/* =========================================================================
 * PADDOCK PRINCIPAL · TRACK SHAPE RESOLVER
 * -------------------------------------------------------------------------
 * One place that turns a track entry into something drawable. Display only:
 * the shape of a lap is data (data/authored/tracks/geometry/<layout_id>.json,
 * built into js/track-geometry.generated.js), never logic of the UI (TECH §3).
 *
 * Order of preference for a track entry { layout, len, map?, corners? }:
 *   1. 'geometry'  TRACK_GEOMETRY[layout]: the authored control points (metres).
 *   2. 'legacy'    the hand-made `map` points still in data.js. Prototype-only
 *                  escape hatch for 1976 layouts that have no geometry file yet;
 *                  delete an entry's `map` when its file lands.
 *   3. 'fallback'  a neutral stadium at the layout's length. Same rule as the
 *                  backend (TrackGeometry.Fallback): a missing geometry costs
 *                  the map its shape, it never throws.
 * All three end up as control points in screen space (y down, longest side
 * SIZE units, so strokes and the mock sim's curvature numbers keep their scale)
 * and go through TrackSpline, the renderer-side twin of TrackGeometry.
 * ========================================================================= */

(() => {
  'use strict';

  const SIZE = 80, PAD = 10;
  const g = typeof window !== 'undefined' ? window : globalThis;

  /* metres, y north -> screen units, y down, north stays up on the screen */
  function normalise(points) {
    const xs = points.map(p => p[0]), ys = points.map(p => p[1]);
    const minX = Math.min(...xs), maxY = Math.max(...ys);
    const k = SIZE / (Math.max(Math.max(...xs) - minX, maxY - Math.min(...ys)) || 1);
    return points.map(p => [(p[0] - minX) * k + PAD, (maxY - p[1]) * k + PAD]);
  }

  /* counter-clockwise stadium: two straights of 3 r and two semicircles of radius r, 2 pi r + 6 r = lap.
     Same construction as TrackGeometry.Fallback (literal sin/cos so it is identical everywhere). */
  const ARC = [[0.3826834323650898, -0.9238795325112867], [0.7071067811865476, -0.7071067811865476], [0.9238795325112867, -0.3826834323650898],
    [1, 0], [0.9238795325112867, 0.3826834323650898], [0.7071067811865476, 0.7071067811865476], [0.3826834323650898, 0.9238795325112867]];
  function fallbackPoints(lengthKm) {
    const r = lengthKm * 1000 / (6 + 2 * Math.PI);
    const unit = [[-1.5, -1], [0, -1], [1.5, -1], ...ARC.map(([c, s]) => [1.5 + c, s]), [1.5, 1], [0, 1], [-1.5, 1], ...[...ARC].reverse().map(([c, s]) => [-1.5 - c, s])];
    return unit.map(([x, y]) => [x * r, y * r]);
  }

  const cache = new WeakMap();

  const TrackShape = {
    SIZE,
    normalise,
    fallbackPoints,

    /* track: a DB.tracks entry. Returns { source, points, spline, corners: [[lapFraction, name]], lengthKm, layout } */
    resolve(track) {
      if (cache.has(track)) return cache.get(track);
      const geom = track.layout && g.TRACK_GEOMETRY ? g.TRACK_GEOMETRY[track.layout] : null;
      let source, points, lengthKm = track.len, namedAt = [];
      if (geom && Array.isArray(geom.points) && geom.points.length >= 3) {
        source = 'geometry'; points = normalise(geom.points); lengthKm = geom.lengthKm || track.len; namedAt = geom.corners || [];
      } else if (track.map && track.map.length >= 3) {
        source = 'legacy'; points = track.map;
      } else {
        source = 'fallback'; points = normalise(fallbackPoints(track.len));
      }
      const spline = new g.TrackSpline(points, lengthKm);
      const corners = source === 'geometry'
        ? namedAt.filter(([i]) => i >= 0 && i < points.length).map(([i, name]) => [spline.knotFraction(i), name]).sort((a, b) => a[0] - b[0])
        : (track.corners || []);
      const out = { source, points, spline, corners, lengthKm, layout: track.layout || null };
      cache.set(track, out);
      return out;
    },
  };

  g.TrackShape = TrackShape;
  if (typeof module !== 'undefined') module.exports = { TrackShape };
})();
