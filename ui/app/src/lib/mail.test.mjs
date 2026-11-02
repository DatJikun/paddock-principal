import assert from 'node:assert/strict';
import test from 'node:test';
import { mailLead, mailParts, sentences } from './mail.mjs';

const part = 'Nowa część aerodynamiczna jest w aucie. Aerodynamika: 43–59 → 47–63. Czołówka: HWM 62–87; Connaught 37–61; Lancia 44–68. Jesteśmy lepsi od 1 z 3 rywali z czołówki.';

test('a mail is cut into a lead, labelled segments and paragraphs, in order', () => {
  assert.deepEqual(mailParts(part), {
    lead: 'Nowa część aerodynamiczna jest w aucie.',
    fields: [
      { label: 'Aerodynamika', value: '43–59 → 47–63', items: null },
      { label: 'Czołówka', value: 'HWM 62–87; Connaught 37–61; Lancia 44–68', items: ['HWM 62–87', 'Connaught 37–61', 'Lancia 44–68'] },
    ],
    paragraphs: ['Jesteśmy lepsi od 1 z 3 rywali z czołówki.'],
  });
  assert.equal(mailLead(part), 'Nowa część aerodynamiczna jest w aucie.');
});

test('no word is lost: the parts hold every character of the text but the spaces between sentences', () => {
  const { lead, fields, paragraphs } = mailParts(part);
  const kept = [lead, ...fields.map((f) => `${f.label}: ${f.value}.`), ...paragraphs].join('').replace(/\s/g, '');
  assert.equal(kept.length, part.replace(/\s/g, '').length);
});

test('numbers, abbreviations and a one-sentence mail stay whole', () => {
  assert.deepEqual(sentences('Zaufanie rośnie o 5 pkt. w obie strony. Bezpiecznie to P4. Oczekiwane to P2.'), [
    'Zaufanie rośnie o 5 pkt. w obie strony.',
    'Bezpiecznie to P4.',
    'Oczekiwane to P2.',
  ]);
  assert.deepEqual(sentences('Opona 1.5 s wolniejsza na okrążeniu'), ['Opona 1.5 s wolniejsza na okrążeniu']);
  assert.deepEqual(mailParts('Kierowca zastępczy za Ascari'), { lead: 'Kierowca zastępczy za Ascari', fields: [], paragraphs: [] });
  assert.deepEqual(mailParts(''), { lead: '', fields: [], paragraphs: [] });
});

test('someone speaking after a colon stays prose, not a segment', () => {
  const parts = mailParts('Przełom! Inżynierowie: koncepcja X wyszła lepiej, niż zakładaliśmy. Termin: 15 stycznia 1955.');
  assert.deepEqual(parts.paragraphs, ['Inżynierowie: koncepcja X wyszła lepiej, niż zakładaliśmy.']);
  assert.deepEqual(parts.fields, [{ label: 'Termin', value: '15 stycznia 1955', items: null }]);
});

test('English mail is cut the same way', () => {
  const parts = mailParts('A new aero part is on the car. Aerodynamics: 43–59 → 47–63. Front runners: HWM 62–87; Lancia 44–68.');
  assert.equal(parts.lead, 'A new aero part is on the car.');
  assert.deepEqual(parts.fields.map((f) => f.label), ['Aerodynamics', 'Front runners']);
});
