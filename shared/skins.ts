// The skin shop: looks for domino tiles, dice and the three boards, bought with game coins.
// The first skin of each kind is the free classic one. Skins change how the game looks to the player who
// owns them (at every table); they never change the rules.

export type SkinCat = 'tiles' | 'dice' | 'tawla' | 'chess' | 'ludo';
export const SKIN_CATS: SkinCat[] = ['tiles', 'dice', 'tawla', 'chess', 'ludo'];

export interface Skin { id: string; cat: SkinCat; price: number }

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
];

export const skinById = (id: string) => SKINS.find(s => s.id === id);
export const defaultSkin = (cat: SkinCat) => SKINS.find(s => s.cat === cat && s.price === 0)!.id;
export type Equipped = Partial<Record<SkinCat, string>>;
/** The skin to show for a kind: the equipped one if it is real and owned (or free), else the classic one. */
export function skinFor(cat: SkinCat, equip: Equipped | undefined, owned: string[] | undefined): string {
  const id = equip?.[cat], s = id ? skinById(id) : undefined;
  return s && s.cat === cat && (s.price === 0 || owned?.includes(s.id)) ? s.id : defaultSkin(cat);
}
