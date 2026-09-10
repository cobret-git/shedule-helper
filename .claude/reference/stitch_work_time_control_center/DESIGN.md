---
name: Clinical Precision
colors:
  surface: '#f7f9fc'
  surface-dim: '#d8dadd'
  surface-bright: '#f7f9fc'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f4f6'
  surface-container: '#eceef1'
  surface-container-high: '#e6e8eb'
  surface-container-highest: '#e0e3e5'
  on-surface: '#191c1e'
  on-surface-variant: '#40484d'
  inverse-surface: '#2d3133'
  inverse-on-surface: '#eff1f4'
  outline: '#70787e'
  outline-variant: '#bfc8ce'
  surface-tint: '#096685'
  primary: '#006382'
  on-primary: '#ffffff'
  primary-container: '#2f7c9c'
  on-primary-container: '#f9fcff'
  inverse-primary: '#89d0f3'
  secondary: '#4b626c'
  on-secondary: '#ffffff'
  secondary-container: '#cbe4ef'
  on-secondary-container: '#4f6670'
  tertiary: '#5b5975'
  on-tertiary: '#ffffff'
  tertiary-container: '#74718f'
  on-tertiary-container: '#fffbff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#c0e8ff'
  primary-fixed-dim: '#89d0f3'
  on-primary-fixed: '#001e2b'
  on-primary-fixed-variant: '#004d66'
  secondary-fixed: '#cee6f2'
  secondary-fixed-dim: '#b2cad6'
  on-secondary-fixed: '#051e27'
  on-secondary-fixed-variant: '#334a53'
  tertiary-fixed: '#e3dfff'
  tertiary-fixed-dim: '#c7c3e4'
  on-tertiary-fixed: '#1a1931'
  on-tertiary-fixed-variant: '#46445f'
  background: '#f7f9fc'
  on-background: '#191c1e'
  surface-variant: '#e0e3e5'
typography:
  headline-lg:
    fontFamily: Libre Franklin
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-lg-mobile:
    fontFamily: Libre Franklin
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Libre Franklin
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-sm:
    fontFamily: Libre Franklin
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Libre Franklin
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Libre Franklin
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-md:
    fontFamily: Libre Franklin
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
  label-sm:
    fontFamily: Libre Franklin
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  gutter: 16px
  margin-mobile: 16px
  margin-desktop: 32px
---

## Brand & Style

This design system is built for environments requiring high cognitive load management, such as healthcare, data-heavy enterprise SaaS, or institutional platforms. The brand personality is grounded, reliable, and meticulously organized. It aims to evoke a sense of calm authority and functional clarity through a "Corporate / Modern" style that leans into systematic utility.

The aesthetic prioritizes legibility and clear information hierarchy. By utilizing a restrained color palette and consistent structural alignment, the UI minimizes visual noise, allowing users to focus on complex tasks with confidence and reduced fatigue.

## Colors

The color strategy is strictly functional. The **Primary** blue is reserved exclusively for interactive elements, providing a clear signifier for action and focus. The **Secondary** and **Tertiary** muted tones are used for supporting UI elements, such as icons, metadata, or decorative accents that should not compete with primary actions.

The background architecture relies on a **Neutral** palette. `#F8F9FA` serves as the base canvas, while `#E0E2E6` is used for surface variances like sidebars, headers, and container backgrounds. This creates a subtle tonal contrast that defines page structure without the need for heavy borders. The **Error** red is high-contrast and used sparingly for destructive actions and critical alerts.

## Typography

The design system uses **Libre Franklin** across all levels. This typeface was chosen for its clean, classic aesthetic and exceptional legibility in dense interfaces. 

- **Headlines:** Use Bold and Semi-Bold weights with tight letter-spacing to create a strong visual anchor.
- **Body:** Standard weights are optimized for readability with generous line heights.
- **Labels:** Small-scale text uses a Medium weight and slight tracking increases to ensure clarity in navigation and data tagging.
- **Scaling:** On mobile devices, large headlines scale down to prevent excessive word-wrapping, while body sizes remain constant to preserve accessibility.

## Layout & Spacing

The layout is governed by a **12-column fluid grid** for desktop and a **4-column grid** for mobile. A strict 4px base unit (the "spacing-base") drives all padding and margin decisions, ensuring mathematical harmony across the UI.

- **Desktop:** 32px outer margins with 16px gutters. Max-width for content containers is 1280px to prevent excessive line lengths.
- **Mobile:** 16px outer margins with 16px gutters.
- **Alignment:** All components should snap to the 4px grid. Vertical rhythm is maintained by using increments of 8px for section spacing.

## Elevation & Depth

Hierarchy is established through **Tonal Layers** rather than heavy shadows. This keeps the interface feeling "flat" and professional.

- **Level 0 (Base):** Neutral background (`#F8F9FA`).
- **Level 1 (Surface):** Neutral variant (`#E0E2E6`) used for cards, sidebars, and input fields to create subtle separation.
- **Interactive Depth:** When an element requires elevation (like a modal or a floating action button), use a very soft, low-opacity ambient shadow (0px 4px 12px rgba(0, 0, 0, 0.05)).
- **Outlines:** Use 1px solid borders in the Neutral Variant shade to define boundaries within the same tonal plane.

## Shapes

The shape language is "Soft," utilizing a standardized 4px (`0.25rem`) corner radius for all core components. This provides a professional, "industrial" feel that is more approachable than sharp corners but more serious than fully rounded shapes.

- **Buttons & Inputs:** 4px radius.
- **Cards & Modals:** 8px (`rounded-lg`) for larger containers to soften the overall layout.
- **Selection Indicators:** Pill shapes are reserved exclusively for status chips and tags to differentiate them from actionable buttons.

## Components

- **Buttons:** Primary buttons use the Primary blue background with white text. Secondary buttons use a transparent background with a Primary blue outline. All buttons have a 4px corner radius.
- **Input Fields:** Use the Surface Neutral (`#E0E2E6`) for the background or a 1px border. Focus states must clearly use the Primary blue.
- **Chips:** Used for filtering and status. Status chips use low-saturation versions of the primary/tertiary colors to indicate state without overwhelming the page.
- **Lists:** High-density lists use subtle 1px dividers in the Neutral Variant shade. Active list items are indicated with a Primary blue left-edge accent (4px width).
- **Cards:** Background should be white or the Neutral Variant, with a 4px or 8px radius and a 1px border.
- **Checkboxes & Radios:** These must use the Primary blue for the "checked" state to ensure clear user feedback.