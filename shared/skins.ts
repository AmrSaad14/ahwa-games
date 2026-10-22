// The skin shop: looks for domino tiles, dice and the three boards, bought with game coins, and the player's
// own look: a hat on their character, the cloth on their game table and the drink in front of them.
// The first skin of each kind is the free classic one. Skins change how the game looks (tiles, dice, boards
// and the table cloth to the player who owns them; the hat and the drink to everyone at the table); they never
// change the rules. Season-pass skins (pass: true) can't be bought: they come from the season track.

export type SkinCat = 'tiles' | 'dice' | 'tawla' | 'chess' | 'ludo' | 'hat' | 'felt' | 'drink';
export const SKIN_CATS: SkinCat[] = ['tiles', 'dice', 'tawla', 'chess', 'ludo', 'hat', 'felt', 'drink'];

export interface Skin { id: string; cat: SkinCat; price: number; pass?: boolean }

export const SKINS: Skin[] = [
  // domino tiles
  { id: 'tiles_ivory', cat: 'tiles', price: 0 },
  { id: 'tiles_ebony', cat: 'tiles', price: 150_000 },
  { id: 'tiles_marble', cat: 'tiles', price: 300_000 },
  { id: 'tiles_jade', cat: 'tiles', price: 450_000 },
  { id: 'tiles_gold', cat: 'tiles', price: 900_000 },
  // dice (tawla and ludo)
  { id: 'dice_ivory', cat: 'dice', price: 0 },
  { id: 'dice_ruby', cat: 'dice', price: 100_000 },
  { id: 'dice_obsidian', cat: 'dice', price: 200_000 },
  { id: 'dice_turquoise', cat: 'dice', price: 300_000 },
  { id: 'dice_gold', cat: 'dice', price: 600_000 },
  // tawla boards
  { id: 'tawla_walnut', cat: 'tawla', price: 0 },
  { id: 'tawla_pearl', cat: 'tawla', price: 400_000 },
  { id: 'tawla_royal', cat: 'tawla', price: 800_000 },
  // chess boards
  { id: 'chess_wood', cat: 'chess', price: 0 },
  { id: 'chess_tournament', cat: 'chess', price: 250_000 },
  { id: 'chess_marble', cat: 'chess', price: 500_000 },
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
  { id: 'felt_royal', cat: 'felt', price: 0, pass: true },
  { id: 'felt_kilim', cat: 'felt', price: 0, pass: true },
  // the drink by your place (shown to everyone at the table)
  { id: 'drink_tea', cat: 'drink', price: 0 },
  { id: 'drink_mint', cat: 'drink', price: 80_000 },
  { id: 'drink_coffee', cat: 'drink', price: 80_000 },
  { id: 'drink_karkade', cat: 'drink', price: 120_000 },
  { id: 'drink_sahlab', cat: 'drink', price: 0, pass: true },
  { id: 'drink_qasab', cat: 'drink', price: 0, pass: true },
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
