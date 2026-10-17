// What each shop skin looks like: colours and finishes for the 3D tiles, dice and boards, and for the
// domino tiles in your hand (CSS). The classic looks are the game's original ones.

/** Domino tiles. */
export interface TileLook {
  face: [string, string];            // face gradient, centre → edge
  veins?: string;                    // stone veins drawn over the face (marble, jade)
  pip: [string, string, string];     // engraved pip: centre, body, rim
  groove: string;
  body: number; rough: number; metal: number;
  css: { face: string; edge: string; dot: string; groove: string };
}
export const TILE_LOOKS: Record<string, TileLook> = {
  tiles_ivory: { face: ['#fbf5e6', '#ebdfc4'], pip: ['#000', '#1e140d', '#4a3a2a'], groove: '#9c8d70', body: 0xefe5cf, rough: 0.28, metal: 0,
    css: { face: 'linear-gradient(145deg, #fdf8ec, #e8dcc2)', edge: '#cfc2a4', dot: '#1e140d', groove: '#9c8d70' } },
  tiles_ebony: { face: ['#2e2824', '#151110'], pip: ['#ffffff', '#efe9dc', '#9a9284'], groove: '#5a4c40', body: 0x1c1714, rough: 0.22, metal: 0,
    css: { face: 'linear-gradient(145deg, #3a332e, #161210)', edge: '#0c0907', dot: '#f4efe4', groove: '#5a4c40' } },
  tiles_marble: { face: ['#f6f4f0', '#dedad2'], veins: 'rgba(120,120,130,.35)', pip: ['#3a3a3e', '#141416', '#6a6a70'], groove: '#a8a49c', body: 0xe8e5df, rough: 0.18, metal: 0,
    css: { face: 'linear-gradient(145deg, #faf9f6, #d9d5cc)', edge: '#bdb8ae', dot: '#18181a', groove: '#a8a49c' } },
  tiles_jade: { face: ['#3a9a72', '#165a3e'], veins: 'rgba(200,255,225,.22)', pip: ['#fff2c4', '#d4a347', '#7a5520'], groove: '#0e3f2b', body: 0x1f6b4c, rough: 0.2, metal: 0.05,
    css: { face: 'linear-gradient(145deg, #45a87e, #155638)', edge: '#0e3f2b', dot: '#e9bf62', groove: '#0e3f2b' } },
  tiles_gold: { face: ['#fbe7a0', '#c9962f'], pip: ['#2a1a0a', '#120a04', '#6a4a18'], groove: '#8a6420', body: 0xd4a347, rough: 0.3, metal: 0.85,
    css: { face: 'linear-gradient(145deg, #fff0b8, #c38f2a)', edge: '#8a6420', dot: '#1c1206', groove: '#8a6420' } },
};

/** Dice. `null` pip colours for the 1 keep the game's own (a red one-spot on ludo dice). */
export interface DiceLook { bg: [string, string]; pip: [string, string]; one?: [string, string]; rough: number; metal: number; css: { face: string; dot: string } }
export const DICE_LOOKS: Record<string, DiceLook | null> = {
  dice_ivory: null,   // the classic: ivory tawla dice, white ludo dice
  dice_ruby: { bg: ['#e0404a', '#7a0e16'], pip: ['#ffffff', '#d8d2c8'], rough: 0.15, metal: 0, css: { face: 'linear-gradient(145deg, #ef5a62, #860f19)', dot: '#fff' } },
  dice_obsidian: { bg: ['#34343a', '#0c0c0e'], pip: ['#fff2c4', '#c9962f'], rough: 0.12, metal: 0.1, css: { face: 'linear-gradient(145deg, #3d3d44, #0d0d10)', dot: '#e7bd5c' } },
  dice_turquoise: { bg: ['#4fd0bf', '#1b7d72'], pip: ['#ffffff', '#e6f6f2'], one: ['#d4a347', '#7a5520'], rough: 0.2, metal: 0, css: { face: 'linear-gradient(145deg, #5fdcc9, #1a7468)', dot: '#fff' } },
  dice_gold: { bg: ['#fbe7a0', '#c9962f'], pip: ['#3a2a10', '#0b0705'], rough: 0.3, metal: 0.8, css: { face: 'linear-gradient(145deg, #fff0b8, #c38f2a)', dot: '#1c1206' } },
};

/** Wood colours: base, dark grain, light grain. */
type Wood = [string, string, string];

