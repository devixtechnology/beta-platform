# Feature Specification: UI Modernization & Enhanced Creation Pages

**Feature Branch**: `002-ui-modernization`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "in ui details i want to enhance the creation pages for machine and order and product and also i want to enhance the ui to be more modern and more professional with the tabs in navbar as it but i want to make the design is different and not basic and be very good and very modern and very nice and fix all issues in the ui"

## Clarifications

### Session 2026-07-08

- Q: What visual design direction should the modernization take? → A: Elevated depth + glass — keep the dark charcoal + Beta orange base but add soft layered shadows, subtle gradients, frosted/glass card surfaces, and refined spacing/typography (modern, low-risk evolution).
- Q: How should the creation forms (Machine, Product, Work Order) be structured? → A: Side-by-side panel — form fields on one side and a live summary/preview (plus contextual help) on the other, on a single page.
- Q: Should the interface stay dark-only, or add a light/dark theme toggle? → A: Add a light/dark theme toggle; the user's choice persists across sessions. (As implemented, the **light** theme is the default for a first-time user; the charcoal dark theme remains available as the alternate and defines the `:root` token set.)
- Q: What accessibility bar should the redesign meet? → A: WCAG 2.1 AA (contrast ≥ 4.5:1 for text and ≥ 3:1 for UI components, visible focus, full keyboard navigation, labeled controls).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Modern, Guided Creation Forms (Priority: P1)

An administrator opens the "Add Machine", "Add Product", or "Add Work Order" page to register a new record. Today these pages are a single flat card with a plain stack of inputs. The administrator wants a form that feels considered and professional: a side-by-side layout with the input fields on one side and a live summary/preview (plus contextual help) on the other, obvious required indicators, inline helper text, immediate validation feedback, and prominent primary/secondary actions — so filling it out feels effortless, the result is previewed before saving, and mistakes are caught before submitting.

**Why this priority**: Data entry is the primary daily task in Phase 1. The creation forms are where administrators spend the most focused time and where errors are most costly. Improving them delivers the highest, most immediate value and is a self-contained slice.

**Independent Test**: Can be fully tested by navigating to each of the three creation pages, completing a valid record end-to-end, and deliberately submitting invalid/empty data — verifying the side-by-side form/preview layout, live preview updates, required-field indicators, inline validation, and clear success/error feedback, all in both English (LTR) and Arabic (RTL).

**Acceptance Scenarios**:

1. **Given** an administrator on the Add Machine page, **When** the page loads, **Then** fields are presented in a modern side-by-side layout (inputs on one side, a live summary/preview and contextual help on the other) with labels, required-field indicators, and helper text — not a bare stack of inputs.
2. **Given** an administrator edits a field, **When** they change its value, **Then** the live summary/preview updates to reflect the current input.
3. **Given** an administrator leaves a required field empty, **When** they attempt to save, **Then** the specific field is highlighted with a clear, human-readable message and the page does not lose the data already entered.
4. **Given** an administrator submits a valid record, **When** the save succeeds, **Then** they receive a clear, visible confirmation and are returned to the relevant list.
5. **Given** the same creation pages viewed in Arabic, **When** the page renders, **Then** the layout mirrors correctly for RTL with no clipped, overlapping, or misaligned elements.
6. **Given** an administrator on a narrow (mobile/tablet) screen, **When** they open any creation page, **Then** the side-by-side panels stack into a single usable column with no horizontal scrolling or overlapping controls.

---

### User Story 2 - Modern, Distinctive Visual Design System (Priority: P2)

An administrator uses the platform throughout the day across the Dashboard, Machines, Products, and Work Orders sections. They want the whole interface to look modern, polished, and distinctive — an "elevated depth + glass" treatment (soft layered shadows, subtle gradients, frosted/glass card surfaces, refined spacing and typography) rather than a default/basic Bootstrap look — while keeping the existing navigation tabs and brand identity. Cards, tables, buttons, badges, and headings should share a consistent, elevated visual language that feels professional and purpose-built. The administrator can also switch between a light and a dark theme, with light as the default and their choice remembered.

**Why this priority**: A cohesive visual refresh raises perceived quality across every screen and reinforces the brand, but it depends on and complements the higher-value form work. It is independently demonstrable on any single page.

**Independent Test**: Can be tested by loading each major section and confirming a consistent, elevated visual treatment (spacing, typography, cards, buttons, badges, status colors) that visibly differs from stock Bootstrap defaults while preserving the current navbar tab structure and brand colors.

**Acceptance Scenarios**:

