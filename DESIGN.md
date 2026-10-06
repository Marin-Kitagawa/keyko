# DESIGN.md — Keyko visual world

Mode: **Operate** (task tool). Brand voice lives in precise details, not decoration.
Pinned brief (overrides saturated-pattern warnings): feminine, cozy, cute, glassy,
pastel, script typography for brand moments.

## Surfaces

- Main window: acrylic over the desktop (functional layering — the desktop is the backdrop)
- Dialogs: same acrylic family, centered
- Toasts: solid near-opaque cards (raw acrylic at toast size reads as a smudge)
- Overlays (color picker, OCR): dimmed fullscreen, no blur

## Color

Dark warm plum is the default ground; light blush is the alternate. Both mutate
at runtime via `UiTheme.Apply` (see Services/Theme.cs — single source of truth).

- Accent: one gradient (Blossom #F472B6 → #A78BFA default) used ONLY for the
  primary action, current selection, toggles, and state indicators
- Category colors: pastel chips for filtering, never fills for icon tiles
- Text roles: primary / secondary / tertiary — tertiary must stay ≥4.5:1 on card
  surfaces (it was below the floor once; do not dim it again)
- Semantic states: Success (armed), Danger (conflict/failure), Warning (no hotkey)

## Typography

Three families, three jobs — nothing else:

| Role | Face | Notes |
| --- | --- | --- |
| Brand + page titles | Lavishly Yours (script) | Titlebar wordmark, page h1, editor title ONLY. Never nav labels, buttons, data. |
| Everything else | Playfair Display | Regular for body/names/buttons; italic for whisper/meta text; NO bold weights (user banned them) |
| Keycaps + code | Fira Code | Hotkey badges and script editors only |

Scale (fixed, ratio ≈1.2): 34 h1 · 23 dialog title · 16 section header · 14 body ·
12.5 meta · 11 caption. Body measure ≤75ch.

## Icons

Segoe Fluent Icons only, one voice: 20px TextSecondary in list rows, accent in
toasts, 12–13px inline. No emoji as icons, no icon-tile squares (impeccable
anti-pattern), no exe-icon extraction.

## Space & depth

- 4px grid; tight groups, generous section separation; more space above a
  heading than below
- Cards: 1px hairline border, radius 14–18, NO wide shadows (border OR shadow, never both)
- Toasts float — they alone carry an offset soft shadow
- No nested cards: sections inside a surface are spacing + headers, not boxes

## Motion

150–260ms ease-out, opacity + small translate only. Toast fade in/out. No
page-load choreography.
