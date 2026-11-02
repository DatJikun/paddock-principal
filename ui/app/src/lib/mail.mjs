/**
 * The layout of one mail in the reader (#320). Mail text comes from the core as one translated message; this only cuts it
 * where the text already has its own seams, so the player sees a lead line, labelled values as segments and the rest as
 * short paragraphs instead of one block. It never changes a word, drops one or reorders the text.
 */

/* A sentence ends at . ! ? (or …) followed by a space and a capital letter, so "1.5", "pkt. w" and "P4." inside a sentence stay. */
const SENTENCE_END = /(?<=[.!?…])\s+(?=[\p{Lu}„"(])/u;

/* "Label: value": a label of at most four words, starting with a capital letter. */
const FIELD = /^(\p{Lu}[\p{L}\p{N}]*(?: [\p{L}\p{N}]+){0,3}): (.+)$/u;

/* A value (or each entry of a list) is a few words at most; a longer one is someone speaking, which stays prose. */
const MAX_VALUE_WORDS = 5;

const words = (text) => text.trim().split(/\s+/).length;

/** Splits a message into sentences without losing a character (apart from the spaces between them). */
export function sentences(text) {
  const trimmed = String(text ?? '').trim();
  return trimmed ? trimmed.split(SENTENCE_END) : [];
}

/**
 * { lead, fields, paragraphs } for a mail. `lead` is the first sentence (also the row title). A later sentence of the form
 * "Label: value" with a short value becomes a field; a value listing several entries with "; " keeps them as `items`.
 * Everything else stays a paragraph, in order.
 */
export function mailParts(text) {
  const [lead = '', ...rest] = sentences(text);
  const fields = [];
  const paragraphs = [];
  for (const sentence of rest) {
    const match = FIELD.exec(sentence);
    const value = match ? match[2].replace(/\.$/, '') : '';
    const items = value.split('; ');
    if (match && !/[.!?]\s/.test(value) && items.every((item) => words(item) <= MAX_VALUE_WORDS)) {
      fields.push({ label: match[1], value, items: items.length > 1 ? items : null });
    } else {
      paragraphs.push(sentence);
    }
  }
  return { lead, fields, paragraphs };
}

/** The first sentence: what a mail row shows as its title. */
export function mailLead(text) {
  return sentences(text)[0] ?? '';
}
