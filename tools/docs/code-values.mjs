// Reads tunable numbers straight from the C# sources, so the docs never drift from the game.
// A name that is missing is an error: the docs build fails and says which file and name.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';

export class CodeValues {
  constructor(root) {
    this.root = root;
    this.files = new Map();
  }

  lines(path) {
    if (!this.files.has(path)) {
      let text;
      try {
        text = readFileSync(join(this.root, path), 'utf8');
      } catch {
        throw new Error(`docs: source file not found: ${path}`);
      }
      this.files.set(path, text.replace(/\r\n?/g, '\n').split('\n'));
    }
    return this.files.get(path);
  }

  /* Index of the declaration line of a constant or static field, or -1. */
  find(path, name) {
    const decl = new RegExp(`\\b${name}\\s*=(?!=|>)`);
    return this.lines(path).findIndex(l => /\b(const|static readonly)\b/.test(l) && decl.test(l));
  }

  /* Declaration text from the line that names it until the closing ";" (for multi-line arrays). */
  declaration(path, name) {
    const lines = this.lines(path);
    const at = this.find(path, name);
    if (at < 0) throw new Error(`docs: ${name} not found in ${path}`);
    let text = '';
    for (let i = at; i < lines.length; i++) {
      text += lines[i].replace(/\/\/.*$/, '') + '\n';
      if (/;\s*$/.test(lines[i].replace(/\/\/.*$/, ''))) break;
    }
    return { text, line: at + 1 };
  }

  /* A scalar constant: { value, line, raw }. Follows a reference to another constant ("Other.Name"). */
  scalar(path, name, depth = 0) {
    const { text, line } = this.declaration(path, name);
    const m = text.match(new RegExp(`\\b${name}\\s*=\\s*([^,;\\n]+)`));
    const raw = m[1].trim();
    const num = parseNumber(raw);
    if (num !== null) return { value: num, line, raw };
    const ref = raw.match(/^(?:[\w.]+\.)?(\w+)$/);
    if (ref && depth < 4) {
      const local = this.find(path, ref[1]);
      if (local >= 0) return { ...this.scalar(path, ref[1], depth + 1), line };
      for (const other of this.files.keys()) {
        if (this.find(other, ref[1]) >= 0) return { ...this.scalar(other, ref[1], depth + 1), line };
      }
    }
    if (/^(true|false)$/.test(raw)) return { value: raw === 'true', line, raw };
    if (/^QualityBand\.|^SeatStatus\./.test(raw)) return { value: raw.split('.').pop(), line, raw };
    throw new Error(`docs: ${name} in ${path} is not a number (${raw})`);
  }

  value(path, name) {
    return this.scalar(path, name).value;
  }

  /* (season, value) anchor arrays: [[1950, 150], ...]. */
  anchors(path, name) {
    const { text } = this.declaration(path, name);
    const out = [...text.matchAll(/\(\s*(\d{4})\s*,\s*([-\d._]+)[dDmMfF]?\s*\)/g)].map(m => [Number(m[1]), parseNumber(m[2])]);
    if (out.length === 0) throw new Error(`docs: ${name} in ${path} has no anchors`);
    return out;
  }

  /* ["key"] = value or ["key"] = [a, b] dictionaries. */
  dict(path, name) {
    const { text } = this.declaration(path, name);
    const out = {};
    for (const m of text.matchAll(/\["([^"]+)"\]\s*=\s*(\[[^\]]*\]|[-\d._]+[dDmMfF]?)/g)) {
      out[m[1]] = m[2].startsWith('[')
        ? m[2].slice(1, -1).split(',').map(v => parseNumber(v.trim()))
        : parseNumber(m[2]);
    }
    if (Object.keys(out).length === 0) throw new Error(`docs: ${name} in ${path} is empty`);
    return out;
  }

  /* new("key", 160) weight lists. */
  weights(path, name) {
    const { text } = this.declaration(path, name);
    return [...text.matchAll(/new\(\s*"([^"]+)"\s*,\s*(\d+)\s*\)/g)].map(m => [m[1], Number(m[2])]);
  }

  /* Calls of a factory, e.g. Dry("C5", 0, 0.0, 0.060, 16, 1): [{ id, args }]. Read from the whole file. */
  calls(path, fn) {
    const text = this.lines(path).join('\n');
    return [...text.matchAll(new RegExp(`\\b${fn}\\(\\s*"([^"]+)"\\s*,([^)]*)\\)`, 'g'))]
      .map(m => ({ id: m[1], args: m[2].split(',').map(v => parseNumber(v.trim())) }));
  }
}

export function parseNumber(raw) {
  const s = String(raw).trim().replace(/_/g, '').replace(/[dDmMfFL]$/, '').replace(/u$/i, '');
  if (!/^-?\d+(\.\d+)?(e-?\d+)?$/i.test(s)) return null;
  return Number(s);
}

/* Linear interpolation through anchors, flat outside (headcount). */
export function linear(anchors, x) {
  if (x <= anchors[0][0]) return anchors[0][1];
  for (let i = 1; i < anchors.length; i++) {
    const [x1, y1] = anchors[i];
    const [x0, y0] = anchors[i - 1];
    if (x <= x1) return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
  }
  return anchors[anchors.length - 1][1];
}

/* Smoothstep through anchors, flat outside (the race engine's EraCurve). */
export function smooth(anchors, x) {
  if (x <= anchors[0][0]) return anchors[0][1];
  for (let i = 1; i < anchors.length; i++) {
    const [x1, y1] = anchors[i];
    const [x0, y0] = anchors[i - 1];
    if (x <= x1) {
      const t = (x - x0) / (x1 - x0);
      return y0 + (y1 - y0) * t * t * (3 - 2 * t);
    }
  }
  return anchors[anchors.length - 1][1];
}
