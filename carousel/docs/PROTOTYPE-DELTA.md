# Prototype delta: Carousel

Prototype: Control Gallery sheet 01 "Carousel" (plates that advance on a timer, captions kept off the image).
Scope: the control's design, behaviour and states. The sheet's own layout (title, spec table, figure labels, phone
frame, states grid) is review presentation and is not rebuilt.

## Design

- Palette (Light, exact): ground `#ECE7DE`, card surface `#F4EFE7`, mat `#F8F4EC`, mat ring `#DDD3C3`, ink `#1D1B18`,
  muted `#5F594F`, hairline `#CFC6B8`, control outline `#857D6E`, skeleton `#DDD4C5`, image placeholder and pressed
  fill `#E4DACA`, accent (index dot) rust `#B5552F`. Dark is derived (warm charcoal ground `#1A1815`, mat `#2A2722`).
- Type: Instrument Serif 400 for titles (34 px, line height 1.06) and the narrow header (30 px); Instrument Sans
  400/500/600 for UI (13 px body, line height 1.55; 12 px meta and labels).
- Items are mounted plates: 14 px mat (`#F8F4EC`), 1 px ring `#DDD3C3`, radius 4 (image radius 2), a soft shadow
  `0 18 32 -24 rgba(70,48,28,.45)`. Images fill the mat (cover / UniformToFill).
- Track with a peek: plates sit in a row with a 24 px gap and the next plate peeks in (52 px on desktop, 24 px on
  phones with a 14 px gap). Inactive plates are at 72 % opacity.
- Caption beside the track (side panel 320 px, 44 px gap): `PLATE 02 OF 05` label (12 px, uppercase, number in 600
  ink), then title, description and medium, then a hairline and the thumbnail strip with the transport row.
- Thumbnails: 44 × 44 (56 × 48 on phones), radius 4, 62 % opacity, 100 % on hover; the current one gets a 2 px ink
  ring 2 px out. A 2 px progress bar under each: done = full ink, current = fills over the interval, later = empty.
- Transport: three 44 px round outlined buttons (outline `#857D6E`, hover fill = mat, pressed `#E4DACA`, press scale
  0.96), 20 px stroked icons (1.75), then a status text `Auto · 6 s` / `Held` / `Paused`.
- Narrow (control under 720 px wide, Fig. 3): header row (serif title + `02 / 05`), the track, the caption below
  (at least 84 px), the thumbnail strip; no transport row (swipe and thumbnails).
- Sample data: the five "Low Tide Series" plates (Dune Field, Morning; Coast at Low Tide; Ridge, Evening; Salt
  Marsh; Headland at Noon) with their captions; the plates are PNGs rendered from the prototype's own SVGs.

## Behaviour

- Auto-advance every 6 s (`AutoPlayInterval`); holds while the pointer is over the carousel or focus is inside it,
  and resumes with the remaining time. Space toggles pause (`IsPaused`). The current thumbnail's bar shows progress.
- Motion: the track glides 700 ms on `cubic-bezier(.22,1,.36,1)` (KeySpline `0.22,1 0.36,1`), no overshoot; plate
  opacity follows on the same curve. Captions cross-fade: old out over 360 ms, new in after 160 ms (opacity 360 ms,
  6 px rise over 480 ms).
- Keys: ← → move (↑ ↓ when vertical), Home / End, Space pauses. Focus ring 2 px ink, 6 px out.
- Touch: swipe moves the track with the finger; past 40 px (or a flick) it advances, otherwise it settles back.
- Accessibility: the position label is a polite live region; buttons are named ("Previous plate", "Pause
  slideshow"...); thumbnails are named "Plate n: title".

## Logic and states

- New `CarouselPlate` control with `State` = Ready / Loading / Failed (VisualStates): Loading shows a spinner on the
  placeholder and skeleton bars for the caption (sizes kept); Failed shows "Image unavailable" and a "Try again"
  button (raises `RetryRequested` and reloads); the state follows the image's load events automatically.
- Long titles: two lines at most, then an ellipsis (caption styles and the plate caption).
- `PlaybackState` (Off, Playing, Held, Paused) is exposed read-only; the empty carousel keeps its empty state.

## Not adopted

- Paper grain overlay (SVG turbulence filter): a full-surface texture costs a composited layer every frame on Skia
  and isn't part of the control.
- Letter spacing on uppercase labels (.12em): `CharacterSpacing` is a no-op on the Uno Skia text stack; the value is
  set in the template so platforms that honour it get it.
- `saturate(.8)` filter on inactive plates: no colour-matrix filter on XAML elements; the 72 % opacity is kept.
- `text-wrap: balance` on titles: no XAML equivalent.
