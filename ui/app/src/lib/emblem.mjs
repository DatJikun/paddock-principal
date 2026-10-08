/* A simple emblem for a team: initials in one of a few shapes. Presentation only. The shape comes from the id, the letters from the
   name, so two teams that share a colour still read apart, and the same team always looks the same. */

export const SHAPES = ['circle', 'shield', 'diamond', 'hex', 'square', 'roundel'];

const FILLER = new Set(['scuderia', 'team', 'equipe', 'ecurie', 'racing', 'cars', 'car', 'company', 'engineering', 'automobiles', 'officine', 'f1', 'works', 'motor', 'motors', 'the', 'and', 'ltd']);

function hash(text) {
  let value = 2166136261;
  for (const char of String(text ?? '')) value = Math.imul(value ^ char.charCodeAt(0), 16777619) >>> 0;
  return value;
}

/** Up to two letters: the first letters of the distinctive words, or the first two letters of a single word. */
export function emblemLetters(name) {
  const words = String(name ?? '')
    .split(/[\s\-_/]+/)
    .map((word) => word.replace(/[^\p{L}\p{N}]/gu, ''))
    .filter(Boolean);
  const keep = words.filter((word) => !FILLER.has(word.toLowerCase()));
  const use = keep.length > 0 ? keep : words;
  if (use.length === 0) return '?';
  if (use.length === 1) return use[0].slice(0, 2).toUpperCase();
  return (use[0][0] + use[1][0]).toUpperCase();
}

export function emblemShape(id) {
  return SHAPES[hash(id) % SHAPES.length];
}

export function emblemOf(id, name) {
  return { shape: emblemShape(id), letters: emblemLetters(name) };
}
