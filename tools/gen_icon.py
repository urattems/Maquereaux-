#!/usr/bin/env python3
"""Génère assets/maquereaux.ico (16 → 256 px) et assets/maquereaux.png depuis le logo du design system.

- Tuile marine arrondie + maquereau au dégradé signature (bleu-vert → argent → turquoise).
- Petites tailles (16, 24) : dessin simplifié (pas de rayures, œil plus gros, marges réduites) pour rester lisible.
Prérequis : Node + Playwright/Chromium (rendu SVG fidèle) et Pillow.
Usage : python3 tools/gen_icon.py
"""
import os, subprocess, sys, tempfile
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SIZES = [16, 24, 32, 48, 64, 128, 256]

# Mêmes tracés que Themes/Logo.xaml (grille 24×24)
BODY = "M 4 12 C 7 7.4 12 6.1 16.6 7.7 C 19.1 8.6 21.1 10.1 22.2 12 C 21.1 13.9 19.1 15.4 16.6 16.3 C 12 17.9 7 16.6 4 12 Z M 4.8 12 L 1 7.8 Q 2.3 12 1 16.2 Z"
BELLY = "M 5.5 12.6 C 8.5 15.6 13.5 16.6 17.5 15.3 C 19.3 14.7 20.7 13.7 21.6 12.6 Z"
STRIPES = "M 8 9.6 Q 8.9 10.4 8.1 11.3 M 10.6 8.7 Q 11.6 9.7 10.7 10.8 M 13.2 8.3 Q 14.2 9.4 13.3 10.5 M 15.8 8.4 Q 16.7 9.5 15.9 10.5"


def svg(simple: bool) -> str:
    # Le poisson occupe ~80 % de la tuile (un peu plus en petit format)
    scale, offset = (1.0, 0.0) if simple else (0.86, 1.68)
    stripes = "" if simple else f'<path d="{STRIPES}" fill="none" stroke="#0B2340" stroke-width="1.1" stroke-linecap="round"/>'
    eye_r = 1.35 if simple else 0.95
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
  <defs>
    <linearGradient id="tile" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#15304D"/><stop offset="1" stop-color="#0A1220"/>
    </linearGradient>
    <linearGradient id="sig" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#1689C9"/><stop offset="0.5" stop-color="#C3D3E2"/><stop offset="1" stop-color="#2BD9C6"/>
    </linearGradient>
  </defs>
  <rect x="0.5" y="0.5" width="23" height="23" rx="{4 if simple else 5}" fill="url(#tile)" stroke="#2D4A72" stroke-width="{0 if simple else 0.5}"/>
  <g transform="translate({offset} {offset}) scale({scale})">
    <path d="{BODY}" fill="url(#sig)" fill-rule="nonzero"/>
    <path d="{BELLY}" fill="#FFFFFF" fill-opacity="0.33"/>
    {stripes}
    <circle cx="18.7" cy="11.2" r="{eye_r}" fill="#0B2340"/>
  </g>
</svg>'''


RENDER_JS = r"""
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const [svgFile, pngFile, size] = process.argv.slice(2);
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: +size, height: +size } });
  const svg = fs.readFileSync(svgFile, 'utf8').replace('width="24" height="24"', `width="${size}" height="${size}"`);
  await page.setContent(`<html><body style="margin:0;background:transparent">${svg}</body></html>`);
  await page.screenshot({ path: pngFile, omitBackground: true, clip: { x: 0, y: 0, width: +size, height: +size } });
  await browser.close();
})();
"""


def render(size: int, tmp: str) -> Image.Image:
    src = os.path.join(tmp, f"icon-{size}.svg")
    out = os.path.join(tmp, f"icon-{size}.png")
    js = os.path.join(tmp, "render.js")
    with open(src, "w", encoding="utf-8") as f:
        f.write(svg(simple=size <= 24))
    with open(js, "w", encoding="utf-8") as f:
        f.write(RENDER_JS)
    # Rendu à 8× puis réduction Lanczos : antialiasing propre, y compris en 16 px
    big = size * 8
    subprocess.run(["node", js, src, out, str(big)], check=True)
    return Image.open(out).convert("RGBA").resize((size, size), Image.LANCZOS)


def main() -> int:
    with tempfile.TemporaryDirectory() as tmp:
        images = [render(s, tmp) for s in SIZES]
    largest = images[-1]
    largest.save(os.path.join(ROOT, "assets", "maquereaux.ico"), format="ICO",
                 sizes=[(s, s) for s in SIZES], append_images=images[:-1])
    largest.save(os.path.join(ROOT, "assets", "maquereaux.png"))
    check = Image.open(os.path.join(ROOT, "assets", "maquereaux.ico"))
    print("ICO :", sorted(check.ico.sizes()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
