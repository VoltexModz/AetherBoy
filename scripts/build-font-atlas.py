#!/usr/bin/env python3
"""Regenerate the bundled SDL fallback from the bundled Noto fonts (requires Pillow).

Only needed when changing the font assets; builds and releases use committed atlases.
"""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1] / "branding" / "fonts"
characters = ''.join(chr(c) for c in list(range(32, 127)) + list(range(160, 384))) + '–—‘’“”…·×□←↑→↓'
for weight in ('Regular', 'Bold'):
    font = ImageFont.truetype(str(root / f'NotoSans-{weight}.ttf'), 32)
    atlas = Image.new('RGBA', (1024, 1024))
    draw = ImageDraw.Draw(atlas)
    glyphs = {}
    for i, char in enumerate(dict.fromkeys(characters)):
        x, y = (i % 16) * 64, (i // 16) * 44
        draw.text((x + 2, y), char, font=font, fill='white')
        glyphs[char] = {'X': x, 'Y': y, 'Advance': font.getlength(char)}
    atlas.save(root / f'NotoSans-{weight}-atlas.png')
    (root / f'NotoSans-{weight}-atlas.json').write_text(json.dumps(glyphs, ensure_ascii=False), encoding='utf-8')
