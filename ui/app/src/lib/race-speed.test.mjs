import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { canStep, pauseOrder, speedOrder, stepOfKey, stepSpeed } from './race-speed.mjs';

/* The watching speeds the server offers (LiveRacePlayback.Speeds, #322). */
const SPEEDS = [1, 2, 5, 10, 20, 30];

const pl = JSON.parse(readFileSync(new URL('../../../../strings/pl.json', import.meta.url), 'utf8'));
const en = JSON.parse(readFileSync(new URL('../../../../strings/en.json', import.meta.url), 'utf8'));

test('the arrows walk the speeds one step at a time and stop at both ends', () => {
  const walked = [1];
  for (let i = 0; i < SPEEDS.length; i++) walked.push(stepSpeed(SPEEDS, walked[walked.length - 1], 1));
  assert.deepEqual(walked, [1, 2, 5, 10, 20, 30, 30]);
  assert.equal(stepSpeed(SPEEDS, 1, -1), 1);
  assert.equal(stepSpeed(SPEEDS, 5, -1), 2);
  assert.equal(canStep(SPEEDS, 30, 1), false);
  assert.equal(canStep(SPEEDS, 1, -1), false);
  assert.equal(canStep(SPEEDS, 1, 1), true);
});

test('the race opens at x1, so a left arrow has nowhere to go and a right arrow goes to x2', () => {
  const opened = { speed: 1, speeds: SPEEDS, paused: true, finished: false };
  assert.equal(speedOrder(opened, -1), null);
  assert.deepEqual(speedOrder(opened, 1), { action: 'setSpeed', speed: 2 });
});

test('pausing keeps the speed: the arrows still step from x20 after a pause, and resuming plays at x20', () => {
  const playing = { speed: 20, speeds: SPEEDS, paused: false, finished: false };
  assert.deepEqual(pauseOrder(playing), { action: 'pause' });
  /* The host answers the pause with the same speed and paused set: the screen shows x20 with the pause on. */
  const paused = { ...playing, paused: true };
  assert.equal(paused.speed, 20);
  assert.deepEqual(speedOrder(paused, 1), { action: 'setSpeed', speed: 30 });
  assert.deepEqual(speedOrder(paused, -1), { action: 'setSpeed', speed: 10 });
  assert.deepEqual(pauseOrder(paused), { action: 'play' });
});

test('a finished race sends no order from the pause button or the arrows', () => {
  const finished = { speed: 10, speeds: SPEEDS, paused: true, finished: true };
  assert.equal(pauseOrder(finished), null);
  assert.equal(speedOrder(finished, 1), null);
  assert.equal(pauseOrder(null), null);
});

test('only the arrow keys move the speed, and space is a pause order, not a step', () => {
  assert.equal(stepOfKey('ArrowRight'), 1);
  assert.equal(stepOfKey('ArrowLeft'), -1);
  assert.equal(stepOfKey(' '), 0);
  assert.equal(stepOfKey('ArrowUp'), 0);
});

test('a speed the list does not offer falls back to the slowest step', () => {
  assert.equal(stepSpeed(SPEEDS, 7, 1), 1);
  assert.equal(canStep(SPEEDS, 7, 1), false);
});

test('the speed control strings exist in Polish and English', () => {
  for (const key of ['live.ui.slower', 'live.ui.faster', 'live.ui.pause', 'live.ui.play', 'live.ui.pace']) {
    assert.ok(key in pl, key);
    assert.ok(key in en, key);
  }
});
