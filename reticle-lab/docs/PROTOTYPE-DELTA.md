# Reticle Loader: prototype delta

Prototype: Control Gallery sheet 4, "Reticle Loader". Compared with the staged sample before this pass (a keyed
`ProgressRing` restyle with a 600-unit HUD in IBM Plex Mono).

## Design

- Typeface: JetBrains Mono 300 / 400 / 500 everywhere (was IBM Plex Mono). Percent 22 units Light, phase 7.5, telemetry
  6.5, status 13 px Medium, detail 12 px.
- Palette (Dark, exact): ground `#050505`, loader panel `#000000`, surface `#0B0B0B`, ink `#F2F2F2`, muted `#8C8C8C`,
  line `#1E1E1E`, control outline `#3C3C3C`, accent `#F2F2F2` (options `#D4F23F`, `#7FD8F2` kept as Tertiary and
  Secondary). Light derived: ground `#F7F7F7`, panel `#FFFFFF`, ink `#111111`, muted `#5E5E5E`.
- Geometry: the prototype's 300-unit SVG, converted path by path: band A (r 128 arcs, 46 ticks), band B (r 118, a 4-unit
  bar, 37 ticks), rings r 94 / 88 / 104 (track at 14%), the X, corner marks, two leaders with dot pairs.
- Panel: 1 px `line` border, radius 6, padding 24. Pills 36 px, radius 18, 14 px sides, 12 px label; checked = ink
  plate. Compact rings 64 / 40 / 24 px: track r 30.9 at 35% plus a 4.5 / 2.8 / 1.7 px arc.

## Behaviour

- Scan: band A 14 s, band B 9 s reverse, linear; the sweep arc turns in 2.4 s on cubic-bezier(.45,.05,.55,.95);
  status dots pulse 1.2 s, staggered 0.2 s.
- Lock: bands stop; the arc fills with the percent (3.599° per %), a node rides its head.
- Done (Locked): the X closes to scale .42, 45°, in 480 ms on cubic-bezier(.3,.7,.2,1); the centre dot turns accent;
  nothing moves after.
- Idle: the HUD dims to 42% in 300 ms. Fades 300 ms.
- Reduced motion: Scan is still, the sweep pulses 1 → .35 → 1 over 2 s, the dots stop pulsing.
- Sequence (demo): Scan 2.6 s, then Lock in bursts every 200 ms, then Locked; "Run again" restarts it.
- Status line announced politely; the loader reports as a progress bar with the prototype's value text.

## Logic and states

- New `State`: Scanning, Locking, Locked, Idle, Interrupted (keeps its progress, arc in ink at 45%, offers Resume).
- Order: percent, phase, telemetry. Percent shows "—" while the size is unknown.
- Three status dots step with the phase (second one at 50%).

## Not adopted

- Letter-spacing (.14em captions, .02em title): `CharacterSpacing` is a no-op on Skia; labels are authored uppercase.
- Bands snapping to 0° when Scan ends (CSS animation removal): they ease home in 480 ms on the X's curve instead, so
  the switch does not jump.
- The phone mock-up's workspace file list and Cancel button: presentation around the control; the demo page stays a
  minimal host.
