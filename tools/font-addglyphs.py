#!/usr/bin/env python3
"""Adds the symbols the game writes in its texts (the card suits, the star, the play arrow, the cup,
the trophy) to the game's fonts, which don't have them: a missing glyph shows as nothing on Android.

The shapes come from DejaVu Sans (free: the Bitstream Vera licence lets them be copied into other
fonts). The fonts are renamed, as the IBM Plex licence asks of a changed copy ("Plex" is reserved).

    python3 tools/font-addglyphs.py           # changes unity/Assets/Resources/Fonts/*.ttf in place
"""
import os, sys
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, '..', 'unity', 'Assets', 'Resources', 'Fonts')
DEJAVU = '/usr/share/fonts/truetype/dejavu/'
# U+1F3C6 (trophy) isn't in DejaVu: the code writes ★ instead.
CHARS = [0x2660, 0x2663, 0x2665, 0x2666, 0x2605, 0x2606, 0x25B6, 0x25C0, 0x2615, 0x2714, 0x2715]
TARGETS = [('PlexArabic.ttf', 'DejaVuSans-Bold.ttf', 'Board Battles Arabic', 'SemiBold'),
           ('PlexArabicBold.ttf', 'DejaVuSans-Bold.ttf', 'Board Battles Arabic', 'Bold'),
           ('ReemKufi.ttf', 'DejaVuSans-Bold.ttf', 'Board Battles Kufi', 'Regular')]


def rename(font, family, style):
    name = font['name']
    for rec in list(name.names):
        if rec.nameID in (1, 3, 4, 6, 16, 17, 21, 22):
            name.removeNames(nameID=rec.nameID)
    full = family + ('' if style == 'Regular' else ' ' + style)
    for nid, val in ((1, family), (2, style), (3, full + ' (game copy)'), (4, full), (6, full.replace(' ', '-'))):
        name.setName(val, nid, 3, 1, 0x409)
        name.setName(val, nid, 1, 0, 0)


def add(target, source, family, style):
    path = os.path.join(FONTS, target)
    font, src = TTFont(path), TTFont(DEJAVU + source)
    scmap, tcmap = src.getBestCmap(), font.getBestCmap()
    cap = getattr(font['OS/2'], 'sCapHeight', 0) or 0.7 * font['head'].unitsPerEm
    order = list(font.getGlyphOrder())
    sglyf, tglyf = src['glyf'], font['glyf']
    added = 0
    for u in CHARS:
        if u in tcmap or u not in scmap:
            continue
        gname = 'bb_uni%04X' % u
        # each symbol as tall as a capital letter, standing on the line, with a little room either side
        sg = sglyf[scmap[u]]; sg.recalcBounds(sglyf)
        h = max(1, sg.yMax - sg.yMin)
        g = cap * (1.0 if u in (0x2615, 0x2714, 0x2715) else 1.06) / h
        side = cap * 0.12
        pen = TTGlyphPen(None)
        sg.draw(TransformPen(pen, (g, 0, 0, g, side - sg.xMin * g, -sg.yMin * g)), sglyf)
        tglyf[gname] = pen.glyph()
        tglyf[gname].recalcBounds(tglyf)
        font['hmtx'][gname] = (round((sg.xMax - sg.xMin) * g + 2 * side), round(side))
        order.append(gname)
        for table in font['cmap'].tables:
            if table.isUnicode() and (u <= 0xFFFF or table.format in (12, 13)):
                table.cmap[u] = gname
        added += 1
    font.setGlyphOrder(order)
    tglyf.glyphOrder = order
    font['maxp'].numGlyphs = len(order)
    if 'post' in font and font['post'].formatType == 2.0:
        font['post'].extraNames = []
        font['post'].mapping = {}
    rename(font, family, style)
    font.save(path)
    print(target, 'added', added)


if __name__ == '__main__':
    if not os.path.isdir(DEJAVU):
        sys.exit('DejaVu fonts not found (apt install fonts-dejavu-core)')
    for t in TARGETS:
        add(*t)
