import assert from 'node:assert/strict';
import test from 'node:test';
import { NAV, navOwner, parseRoute, screenId, screenKey } from './shell-nav.mjs';

test('a race page is its own route and lights the calendar', () => {
  assert.deepEqual(parseRoute('#/wyscig/3'), { name: 'wyscig', args: ['3'] });
  assert.equal(navOwner('wyscig'), 'kalendarz');
  assert.equal(screenKey('wyscig'), 'shell.nav.calendar');
});

test('an inbox item travels in the route', () => {
  assert.deepEqual(parseRoute('#/skrzynka/inbox%3A12'), { name: 'skrzynka', args: ['inbox:12'] });
  assert.equal(screenId('#/skrzynka/inbox%3A12'), 'skrzynka');
});

test('unknown and removed screens fall back to the dashboard', () => {
  assert.deepEqual(parseRoute(''), { name: 'pulpit', args: [] });
  assert.deepEqual(parseRoute('#/kronika'), { name: 'pulpit', args: [] });
  assert.ok(!NAV.some((item) => item.id === 'kronika'));
});
