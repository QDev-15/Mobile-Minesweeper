# Dò Mìn — Redesign Pass 2, Design Spec
**Date:** 2026-10-19 · **Status:** design-only, for owner review before any app code changes · **Scope:** this document only — no `.xaml`/`.cs`/`.csproj` under `src/Minesweeper` was touched to produce it.

This spec evolves the 2026-10-18 redesign (dark/light split, card UI, currently live and crash-free) rather than replacing it wholesale. Where a section proposes a real departure, it's labeled so explicitly — see §7.

---

## 0. Reference basis

No web access was used this round. Reasoning is from the following characterizations only (no specific screenshot or current-build claim):

| Reference | What it's standing in for here |
|---|---|
| **Two Dots** | Soft, desaturated fills over saturated blocks; small satisfying micro-animation exactly at the moment of a successful match/action, not everywhere. |
| **Monument Valley** | Restrained palette, generous whitespace, decoration never competing with the thing you're trying to read. |
| **Modern Sudoku.com** | A numeric grid stays legible because cell geometry is crisp and boring on purpose — grid chrome doesn't try to be interesting. |
| **Wordle / NYT Games family** | Confident, slightly oversized type; one accent color doing almost all of the expressive work; a shareable result as a first-class, designed artifact, not an afterthought share-sheet dump. |
| **2026 mobile puzzle-UI consensus** (persona brief) | Touch-first targets, zero-friction onboarding, instant feedback, at-a-glance contrast, light/dark/high-contrast coverage, configurability, system-wide consistency. |
| **Cautionary case: isometric-3D Minesweeper reskin** | The standing reason this spec repeatedly chooses restraint over decoration when the two trade off against board/HUD legibility. |

---

## 1. Design tokens — evolving, not replacing

Every color/spacing/radius value below traces to a named resource. Current tokens (`Resources/Styles/Colors.Light.xaml` / `Colors.Dark.xaml`) are kept; additions are called out as **NEW**.

### 1.1 Color

**Kept as-is:** `Primary`, `Accent`, `Danger`, `MilkBackground`, `SurfaceTonal`, `HairLine`, `InkSoft`, `OffBlack`, `CellHidden`, `CellHiddenPressed`, `CellRevealed`, `CellMine`, `Number1–8`.

**NEW tokens proposed** (one per theme file, light+dark values, following the existing "same key set, re-tuned per theme" pattern):

