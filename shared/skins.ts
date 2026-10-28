// The skin shop: looks for the boards (domino table, tawla, chess, ludo), the pieces (domino tiles, tawla
// checkers, chess pieces), the dice, bought with game coins, and the player's
// own look: a hat on their character, the cloth on their game table and the drink in front of them.
// The first skin of each kind is the free classic one. Skins change how the game looks (tiles, dice, boards
// and the table cloth to the player who owns them; the hat and the drink to everyone at the table); they never
// change the rules. Season-pass skins (pass: true) can't be bought: they come from the season track.

// Boards: 'felt' (the domino table), 'tawla', 'chess', 'ludo'. Pieces: 'tiles' (dominoes), 'checkers' (tawla),
// 'pieces' (chess). Then the dice, and your own look: 'hat' and 'drink'.
export type SkinCat = 'tiles' | 'checkers' | 'pieces' | 'dice' | 'tawla' | 'chess' | 'ludo' | 'hat' | 'felt' | 'drink' | 'win';
export const SKIN_CATS: SkinCat[] = ['tiles', 'checkers', 'pieces', 'dice', 'tawla', 'chess', 'ludo', 'hat', 'felt', 'drink', 'win'];

/** event: a limited skin, sold only while an event of that kind is on (see shared/events.ts). */
export interface Skin { id: string; cat: SkinCat; price: number; pass?: boolean; event?: 'ramadan' | 'eid' }

