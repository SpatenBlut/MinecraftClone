from PIL import Image
from pathlib import Path

SRC = Path(__file__).resolve().parents[2] / "Textures/assets/minecraft/textures/block"
TILE = 16
GRID = 16           # 16x16 Tiles
SIZE = TILE * GRID  # 256 px

atlas = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))

TILES = {
    (0, 0): "grass_block_top.png",
    (1, 0): "grass_block_side.png",
    (2, 0): "dirt.png",
    (3, 0): "stone.png",
    (4, 0): "oak_log.png",
    (5, 0): "oak_log_top.png",
    (6, 0): "oak_leaves.png",
    (7, 0): "sand.png",
}

for (col, row), filename in TILES.items():
    src_path = SRC / filename
    tex = Image.open(src_path).convert('RGBA')
    if tex.size != (TILE, TILE):
        tex = tex.crop((0, 0, TILE, TILE))
    atlas.paste(tex, (col * TILE, row * TILE))

# Water (erster Frame aus dem animierten 16x512-Strip)
water = Image.open(SRC / "water_still.png").convert('RGBA').crop((0, 0, TILE, TILE))
atlas.paste(water, (8 * TILE, 0))

atlas.save(Path(__file__).parent / "atlas.png")
print(f"Atlas {SIZE}x{SIZE} mit {len(TILES) + 1} Tiles erstellt.")