1. **Given** any authenticated page, **When** it renders, **Then** shared components (cards, buttons, tables, badges, form controls) follow one consistent "elevated depth + glass" visual style (layered shadows, subtle gradients, frosted surfaces) rather than default framework styling.
2. **Given** the top navigation, **When** the visual refresh is applied, **Then** the existing set of tabs (Dashboard, Machines, Products, Work Orders) and their order are preserved, but their appearance is modernized (active state, hover, spacing, iconography).
3. **Given** the established brand (Beta orange, with a light default theme and charcoal dark as the alternate), **When** the new design is applied, **Then** brand colors and the logo remain the primary identity and are used purposefully for emphasis and status in both themes.
4. **Given** an administrator, **When** they toggle between the light and dark theme, **Then** every screen renders correctly in the selected theme, the choice persists across sessions, and light is the default for a first-time user.
5. **Given** a returning administrator, **When** they move between sections, **Then** headings, page titles, spacing, and action placement are consistent so the interface feels like one coherent product.

---

### User Story 3 - UI Defect Cleanup & Consistency Pass (Priority: P3)

An administrator regularly encounters small UI rough edges — inconsistent spacing, misaligned controls, plain/unstyled list and detail pages, weak empty states, and layout issues that appear in RTL or on small screens. They want these issues found and fixed so the interface feels finished and trustworthy.

**Why this priority**: These fixes elevate overall quality and remove friction, but they are refinements on top of the core form and visual work. They can be delivered and verified incrementally.

**Independent Test**: Can be tested by walking every authenticated screen in both languages and both a wide and narrow viewport, cataloguing and then confirming resolution of alignment, spacing, overflow, empty-state, and readability issues.

**Acceptance Scenarios**:

1. **Given** any list page with no records, **When** it loads, **Then** a clear, styled empty state explains the situation and offers the primary "add" action — rather than a blank or bare table.
2. **Given** any authenticated page rendered in Arabic (RTL), **When** it loads, **Then** icons, spacing, and alignment mirror correctly with no visual defects.
3. **Given** any authenticated page on a small screen, **When** it loads, **Then** content reflows without overlap, clipping, or horizontal scrolling.
4. **Given** the detail and edit pages for records, **When** they load, **Then** they share the same modern, consistent styling as the rest of the application.

---

### Edge Cases

- **Long content**: Very long machine/product/work-order names or codes must wrap or truncate gracefully without breaking card, table, or form layout.
- **Validation while typing**: Correcting an invalid field should visibly clear its error state without a full page reload where feasible.
- **Empty and error states**: Every list, dashboard widget, and detail view needs a defined appearance when data is empty or fails to load, not a blank region.
- **RTL-specific layout**: Directional icons (arrows, chevrons), field alignment, and action-button order must be correct in Arabic, not merely mirrored text.
- **Slow/failed saves**: The primary action must communicate progress and prevent duplicate submissions if a save is slow.
- **Dashboard polling refresh**: The visual refresh must not disrupt the existing ~5-second dashboard auto-refresh or cause flicker/layout shift on update.

## Requirements *(mandatory)*

### Functional Requirements

**Creation pages (Machine, Product, Work Order)**

- **FR-001**: The Machine, Product, and Work Order creation pages MUST present a side-by-side layout — input fields on one side and a live summary/preview with contextual help on the other — on a single page, rather than a single flat stack of inputs.
- **FR-001a**: The live summary/preview panel MUST update to reflect the administrator's current field values as they are entered, before saving.
- **FR-002**: Each creation form MUST visually distinguish required fields and provide concise inline helper text where a field's expected input is not self-evident.
- **FR-003**: Each creation form MUST show clear, field-level, human-readable validation feedback on invalid submission and MUST retain the administrator's already-entered values.
- **FR-004**: Each creation form MUST present a clearly emphasized primary action (Save) and a distinct secondary action (Cancel), with the primary action indicating progress and preventing duplicate submission during a save.
- **FR-005**: On successful creation, the system MUST show a visible success confirmation and return the administrator to the corresponding list.
- **FR-006**: The Edit and Details pages for Machine, Product, and Work Order MUST adopt the same modern, consistent styling as their creation counterparts.

**Visual design system**

- **FR-007**: The application MUST apply one consistent "elevated depth + glass" visual style (soft layered shadows, subtle gradients, frosted/glass card surfaces, refined spacing and typography) across shared components (cards, buttons, tables, badges, form controls, headings) that is visibly distinct from default framework styling.
- **FR-008**: The top navigation MUST preserve the existing tabs (Dashboard, Machines, Products, Work Orders) and their order while modernizing their appearance, including a clear active state and hover treatment.
- **FR-009**: The design MUST preserve the existing Beta brand identity (logo, orange accent) and use brand and status colors purposefully and consistently, with the Beta orange accent retained in both the light and dark theme.
- **FR-009a**: The interface MUST offer a light and a dark theme with a user-accessible toggle; the **light** theme MUST be the default for a first-time user (no `beta_theme` cookie), and the selected theme MUST persist across sessions and apply consistently to every authenticated screen. (The charcoal dark theme remains fully supported as the alternate and defines the `:root` token set.)
- **FR-010**: Page structure MUST be consistent across sections: consistent page titles/headers, spacing rhythm, and placement of primary actions.

