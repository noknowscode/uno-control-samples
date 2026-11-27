# Sales Heatmap: prototype delta

Prototype: Control Gallery sheet 6, "Sales Heatmap". Compared with the staged sample before this pass (a 400-wide
UserControl with a random 12 × 8 opacity grid, monthly and yearly totals and three regions, scaled by a Viewbox).

## Design

- A calendar heatmap: 14 weeks × 7 days of Q4 2026, column-major, month labels on top, Mon / Wed / Fri at the left.
  30 px cells, 6 px gap, radius 7 (compact: the pitch fills the width, 23 px at 390, radius ≈ 4).
- Five steps of one hue: `#1E4A56` `#236A80` `#2E8FA8` `#4DB8CE` `#8EE3F0` at $4k / $7k / $10k / $13k+, shown in the
  legend with Closed (outline `#3A5058`), No data (hatch `#152024` / `#3A5058`) and Ahead (dashed `#33484F`).
- Typeface: Bricolage Grotesque 500 / 600 (title 21 px, total 38 px, figures 19 px), Figtree 400 / 500 / 600 (text),
  Spline Sans Mono 400 / 500 (11 px labels). Was Roboto.
- Palette (Dark, exact): page `#0F1619`, card `#131D21`, ink `#E8EEF0`, muted `#93A5AB`, line `#24363C`, outline
  `#567079`, accent `#63D4E4`, hover `#1B282D`, detail panel `#18262B` / `#2C434A`, banner `#1C2A2F`, skeleton
  `#1E2C31`, up `#7EDCA4`, down `#FF9A85`. Light derived (pale ground, darker ramp so heat still reads as "more").
- Card radius 32, padding 22; period selector outline radius 12 with radius-8 buttons; detail panel radius 14.

## Behaviour

- Hover a day for a tooltip (date; revenue and orders, or Closed / No data / Not yet); click to select (2 px ink ring,
  2 px gap). Keys on the focused grid: ↑ ↓ a day, ← → a week, skipping empty and future days; Esc hides the tooltip.
- Periods Oct / Nov / Dec / Q4: other months dim to 22% (250 ms) but stay in place; totals, change and regions follow.
- Deltas: arrow, sign and colour together (▲ +23.4% vs Oct 1–25).
- The selected day's panel is announced politely.

## Logic and states

- New `State`: Loading (grid outlines, "$—", skeleton bars; everything keeps its size), Ready, Empty ("No sales
  recorded" in place with Clear filters, the filter in the header, figures "—", never zeros), Stale (banner "Showing
  data from 6:40 PM" with Retry above the last good grid).
- Data from the prototype's generator (`SalesSnapshot.Q4Sample`): today Nov 25, Oct 12 closed, Oct 20–21 no data.
- Compact layout below 860 px of card width: title + "Updated" time, full-width periods, figures, grid, day, regions.

## Not adopted

- Letter-spacing on labels (.08em): no-op on Skia; labels are authored uppercase.
- The old live feed (a new random snapshot every 2 s): the prototype's data is a fixed quarter; the model loads it once.
