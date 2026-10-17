# Cirra — a collection of worlds

The signature is a quiet instrument rail beside an expressive collection stage. Artwork supplies emotional scale; typography and precise surface edges supply control. Reserve light for selection, primary actions, and milestones. Decorative motion belongs behind content. The following six phases distinguish implemented work from acceptance gates that require full application review.

## 1. Visual identity audit

**Goal:** Establish a coherent identity across desktop, Horizon, dialogs, and startup.

**Why it matters:** Multiple generations of material overrides currently compete. A beautiful individual panel cannot make an inconsistent application feel considered.

**Visual transformations:** Reduce competing atmospheric layers; recover readable surfaces; replace theme sameness with recognizable silhouettes.

**Specific implementation changes:** Audited the resource dictionary, runtime appearance method, theme renderer, motion tokens, Home composition, and existing visual tests. Found that the prior order change bypassed contrast-tested materials. Restored the tested material contract as the final authority and extended it with structural identity. Found and corrected the spotlight label's stacking order and the obsolete welcome brand.

**Expected user impact:** Theme changes produce deliberate, consistent surfaces and legible controls.

**Success criteria:** All nine material packs pass contrast checks; switching from another theme produces the same result as a fresh application. Full-screen screenshots remain a separate acceptance gate.

## 2. Foundational design system reinvention

**Goal:** Make hierarchy depend on composition and material, not a proliferation of borders and badges.

**Why it matters:** A game collection should feel curated while keeping maintenance tools easy to find.

**Visual transformations:** Give heroes more interior space and directional edges. Separate display typography from readable body typography. Differentiate panel and control silhouettes.

**Specific implementation changes:** Shared heroes now use 24 × 22 padding, theme-directed edge gradients, and large-surface geometry. Shared panels consume theme border thickness; Archive becomes a ruled surface and High Contrast receives a stronger outline. Body and input text retain familiar Windows typography across themes.

**Expected user impact:** Headings feel authored; controls remain familiar; important surfaces have room to breathe.

**Success criteria:** At 1280 × 740 and common Windows scaling levels, long titles, translated labels, search results, and settings values fit without hiding their actions. These complete-screen layout checks remain required.

## 3. Theme ecosystem overhaul

**Goal:** Each theme remains identifiable in grayscale.

**Why it matters:** A palette preference alone is not a visual identity.

**Visual transformations and specific implementation changes:** The final material contract now assigns each theme panel and control geometry, display typography, gradient direction, and interaction cadence. Existing role-aware atmospheric compositions remain the background vocabulary.

| Theme | Emotional world and material | Geometry / typography | Lighting, depth, accents and motion | Icon treatment / hierarchy |
|---|---|---|---|---|
| Cirra Core | Confident mineral console | Broad upper corners, tighter lower corners; variable display | Diagonal mineral reflection; ice focus, champagne milestones; small measured lift | Familiar glyphs held in precise glass controls; artwork first |
| Midnight Observatory | OLED screening room | Fine rectangular frames; light display face | Low-reflection panels, orbital lines, blue-white and brass; slow orbital drift | Sparse instrument marks; title and selected object dominate |
| Verdant Glass | Botanical retreat | Alternating organic corners; humanist display | Vertical illumination, etched contours, mint and warm metal; gentle lift | Glyphs in organic control silhouettes; calm groups |
| Signal Rain | Precision cockpit | One clipped corner; monospaced display | Lateral phosphor surfaces, vertical signal motion; stationary hover | Instrument-like controls; aligned data and direct selection |
| Arcade Static | Restored premium arcade hardware | Rounded crown and tight base; Bahnschrift | Short vertical reflection, cabinet grid, pink/cyan; brisk atmosphere | Compact cabinet controls; collection rhythm |
| Archive Ivory | Collector's archive | Square ruled panels; Constantia titles | Matte paper, horizontal light, ink and bronze; minimal movement | Square utility glyph housings; editorial headings |
| Tidal Glass | Sculpted entertainment glass | Fully rounded surfaces; variable display | Oblique reflection, wave fields, sea glass and coral; longer settling cadence | Rounded housings; spacious artwork |
| Impact Noir | Performance hardware | Asymmetric cut corner; Bahnschrift | Diagonal facets, pale impact accents over ink; fast onset without bounce | Cut control housings; decisive selection |
| High Contrast | Absolute clarity | Square geometry, strong outlines; familiar display | Black/white and yellow; no ambient composition or hover lift | Explicit outlines and keyboard focus; function first |

