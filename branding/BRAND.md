# AetherBoy visual identity

## Current logo — approved 2026-10-02

`aetherboy-logo-2026-10-02.jpg` is the user's finished artwork, retained as an
unmodified source file. It combines the interlocking AB monogram, directional
pad and action buttons with the AetherBoy wordmark and violet/cyan frame.

The user's 2026-10-03 request authorizes transparent, recolored theme derivatives.
Keep the original JPEG unchanged. Retain the AB/D-pad/buttons, custom wordmark
and rounded frame; do not substitute a different logo. At small system-icon
sizes the wordmark naturally loses detail; do not silently substitute the old
mark. The original Aether Wave SVG files remain as historical design sources,
but no longer generate the active app logo.

Windows and Linux use the same six transparent 512 px PNG exports in the UI and
standard start animation. Their accents select Aether Original, Neko Sakura,
Deep Ocean, Emerald Circuit, Amber Arcade or Pocket Light. Pocket Light uses
dark green/bronze for the light surface. Theme changes refresh live logos without
restarting. The settings card specifically previewing Aether Original stays original.

Custom accent pairs use the transparent Aether Original fallback; changing only
the background retains the matching accent variant. No user color is overwritten.
Custom intro images/sounds remain independent and are not recolored. Windows ICO,
Linux launcher PNGs and window icons keep the original JPEG-derived identity.
The intro still has no duplicate wordmark or visible skip button.

## Core palette

| Name | Hex | Use |
| --- | --- | --- |
| Void | `#050712` | Icon tile and primary dark surface |
| Aether violet | `#8B38FF` | Wave origin and focus accent |
| Pulse violet | `#A942F5` | Transition midpoint |
| Signal cyan | `#29E2ED` | Wave destination and active state |

The gradient belongs to the brand mark and high-value focus states. It should
not be repeated on every UI surface or control.

## Source and exports

- `aetherboy-logo-2026-10-02.jpg`: current, user-supplied master artwork.
- `aetherboy-mark.svg`, `aetherboy-mark-small.svg`: historical Aether Wave sources.
- `exports/`: PNG exports at 16–512 px, keeping the complete artwork.
- `nanoboy/Branding/AetherBoy.ico`: generated multi-resolution Windows icon.
- `nanoboy/Branding/AetherBoyMark.png`: generated 512 px application artwork.
- `themes/*.png`: six transparent theme masters, edited with the built-in image
  tool from the supplied logo. These derivatives are not pixel-identical copies
  of the JPEG. No image generation occurs during builds.
- `themes/generation.json`: exact edit prompts, palette targets and provenance.
- `exports/themes/*.png`: the same 512 px RGBA files for both frontends.
- `exports/themes/64/` and `exports/themes/128/`: prefiltered Linux header sizes;
  SDL selects a texture by the actual display size, including render scaling.

Run `node tools/branding/render-brand-assets.cjs` with `sharp` available to
rebuild the original system icons from the JPEG and resize the checked-in theme
PNGs without recoloring, flattening or removing alpha. No remote attachment path
or image-generation access is needed after cloning the repository.

`node tools/branding/render-theme-preview.cjs` renders the labeled contact sheet
`exports/theme-overview.png` on theme surfaces for review. That sheet is not a
transparent application asset; use the individual PNGs when embedding a logo.
