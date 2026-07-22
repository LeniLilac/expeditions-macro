---
name: Expeditions Macro
description: A quiet, precise Windows control surface for dependable Roblox automation.
colors:
  dark-canvas: "#0F1010"
  dark-sidebar: "#121313"
  dark-surface: "#181819"
  dark-raised: "#252528"
  dark-hover: "#202123"
  dark-selected: "#2B2C30"
  dark-border: "#333438"
  dark-text: "#F0F1F2"
  dark-muted: "#A1A4A8"
  dark-faint: "#74777C"
  dark-accent: "#7780FA"
  dark-success: "#55B887"
  dark-warning: "#D6A84B"
  dark-error: "#D96770"
  light-canvas: "#F4F5F5"
  light-sidebar: "#ECEEEF"
  light-surface: "#FAFAFA"
  light-raised: "#EEF0F1"
  light-hover: "#E6E8E9"
  light-selected: "#DDE0E2"
  light-border: "#D2D5D7"
  light-text: "#181A1C"
  light-muted: "#646A70"
  light-faint: "#858B90"
  light-accent: "#5F68E8"
  light-success: "#247A55"
  light-warning: "#926A1A"
  light-error: "#B13E48"
  on-accent: "#FFFFFF"
typography:
  page-title:
    fontFamily: "Fredoka"
    fontSize: "22px"
    fontWeight: 600
    lineHeight: 1.25
  section-title:
    fontFamily: "Fredoka"
    fontSize: "14px"
    fontWeight: 600
    lineHeight: 1.35
  body:
    fontFamily: "Fredoka"
    fontSize: "13px"
    fontWeight: 400
    lineHeight: 1.45
  caption:
    fontFamily: "Fredoka"
    fontSize: "12px"
    fontWeight: 400
    lineHeight: 1.4
rounded:
  compact: "3px"
  control: "4px"
  navigation: "5px"
spacing:
  compact: "4px"
  tight: "8px"
  field: "10px"
  control: "14px"
  section: "22px"
components:
  button-primary:
    backgroundColor: "{colors.dark-accent}"
    textColor: "{colors.on-accent}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "0 14px"
    height: "44px"
  button-secondary:
    backgroundColor: "{colors.dark-raised}"
    textColor: "{colors.dark-text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "0 14px"
    height: "40px"
  input:
    backgroundColor: "{colors.dark-surface}"
    textColor: "{colors.dark-text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "0 10px"
    height: "40px"
  navigation-item:
    backgroundColor: "{colors.dark-selected}"
    textColor: "{colors.dark-text}"
    typography: "{typography.body}"
    rounded: "{rounded.navigation}"
    padding: "0 11px"
    height: "38px"
---

# Design System: Expeditions Macro

## Overview

**Creative North Star: "The Quiet Control Surface"**

Expeditions Macro is quiet, precise, and capable. Its interface uses compact alignment, restrained density, and clear state changes so the user can configure a run once, supervise it at a glance, and intervene without studying the screen.

The visual system is flat and tool-like, with the calm hierarchy of Linear rather than the visual noise of a traditional macro utility. Primary actions are obvious but rare; advanced tuning stays secondary until requested. Every screen must make the current operation, recovery state, and stopping behavior unmistakable.

**Key Characteristics:**

- Compact controls aligned to a consistent grid.
- One restrained indigo accent reserved for primary action and focus.
- Tonal surfaces and hairline borders instead of decorative depth.
- Equal-quality dark and light themes with native Windows typography.
- Progressive disclosure for advanced tuning and diagnostics.

## Colors

The palette is neutral and low-chroma, with a restrained indigo signal and semantic colors used only for real status.

### Primary

- **Operational Indigo:** The single action accent for primary buttons, keyboard focus, progress, and selected controls that require stronger emphasis.

### Neutral

- **Near-Black Canvas / Quiet Paper:** The outer working background for dark and light themes.
- **Sidebar Tone:** A subtly separated navigation rail that never competes with page content.
- **Working Surface:** The default field and content surface.
- **Raised Control:** A small tonal step used by buttons, read-only fields, and menus.
- **Hairline Border:** One-pixel structure for controls, dividers, and data grids.
- **Primary Text / Muted Text / Faint Text:** Three deliberate levels for content, explanation, and tertiary metadata.

### Semantic

- **Measured Success:** Confirmed completion and healthy state only.
- **Reserved Warning:** Waiting, caution, or recoverable attention only.
- **Clear Error:** Failures, destructive actions, and stopped operations only.

**The One Voice Rule.** Operational Indigo is scarce. Never use it as decoration or as a background for entire sections.

