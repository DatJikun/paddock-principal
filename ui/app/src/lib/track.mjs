/*
 * A layout's silhouette for a tile or a page. Display only (TECH §3): the points are the authored centre line the
 * bridge sends, in metres with y north. They are scaled so the longest side is SIZE units, flipped so north stays up,
 * and joined with a closed centripetal Catmull-Rom curve, the same family of curve the prototype's race map draws.
 */

const SIZE = 80;
const PAD = 4;

function scaled(points) {
  const xs = points.map((p) => p.x);
  const ys = points.map((p) => p.y);
  const minX = Math.min(...xs);
  const maxY = Math.max(...ys);
  const span = Math.max(Math.max(...xs) - minX, maxY - Math.min(...ys)) || 1;
  const k = SIZE / span;
  return points.map((p) => [(p.x - minX) * k, (maxY - p.y) * k]);
}

function distinct(points) {
  const out = [];
  for (const point of points) {
    const last = out[out.length - 1];
    if (!last || Math.hypot(point[0] - last[0], point[1] - last[1]) > 1e-6) out.push(point);
  }
  if (out.length > 1) {
    const first = out[0];
    const last = out[out.length - 1];
    if (Math.hypot(first[0] - last[0], first[1] - last[1]) <= 1e-6) out.pop();
  }
  return out;
}

const f = (n) => (Math.round(n * 100) / 100).toString();

/** Bezier controls of a centripetal Catmull-Rom segment p1 -> p2 (alpha 0.5). */
function controls(p0, p1, p2, p3) {
  const d1 = Math.max(Math.hypot(p1[0] - p0[0], p1[1] - p0[1]) ** 0.5, 1e-4);
  const d2 = Math.max(Math.hypot(p2[0] - p1[0], p2[1] - p1[1]) ** 0.5, 1e-4);
  const d3 = Math.max(Math.hypot(p3[0] - p2[0], p3[1] - p2[1]) ** 0.5, 1e-4);
  const c1 = [0, 1].map((i) => {
    const num = d1 * d1 * p2[i] - d2 * d2 * p0[i] + (2 * d1 * d1 + 3 * d1 * d2 + d2 * d2) * p1[i];
    return num / (3 * d1 * (d1 + d2));
  });
  const c2 = [0, 1].map((i) => {
    const num = d3 * d3 * p1[i] - d2 * d2 * p3[i] + (2 * d3 * d3 + 3 * d3 * d2 + d2 * d2) * p2[i];
    return num / (3 * d3 * (d3 + d2));
  });
  return [c1, c2];
}

/**
 * Points in metres -> { viewBox, d, tick } for an SVG, or null when there is no shape to draw.
 * `tick` is a short mark across the start line (point 0).
 */
export function trackOutline(points) {
  if (!Array.isArray(points) || points.length < 3) return null;
  const pts = distinct(scaled(points));
  if (pts.length < 3) return null;
  const n = pts.length;
  let d = `M${f(pts[0][0])},${f(pts[0][1])}`;
  for (let i = 0; i < n; i++) {
    const p0 = pts[(i - 1 + n) % n];
    const p1 = pts[i];
    const p2 = pts[(i + 1) % n];
    const p3 = pts[(i + 2) % n];
    const [c1, c2] = controls(p0, p1, p2, p3);
    d += `C${f(c1[0])},${f(c1[1])} ${f(c2[0])},${f(c2[1])} ${f(p2[0])},${f(p2[1])}`;
  }
  d += 'Z';
  const xs = pts.map((p) => p[0]);
  const ys = pts.map((p) => p[1]);
  const x0 = Math.min(...xs) - PAD;
  const y0 = Math.min(...ys) - PAD;
  const w = Math.max(...xs) - Math.min(...xs) + PAD * 2;
  const h = Math.max(...ys) - Math.min(...ys) + PAD * 2;
  const [sx, sy] = pts[0];
  const [nx, ny] = pts[1];
  const len = Math.hypot(nx - sx, ny - sy) || 1;
  const ux = -(ny - sy) / len;
  const uy = (nx - sx) / len;
  const tick = `M${f(sx - ux * 3)},${f(sy - uy * 3)}L${f(sx + ux * 3)},${f(sy + uy * 3)}`;
  return { viewBox: `${f(x0)} ${f(y0)} ${f(w)} ${f(h)}`, d, tick };
}
