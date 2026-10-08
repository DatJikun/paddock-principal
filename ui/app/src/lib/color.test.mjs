import assert from 'node:assert/strict';
import test from 'node:test';
import { contrast, luminance, parseHex, readableOn } from './color.mjs';
import { emblemLetters, emblemOf, SHAPES } from './emblem.mjs';
import { livery } from './livery.mjs';

test('hex colours parse in both lengths and reject anything else', () => {
  assert.deepEqual(parseHex('#fff'), [255, 255, 255]);
  assert.deepEqual(parseHex('#0e4d2f'), [14, 77, 47]);
  assert.equal(parseHex('red'), null);
});

test('contrast is the WCAG ratio', () => {
  assert.equal(Math.round(contrast('#000000', '#ffffff')), 21);
  assert.equal(contrast('#777777', '#777777'), 1);
  assert.ok(luminance('#ffffff') > luminance('#000000'));
});

test('readable text keeps the preferred colour only when it reads', () => {
  assert.equal(readableOn('#16130e', '#f0dba0'), '#f0dba0');
  assert.notEqual(readableOn('#16130e', '#101010'), '#101010');
  assert.equal(readableOn('#f5c518', '#ffffff'), '#0a0705');
  assert.equal(readableOn('#1c1c1c'), '#ffffff');
});

test('every team colour has readable text, for authored and generated liveries alike', () => {
  const ids = ['mercedes', 'ferrari', 'lotus', 'tyrrell', 'vanwall', 'cooper', 'hwm', 'connaught', 'arzani-volpini', 'osca', 'some-new-team', 'x'];
  for (const id of ids) {
    const colours = livery(id);
    assert.ok(contrast(colours.main, colours.on) >= 4, `${id} main`);
    assert.ok(contrast(colours.accent, colours.onAccent) >= 4, `${id} accent`);
  }
});

test('unlisted teams get colours of their own and the listed green teams differ', () => {
  assert.notEqual(livery('arzani-volpini').main, livery('some-new-team').main);
  assert.equal(livery('arzani-volpini').main, livery('arzani-volpini').main);
  const greens = ['cooper', 'vanwall', 'connaught', 'hwm'].map((id) => `${livery(id).main}/${livery(id).accent}`);
  assert.equal(new Set(greens).size, greens.length);
});

test('emblem letters skip filler words, and the shape is stable per id', () => {
  assert.equal(emblemLetters('Scuderia Ferrari'), 'FE');
  assert.equal(emblemLetters('Cooper Car Company'), 'CO');
  assert.equal(emblemLetters('Gordini'), 'GO');
  assert.equal(emblemLetters('Arzani-Volpini'), 'AV');
  assert.equal(emblemLetters(''), '?');
  assert.equal(emblemOf('vanwall', 'Vanwall').shape, emblemOf('vanwall', 'Vanwall').shape);
  assert.ok(SHAPES.includes(emblemOf('hwm', 'HWM').shape));
});
