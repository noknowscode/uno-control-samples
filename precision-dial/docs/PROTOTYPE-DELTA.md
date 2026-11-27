# Prototype delta: Precision Dial

> **Superseded in part (single-control pass):** the sample no longer ships `PrecisionDialChannel` or the tabs this
> delta mentions. It is now one `PrecisionDial` with a Volume / Menu switch; the continuous ring (`ArcStyle`), the
> palette, the fonts and the keyboard behaviour below still apply to that control. See the README.

Prototype: Control Gallery sheet 03 "Precision Dial" (brushed-metal knobs with a brass arc that warms to copper).
Scope: the control's design, behaviour and states; the sheet's layout (title, spec table, figure labels, phone
frame, states grid) is review presentation and is not rebuilt. The other tabs (menu dial, studio, audio, square)
are kept as they are.

## Design

- Palette (Dark is the sheet; exact): ground `#121212`, panel `#171717` with a radial `#1B1B1B → #141414`, hairline
  `#2A2A2A`, ink `#E8E6E1`, muted `#9A9893`, control outline `#5E5E60`, brass accent `#D9A441`, hot copper `#E2703A`,
  unlit digits `#4A4A4C`, unlit segments `#2C2C2E`, arc track `#262628`, major ticks `#4A4A4C`, minor ticks `#333335`.
  Light is derived (warm paper stage, deeper brass `#9A6A12` and copper `#B4501E` for contrast); the knob stays dark
  metal in both themes.
- Type: Barlow 400/600 for UI text, Barlow Condensed 500 uppercase for titles ("OUTPUT GAIN"), IBM Plex Mono for
  labels (11 px, uppercase), digits and chips (the sheet names IBM Plex Mono for `--font-mono` without bundling it;
  it is bundled here from the repo's OFL copy).
- Ring: a continuous 2 px arc at 0.355 × size over a 2 px `#262628` track, with an 8 px glow at 16 % (20 % for copper);
  44 ticks outside it (every fourth longer and lighter), lit in brass up to the value; above 80 the arc, ticks, digits
  and segments turn copper. A value tip (`62.0`) at 0.48 × size in the phone layout.
- Readout: three-digit mono value, unlit leading zeros `#4A4A4C`, one decimal when the value has one (`062.4`); 24
  segments 2 px tall, 3 px apart, lit with the value (copper past 80 %).
- Channel chrome: 11 px mono label ("STANDARD · 200"), chips 24 px radius 12 with a 1 px border ("FINE ×0.1",
  "MAX · 100"), a lock and the preset name when disabled. Phone: "CHANNEL 2" overline, "OUTPUT GAIN" title, a FINE
  toggle (44 px, radius 16), the knob with its tip, and 56 px − / + steppers.

## Behaviour

- Drag up or down: 0.5 per px, rounded to 1; Shift (or Fine) 0.05 per px rounded to 0.1. Keys when focused: ↑ → +1,
  ↓ ← −1 (0.1 with Shift or Fine), PgUp / PgDn ±10, Home / End to 0 / 100. Keyboard and stepper changes animate the
  knob (the existing spring); the readout follows the value.
- Focus: a 2 px ink ring 2 px outside the dial.
- Arc, pointer, digits and segments show one value.
- Accessibility: the channel is a slider for automation (value, range, small change 1, large change 10); the steppers
  and the FINE toggle are named.

## Logic and states

- `PrecisionDialChannel` (new) composes the dial, readout, segments, label and chips, with VisualState groups for
  focus (Focused / Unfocused), value (Normal / Fine / Max), enabled (Enabled / Disabled) and layout (steppers,
  header).
- Fine: `IsFineMode` (toggle or Shift) steps by 0.1 and shows the "FINE ×0.1" chip.
- Hot range: `HotThreshold` (80) turns arc, ticks, digits and segments copper; at the maximum the "MAX · 100" chip
  shows.
- Disabled: `IsEnabled = false` dims everything, is not focusable and shows `DisabledReason` with a lock
  ("Preset "Broadcast"").
- `PrecisionDial` gets `ArcStyle` (Dashed, the existing look, or Continuous, the sheet's), `HotThreshold`, `HotBrush`,
  `ValueStep`, `IsFineMode`, `FineStep` and `ShowValueTip`.

## Not adopted

- Letter spacing on labels (.16em) and titles: `CharacterSpacing` is a no-op on the Uno Skia text stack.
- The sheet's 180 ms ease-out on the shown number: the knob keeps the control's spring; the digits change at once.
- Circular drag on phones is the existing angular drag (outer ring), unchanged.
