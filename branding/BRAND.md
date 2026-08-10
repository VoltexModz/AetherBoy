# AetherBoy visual identity

## Aether Wave

The AetherBoy mark combines an angular `A`, a dissolving signal wave and a
compact handheld silhouette. The wave communicates translation between real
Game Boy hardware and the deterministic emulator core without copying the
outline of a specific Nintendo device.

The detailed master mark is used at 48 px and above. The compact mark reduces
the particle trail and controls for 16–40 px system surfaces.

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

- `aetherboy-mark.svg`: detailed source for large icon and brand use.
- `aetherboy-mark-small.svg`: optically simplified source for Windows icon sizes.
- `exports/`: generated PNG previews and individual icon frames.
- `nanoboy/Branding/AetherBoy.ico`: generated multi-resolution Windows icon.
- `nanoboy/Branding/AetherBoyMark.png`: generated 512 px application artwork.

Run `node tools/branding/render-brand-assets.cjs` with `sharp` available to
rebuild every raster asset from the SVG sources.
