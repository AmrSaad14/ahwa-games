// Keeps the two languages clean: no Arabic inside English texts, nothing missing, the same {placeholders}.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { DICTS } from '../client/src/i18n';

const ARABIC = /[؀-ۿ]/;
const flat = (v: unknown): string[] => (Array.isArray(v) ? v.map(String) : [String(v)]);
const holes = (s: string) => (s.match(/\{\w+\}/g) ?? []).sort().join(',');

test('English texts contain no Arabic script', () => {
  const bad = Object.entries(DICTS.en).flatMap(([k, v]) => flat(v).filter(s => ARABIC.test(s)).map(s => `${k}: ${s}`));
  assert.deepEqual(bad, []);
});

test('every text exists in both languages, is not empty, and has the same placeholders', () => {
  for (const [k, v] of Object.entries(DICTS.en)) {
    const a = (DICTS.ar as Record<string, unknown>)[k];
    assert.ok(a !== undefined, `Arabic is missing ${k}`);
    for (const s of [...flat(v), ...flat(a)]) assert.ok(s.trim().length > 0, `empty text for ${k}`);
    if (!Array.isArray(v)) assert.equal(holes(String(a)), holes(String(v)), `placeholders differ for ${k}`);
  }
});
