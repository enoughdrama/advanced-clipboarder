"""Generate icon.png (1024) and icon.ico (multi-res) for Clipboarder.

Minimal, monochrome clipboard silhouette on a charcoal squircle — no
accent colour, no inner lines, just the figure. Matches assets/icon.svg
and renders cleanly down to 16x16 (tray size).
"""
from PIL import Image, ImageDraw
from pathlib import Path

# Charcoal matches Theme.xaml AppBgA (#1E1C26) so the icon reads as part
# of the same visual family as the window chrome. FG is the warm off-white
# the app uses for primary text (Fg0 = #F4F1EA) rather than pure #FFFFFF,
# which shimmers against dark backgrounds at small sizes.
BG = (30, 28, 38, 255)
FG = (244, 241, 234, 255)

HERE = Path(__file__).parent


def draw_icon(size: int) -> Image.Image:
    """Render the icon at the given pixel size. Uses 4x supersampling for crispness."""
    s = size * 4
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Rounded-square background (iOS/Windows-11 squircle-ish).
    d.rounded_rectangle(
        [(0, 0), (s, s)],
        radius=int(s * 0.225),
        fill=BG,
    )

    # Clip tab (the clamp sticking out of the top of the clipboard).
    clip_w = int(s * 0.26)
    clip_h = int(s * 0.13)
    clip_x1 = (s - clip_w) // 2
    clip_y1 = int(s * 0.17)
    d.rounded_rectangle(
        [(clip_x1, clip_y1), (clip_x1 + clip_w, clip_y1 + clip_h)],
        radius=int(s * 0.04),
        fill=FG,
    )

    # Clipboard body. No inner content lines — the figure itself is the
    # whole icon, which is what "more minimalist / more figure-driven"
    # means here.
    body_x1 = int(s * 0.19)
    body_y1 = int(s * 0.27)
    body_x2 = int(s * 0.81)
    body_y2 = int(s * 0.85)
    d.rounded_rectangle(
        [(body_x1, body_y1), (body_x2, body_y2)],
        radius=int(s * 0.075),
        fill=FG,
    )

    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    hi = draw_icon(1024)
    hi.save(HERE / "icon.png", optimize=True)

    for sz in (16, 24, 32, 48, 64, 128, 256):
        draw_icon(sz).save(HERE / f"icon-{sz}.png", optimize=True)

    ico_sizes = [(256, 256), (128, 128), (64, 64), (48, 48),
                 (32, 32), (24, 24), (16, 16)]
    draw_icon(256).save(HERE / "icon.ico", format="ICO", sizes=ico_sizes)
    print("wrote:", sorted(p.name for p in HERE.glob("icon*")))


if __name__ == "__main__":
    main()
