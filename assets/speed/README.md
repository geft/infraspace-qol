# Speed icon artwork

Generated with the built-in imagegen tool, then downsampled to 256 × 256 PNG masks.

- `speed-4x.png`: three arrowheads for Very Fast.
- `speed-8x.png`: four arrowheads for Superspeed.

`SpeedPatches` reads mask luminance as alpha, removes the black background during sprite creation, and supplies the toolbar's blue, white, selected, and disabled colors. Each image is embedded in the DLL; the installer requires no separate asset directory.

The renderer centers the glyph inside transparent button padding, at up to 45% of the button height and 80% of its width. The sprite keeps its full canvas rectangle so the glyph cannot stretch to fill the clickable target. `preview.png` is rendered through the same pixel-sizing method as the in-game sprites, using 80-pixel button targets.

## Final generation prompts

The following prompt was used twice, with `{count}` replaced by `3` for 4x and `4` for 8x. Built-in mode was used, with `transparent_background: false`; these are black-and-white masks rather than finished colored sprites.

> A single flat vector-style game UI icon mask. Exactly {count} right-pointing white triangular arrowheads, evenly spaced horizontally on solid pure BLACK (#000000) background. Pure WHITE (#FFFFFF) solid fill shapes only. Each arrow is tall and narrow: height is THREE times width. Equal arrow sizes, identical straight clean geometric edges, no outline. Whole group centered, fills 75% of square canvas. Perfect crisp minimalist geometric graphic suitable for a tiny game toolbar. No texture, no brush strokes, no distressed effects, no noise, no stray fragments, no glow, no lighting, no gradients, no drop shadow, no lettering, no numbers, no frame. All space outside the {count} triangular arrowheads is perfectly solid black. Technical black-and-white alpha-mask artwork.
