# Prototype delta: Adaptive Input

Prototype: Control Gallery sheet 9. Staged control before this pass: a `UserControl` with a coloured type badge,
seven detected types (mention, colour incl. keywords, tag, number range, date, URL, e-mail), a picker per type
(HSV colour picker, range slider...), Roboto, a navy palette.

## Design

- Typeface: DM Sans 400/500/600, DM Mono 400/500 (was Roboto).
- Palette (Dark, exact): #131112 ground, #1C191B surface, #221E21 raised, #F1ECEE ink, #A69DA2 muted, #322C30
  hairline, #73686E outline, #FF5D8F accent, #131112 on accent, accent 16% token tint with #FFD3E1 text, #7D747A
  placeholder and past days, #3A3337 avatar. Light derived (accent #C2185B).
- Field 56 px, 18 px radius, focus = accent edge; 17 px text. Suggestion 36 px dashed #73686E, 10 px radius, "Tab" key
  22 px (6 px radius, DM Mono 11). Clear 36 px; add 40 px ink square. Status 12 px muted.
- Picker 300 px, 16 px padding, 26 px radius. Date: QUICK PICKS (140 px, 36 px chips, ink when on) | month (15 px
  SemiBold) and 7×34 px days, today ringed 1.5 px, selected accent. Colour: 96 px swatch (14 px radius), HEX/RGB in
  DM Mono, "Use this colour". People/tags: 40 px rows, 28 px avatar, raised + 2 px accent bar when active.
- Phone: suggestion bar, 44 px chips, hairline above.

## Behaviour

- Recognises dates (today, tomorrow, tmrw, next week, weekdays, "nov 30", optional "3pm"), #RGB/#RRGGBB colours,
  @people, #tags — the prototype's regexes and order.
- Tab or a click accepts; Esc keeps the text; Backspace in the empty field restores the text; Up/Down + Enter for
  people and tags; Enter on plain text submits.
- Token enters in 260 ms (scale .88, 24 px, cubic-bezier(.2,.9,.3,1)).
- Status line and picker announce politely.

## Logic and states

- `State`: Empty, Recognised, Plain, NotRecognised, Accepted; `DetectedType`; `AcceptedToken`.
- Visual state groups: FieldStates, InputStates, PanelStates, SuggestionStates, SuggestionKindStates, TokenStates,
  MatchStates, PlacementStates.
- Not recognised: text that starts like a date but does not parse stays text and offers the picker.

## Not adopted

- The sheet's figures, spec table and state cards; states are Hot Design previews.
- Number ranges, URLs, e-mail, colour keywords and their pickers: the prototype recognises dates, colours, people and
  tags only.
- The accent caret colour: WinUI's `TextBox` has no caret-brush property.
- The phone's "Notes" field and the drawn keyboard zone: page content, not the control.
- The focus ring as a box-shadow: drawn as a 2 px accent border (the padding shrinks by 1 px so nothing moves).
- The sheet's fixed "today" (Nov 25): the control uses the real date; previews set `ReferenceDate` to the sheet's.