export const SKINS: Skin[] = [
  // domino tiles
  { id: 'tiles_ivory', cat: 'tiles', price: 0 },
  { id: 'tiles_ebony', cat: 'tiles', price: 150_000 },
  { id: 'tiles_marble', cat: 'tiles', price: 300_000 },
  { id: 'tiles_jade', cat: 'tiles', price: 450_000 },
  { id: 'tiles_gold', cat: 'tiles', price: 900_000 },
  // tawla checkers
  { id: 'checkers_classic', cat: 'checkers', price: 0 },
  { id: 'checkers_marble', cat: 'checkers', price: 150_000 },
  { id: 'checkers_amber', cat: 'checkers', price: 250_000 },
  { id: 'checkers_ruby', cat: 'checkers', price: 300_000 },
  { id: 'checkers_gold', cat: 'checkers', price: 600_000 },
  { id: 'checkers_pharaoh', cat: 'checkers', price: 0, pass: true },
  // chess pieces
  { id: 'pieces_wood', cat: 'pieces', price: 0 },
  { id: 'pieces_porcelain', cat: 'pieces', price: 250_000 },
  { id: 'pieces_marble', cat: 'pieces', price: 300_000 },
  { id: 'pieces_ruby', cat: 'pieces', price: 400_000 },
  { id: 'pieces_gold', cat: 'pieces', price: 800_000 },
  { id: 'pieces_onyx', cat: 'pieces', price: 0, pass: true },
  // dice (tawla and ludo)
  { id: 'dice_ivory', cat: 'dice', price: 0 },
  { id: 'dice_ruby', cat: 'dice', price: 100_000 },
  { id: 'dice_obsidian', cat: 'dice', price: 200_000 },
  { id: 'dice_turquoise', cat: 'dice', price: 300_000 },
  { id: 'dice_emerald', cat: 'dice', price: 250_000 },
  { id: 'dice_gold', cat: 'dice', price: 600_000 },
  { id: 'dice_pharaoh', cat: 'dice', price: 0, pass: true },
  // tawla boards
  { id: 'tawla_walnut', cat: 'tawla', price: 0 },
  { id: 'tawla_olive', cat: 'tawla', price: 200_000 },
  { id: 'tawla_ebony', cat: 'tawla', price: 300_000 },
  { id: 'tawla_pearl', cat: 'tawla', price: 400_000 },
  { id: 'tawla_royal', cat: 'tawla', price: 800_000 },
  { id: 'tawla_pharaoh', cat: 'tawla', price: 0, pass: true },
  // chess boards
  { id: 'chess_wood', cat: 'chess', price: 0 },
  { id: 'chess_blue', cat: 'chess', price: 200_000 },
  { id: 'chess_tournament', cat: 'chess', price: 250_000 },
  { id: 'chess_rosewood', cat: 'chess', price: 350_000 },
  { id: 'chess_marble', cat: 'chess', price: 500_000 },
  { id: 'chess_gold', cat: 'chess', price: 0, pass: true },
  // ludo boards
  { id: 'ludo_classic', cat: 'ludo', price: 0 },
  { id: 'ludo_night', cat: 'ludo', price: 200_000 },
  { id: 'ludo_pharaoh', cat: 'ludo', price: 350_000 },
  // the hat on your character (shown to everyone at the table): the classic is the character's own
  { id: 'hat_classic', cat: 'hat', price: 0 },
  { id: 'hat_fez_red', cat: 'hat', price: 120_000 },
  { id: 'hat_fez_black', cat: 'hat', price: 180_000 },
  { id: 'hat_fez_green', cat: 'hat', price: 220_000 },
  { id: 'hat_fez_navy', cat: 'hat', price: 260_000 },
  { id: 'hat_fez_white', cat: 'hat', price: 0, pass: true },
  { id: 'hat_fez_gold', cat: 'hat', price: 0, pass: true },
  // the cloth on your game table
  { id: 'felt_wood', cat: 'felt', price: 0 },
  { id: 'felt_green', cat: 'felt', price: 150_000 },
  { id: 'felt_red', cat: 'felt', price: 150_000 },
  { id: 'felt_blue', cat: 'felt', price: 200_000 },
  { id: 'felt_marble', cat: 'felt', price: 350_000 },
  { id: 'felt_royal', cat: 'felt', price: 0, pass: true },
  { id: 'felt_kilim', cat: 'felt', price: 0, pass: true },
  // the drink by your place (shown to everyone at the table)
  { id: 'drink_tea', cat: 'drink', price: 0 },
  { id: 'drink_mint', cat: 'drink', price: 80_000 },
  { id: 'drink_coffee', cat: 'drink', price: 80_000 },
  { id: 'drink_karkade', cat: 'drink', price: 120_000 },
  { id: 'drink_sahlab', cat: 'drink', price: 0, pass: true },
  { id: 'drink_qasab', cat: 'drink', price: 0, pass: true },
  // how you celebrate a win (everyone at the table sees it)
  { id: 'win_coins', cat: 'win', price: 0 },
  { id: 'win_confetti', cat: 'win', price: 150_000 },
  { id: 'win_stars', cat: 'win', price: 250_000 },
  { id: 'win_fireworks', cat: 'win', price: 450_000 },
  // Ramadan: only while a Ramadan event is on
  { id: 'felt_ramadan', cat: 'felt', price: 200_000, event: 'ramadan' },
  { id: 'dice_ramadan', cat: 'dice', price: 120_000, event: 'ramadan' },
  { id: 'tawla_ramadan', cat: 'tawla', price: 300_000, event: 'ramadan' },
  { id: 'hat_fez_ramadan', cat: 'hat', price: 150_000, event: 'ramadan' },
  { id: 'drink_qamar', cat: 'drink', price: 90_000, event: 'ramadan' },
  { id: 'win_lanterns', cat: 'win', price: 250_000, event: 'ramadan' },
  // Eid: only while an Eid event is on
  { id: 'felt_eid', cat: 'felt', price: 200_000, event: 'eid' },
  { id: 'dice_eid', cat: 'dice', price: 120_000, event: 'eid' },
  { id: 'hat_fez_eid', cat: 'hat', price: 150_000, event: 'eid' },
  { id: 'drink_sobia', cat: 'drink', price: 90_000, event: 'eid' },
  { id: 'win_balloons', cat: 'win', price: 250_000, event: 'eid' },
];

export const skinById = (id: string) => SKINS.find(s => s.id === id);
export const isFree = (s: Skin) => s.price === 0 && !s.pass;
export const defaultSkin = (cat: SkinCat) => SKINS.find(s => s.cat === cat && isFree(s))!.id;
export type Equipped = Partial<Record<SkinCat, string>>;
/** The skin to show for a kind: the equipped one if it is real and owned (or free), else the classic one. */
export function skinFor(cat: SkinCat, equip: Equipped | undefined, owned: string[] | undefined): string {
  const id = equip?.[cat], s = id ? skinById(id) : undefined;
  return s && s.cat === cat && (isFree(s) || owned?.includes(s.id)) ? s.id : defaultSkin(cat);
}
