import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import test from 'node:test';
import { driverHref, formatAge, profileHref, subjectKind } from './person.mjs';

const root = new URL('..', import.meta.url);

function sources(folder) {
  return readdirSync(new URL(`${folder}/`, root))
    .filter((file) => file.endsWith('.svelte'))
    .map((file) => ({ file: `${folder}/${file}`, text: readFileSync(new URL(`${folder}/${file}`, root), 'utf8') }));
}

const all = [...sources('screens'), ...sources('lib/components'), { file: 'App.svelte', text: readFileSync(new URL('App.svelte', root), 'utf8') }];
const source = (file) => all.find((item) => item.file === file).text;

test('a driver and a pool junior open the driver profile, everyone else the staff profile', () => {
  assert.equal(driverHref('gen:7'), '#/kierowca/gen%3A7');
  assert.equal(driverHref('talent-12'), '#/kierowca/talent-12');
  assert.equal(profileHref('driver', 'fangio'), '#/kierowca/fangio');
  assert.equal(profileHref('RaceEngineer', 'gen:3'), '#/osoba/gen%3A3');
  assert.equal(profileHref('staff', 'x y'), '#/osoba/x%20y');
});

test('a negotiation subject names the kind of profile', () => {
  assert.equal(subjectKind('DriverSeat'), 'driver');
  assert.equal(subjectKind('StaffRole'), 'staff');
});

test('an age is the number the bridge sent, or a dash', () => {
  assert.equal(formatAge(17), '17');
  assert.equal(formatAge(0), '0');
  assert.equal(formatAge(undefined), '—');
  assert.equal(formatAge(null), '—');
});

test('every person cell of every screen is a link to the shared profile', () => {
  const missing = [];
  for (const { file, text } of all) {
    for (const match of text.matchAll(/<PersonCell\b[^>]*>/g)) {
      if (!/\bhref=/.test(match[0])) missing.push(`${file}: ${match[0]}`);
    }
  }
  assert.deepEqual(missing, []);
});

test('the academy opens the shared profile from its table, its tiles and its detail', () => {
  const academy = source('screens/Akademia.svelte');
  assert.equal(academy.match(/<PersonCell\b/g).length, 2);
  assert.equal(academy.match(/href=\{driverHref\(/g).length, 3);
});

test('the lists of results and standings link a driver to the shared profile', () => {
  for (const file of ['screens/Wyscig.svelte', 'screens/Klasyfikacje.svelte', 'lib/components/SeasonGrid.svelte']) {
    assert.match(source(file), /<PersonName\b/, file);
    assert.doesNotMatch(source(file), /<span class="person">/, file);
  }
});

test('no screen writes a nationality, an age or a contract end with its own code', () => {
  const own = [];
  for (const { file, text } of all) {
    if (file.endsWith('Nationality.svelte') || file.endsWith('ContractEnd.svelte') || file.endsWith('SponsorExtras.svelte')) continue;
    if (/countryName\(tr, [\w.?]*nationality\)/.test(text)) own.push(`${file}: nationality`);
    if (/\{[\w.?]*\.age\}/.test(text) || /'team\.card\.age'/.test(text)) own.push(`${file}: age`);
    if (/formatDate\([\w.?]*(contractEnd|contract\.end)\b/.test(text)) own.push(`${file}: contract end`);
  }
  assert.deepEqual(own, []);
});
