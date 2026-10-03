# AetherBoy visual identity

## Current logo — approved 2026-10-02

`aetherboy-logo-2026-10-02.jpg` is the user's finished artwork, retained as an
unmodified source file. It combines the interlocking AB monogram, directional
pad and action buttons with the AetherBoy wordmark and violet/cyan frame.

Use this exact composition: no AI regeneration, cropping, recoloring or new
typography. PNG and ICO exports only resize and encode it. At small system-icon
sizes the wordmark naturally loses detail; do not silently substitute the old
mark. The original Aether Wave SVG files remain as historical design sources,
but no longer generate the active app logo.

Windows and Linux use the same 512 px image in their UI and start animation.
Windows also embeds the multi-resolution ICO; Linux installation uses the PNG
icon sizes. The intro does not draw a duplicate wordmark or a visible skip button.
Existing custom intro images/sounds and all six themes remain independent of
this bundled logo. The artwork keeps its own colors, including on light themes.

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

Run `node tools/branding/render-brand-assets.cjs` with `sharp` available to
rebuild every raster asset from the approved JPEG. No remote attachment path is
needed after cloning the repository.
