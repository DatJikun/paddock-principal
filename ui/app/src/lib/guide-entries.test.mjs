// Where the guide can be opened from (#268): the main menu, the side navigation of a career and the in-game menu (Esc).
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { NAV, navOwner, parseRoute, screenKey } from './shell-nav.mjs';

const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
const en = JSON.parse(readFileSync(new URL('../../../../strings/en.json', import.meta.url), 'utf8'));
const source = name => readFileSync(new URL(name, import.meta.url), 'utf8');

test('the guide is a screen of the career with its own entry, label and icon next to the others', () => {
  assert.deepEqual(parseRoute('#/poradnik'), { name: 'poradnik', args: [] });
  const entry = NAV.find(item => item.id === 'poradnik');
  assert.ok(entry, 'the side navigation has the guide');
  assert.equal(entry.key, 'shell.nav.guide');
  assert.match(entry.icon ?? '', /^<path /);
  assert.equal(navOwner('poradnik'), 'poradnik');
  assert.equal(screenKey('poradnik'), 'shell.nav.guide');
  const icons = NAV.filter(item => item.id).map(item => item.icon);
  assert.equal(new Set(icons).size, icons.length, 'no two entries share an icon');
});

test('every place that opens the guide has a label in both languages', () => {
  for (const key of ['menu.guide', 'shell.nav.guide', 'game.menu.guide', 'guide.intro', 'guide.contents', 'guide.prev', 'guide.next', 'guide.polishOnly']) {
    assert.ok(pl[key], `pl: ${key}`);
    assert.ok(en[key], `en: ${key}`);
  }
});

test('the in-game menu and the main menu both lead to the guide', () => {
  const game = source('./components/GameMenu.svelte');
  assert.match(game, /id: 'guide', key: 'game\.menu\.guide'/);
  assert.match(game, /onGuide\(\)/);
  assert.match(source('./components/MenuHome.svelte'), /onOpen\('guide'\)/);
  const app = source('../App.svelte');
  assert.match(app, /onGuide=\{openGuide\}/);
  assert.match(app, /location\.hash = '#\/poradnik'/);
  assert.match(app, /route\.name === 'poradnik'/);
});
