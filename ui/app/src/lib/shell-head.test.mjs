import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { headName } from './shell-head.mjs';

const app = readFileSync(new URL('../App.svelte', import.meta.url), 'utf8');
const headStart = app.indexOf('<header class="top">');
const header = app.slice(headStart, app.indexOf('</header>', headStart));
const settingsStart = app.indexOf("route.name === 'ustawienia'");
const settings = app.slice(settingsStart, app.indexOf('{:else}', settingsStart));

test('the player name is the manager profile name, or empty without a principal', () => {
  assert.equal(headName({ found: true, name: 'Enzo Test' }), 'Enzo Test');
  assert.equal(headName({ found: true, name: '  Enzo Test ' }), 'Enzo Test');
  assert.equal(headName({ found: false, name: '' }), '');
  assert.equal(headName(null), '');
});

test('the header shows the team and the player name, with no role label', () => {
  assert.match(header, /<b>\{shell\?\.organizationName \?\? '—'\}<\/b><small>\{managerName \|\| '—'\}<\/small>/);
  assert.doesNotMatch(header, /shell\.role/);
});

test('the round badge shows the initials of the player, not the team', () => {
  assert.match(header, /<span class="av">\{managerName \? initials\(managerName\) : '—'\}<\/span>/);
  assert.doesNotMatch(header, /initials\(shell\?\.organizationName/);
});

test('the three-line menu button is not in the header; the settings screen opens the game menu', () => {
  assert.doesNotMatch(header, /menu-btn/);
  assert.doesNotMatch(header, /openGameMenu/);
  assert.match(settings, /onclick=\{openGameMenu\}/);
  assert.match(settings, /t\('game\.menu\.open'\)/);
});
