/* Readable text on any team colour. A presentation rule (WCAG relative luminance and contrast ratio), nothing the game computes:
   a team theme paints buttons, badges and bands in its own colours, and the text on them has to stay legible whatever the
   colours are. */

const LIGHT = '#ffffff';
const DARK = '#0a0705';

/** "#rgb" or "#rrggbb" to [r, g, b] in 0..255, or null when the text is not a hex colour. */
export function parseHex(hex) {
  const text = String(hex ?? '').trim().replace(/^#/, '');
  const full = text.length === 3 ? text.split('').map((digit) => digit + digit).join('') : text;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) return null;
  return [0, 2, 4].map((at) => parseInt(full.slice(at, at + 2), 16));
}

/** WCAG relative luminance, 0 (black) to 1 (white). An unreadable colour counts as mid grey. */
export function luminance(hex) {
  const rgb = parseHex(hex) ?? [128, 128, 128];
  const [r, g, b] = rgb.map((value) => {
    const channel = value / 255;
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** WCAG contrast ratio of two colours, 1 to 21. */
export function contrast(a, b) {
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (high + 0.05) / (low + 0.05);
}

/** The text colour that reads best on a background: the preferred one when it is readable enough, else white or near-black. */
export function readableOn(background, preferred = null, minimum = 4.5) {
  if (preferred && contrast(background, preferred) >= minimum) return preferred;
  return contrast(background, LIGHT) >= contrast(background, DARK) ? LIGHT : DARK;
}
