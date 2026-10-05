/* Team colours for the cards and the summary. A presentation table only: it decides how a team is drawn, not anything the
   game computes. Colours follow the liveries the team is known for in its first decades (national racing colours for the
   rest); a team that is not listed is drawn in neutral ink. Keys are the authored constructor ids. */

const RED = ['#c4161c', '#f5c518', '#ffffff'];
const GREEN = ['#0e4d2f', '#d9b13b', '#ffffff'];
const BLUE = ['#1d3f8f', '#f1f1ec', '#ffffff'];
const SILVER = ['#aeb4b9', '#1c1c1c', '#101010'];
const BLACK = ['#16130e', '#c9a24a', '#f0dba0'];

const TABLE = {
  ferrari: RED,
  alfa: ['#8b1a1a', '#e8e0cf', '#ffffff'],
  maserati: ['#9a1b22', '#2a4b8d', '#ffffff'],
  osca: RED,
  lancia: RED,
  mercedes: SILVER,
  porsche: SILVER,
  emw: SILVER,
  afm: SILVER,
  bmw: ['#e8e8ea', '#1c69d4', '#101010'],
  cooper: GREEN,
  'cooper-climax': GREEN,
  'cooper-maserati': GREEN,
  'cooper-ford': GREEN,
  connaught: GREEN,
  vanwall: GREEN,
  hwm: GREEN,
  brm: GREEN,
  lister: GREEN,
  team_lotus: ['#1f5a3a', '#f4d03f', '#ffffff'],
  'lotus-climax': ['#1f5a3a', '#f4d03f', '#ffffff'],
  'lotus-ford': ['#1f5a3a', '#f4d03f', '#ffffff'],
  brabham: ['#0e4d2f', '#d9b13b', '#ffffff'],
  'brabham-climax': ['#0e4d2f', '#d9b13b', '#ffffff'],
  'brabham-repco': ['#0e4d2f', '#d9b13b', '#ffffff'],
  'brabham-ford': ['#0e4d2f', '#d9b13b', '#ffffff'],
  lotus: BLACK,
  'lotus-pw': BLACK,
  gordini: BLUE,
  lago: BLUE,
  simca: BLUE,
  matra: BLUE,
  'matra-ford': BLUE,
  ligier: BLUE,
  tyrrell: ['#1f4f9a', '#e03a3e', '#ffffff'],
  williams: ['#0b2a6f', '#f5f5f5', '#ffffff'],
  mclaren: ['#f26a1b', '#1d1d1d', '#101010'],
  'mclaren-ford': ['#f26a1b', '#1d1d1d', '#101010'],
  march: ['#e9e4d6', '#c9242f', '#101010'],
  surtees: ['#c4161c', '#ffffff', '#ffffff'],
  shadow: BLACK,
  hesketh: ['#f2f0e8', '#1f3b86', '#101010'],
  renault: ['#f4d03f', '#16130e', '#16130e'],
  benetton: ['#00a651', '#ffd100', '#ffffff'],
  toleman: ['#1f3b86', '#ffffff', '#ffffff'],
  arrows: ['#f4f1e8', '#ee7c1b', '#101010'],
  wolf: ['#1d2b64', '#d9b13b', '#ffffff'],
  jordan: ['#f6d900', '#1d2b64', '#16130e'],
  sauber: ['#1d3f8f', '#cfd4d8', '#ffffff'],
  minardi: ['#16130e', '#f4d03f', '#f4d03f'],
  honda: ['#f2f0e8', '#c4161c', '#101010'],
  eagle: ['#16365f', '#f2f0e8', '#ffffff'],
  'eagle-climax': ['#16365f', '#f2f0e8', '#ffffff'],
  'eagle-weslake': ['#16365f', '#f2f0e8', '#ffffff'],
  red_bull: ['#1f2a5a', '#d6283c', '#ffffff'],
  brawn: ['#f2f0e8', '#d8f24a', '#101010'],
  haas: ['#f2f0e8', '#c4161c', '#101010'],
  alpine: ['#1f6fd1', '#e91e8c', '#ffffff'],
  audi: ['#16130e', '#d6283c', '#ffffff'],
};

const NEUTRAL = ['#3b3027', '#b9a98f', '#ffffff'];

/** { main, accent, on } for a constructor id; neutral ink when the team has no livery here. */
export function livery(id) {
  const [main, accent, on] = TABLE[id] ?? NEUTRAL;
  return { main, accent, on, known: id in TABLE };
}
