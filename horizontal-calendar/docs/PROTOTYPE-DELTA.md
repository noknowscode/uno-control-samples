# Prototype delta: Horizontal Calendar

Prototype: Control Gallery sheet 02 "Horizontal Calendar" (a week strip that shows how full each day is, with the
agenda below). Scope: the control's design, behaviour and states; the sheet's layout (title, spec table, figure
labels, phone frame, states grid) is review presentation and is not rebuilt.

## Design

- From a dark glass month strip to a light, flat week card. Palette (Light, exact): ground `#F5F6F8`, card `#FFFFFF`,
  ink (navy) `#0F1B2D`, muted `#556070`, hairline `#DCE0E6`, control outline `#848E9D`, skeleton `#E8EBF0`, hover
  `#EEF1F5`, hatch `#E6E9EE` (hover `#DCE0E6`), accent (cobalt) `#2B59FF` with white text. Dark is derived (navy ground
  `#0B111B`, card `#121A27`, same cobalt).
- Type: Schibsted Grotesk 400/500/600/700 for everything, Azeret Mono 400/500 for weekday labels and the "CLOSED"
  label. Month title 24 px 600 (28 px on phones), day numbers 26 px 600 (20 px on phones), weekday 11 px uppercase
  mono, agenda day 17 px 600, row title 15 px 600, details 13 px.
- Card: 1 px hairline, radius 32, padding 20 / 22 / 14. Header 40 px: month + range on the left; Today (40 px,
  radius 10, outline `#848E9D`) and previous / next week (40 × 40, radius 10) on the right.
- Days: 7 per week, 84 px tall, radius 14, 4 px apart. Selected = cobalt pill with white text; today = 1.5 px navy
  inner ring (a white ring 3 px in when also selected); busy = one 8 × 3 px tick per event (radius 2, 3 px apart, up
  to four); unavailable = 135° hatching, struck-through muted number and a "CLOSED" / "FULL" label; past days have a
  muted number.
- Legend under the strip (Selected, Today, One tick per event, Unavailable), 30 px.
- Agenda: 60 px header (day title + "4 events · 4 h", cobalt "Add event" pill 40 px with a plus icon), rows 54 px
  with a top hairline: time (15 px 600 + 11 px AM/PM) and duration, title and detail, location, a 40 px "More
  options" button.
- Phone (control under 600 px wide): no card chrome, month name 28 px with the range below, 44 px week buttons, no
  Today button and no legend, 66 px days, 64 px rows with the location under the title (video icon for online
  places, a pin otherwise), and a full-width 48 px "Add event" button docked at the bottom.
- Sample data: the sheet's three weeks of events (standups, client strategy call, team lunch, code review, hockey...),
  "Office closed" on Friday 27 and "Booked out" on December 1, anchored to the current week.

## Behaviour

- The selection pill slides 380 ms between days; the strip slides 460 ms between weeks; both on
  `cubic-bezier(.3,.7,.2,1)` (KeySpline `0.3,0.7 0.2,1`). The agenda fades in (220 ms) and rises 6 px (320 ms) 80 ms
  after a day change. Hover and colour changes are 200 ms.
- Keys on the strip: ← → previous / next selectable day (skipping unavailable days, crossing weeks), PgUp / PgDn a week,
  T today. Previous / next week buttons page the week; Today selects today.
- Unavailable days are focusable (they say why) but cannot be selected.
- Accessibility: each day is named "Thursday, November 26, today, 4 events" or "Friday, November 27, Office closed, not
  bookable"; the range is a polite live region; the error message is announced (assertive); "More options", Retry and
  the week buttons are named.

## Logic and states

- `AgendaState` = Ready / Loading / Error (VisualStates); the empty day is a state of its own when a Ready day has no
  events. Loading keeps the dates usable, hides the ticks and shows three skeleton rows that hold their height. Error
  replaces only the agenda ("Events didn’t load", "Your selected day is kept.", Retry raising `RetryRequested`). Empty
  shows "Nothing on Saturday", "The day is open. Add an event or pick another day." and an outline "Add event".
- `UnavailableDays` (date → label and reason), `MinDate` / `MaxDate` (paging stops there), `Today`.

## Not adopted

- Letter spacing on weekday labels (.08em) and negative tracking on numbers: `CharacterSpacing` is a no-op on the Uno
  Skia text stack.
- Tabular figures (`font-variant-numeric`): no OpenType feature control on Skia text.
- The `data-tip` tooltip on unavailable days is kept as a tooltip with the reason; no hover-delay tuning.
