from PIL import Image, ImageDraw

# 4x4 Atlas, jeder Block 16x16
atlas = Image.new('RGB', (64, 64))
draw = ImageDraw.Draw(atlas)

# Grass top (0,0)
draw.rectangle([0, 0, 15, 15], fill=(86, 125, 70))

# Dirt (16,0)
draw.rectangle([16, 0, 31, 15], fill=(139, 115, 85))

# Grass side (32,0)
draw.rectangle([32, 0, 47, 15], fill=(107, 142, 93))

# Sand (48,0)
draw.rectangle([48, 0, 63, 15], fill=(224, 197, 153))

# Stone (0,16)
draw.rectangle([0, 16, 15, 31], fill=(119, 119, 119))

# Wood (16,16)
draw.rectangle([16, 16, 31, 31], fill=(139, 111, 71))

# Leaves (32,16)
draw.rectangle([32, 16, 47, 31], fill=(74, 124, 60))

# Water (48,16)
draw.rectangle([48, 16, 63, 31], fill=(59, 90, 140))

atlas.save('atlas.png')
print("Atlas created successfully!")
