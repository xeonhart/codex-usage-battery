from pathlib import Path

from PIL import Image, ImageDraw


sizes = [16, 24, 32, 48, 64, 128, 256]
images = []
for size in sizes:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    scale = size / 64
    draw.rounded_rectangle(
        (3 * scale, 8 * scale, 57 * scale, 56 * scale),
        radius=10 * scale,
        fill=(23, 34, 48, 255),
        outline=(105, 129, 148, 255),
        width=max(1, round(2 * scale)),
    )
    draw.rounded_rectangle(
        (11 * scale, 17 * scale, 42 * scale, 47 * scale),
        radius=5 * scale,
        fill=(75, 216, 155, 255),
    )
    draw.rounded_rectangle(
        (56 * scale, 25 * scale, 62 * scale, 39 * scale),
        radius=2 * scale,
        fill=(105, 129, 148, 255),
    )
    images.append(image)

target = Path(__file__).with_name("CodexUsageBattery.ico")
images[-1].save(target, format="ICO", sizes=[(n, n) for n in sizes])
print(target)
