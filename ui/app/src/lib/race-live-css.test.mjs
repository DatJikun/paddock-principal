import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

/* The race screen's styles are scoped, but app.css and race.css are global. A global rule on a bare class that the race
   screen also uses lays its box out from outside: `.ov{min-width:640px}` from the season grid once stretched every race
   panel to 640px and pushed the strategy and radio panels off the window. */

const raceLive = readFileSync(new URL('./components/RaceLive.svelte', import.meta.url), 'utf8');
const globals = ['app.css', 'race.css'].map((name) => [name, readFileSync(new URL(`../styles/${name}`, import.meta.url), 'utf8')]);

const LAYOUT = /(^|;)\s*(width|min-width|max-width|height|min-height|max-height|position|inset|top|left|right|bottom)\s*:/;

function classesUsed(source) {
  const markup = source.slice(0, source.indexOf('<style>'));
  const used = new Set();
  for (const match of markup.matchAll(/class="([^"]*)"/g)) {
    for (const token of match[1].replace(/\{[^}]*\}/g, ' ').split(/\s+/)) if (token) used.add(token);
  }
  for (const match of markup.matchAll(/class:([\w-]+)/g)) used.add(match[1]);
  return used;
}

test('no global rule on a bare class sizes or places a race screen box', () => {
  const used = classesUsed(raceLive);
  const clashes = [];
  for (const [name, text] of globals) {
    const css = text.replace(/\/\*[\s\S]*?\*\//g, '');
    for (const rule of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
      for (const selector of rule[1].split(',')) {
        const bare = selector.trim().match(/^\.([\w-]+)$/);
        if (bare && used.has(bare[1]) && LAYOUT.test(rule[2])) clashes.push(`${name}: ${selector.trim()}`);
      }
    }
  }
  assert.deepEqual(clashes, []);
});
