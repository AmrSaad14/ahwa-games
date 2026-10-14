import { ArmTile, End, Line, TILES, leftVal, rightVal } from './engine';

/** Where a tile sits on the table, in tile units: a tile is 2 long and 1 wide.
 *  x grows to the right, y grows toward the bottom player. `ang` is the direction of the tile's
 *  long axis; value `a` is drawn on the tail end and `b` on the head end. */
export interface Placement { id: number; a: number; b: number; x: number; y: number; ang: number }

/**
 * Lays out one arm of the line like players do at a real table:
 * tiles touch end to end on matching numbers, doubles sit crosswise,
 * and when the arm reaches the table edge it turns an L-shaped corner,
 * steps one tile toward the table edge and runs back the other way.
 */
export function layoutArm(items: readonly ArmTile[], sign: 1 | -1, firstExtent: number, xMax: number) {
  let p = { x: sign * firstExtent, y: 0 };
  let d: { x: number; y: number } = { x: sign, y: 0 };
  let vertical = false;
  let hdir: number = sign;
  const out: Placement[] = [];
  const put = ([id, inner, outer]: ArmTile) => {
    const corner = (nd: { x: number; y: number }) => {
      out.push({ id, a: inner, b: outer, x: p.x + d.x * 0.5 + nd.x * 0.5, y: p.y + d.y * 0.5 + nd.y * 0.5, ang: Math.atan2(nd.y, nd.x) });
      p = { x: p.x + d.x * 0.5 + nd.x * 1.5, y: p.y + d.y * 0.5 + nd.y * 1.5 };
      d = nd;
    };
    if (vertical) { vertical = false; hdir = -hdir; return corner({ x: hdir, y: 0 }); }
    const dbl = inner === outer;
    const len = dbl ? 1 : 2;
    if (Math.abs(p.x + d.x * len) > xMax) { vertical = true; return corner({ x: 0, y: sign }); }
    if (dbl) {
      out.push({ id, a: inner, b: outer, x: p.x + d.x * 0.5, y: p.y + d.y * 0.5, ang: Math.atan2(d.y, d.x) + Math.PI / 2 });
      p = { x: p.x + d.x, y: p.y + d.y };
    } else {
      out.push({ id, a: inner, b: outer, x: p.x + d.x, y: p.y + d.y, ang: Math.atan2(d.y, d.x) });
      p = { x: p.x + d.x * 2, y: p.y + d.y * 2 };
    }
  };
  items.forEach(put);
  return { out, next: (it: ArmTile) => { put(it); return out[out.length - 1]; } };
}

export function layoutLine(line: Line, xMax: number): Placement[] {
  if (line.f === null || !line.fo) return [];
  const [a, b] = line.fo;
  const dbl = a === b;
  const ext = dbl ? 0.5 : 1;
  return [
    { id: line.f, a, b, x: 0, y: 0, ang: dbl ? Math.PI / 2 : 0 },
    ...layoutArm(line.R, 1, ext, xMax).out,
    ...layoutArm(line.L, -1, ext, xMax).out,
  ];
}

/** Where a tile would land if played on `end` (used for the glowing hint). */
export function previewPlacement(line: Line, id: number, end: End, xMax: number): Placement {
  const [a, b] = TILES[id];
  if (end === 'C' || line.f === null || !line.fo) return { id, a, b, x: 0, y: 0, ang: a === b ? Math.PI / 2 : 0 };
  const v = end === 'L' ? leftVal(line) : rightVal(line);
  const o = a === v ? b : a;
  const ext = line.fo[0] === line.fo[1] ? 0.5 : 1;
  return layoutArm(end === 'L' ? line.L : line.R, end === 'L' ? -1 : 1, ext, xMax).next([id, v, o]);
}

/** Axis-aligned footprint of a placement, for tests. */
export function footprint(p: Placement) {
  const horiz = Math.abs(Math.cos(p.ang)) > 0.5;
  return horiz ? { x0: p.x - 1, x1: p.x + 1, y0: p.y - 0.5, y1: p.y + 0.5 } : { x0: p.x - 0.5, x1: p.x + 0.5, y0: p.y - 1, y1: p.y + 1 };
}