export interface TawlaLook { field: Wood; pointA: Wood; pointB: Wood; side: Wood; tray: string; line: string; css: string }
export const TAWLA_LOOKS: Record<string, TawlaLook> = {
  tawla_walnut: { field: ['#d9bf8c', '#b8965d', '#ecd7aa'], pointA: ['#3d2414', '#20110a', '#5a3820'], pointB: ['#7a2418', '#4a0f0a', '#9a3a26'], side: ['#3a1a10', '#1e0b05', '#58291a'], tray: '#1f3a2a', line: '#efe0bd',
    css: 'linear-gradient(90deg, #d9bf8c 0 46%, #3a1a10 46% 54%, #d9bf8c 54%)' },
  tawla_pearl: { field: ['#f2eee6', '#d8d2c6', '#ffffff'], pointA: ['#1d1d24', '#0a0a0e', '#3a3a44'], pointB: ['#1f8f86', '#0f5a54', '#3ab0a6'], side: ['#2a1d14', '#120b06', '#463020'], tray: '#13403c', line: '#c9a25a',
    css: 'linear-gradient(90deg, #f2eee6 0 46%, #2a1d14 46% 54%, #f2eee6 54%)' },
  tawla_royal: { field: ['#145a45', '#0b3a2c', '#1f7a5e'], pointA: ['#e3b75a', '#a7792a', '#f6d58c'], pointB: ['#18110f', '#060303', '#30241f'], side: ['#120c0a', '#050302', '#2a1c16'], tray: '#3a070c', line: '#f3cd62',
    css: 'linear-gradient(90deg, #145a45 0 46%, #120c0a 46% 54%, #145a45 54%)' },
};

export interface ChessLook { kind: 'wood' | 'flat' | 'marble'; light: Wood; dark: Wood; frame: Wood; css: string }
export const CHESS_LOOKS: Record<string, ChessLook> = {
  chess_wood: { kind: 'wood', light: ['#e7cf9f', '#c7a46b', '#f6e6c4'], dark: ['#5a321b', '#331a0c', '#7a4a2a'], frame: ['#4a2116', '#2a0f08', '#6e3424'],
    css: 'conic-gradient(#e7cf9f 25%, #5a321b 0 50%, #e7cf9f 0 75%, #5a321b 0)' },
  chess_tournament: { kind: 'flat', light: ['#eeeed2', '#e2e2c4', '#f6f6e2'], dark: ['#769656', '#63804a', '#86a664'], frame: ['#2e3a24', '#1a2214', '#45553a'],
    css: 'conic-gradient(#eeeed2 25%, #769656 0 50%, #eeeed2 0 75%, #769656 0)' },
  chess_marble: { kind: 'marble', light: ['#f4f2ee', '#d8d4cc', '#ffffff'], dark: ['#3e434a', '#23272c', '#6a7078'], frame: ['#1c1c20', '#0a0a0c', '#34343a'],
    css: 'conic-gradient(#f4f2ee 25%, #3e434a 0 50%, #f4f2ee 0 75%, #3e434a 0)' },
};

export interface LudoLook { base: string; track: string; colors: string[]; line: string; star: string; frame: Wood; css: string }
export const LUDO_LOOKS: Record<string, LudoLook> = {
  ludo_classic: { base: '#f6efdf', track: '#fbf7ee', colors: ['#c8402f', '#2f8a4a', '#e0b030', '#2f6fb8'], line: '#6b5a45', star: '#b9a68a', frame: ['#4a2a17', '#26140a', '#6b4128'],
    css: 'conic-gradient(#2f8a4a 25%, #e0b030 0 50%, #2f6fb8 0 75%, #c8402f 0)' },
  ludo_night: { base: '#1b1e2b', track: '#2a2f42', colors: ['#ff4d6d', '#36d399', '#ffd166', '#4cc9f0'], line: '#454b66', star: '#ffd166', frame: ['#14161f', '#07080c', '#262a3a'],
    css: 'conic-gradient(#36d399 25%, #ffd166 0 50%, #4cc9f0 0 75%, #ff4d6d 0)' },
  ludo_pharaoh: { base: '#e9d7a8', track: '#f4e7c4', colors: ['#b5442c', '#2f7d6d', '#d9a93a', '#2a4f9a'], line: '#8a6a3a', star: '#b8860b', frame: ['#2a4f9a', '#162c5a', '#3a66b8'],
    css: 'conic-gradient(#2f7d6d 25%, #d9a93a 0 50%, #2a4f9a 0 75%, #b5442c 0)' },
};
