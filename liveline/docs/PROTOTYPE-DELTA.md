# Liveline: prototype delta

Prototype: Control Gallery sheet 5, "Liveline". Compared with the staged sample before this pass (a bare SkiaSharp
chart with a smooth line, gradient fill, badge and lerp easing, 150 ms random walk).

## Design

- The control is now the whole card: title, value with unit and change, status chip, window selector, pause, chart,
  and the Min / Avg / Max / Window / Samples row.
- Typeface: Geist 400 / 500 / 600 (text, 44 px SemiBold value), Geist Mono 400 / 500 (axis 11 px, change 13 px,
  tag 12 px, stats 14 px). Was Segoe UI with fallback.
- Palette (Light, exact): page `#EEF2EC`, card `#F8FAF6`, ink `#11251B`, muted `#4E6356`, line `#D2DBD0`, grid
  `#DCE4DA`, control outline `#7E8F83`, accent `#11805A`, hover `#E4EBE2`. Dark derived on the same green-grey ladder.
- Card radius 24, padding 20 / 24 / 16, 1 px line border. Chip 30 px (radius 15), segments 36 px outline with 28 px
  radius-14 buttons, pause 36 px circle; phone: 40 px segments, 48 px pause.
- Chart: straight 2 px line, area at 7% accent, grid lines with left labels ("70 ms" on top), dashed alert line at 65
  (ink 55%, dash 3 5) labelled "Alert above 65", dashed "now" line (ink 22%, dash 2 3), live dot r 5 with a 2 px card
  ring, value tag 56 × 24 radius 12 in ink with card-coloured mono text, 64 px reserved on the right, 24 px below.

## Behaviour

- One sample a second; the line glides between samples (1000 ms, linear): the plot trails the newest sample by one
  interval and catches up, the live dot moves to the new value. Frames run only during the glide (33 ms timer).
- Axis fixed 40–70; time labels count back from now (−1 min, −45 s … now; 3 labels in the compact layout).
- Inspect: hover, or focus the plot and use ← → (Home / End), Esc closes; crosshair, dot and an ink tooltip with the
  value and how long ago.
- Pause stops at the next sample and shows the time ("Paused · 6:52:14 PM"); Resume continues.
- Window 1 / 5 / 15 min: the plot, change and stats use that span.
- Reduced motion: the line steps once a second instead of gliding.

## Logic and states

- New `State`: Connecting (axes and tag space reserved, "Waiting for the first value", outlined "—" tag), Live,
  Pausing, Paused (line and tag grey out), Disconnected (line stops, the gap since is hatched, hollow dot, Reconnect).
- Compact layout below 560 px of card width: chart first, controls low, Min / Avg / Max only.

## Not adopted

- Letter-spacing on the value (−.03em): no-op on Skia.
- The old easing engine, momentum dot, markers, forecast and gradient fill: not in the prototype; removed with their
  properties (see the README's API changes).
