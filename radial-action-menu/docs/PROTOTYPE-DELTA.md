# Prototype delta: Radial Action Menu

Prototype: Control Gallery sheet 8. Staged control before this pass: a CRT terminal look (phosphor green, IBM Plex
Mono, square bordered items with Segoe glyphs), elastic spin-in animation, 90 px radius, three menus on the page.

## Design

- Typeface: Archivo 400/600 for UI, Archivo Bold Expanded (700, width 112) for the title (was IBM Plex Mono).
- Palette (Light, exact): ground #DCD9D2, surface #ECEAE5, chip #FBFAF7, ink #252422, muted #55524C, hairline
  #BDB8AE, guide #8E897F, disc edge #7F7A70, avatar #CFCBC2, accent #CC4A14, scrim rgba(37,36,34,.34).
- Trigger: 64 px round accent button, white 26 px plus at 1.75 stroke, shadow 0 10 22 -10 rust 70%, focus ring 2 px
  ink 3 px out, 24 px from both edges.
- Discs: 52 px (56 touch), chip fill, 1 px #7F7A70 edge, 22 px Tabler icons, shadow 0 8 16 -10; hover white; pressed
  or slide-highlighted ink with a chip icon; disabled 40% (badge 50%).
- Labels: 30 px pill, padding 0 6 0 10, 8 px radius, 13 px SemiBold, key badge 20 px, 5 px radius, 11 px SemiBold;
  focus inverts the pill. Touch layout: badge at the disc's top-left (-5, -5).
- Readout: 40 px, 12 px radius, ink, "key Name · release to open" 15 px. Hint: "Quick actions Space", 12 px SemiBold,
  ink, 8 px radius.
- Dotted guide arc: 1.5 px, 1 px dots every 8 px.

## Behaviour

- Up to five items on a 90° arc, 22.5° apart, radius 208 (172 touch); mirrors on the left edge.
- Open: 320 ms cubic-bezier(.2,.9,.3,1.04) from the trigger at scale .4, 30 ms stagger, 160 ms fade. Close: 200 ms
  cubic-bezier(.4,0,1,1), 20 ms reverse stagger, 140 ms fade. Scrim 220 ms, guide 200 ms, plus 45° in 240 ms.
- Keys: Space opens (focus to the first item), 1–5 pick, arrows move (up/right next, down/left previous, wrapping),
  Esc closes; focus returns to the trigger.
- Touch: press the trigger and slide; the item under the finger highlights and the readout names it; release picks.
- Scrim tap closes without an action.

## Logic and states

- `IsOpen`, `IsHintVisible`, `HighlightedIndex`, `ShowLabels`, `Position`, per-item `IsEnabled`; menu groups
  OpenStates, HintStates, SlideStates; item groups CommonStates, KeyboardFocusStates, LabelStates, HighlightStates.

## Not adopted

- The sheet's figures, spec table and state cards: review presentation; states are Hot Design previews.
- The 1.04 overshoot in the open curve: a KeySpline cannot exceed 1; the curve is clamped to (.2,.9,.3,1).
- Blurred box shadows: drawn as radial-gradient ellipses; the Toolkit ShadowContainer left stale pixels on Skia when
  an item's focus look changed.
- The selected-row gradient (6% ink fading right): a flat 5% wash; the gradient banded on Skia. It is demo chrome.
- "Frame at 120 ms" (Fig. 2b) is a still of the animation; the preview plays the animation instead.
- Fig. 2c's smaller 44 px discs and 52 px trigger are just the card's scale; they are reachable through `DiscSize`,
  `Radius` and `TriggerSize` (the left-edge preview uses them).
