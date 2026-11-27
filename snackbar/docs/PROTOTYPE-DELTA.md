# Prototype delta: Snackbar

Prototype: Control Gallery sheet 7 ("Snackbar"). Staged control before this pass: Material 3 snackbar (purple
palette, Roboto, 4 px radius, 150/75 ms M3 motion, host-owned `Task.Delay` timer, no pause, `DefaultDuration`).

## Design

- Typeface: Hanken Grotesk 400/500/600 for UI and message text, Fraunces 500 for the list title (was Roboto).
- Palette (Light, exact): page #FAF7F0, surface #FFFFFF, ink #161514, muted #5E5A52, hairline #E2DCCF, control
  outline #8C8578; slip #161514, slip text #F6F2EA, close icon #A9A296, track #2E2C29, drain #6E685E, action #F5C542.
  Dark derived: ground #141311, surface #1D1B18, slip lifted to #2B2925 with a #3A3631 hairline, gold action kept.
- Slip: radius 12 (was 4), min height 52 (60 below 600 px), padding 0 8 0 16, 12 px gaps, max width 460 docked
  20 px from the bottom and sides (was 344-672, 8 px).
- Message 15/21 px, wraps at about 44 characters (370 px). Action 15 px SemiBold, 36 px tall, radius 18, padding 0 12,
  hover = action colour at 12%. Close 36×36, radius 18, #A9A296, hover = slip text colour. Press scale .96.
- Countdown track: 2 px, inset 16 px, #2E2C29 with a #6E685E drain (new).
- Attention: Tabler warning icon (18 px, 1.75 stroke) in the action colour; two-row layout 14 8 10 16 with the action
  right-aligned below (new).

## Behaviour

- Motion: rise in 220 ms (opacity + 12 px, cubic-bezier(.2,.8,.2,1)), fade out 160 ms (ease) (was 150/75 ms, 80 px).
- Replace: a new message waits for the current one's 160 ms exit, never stacks (same semantics, now exposed as
  `IsReplacing` and a `Replacing` visual state).
- Pause on hover, keyboard focus (anywhere inside) or touch; resume on blur. The track stops where it is.
- Announce: role=status, polite (was Assertive): `LiveSetting="Polite"` on the control, `LiveRegionChanged` on show.
- Kept: Escape and swipe to dismiss.

## Logic and states

- `Kind`: Confirm (4 s, no action), Undo (7 s, one action), Attention (indefinite, icon, close, action below).
  `Duration` null means the kind's default (replaces the host's `DefaultDuration`).
- Long message (> `MultiLineThreshold`, 44 chars) switches to the two-row layout with the action below.
- Held: `IsHeld` (set by hover/focus/touch, or by the app). `Progress` shows a part-drained track.
- Visual state groups: LayoutStates, ActionStates, DismissStates, IconStates, TimerStates, HoldStates; host
  DisplayStates (Hidden, Visible, Replacing).

## Not adopted

- The sheet's figures, spec table, "By purpose" column and states grid: review presentation, not the control.
  States are Hot Design previews.
- Row collapse animation (240 ms grid-rows): the demo list uses `RepositionThemeTransition`; it is demo chrome.
- Fraunces `SOFT 50` on the sheet title: only the sheet header used it.
- Reduced-motion variant (static half track): the sample has no reduced-motion setting.
- Phone toolbar: the prototype phone shows no Save/Export; the demo keeps them as icon-only buttons so all three
  kinds stay reachable at 390 px.
