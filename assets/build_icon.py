"""Generate icon.png (1024) and icon.ico (multi-res) for Advanced Clipboarder.

Minimalist clipboard glyph on a purple squircle background. Drawn
procedurally with PIL — matches assets/icon.svg and renders cleanly
down to 16×16 (tray size).
"""
from PIL import Image, ImageDraw, ImageFilter
from pathlib import Path

ACCENT = (124, 92, 255, 255)   # #7C5CFF
ACCENT_SOFT = (124, 92, 255, 140)
ACCENT_FAINT = (124, 92, 255, 90)
WHITE = (255, 255, 255, 255)

HERE = Path(__file__).parent

def draw_icon(size: int) -> Image.Image:
    """Render the icon at the given pixel size. Uses 4x supersampling for crispness."""
    s = size * 4
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Background squircle-ish (rounded square)
    d.rounded_rectangle(
        [(0, 0), (s, s)],
        radius=int(s * 0.225),
        fill=ACCENT,
    )

    # Top clip (the clamp sticking out of the clipboard)
    clip_w = int(s * 0.25)
    clip_h = int(s * 0.125)
    clip_x1 = (s - clip_w) // 2
    clip_y1 = int(s * 0.172)
    d.rounded_rectangle(
        [(clip_x1, clip_y1), (clip_x1 + clip_w, clip_y1 + clip_h)],
        radius=int(s * 0.04),
        fill=WHITE,
    )

    # Clipboard body
    body_x1 = int(s * 0.195)
    body_y1 = int(s * 0.273)
    body_x2 = int(s * 0.805)
    body_y2 = int(s * 0.844)
    d.rounded_rectangle(
        [(body_x1, body_y1), (body_x2, body_y2)],
        radius=int(s * 0.07),
        fill=WHITE,
    )

    # Content lines — only render at sizes >= 48 so the 32/24/16 glyphs stay clean
    if size >= 48:
        line_h = int(s * 0.027)
        line_r = line_h // 2
        line_x = int(s * 0.297)
        for y_frac, w_frac, color in [
            (0.476, 0.265, ACCENT),
            (0.554, 0.406, ACCENT_SOFT),
            (0.632, 0.344, ACCENT_FAINT),
        ]:
            ly = int(s * y_frac)
            d.rounded_rectangle(
                [(line_x, ly), (line_x + int(s * w_frac), ly + line_h)],
                radius=line_r,
                fill=color,
            )

    # Downscale with LANCZOS for sharp edges
    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    # Hi-res PNG for README / store listings
    hi = draw_icon(1024)
    hi.save(HERE / "icon.png", optimize=True)

    # Per-size PNGs (useful for debugging / docs)
    for sz in (16, 24, 32, 48, 64, 128, 256):
        draw_icon(sz).save(HERE / f"icon-{sz}.png", optimize=True)

    # Multi-resolution ICO. PIL's `append_images` doesn't work for ICO, so
    # we render the largest source frame and let PIL resample each requested
    # size with LANCZOS. The `draw_icon` function still renders at 4× for
    # the source, so the downscale starts from 1024 px of detail.
    ico_sizes = [(256, 256), (128, 128), (64, 64), (48, 48),
                 (32, 32), (24, 24), (16, 16)]
    draw_icon(256).save(HERE / "icon.ico", format="ICO", sizes=ico_sizes)
    print("wrote:", sorted(p.name for p in HERE.glob("icon*")))


if __name__ == "__main__":
    main()