**Quality, accessibility, and responsiveness**

- **FR-011**: All authenticated pages MUST render correctly in both English (LTR) and Arabic (RTL), in both the light and dark theme, with no clipped, overlapping, or mis-mirrored elements.
- **FR-012**: All authenticated pages MUST be usable on small (mobile/tablet) viewports without horizontal scrolling or overlapping controls; the creation pages' side-by-side panels MUST collapse into a single column on narrow viewports.
- **FR-013**: Every list view MUST provide a clear, styled empty state that explains the absence of data and surfaces the primary add action.
- **FR-014**: The interface MUST meet WCAG 2.1 Level AA: text contrast ≥ 4.5:1 and UI-component/graphical contrast ≥ 3:1 (in both themes), a visible focus indicator on every interactive element, full keyboard navigability, and correctly labeled controls.
- **FR-015**: Known UI defects catalogued during the consistency pass (alignment, spacing, overflow, weak empty/error states, RTL and small-screen issues) MUST be resolved across all authenticated screens.
- **FR-016**: The visual refresh MUST NOT alter existing data, workflows, routes, or the dashboard's existing auto-refresh behavior; it changes presentation only.

### Key Entities *(include if feature involves data)*

This feature is presentation-only and introduces no new data entities. It restyles the pages that manage the existing entities: **Machine**, **Product**, and **Work Order**, plus the shared **Dashboard**, **navigation**, and **layout** surfaces.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator can complete a valid Machine, Product, or Work Order creation on the first attempt without confusion about required fields or actions, verified in usability walkthroughs for all three forms in both languages.
- **SC-002**: 100% of authenticated pages render without layout defects (no overlap, clipping, or horizontal scrolling) across a wide and a narrow viewport, in both English (LTR) and Arabic (RTL), and in both the light and dark theme.
- **SC-003**: Invalid submissions on every creation form surface field-level, human-readable messages and never discard already-entered values, in 100% of tested validation cases.
- **SC-004**: Every list view displays a defined, styled empty state when it contains no records — 0 blank or bare-table pages remain.
- **SC-005**: The top navigation retains exactly the current four tabs in the same order, confirmed before and after the refresh.
- **SC-006**: The interface passes WCAG 2.1 AA checks — text contrast ≥ 4.5:1, UI-component contrast ≥ 3:1, a visible focus state on every interactive element, and full keyboard navigation — verified across the shared component set in both the light and dark theme.
- **SC-007**: Existing functionality is unchanged — all current create/edit/list/dashboard workflows and the dashboard auto-refresh continue to work exactly as before the refresh.
- **SC-008**: Reviewers and stakeholders agree the interface reads as modern and professional rather than basic/default, confirmed by sign-off against agreed reference screens for each major page.
- **SC-009**: A user can switch between light and dark themes from any page; the change applies immediately to the whole interface, persists across sessions, and a first-time user sees the light theme by default.

## Assumptions

- **Scope is presentation-only**: This feature changes visual design, layout, and UX polish of existing pages. It does not add new business capabilities, fields, entities, routes, or reports. (New fields would be a separate feature.)
- **Existing stack and brand are retained**: The current framework, Beta orange accent, and logo are kept; the refresh elevates rather than replaces them. Both a light and a charcoal dark theme are provided; as implemented, **light** is the default for a first-time user and dark is the alternate (see Clarifications).
- **Navigation set is fixed**: The four existing navbar tabs and their order are preserved by explicit request; only their styling changes.
- **Bilingual and responsive are mandatory**: Both English/Arabic (LTR/RTL) and small-to-large viewport support are in scope for every touched screen, consistent with existing platform expectations.
- **"Order" refers to Work Order**: The user's "order" maps to the existing Work Order creation page.
- **"Fix all issues in the UI" is scoped to the authenticated application surface** (Dashboard, Machines, Products, Work Orders, and shared layout/auth pages) discovered during a deliberate consistency-pass audit; the catalogue of defects is produced as part of User Story 3.
- **Success is assessed against agreed reference screens**: Because "modern/professional" is qualitative, stakeholder sign-off is captured against reference visuals for each major page rather than a numeric aesthetic score.