**The Theme Parity Rule.** Every semantic role must remain legible and equivalent in both theme palettes; never solve contrast in only one theme.

## Typography

**Display Font:** Fredoka (embedded static family)
**Body Font:** Fredoka (embedded static family)
**Icons:** Lucide native vector geometry, inheriting the surrounding semantic color

**Character:** Friendly but precise. Fredoka softens the dense utility without weakening hierarchy; restrained weights and spacing keep operational text easy to scan.

### Hierarchy

- **Page title** (Semibold, 22px, 1.25): One concise title at the top of each workspace page.
- **Section title** (Semibold, 14px, 1.35): Divides workflows and names meaningful control groups.
- **Body** (Regular, 13px, 1.45): Controls, values, status, and ordinary explanatory copy.
- **Caption** (Regular, 12px, 1.4): Secondary guidance and metadata; always wraps instead of clipping.

**The Native Clarity Rule.** Use weight and spacing for hierarchy. Never introduce decorative display faces, all-caps blocks, or compressed labels.

## Elevation

The system is flat by default and uses no application-level shadow vocabulary. Depth comes from tonal layering between canvas, sidebar, surface, raised, hover, and selected roles, reinforced by one-pixel borders. Popups may use the platform's native window treatment, but ordinary page sections never float as cards.

**The Tonal Depth Rule.** If a container needs a shadow to be understood, its hierarchy is wrong. Correct the spacing, surface role, or border first.

**The One Divider Rule.** Use a single hairline divider between major sections; never stack borders, cards, and shadows around the same content.

## Components

### Buttons

- **Shape:** Gently compact corners (4px) and a 40px default height; primary actions use 44px.
- **Primary:** Operational Indigo with white text, semibold weight, and at least 128px width for the main workflow action.
- **Secondary:** Raised neutral surface, primary text, and a one-pixel border.
- **Hover / Pressed:** Tonal or opacity feedback only; no translation, glow, or decorative animation.
- **Focus:** A visible Operational Indigo border while preserving native keyboard behavior.
- **Disabled:** Reduced opacity (42%) with an arrow cursor; the label remains readable.

### Cards / Containers

- **Corner Style:** Containers are usually open sections; bounded list and data surfaces use compact 4px corners.
- **Background:** Canvas at the page level, working surface for editable controls, and raised tone only where interaction requires separation.
- **Shadow Strategy:** None; use tonal depth and one-pixel borders.
- **Internal Spacing:** 22px between major sections, 14px within control groups, and 8–10px for compact relationships.

### Inputs / Fields

- **Style:** 40px minimum height, 4px corners, working-surface fill, one-pixel border, and 10px horizontal padding.
- **Focus:** Border changes to Operational Indigo without layout shift.
- **Read-only / Disabled:** Raised neutral fill for read-only values; disabled fields retain content at 45% opacity.
- **Error:** Clear Error is reserved for a real validation failure and is paired with explanatory text.

### Navigation

- **Style:** A quiet sidebar with 38px items, 5px corners, and 11px horizontal padding.
- **Default / Hover / Active:** Muted text at rest, hover tone with primary text, and selected tone with primary text.
- **State:** The persistent status block stays separate from navigation and uses text in addition to its status dot.

### Data and Progress

- **Tables:** Open surfaces with one outer hairline and horizontal row rules; column headers use the raised neutral tone.
- **Progress:** A restrained 3px indigo line with no decorative track treatment.
- **Scrollbars:** Narrow, low-contrast, and visible only as structure; scrolling must remain available whenever content exceeds the viewport.

## Do's and Don'ts

### Do:

- **Do** keep the primary action and current automation state visible without scrolling whenever practical.
- **Do** place explanatory copy beneath its section heading and above controls.
- **Do** use the 22px section rhythm, 40px controls, 4px control corners, and one-pixel borders consistently.
- **Do** reveal advanced tuning progressively while keeping the primary workflow calm.
- **Do** preserve native keyboard focus, readable contrast, theme parity, and uncropped text at supported Windows scaling.
- **Do** pair success, warning, and error colors with explicit text so state never relies on color alone.

### Don't:

- **Don't** recreate legacy auto-clicker or WinForms aesthetics.
- **Don't** use oversized controls, nested cards, decorative gradients, excessive borders, or generic dashboard tiles.
- **Don't** expose developer-facing implementation language in user-visible copy.
- **Don't** turn Operational Indigo into ambient decoration, large colored panels, or neon glow.
- **Don't** place long prose beside controls where it compresses inputs or causes overlap.
- **Don't** introduce ornamental type, heavy drop shadows, glassmorphism, or motion that competes with automation state.