| Token | Purpose | Why it's needed |
|---|---|---|
| `PrimarySoft` | Tonal (~14% tint) fill for large surfaces (Daily Challenge card, Resume card background) | Two Dots principle: large fills should be soft, not saturated; `Primary` itself stays reserved for buttons/links/icons, i.e. things you *act on*. Separates "this is a surface" from "this is interactive" purely by saturation, which also helps colorblind legibility since it's not the only signal (shape/icon still carries meaning). |
| `AccentSoft` | Tonal fill counterpart for Accent-colored surfaces (e.g. hint-highlight glow, level-unlock pulse backing) | Same reasoning, for warm accent surfaces. |
| `Overlay` | Formalizes the scrim currently hardcoded as `#CC15181D` inline in `GamePage.xaml`'s `ResultOverlay` | No design value is currently a floating literal except this one; pulling it into a token is a pure cleanup, themed separately so dark mode doesn't double-darken. |
| `Success` — **rejected, not added** | — | Considered for win-specific color, deliberately **not** added. A win is already told via Accent-colored stars, title copy, and motion (§2). Adding a second "positive" hue on top of Accent (and next to `Number2`'s green) dilutes the "one accent doing most of the expressive work" move borrowed from Wordle/NYT. Restraint call — see §7. |

### 1.2 Type

Kept exactly: `OpenSansRegular` / `OpenSansSemibold` (no new font — persona constraint: no web fonts beyond what's bundled). Refinement, not addition:

- Establish explicit *roles* instead of ad-hoc per-page `FontSize`: **Display** 28sp Semibold (page H1, e.g. "Dò Mìn" wordmark), **Title** 19–22sp Semibold (page headers, result title), **Body** 15sp Regular, **Caption** 12–13sp Regular/InkSoft, **Numeral** 15–18sp Semibold reserved for anything counting (timer, mine counter, board digits, stars) — Semibold-for-numbers is already true for board digits today; extend it consistently to the HUD's timer/mine-counter labels (currently already Semibold — keep) and to any new numeral UI this spec adds (hint counter, share-card stats).
- Fix each numeral label's layout to a stable minimum width so digits don't jitter the row as they change width (e.g. timer going 0:09 → 0:10). Layout-only, no new token.

### 1.3 Spacing & radius scale

Formalizing what's already mostly true in the shipped XAML, so new screens/popups don't invent their own numbers:

- **Spacing scale:** 4 / 8 / 12 / 16 / 20 / 24 (dp). Icon-to-label gaps use 4–8; stacked card content uses 8–12; card internal padding stays 16 (`Card` style, unchanged); page horizontal margin stays 20 (`HomePage`/`AboutPage` already do this); section-to-section spacing uses 24.
- **Radius scale:** `RadiusSm` 10 (level tiles — already this value, now named), `RadiusMd` 16 (`Card` style — unchanged), `RadiusPill` 20–22 (buttons — unchanged). Board cells stay at radius 3, deliberately **not** increased — this is the Sudoku.com reference point: a puzzle grid reads as a grid because the cells are crisp near-squares, not rounded chips. Over-rounding cells is explicitly rejected here for the same reason the isometric reskin is the cautionary tale: decoration competing with grid legibility.

### 1.4 Theming: light, dark, and high-contrast

- Light/dark: already shipped (2026-10-18), stays as the base.
- **NEW, proposed:** a third `Colors.HighContrast.xaml` (same key set again) — pure black/white base, `CellHidden`/`CellRevealed` pushed to near-maximum luminance separation, `Number1–8` re-picked from a colorblind-safe set (the persona brief calls high-contrast table stakes now, and this app doesn't have one yet). Needs a manual theme picker in `SettingsPage` (today the app only follows `RequestedTheme` automatically, with no in-app override) — **this is new UI, not just new colors**, flagged in §6.

---

## 2. Motion system — "mượt mà không cứng nhắc"

Nothing in the app currently animates — confirmed by code search: no `.FadeTo`/`.ScaleTo`/`.TranslateTo`/CommunityToolkit animation helper calls exist anywhere in `src/`. Every state change today (cell reveal, flag, popup open, switch toggle) is an instant property snap. This section is therefore a genuine addition across the whole app, not a tune-up — see §7.

### 2.1 Ground rules

- **MAUI's `VisualStateManager` does not animate transitions on its own** — it only swaps property values instantly, same as today. Anywhere this spec says "smooth," the implementation is an **explicit animation** (`.FadeTo`/`.ScaleTo`/`.TranslateTo`, awaited and sequenced in code-behind), not a VSM trick. VSM is still fine for *non-motion* state swaps (e.g. a disabled-button dimming) where no one asked for movement.
- **CommunityToolkit.Maui is already a dependency** (`15.0.1`) and already proven working in this app via `TouchBehavior` on board cells. Two of its features are reused rather than reinvented:
  - `TouchBehavior.PressedScale` / `PressedOpacity` — built-in, declarative press feedback, no code needed.
  - Everything else (cascades, popup choreography, win celebration) uses plain MAUI `ViewExtensions` animation calls, because it needs custom sequencing CommunityToolkit's generic behaviors don't give enough control over.
- **Never add a second gesture mechanism to a view that already has `TouchBehavior`.** This already bit the app once (board cells, per `GamePage.xaml.cs`'s own comment). Where this spec recommends adding press feedback to a view that currently uses `TapGestureRecognizer` only (Home cards, Settings rows), the recommendation is to **replace** the `TapGestureRecognizer` with `TouchBehavior` (`Command` = same tap handler, plus `PressedScale`), never to add both.

### 2.2 Interaction → motion table

| Interaction | Trigger | What animates | Duration | Easing | Primitive |
|---|---|---|---|---|---|
| Single cell reveal | `Reveal()` flips one cell from Hidden→Revealed | Cell scales 0.85→1.0 and fades 0→1 as the flat surface appears; `Shadow` drop (already coded) stays instant | 140ms | `Easing.CubicOut` | Explicit `border.ScaleTo`/`FadeTo` in `RefreshAllCells`, per changed cell only (not a full-board redraw) |
| Flood-fill cascade | A 0-cell reveal opens many neighbors at once | Cells pop in the single-cell style above, but staggered by BFS distance from the tapped cell (ripple outward), not all at once | 140ms per cell, ~18ms stagger per BFS ring, **capped at ~10 rings** (~180ms added) then remaining cells snap in together | `Easing.CubicOut` | Sequenced `Task.Delay` + `ScaleTo`/`FadeTo` fired per ring, fire-and-forget past the cap so a 480-cell Hard-board flood doesn't feel sluggish |
| Flag plant | Long-press or flag-mode tap on a hidden cell | 🚩 glyph pops in: scale 0.6→1.15→1.0 | 180ms | `Easing.SpringOut` | `label.ScaleTo` chain; pair with `HapticFeedback.Default.Perform(Click)` — **new**, app has no haptics today |
| Flag remove | Second tap/long-press toggles flag off | Glyph fades + shrinks out before clearing | 120ms | `Easing.CubicIn` | `label.FadeTo`/`ScaleTo` then clear text |
| Mine explosion (loss) | `Reveal()` returns a mine | Tapped mine: quick scale-bounce + red flash; then the rest of the board's mines reveal in an outward ripple from the tapped cell (same stagger idea as cascade, not simultaneous); one subtle board-wide shake (±4dp, 2 cycles) on the *first* mine only — not repeated per mine | Tapped mine 160ms; ripple ~500–700ms total; shake 150ms | `Easing.CubicOut` (reveals), `Easing.BounceOut` (tapped mine only) | Explicit per-cell animation loop, same stagger helper as cascade; shake via `TranslateTo` on `BoardHost` |
| Win celebration | `Status == Won` | Result card entrance gets a soft scale-pulse (1.0→1.05→1.0); star row (Tutorial mode) pops in star-by-star, 80ms stagger each | Card pulse 220ms; stars 140ms each + 80ms stagger | `Easing.SpringOut` | `.ScaleTo` chain on card; `.ScaleTo` per star label. **Deliberately no confetti/particles** — restraint call, same reasoning as the rejected `Success` color (§1.1) and the cautionary-tale note in §0 |
| Popup/dialog enter | Any custom overlay becomes visible (win/lose result today; new popups in §4) | Scrim fades in; card follows ~40ms later with translateY +24dp→0, scale 0.92→1.0, fade 0→1 | Scrim 160ms, card 220ms | `Easing.CubicOut` | `Grid.FadeTo` (scrim) then `Border.TranslateTo`+`.ScaleTo`+`.FadeTo` (card), not simultaneous |
| Popup/dialog exit | Any custom overlay dismissed | Card and scrim fade/scale down together, faster than entrance | 150ms | `Easing.CubicIn` | Same primitives, reversed, awaited before `IsVisible = false` |
| Toggle switch (No-guess) | `Switch.Toggled` | **No custom animation** — the native `Switch` thumb already animates on both platforms; adding a second effect would double up | — | — | None — explicitly a "don't" entry, not an omission |
| Button press feedback (CTAs: "Chơi ngay", "Chơi", Play Again, etc.) | Press/Release | Scale 1.0→0.96 on press, back to 1.0 on release | 60ms down / 90ms up | `Easing.CubicOut` | **Verify against native Android Material ripple first** — MAUI `Button` already gets a platform ripple; if that already reads as "pressed," do not stack a second scale effect on top. Recommend keeping native ripple only, scale reserved for... |
| Card tap feedback (Home cards, Settings rows, level tiles) | Tap | Scale 1.0→0.97 while pressed | 80ms | `Easing.CubicOut` | `TouchBehavior.PressedScale="0.97"`, **replacing** each view's `TapGestureRecognizer` (see §2.1's rule) |
| Level unlock (LevelsPage) | A previously-locked tile becomes unlocked (returning from a win) | Border opacity 0.4→1.0, Accent-colored stroke flashes once then settles to normal | 300ms | `Easing.CubicOut` | Triggered in `OnAppearing` by diffing the previous vs. current `IsNext`/`Unlocked` state per tile |
| LevelsPage first appearance | Grid first renders | Whole grid's tiles fade/scale in with a small stagger, **capped at the first ~30 visible tiles** (not all 100 — a 100-cell stagger would read as slow, not smooth) | 120ms each, ~10ms stagger | `Easing.CubicOut` | Bounded loop over the `CollectionView`'s realized items |

### 2.3 Page-to-page navigation

MAUI Shell already gives a native push/pop slide on Android for free, and this spec does **not** propose replacing it with a custom Shell page-transition handler — that's a disproportionate platform-specific lift for a puzzle app's navigation and risks fighting the OS back-gesture. What *is* proposed: on `GamePage`'s own entrance, fade+scale the HUD and board in together (200ms, `CubicOut`) once `BuildEmptyBoard` finishes, so the page doesn't "pop" its content on top of the Shell's own slide.

---

## 3. Screens

### 3.1 HomePage

- Structural layout (header row, Resume card, Daily card, Tutorial card, "CHƠI THƯỜNG" section, 3 difficulty cards, bottom banner) **stays** — it's already the right shape: real content on first screen, no empty shell, matches the persona's "minimal onboarding friction" point directly.
- Daily Challenge and Resume cards move their fills to `PrimarySoft`/`AccentSoft` (§1.1) instead of flat `Primary`/`Accent` — softer, Two-Dots-style surfaces; button and text accents on top stay full-saturation `Primary`/`Accent`/`White` for contrast.
- All three difficulty cards and the Tutorial card switch from `TapGestureRecognizer` to `TouchBehavior` with `PressedScale="0.97"` (§2.2).
- Daily card gets a small persistent "HÔM NAY" badge (Caption, Accent-on-AccentSoft chip) instead of relying on the date label alone to say "this is special" — cheap, reinforces the Wordle/NYT "this day's puzzle is an event" framing without new logic.

### 3.2 GamePage (HUD, toolbar, board)

- Top bar (back / mine counter / timer / hint / restart) keeps its structure; numerals get the fixed-width treatment from §1.2 so the HUD doesn't jitter.
- **Daily-mode distinction (new, no logic beyond a visibility bind on existing `_isDaily`):** when playing the Daily Challenge, the center HUD group shows a small Accent-colored "Thử thách hôm nay" pill above/instead of the bare timer row, so the player is never in doubt they're in daily mode (today nothing distinguishes a Daily board from a normal Medium game once play starts).
- Flag-mode toggle row (Mở ô / Cắm cờ) stays exactly where it is, directly above the ad banner — see §5 for why its position relative to the banner is deliberately *not* changed.
- Board cell rendering stays Border+Label+TouchBehavior (proven pattern); only the motion in §2.2 is new, no structural change to `BuildEmptyBoard`/`RefreshAllCells`.

### 3.3 Daily Challenge

Not a separate screen today (routes into `GamePage` via `daily=True` from a HomePage card) — and this spec keeps it that way. A dedicated Daily screen would duplicate GamePage's entire board UI for no real gain; the HUD badge in §3.2 plus the share card in §4 (win/lose) carry the "this is special" feeling instead. This is a place the spec **declines** to add a screen the owner's brief implied might exist — flagged in §6 along with the share mechanic itself, which doesn't exist in code yet either.

### 3.4 Tutorial / LevelsPage

- Grid structure (5-wide, 100 tiles, locked/unlocked/next states) stays.
- Adds the unlock-pulse and bounded entrance-stagger from §2.2.
- Header row gets a small aggregate stats line (unlocked count + total stars, already computed on HomePage as `TutorialProgressLabel` — just surfaced here too) so the player doesn't have to go back to Home to see overall progress.
- **First-run coach marks (new, small logic):** on Level 1 only, two dismissible micro-tooltips over the real board ("Chạm để mở ô" / "Giữ để cắm cờ"), shown once via a persisted boolean, never as a separate tutorial wall — consistent with the "teach itself" principle. Flagged in §6 (new persisted flag, however small).

### 3.5 Win / Lose result (within GamePage)

- Stays a **custom in-XAML overlay** (`ResultOverlay` + `Card`-styled `Border`) — this is the one popup in the app that was already fully restylable before this spec, and the right one to carry the most design weight.
- Win: title stays plain `OffBlack` text (no new `Success` color, §1.1); stars (Tutorial mode) get the pop-in stagger; non-Tutorial win keeps its best-time/record copy unchanged.
- Loss: title/copy unchanged; the mine-ripple reveal (§2.2) happens on the board *underneath*, finishing right before the overlay fades in, so the player sees *why* they lost before the modal covers it — sequencing, not just visual, matters here.
- **Shareable result card (new feature, not currently in code — see §6):** a small secondary action, "Chia sẻ", on the win overlay (and optionally loss, Wordle-style "share your attempt" works for losses too). Renders a compact card: difficulty/day, time, stars (if Tutorial), a tiny abstracted board-pattern glyph row (NYT-style emoji grid, generated from the real board's reveal pattern, not a screenshot) — then hands off to the platform share sheet. This is the single biggest net-new feature in this spec and needs its own small design pass once approved; this section only states the shape, not final copy/pixel layout.

### 3.6 SettingsPage

- Structure (Pro section, App section) stays. Cards move to the `TouchBehavior`/`PressedScale` pattern for the "Thông tin ứng dụng" row.
- **New row, if the high-contrast theme (§1.4) is approved:** a theme picker (Hệ thống / Sáng / Tối / Độ tương phản cao) — segmented control or a small row of 4 chips, `Primary`-stroked when selected. This is the one place in this redesign that needs a logic change (persisting a manual theme override, since today the app only follows `RequestedTheme`), flagged in §6.

### 3.7 AboutPage

Changes little — two lines is enough: tighten the vertical rhythm to the 8/16/24 spacing scale (currently ad-hoc `Margin="0,10,0,0"` repeats) and nothing else. Not worth more design attention than this.

---

## 4. Popups & dialogs

Mechanism check first — this determines what's even possible to restyle:

| Popup | Trigger today | Mechanism today | Can it be restyled? |
|---|---|---|---|
| Win result | Game ends Won | Custom overlay (`ResultOverlay`) | Yes — already custom, see §3.5 |
| Lose result | Game ends Lost | Custom overlay (`ResultOverlay`) | Yes — already custom, see §3.5 |
| Confirm restart ("Chơi lại?") | Restart button mid-game | Native `DisplayAlert` | **No**, not without replacing the mechanism |
| Confirm exit mid-game | Back button/gesture mid-game | Native `DisplayAlert` | **No** |
| Resume saved game | — | **Doesn't exist as a popup** — it's the persistent `ResumeCard` on HomePage | N/A — see below |
| Hint confirmation/limit | — | **Doesn't exist at all** — hint is unlimited today, no gate in code | N/A — see §6 |
| Pro purchase upsell | — | Only inline in `SettingsPage`, no popup trigger elsewhere | N/A — proposed new, see below |
| Permission/consent | — | **None in app code** — manifest permissions are install-time only, no runtime/GDPR-UMP flow implemented | N/A |

Per-popup design:

- **Confirm restart / confirm exit mid-game** — recommend migrating both off native `DisplayAlert` to a custom semi-transparent scrim + centered `Card`-styled dialog (title, body, two pill buttons stacked or side-by-side, same enter/exit choreography as §2.2). This is the most direct way to make the app's confirm flows visually match everything else, and the risk is low — both are simple two-button confirms, no platform-specific behavior (like the hardware back button's synchronous contract in `GamePage.xaml.cs`) depends on `DisplayAlert` specifically; they depend on getting a `bool` back, which a custom modal can return via `Task<bool>` just as well.
- **Resume saved game** — recommend **keeping it a card, not promoting it to a popup.** A modal that blocks the Home screen every time there's a saved game is more interruptive than a persistent, ignorable card, and contradicts "minimal onboarding friction." Only change: add a subtle entrance (fade+scale, same as §2.2's popup choreography but non-blocking) when the card appears on `OnAppearing` so it doesn't just snap into existence.
- **Hint confirmation/limit-reached** — design shape only, since the gating logic doesn't exist (§6): a small custom toast-card near the hint button ("Còn 2/3 gợi ý hôm nay" or, at zero, "Hết gợi ý hôm nay — Nâng cấp Pro để dùng không giới hạn" with a CTA into Settings). Not a full-screen modal — a hint is a minor, frequent action and shouldn't get a heavy interruption.
- **Pro purchase upsell (new popup, beyond Settings' inline section)** — a custom overlay, `Card`-styled, reusable, triggerable from the hint-limit moment above. **Sequencing caution:** do not show this in the same game-end moment an interstitial is already due (§5) — stacking two interruptions back to back after a loss is the kind of thing that erodes trust in the controls per the persona's own "instant feedback over silence" point cutting the other way (too much feedback is its own failure mode). Recommend showing the upsell only on hint-limit-reached or a capped frequency (e.g. first loss of the session), not every game end.
- **Permission/consent** — nothing to design today; if a GDPR/UMP consent flow is added later for EU users, that form is Google's own native WebView-rendered UMP dialog and is not re-skinnable from this app's XAML either, same category as a native `DisplayAlert`. Noted for completeness only.

---

## 5. Ad placement

**Current state, confirmed in code:** `AdBannerSurface` is placed only on `HomePage` (bottom row, below the scrollable content) and `GamePage` (bottom row, below the flag-mode toggle, above the win/lose overlay's `Grid.RowSpan`). `LevelsPage`, `SettingsPage`, and `AboutPage` have no banner today. The interstitial policy (`Minesweeper.Core/Ads/AdsPolicy.cs`) is: at most once per hour, shown only right after a game ends (win or lose), never mid-game, never for Pro.

**Recommendation: keep both as-is.**

- *Banner stays Home + Game only, not expanded to Levels/Settings/About.* Those three are low-dwell utility/navigation screens; adding a banner to them trades a small amount of marginal impression revenue for visual clutter on exactly the screens this spec is trying to make feel calmer (Monument Valley whitespace point, §0). Settings in particular already hosts the "remove ads" CTA — an ad slot there doesn't help that moment.
- *Interstitial timing stays unchanged.* "Once per hour, only after win/lose, never mid-play" is already the correct policy for a puzzle game — no change recommended.
- *New interaction to guard against:* the Pro-upsell popup proposed in §4 must not fire in the same game-end moment an interstitial is already due — see the sequencing note there. This is a new risk this spec introduces (the upsell is new), not a change to the existing interstitial rule itself.

**Visual separation (new):** today the banner has no app-added chrome — it's whatever the ad SDK renders, with no label or divider. Add:
- a 1px top hairline using `HairLine`/`HairLineBrush` directly above the banner row on both `HomePage` and `GamePage`,
- a small "Quảng cáo" caption (Caption role, `InkSoft`, ~10–11sp) left-aligned just inside that hairline, so the slot reads unambiguously as "ad," not "more game" — this is standard, policy-safe ad labeling, not decoration.

**Accidental-tap spacing check:** on `GamePage`, the flag-mode toggle row (`Mở ô` / `Cắm cờ`) sits directly above the banner with only its own `Padding="14,6"` between them today — no explicit gap. This spec adds the hairline-plus-label treatment above as the visual separator, and recommends an explicit ~8dp margin between the toggle row and the banner so the two don't feel like one continuous tappable strip. **Confirmed:** none of this spec's screen changes move any interactive game element (board, flag toggle, HUD buttons) closer to the ad slot than it is today — the ad stays the last, bottom-most row on both screens, same as now.

---

## 6. Scope flags — decisions needed before implementation

These are called out explicitly per the persona's scope-discipline rule: each needs a product decision and a (small) logic change, not just a visual redesign, so none of them should be silently implemented alongside the purely visual work.

1. **Hint limiting** — hint is currently unlimited for every player (no code gates it), even though `SettingsPage` already advertises "Gợi ý (Hint) không giới hạn" as a *Pro* perk. Designing the limit-reached popup (§4) assumes this gets implemented; it needs a persisted per-day (or per-game) hint counter and a reset rule that don't exist yet.
2. **Shareable result card** — the owner's brief describes this as an existing mechanic to lean into; it was **not found in code** (no `Share.Default`/clipboard call anywhere in `src/`). Treat §3.5's share card as new, not an evolution — it needs share-text/image generation and a platform Share API call.
3. **First-run coach marks on Level 1** (§3.4) — small new persisted boolean ("has seen tutorial tip"), otherwise no engine/logic change.
4. **Manual theme picker + high-contrast theme** (§1.4, §3.6) — today the app only follows the OS's `RequestedTheme`; a manual override is new state (persisted preference) and a new `Colors.HighContrast.xaml`.
5. **Custom confirm-restart/confirm-exit dialogs replacing `DisplayAlert`** (§4) — not a logic change exactly, but a mechanism change touching `GamePage.xaml.cs`'s synchronous `OnBackButtonPressed` contract; worth a focused look before implementation, not bundled silently into the visual pass.

None of these were started — this round produced design only.

---

## 7. Evolution vs. departure

| Area | Evolution (builds on 2026-10-18) | Departure (genuinely new) |
|---|---|---|
| Color tokens | Primary/Accent/Danger/surfaces/cell/number tokens all kept unchanged | `PrimarySoft`, `AccentSoft`, `Overlay` tokens added; high-contrast theme file |
| Typography | Same two font weights, same general sizes | Formal Display/Title/Body/Caption/Numeral role naming |
| Spacing/radius | Mostly already-true values, now named as a scale | Cell radius explicitly pinned (not changed) as a Sudoku.com-reference decision |
| Motion | — | **Entirely new.** Nothing in the app animates today; §2 is a from-scratch system |
| HomePage/GamePage/LevelsPage/SettingsPage/AboutPage layout | Structure of every screen kept | Daily-mode HUD badge, coach marks, theme picker row |
| Win/lose popup | Stays the existing custom overlay mechanism | Share card is new; motion choreography is new |
| Confirm restart/exit | — | Proposed migration off native `DisplayAlert` to custom overlay |
| Resume prompt | Kept as a non-blocking Home card (not promoted to a modal) | Entrance animation only |
| Hint | — | Limit/upsell concept is entirely new; no existing gating logic |
| Daily Challenge | Stays routed through GamePage, not a separate screen | HUD badge + share card |
| Ad banner placement | Home + Game only, unchanged | Added visual separation (hairline + label), explicit spacing |
| Interstitial policy | Unchanged | New sequencing rule: don't stack with the new Pro-upsell popup |