**Expected user impact:** Changing themes changes the object's character, not just its tint.

**Success criteria:** Compare the rendered theme sheet in color and grayscale; use the same artwork across themes for a fair comparison. Structural differences are implemented; a user recognition study has not been performed.

## 4. Immersion, motion and depth systems

**Goal:** Give motion weight and purpose.

**Why it matters:** Overshoot and repeated illumination can make expensive materials feel weightless.

**Visual transformations:** Replace recoil bounce with controlled deceleration. Keep ambient drawing behind content and avoid stacking legacy art, rain, and sweeps over the role-aware renderer.

**Specific implementation changes:** Recoil uses cubic deceleration. Theme material application resets legacy shell and sidebar atmosphere and sweep opacity. Per-theme hover lift and timing tokens now live with the material identity. Existing efficient/off rendering modes and minimized-window suspension remain in use.

**Expected user impact:** Quieter resting screens and more deliberate interaction.

**Success criteria:** Renderer tests cover frame differences, cache bounds, static chrome, disabled effects, and high contrast. Remaining hardcoded XAML animation durations need a comprehensive reduced-motion audit before claiming every interaction follows theme timing.

## 5. Premium dashboard and library convergence

**Goal:** Home and Library should read as two views of the same collection.

**Why it matters:** Returning to a game should carry the same visual confidence as managing its metadata.

**Visual transformations:** Consistent hero edges, surface shapes, typography, and inset controls across the shared styles. Clear artwork labels and a branded first-run introduction.

**Specific implementation changes:** Updated shared hero and panel styles used throughout the application; fixed the Home spotlight badge above its image and shading; rewrote the first-run introduction for Cirra.

**Expected user impact:** The first-run and populated experiences share an identity, and selected-game content remains labeled.

**Success criteria:** Review empty, populated, missing-art, long-title, grid, list, loading, search, settings, and Horizon states with real collections. A wholesale rearrangement of these screens has not been implemented in this pass.

## 6. Final polish and signature language

**Goal:** Make quality demonstrable and repeatable.

**Why it matters:** A design is only as strong as its least considered state.

**Visual transformations:** Theme-specific silhouettes, restrained materials, clear type, protected labels, and consistent identity.

**Specific implementation changes:** Updated the WPF theme gallery to use actual display fonts and panel corners alongside shipped button templates. The existing gallery generator produces a review sheet directly from rendered WPF controls.

**Expected user impact:** Theme identity can be reviewed before release and regressions are easier to spot.

**Success criteria:** Passing build and material/renderer tests; visual inspection of the generated sheet; full application review at minimum size and multiple DPI levels; keyboard focus review; reduced-motion review. Competitive superiority, five-year durability, and emotional impact are design ambitions, not validated facts.

## Delivery boundary

### Subsequent interaction polish

Primary, secondary, and ghost buttons now use stationary hover feedback rather than scale, moving shine, and animated shadow blur. Keyboard focus has an explicit surface and edge cue, in addition to the existing focus outline. Pressed fills are authoritative: the legacy pressed tint is transparent, avoiding a second colour treatment over contrast-tested material pairs. This addresses these three shared button families; other controls and page animations still require the reduced-motion audit described above.

This pass implements the shared identity foundation, nine structural theme treatments, motion refinement, hero styling, and two Home polish fixes. It does not represent completion of every application-wide composition and interaction in the original brief. The outstanding acceptance gates above are explicit so the six-phase strategy cannot be mistaken for six fully validated phases.
