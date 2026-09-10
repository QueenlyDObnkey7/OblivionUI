"""User-authorized local silhouette cleanup; retain original RGB artwork."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent
source = root / 'BookBackground-original.png'
target = root / 'BookBackground.png'
if not source.exists():
    source.write_bytes(target.read_bytes())
im = Image.open(source).convert('RGBA')
assert im.size == (1123, 1400), im.size
scale = 4
mask = Image.new('L', (im.width*scale, im.height*scale))
draw = ImageDraw.Draw(mask)
# Follow the outside of the dark leather and raised brass clasp, inset one
# pixel to discard the generator's pale fringe without altering the paper.
draw.rounded_rectangle(tuple(v*scale for v in (41, 69, 1084, 1361)), radius=18*scale, fill=255)
clasp = [(198,108),(208,96),(228,84),(240,66),(247,48),(249,38),
         (270,38),(276,31),(850,31),(869,38),(877,58),(884,74),
         (899,91),(921,107)]
draw.polygon([(x*scale,y*scale) for x,y in clasp], fill=255)
mask = mask.resize(im.size, Image.Resampling.LANCZOS)
im.putalpha(mask)
im.save(target)
print(f'Saved {target}; alpha bounds {mask.getbbox()}')
