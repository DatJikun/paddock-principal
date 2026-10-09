import assert from 'node:assert/strict';
import test from 'node:test';
import { headlineHref } from './news.mjs';
import { parseRoute } from './shell-nav.mjs';

test('every kind of link opens a screen that exists', () => {
  const links = [
    [{ kind: 'race', id: '3' }, 'wyscig'],
    [{ kind: 'driver', id: 'gen:12' }, 'kierowca'],
    [{ kind: 'person', id: 'gen:40' }, 'osoba'],
    [{ kind: 'standings', id: '' }, 'klasyfikacje'],
    [{ kind: 'inbox', id: 'inb:7' }, 'skrzynka'],
    [{ kind: 'inbox', id: '' }, 'skrzynka'],
  ];
  for (const [link, screen] of links) assert.equal(parseRoute(headlineHref(link)).name, screen);
});

test('the id travels in the address, and an unnamed place falls back to the standings', () => {
  assert.equal(headlineHref({ kind: 'race', id: '3' }), '#/wyscig/3');
  assert.equal(headlineHref({ kind: 'driver', id: 'real:fangio' }), '#/kierowca/real%3Afangio');
  assert.equal(headlineHref({ kind: 'inbox', id: '' }), '#/skrzynka');
  assert.equal(headlineHref(null), '#/klasyfikacje');
});

test('the address of a driver gives back the id the host sent', () => {
  const route = parseRoute(headlineHref({ kind: 'driver', id: 'real:fangio' }));
  assert.deepEqual(route.args, ['real:fangio']);
});
