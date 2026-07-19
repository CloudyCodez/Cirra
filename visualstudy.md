# CloudBerry Visual Study

Date: May 29, 2026  
Scope: modern professional visual effects methods for CloudBerry's current stack, plus practical helper technologies that can be added without making the app feel slow, gimmicky, or obviously generated.

## 1.3.0 Visual Pass Notes

- Added a saved FX intensity control so theme effects can be tuned without editing code.
- Reworked the ambient effect layer for Matrix, Glitch, NieR, and Wuthering Waves with cleaner deterministic motion.
- Upgraded profile card, sidebar profile, score band, and avatar tile styling so the profile system reads more premium.
- Tightened Cloudscape focus treatment with card aura, inner rim, and selection sheen while avoiding fragile XAML trigger targets.
- Routed pointer glow and click pulses through the intensity setting to keep the shell snappy on quieter setups.

## 1.3.1 Cloudscape Readability And Matrix Smoothness Notes

- Gave Cloudscape game cards theme-independent high-contrast title, subtitle, and achievement brushes so NieR parchment text no longer disappears on cover art.
- Added a readable lower text plate, stronger selected rim, and shelf header count chip for a cleaner console-dashboard shelf feel.
- Removed duplicate Matrix rain animation clocks from shell/sidebar resources; the live effect layer owns motion while frozen pattern brushes add depth.
- Cached Matrix glyph text and made Matrix frame pacing tier-aware to smooth the effect without stripping its visual identity.

## 1.3.2 NieR Revamp Notes

- Made NieR artwork a first-class shell and Cloudscape background layer instead of a barely visible watermark.
- Reduced parchment wash and panel opacity in the NieR theme so the supplied artwork can actually carry the screen.
- Rebuilt the deletion effect around horizontal erase sweeps, save-slot boxes, block fragments, and terminal ghost labels.
- Tuned theme-synced UI sounds toward short low terminal beeps for navigation, confirmation, metadata apply, and launch handoff.

## Current Technical Ground

CloudBerry is currently a WPF desktop app targeting `net10.0-windows` with XAML-driven views, C# view models/services, companion tools in Node.js and Python, PowerShell-based deployment, and a self-contained Windows installer/update flow.

That matters because CloudBerry can already achieve a premium product feel without a full rewrite:

- WPF can handle layout, navigation, transitions, cards, media surfaces, themed resource dictionaries, routed input, and normal UI animations.
- WPF effects like blur and shadow exist, but they need discipline. They are useful for small accents and hazardous when placed on large, constantly animating surfaces.
- Composition-style effects, shader effects, WebView2 render islands, SkiaSharp, Lottie, and audio helpers can be added selectively for high-value moments.
- The right visual plan is not "more effects everywhere." The right plan is a small number of high-quality motion systems that repeat consistently: selection, hover, drag, page transition, launch handoff, metadata apply, achievement unlock, profile presence, update state.

## Research Sources

The research leaned on official documentation and primary project sources where possible:

- WPF graphics rendering overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/wpf-graphics-rendering-overview
- WPF animation overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-overview
- WPF animation performance guidance: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-other-recommendations
- WPF graphics rendering tiers: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/graphics-rendering-tiers
- WPF effects overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/bitmap-effects-overview
- Windows Composition API overview: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/using-the-visual-layer-with-wpf
- Windows App SDK Mica/system backdrop: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/system-backdrop-controller
- DirectComposition overview: https://learn.microsoft.com/en-us/windows/win32/directcomp/directcomposition-portal
- Direct2D effects overview: https://learn.microsoft.com/en-us/windows/win32/direct2d/effects-overview
- WebView2 WPF getting started: https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf
- Fluent Design motion: https://learn.microsoft.com/en-us/windows/apps/design/motion/
- Fluent Design materials: https://learn.microsoft.com/en-us/windows/apps/design/style/material
- Windows animation connected animation guidance: https://learn.microsoft.com/en-us/windows/apps/design/motion/connected-animation
- Lottie overview: https://airbnb.io/lottie/
- LottieFiles for developers: https://lottiefiles.com/developers
- SkiaSharp WPF forms/views: https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.wpf
- SkiaSharp documentation: https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/
- NAudio project: https://github.com/naudio/NAudio
- XAudio2 documentation: https://learn.microsoft.com/en-us/windows/win32/xaudio2/xaudio2-apis-portal
- FFmpeg project documentation: https://ffmpeg.org/documentation.html
- ImageMagick CLI documentation: https://imagemagick.org/script/command-line-processing.php
- Velopack documentation, useful for future updater architecture study: https://docs.velopack.io/

## Professional Visual Product Patterns

Modern premium apps do not look premium because they have the most animation. They look premium because every surface has hierarchy, feedback, and restraint.

The repeatable patterns that matter most for CloudBerry:

- **One primary visual stage per screen.** Library should make game art dominant. Metadata should make search and selected result dominant. Profile should make the player identity dominant. Settings should be quiet.
- **Motion reinforces state.** Hover means inspectable. Click means committed. Drag means browsing. Selection means ownership. Launch means leaving CloudBerry and entering a game.
- **Theme identity must affect more than color.** Matrix should change motion cadence, text treatment, overlays, pointer accents, and sound. NieR should feel sparse, deliberate, parchment/mechanical, and archival. Wuthering Waves should feel glassy, bright, sweeping, and airy.
- **Effects must be layered by cost.** Cheap effects can run constantly. Expensive effects should run briefly or only on selected/focused items.
- **The app should have visual memory.** Art crossfades, profile cards, recently played rows, achievement pop styling, and launch handoffs make CloudBerry feel alive.

## WPF Native Effects We Should Use More

### 1. Transform-Based Microinteractions

Best use:

- Buttons
- Cards
- tabs/nav items
- game tiles
- profile cards
- metadata result rows

Preferred properties:

- `Opacity`
- `RenderTransform.TranslateTransform`
- `RenderTransform.ScaleTransform`
- `RenderTransform.RotateTransform` only for tiny icon or badge motion

Avoid animating:

- `Width`
- `Height`
- `Margin`
- `Padding`
- `Grid.RowDefinition.Height`
- `BlurEffect.Radius` on large surfaces
- `DropShadowEffect.BlurRadius` on many items simultaneously

Reason: WPF layout animation triggers measure/arrange work. Transform animation generally avoids relayout and feels smoother.

CloudBerry implementation idea:

- Create `MotionTokens.xaml` with durations and easing:
  - `FastTapDuration`: 80-110 ms
  - `HoverLiftDuration`: 140-180 ms
  - `PageEnterDuration`: 240-320 ms
  - `HeroCrossfadeDuration`: 220-360 ms
- Create shared attached behavior or style fragments:
  - `PressScale=0.985`
  - `HoverScale=1.012`
  - `FocusGlowOpacity`
  - `DragCueOpacity`

### 2. Art Crossfades

CloudBerry should crossfade image-heavy areas when selection changes:

- Library hero background
- Cloudscape large hero
- Profile background panel
- Metadata preview art
- Achievement game header

Simple WPF method:

- Keep two `Image` layers.
- Load the next image into the hidden layer.
- Fade hidden layer up while fading old layer down.
- Swap references after the animation.

Why this is worth doing:

- It hides image decode timing.
- It makes browsing feel intentional.
- It removes the "hard pop" that currently makes some surfaces feel unfinished.

Performance notes:

- Decode large images to the displayed size using `BitmapImage.DecodePixelWidth` or `DecodePixelHeight`.
- Cache decoded artwork thumbnails separately from full-size artwork.
- Use `BitmapCache` on static image layers when they animate opacity.

### 3. Theme-Specific Page Transitions

Each theme should have a different transition pack:

- **Dark:** smooth fade + slight upward lift.
- **Matrix:** code sweep wipe, short green scan pass, terminal blink.
- **Glitch:** 1-2 frame horizontal channel offset, never constant noise.
- **NieR:** pale fade, thin black line sweep, brief deletion fragments on major transitions only.
- **Wuthering Waves:** airy diagonal light pass, glass blur shimmer, soft cyan/pink rim.

WPF implementation:

- Keep the transition pack as resources selected by theme:
  - `ThemePageEnterStoryboard`
  - `ThemeSelectionChangedStoryboard`
  - `ThemeLaunchStoryboard`
  - `ThemeMetadataApplyStoryboard`
- Do not bind large storyboards directly to every list item. Use them on screen-level containers and selected rows.

### 4. Focus States for Controller-First UI

Cloudscape should feel like a console dashboard. That means focus states matter more than hover states.

Recommended focus treatment:

- selected card grows 2-4%
- title sharpens and brightens
- background art subtly parallax shifts
- bottom metadata strip fades in
- non-selected cards dim only slightly, not so much that browsing feels muddy

Input behavior:

- left-click selects only if drag threshold was not crossed
- drag scrolling should show a subtle "shelf moving" cue
- keyboard/controller focus should use the same selected-card visuals
- cursor should stay normal; no four-arrow cursor

Current CloudBerry already started moving this way. The next pass should formalize the states into reusable styles.

## WPF Effects To Use Carefully

### DropShadowEffect

Good:

- one selected card glow
- one profile card rim
- one focused button glow
- modal/dialog depth

Bad:

- every card has animated shadow
- large containers with blur/shadow and clipping
- animating shadow radius for dozens of list items

Recommendation:

- Use static shadows on many items.
- Animate glow only on selected/hovered item.
- Use a cheap semi-transparent border/rim for most cards instead of real shadow.

### BlurEffect

Good:

- background art behind a hero
- small glass strips
- frozen/static layers

Bad:

- full-window animated blur
- blur on scrolling lists
- blur on large surfaces under constantly changing opacity

Recommendation:

- Prefer pre-blurred cached images for background panels.
- Use WPF `BlurEffect` only on limited, mostly static layers.
- For heavy blur, consider SkiaSharp or Direct2D render island later.

### VisualBrush

Good:

- subtle reflected art
- small masked duplicate
- low-frequency decorative treatment

Bad:

- live mirrored views of large scrolling areas
- feedback loops
- animated VisualBrush over complex UI

Recommendation:

- Use sparingly for profile card reflections or hero art echo.

## Windows Composition and DirectComposition

Windows Composition provides a modern visual layer with compositor-driven animations, shadows, clips, effects, and backdrops. It is excellent for smooth UI surfaces because the compositor can keep animations running more independently of normal UI layout work.

For WPF, Microsoft documents using the visual layer with WPF through interop. This is powerful but not free: it introduces complexity around HWND hosting, lifetime, resizing, input, and device behavior.

Best CloudBerry use cases:

- top-level animated backdrop layer
- theme particles/code rain layer
- acrylic-like blur layer
- high-performance parallax background
- launch transition overlay

Bad first use cases:

- replacing all buttons
- replacing normal WPF lists
- rebuilding every card in Composition

Recommendation:

- Add a `VisualEffectsHost` as an optional render island later.
- Keep WPF in charge of UI controls.
- Let Composition handle ambient/backdrop effects that do not need individual item layout.

## Direct2D / DirectComposition / DirectX Path

Direct2D and DirectComposition are the deeper Windows-native path for professional visual rendering. Direct2D has a rich effect pipeline, including blur, color matrix, shadow, saturation, morphology, lighting, displacement, and composite effects.

Best CloudBerry use cases:

- Matrix rain rendered at 60fps without thousands of WPF TextBlocks
- glitch shader/composite pass
- CRT scanline and phosphor simulation
- high-quality blur and bloom
- achievement pop effect layer

Likely implementation approaches:

- Use a WPF `HwndHost`/interop island backed by Direct2D/DirectComposition.
- Use Vortice.Windows as .NET bindings for DirectX APIs if we want managed code.
- Keep this for a later focused technical pass because it is powerful but can destabilize the app if rushed.

Recommendation:

- Do not start with full DirectX.
- Start with SkiaSharp or WebView2 render islands if we want fast practical gains.
- Revisit Direct2D if Matrix/glitch effects need true shader-level quality.

## SkiaSharp

SkiaSharp gives .NET access to the Skia 2D graphics library. It can render custom canvas visuals inside WPF. It is a strong middle ground between pure WPF and full DirectX.

Best CloudBerry use cases:

- Matrix code rain
- Wuthering Waves particle streaks
- NieR deletion fragments
- profile card shine/reflection
- achievement pop animations
- loading screen visualizer
- custom waveform/audio-reactive loading accent
- generated thumbnails and blurred background caches

Why it fits:

- C# friendly
- easier than DirectX
- good for canvas-style effects
- can be isolated to a few custom controls

Risks:

- another dependency
- render loop must pause when not visible
- careless redraws can waste CPU/GPU

Implementation idea:

- Add `CloudBerry.App/Effects/SkiaEffectHost.cs`.
- The host exposes:
  - `EffectKind`
  - `ThemeKey`
  - `Intensity`
  - `IsActive`
  - `ReducedMotion`
- It uses a timer/render invalidation only when visible and active.
- Effects are deterministic and hand-tuned, not random-looking.

First Skia effects worth building:

1. MatrixRainEffect
2. WutheringWaveStreaksEffect
3. NierDeletionParticlesEffect
4. ProfileCardFoilEffect

## WebView2 Render Islands

WebView2 lets a WPF app host web content using Microsoft Edge. It is useful for canvas/WebGL/CSS-driven effects, and it gives access to a huge ecosystem of front-end animation techniques.

Best CloudBerry use cases:

- one isolated full-screen visual effect layer
- advanced Matrix rain using Canvas
- profile card editor preview
- particle effects where CSS/Canvas is faster to iterate
- rich release notes/update page

Benefits:

- HTML Canvas and CSS transitions are productive.
- Web animations can be very smooth.
- JavaScript helper code can be iterated quickly.

Risks:

- embedded browser adds weight
- transparency/input passthrough can be awkward
- can feel disconnected from WPF if overused
- needs careful packaging and runtime assumptions

Recommendation:

- Do not use WebView2 for ordinary controls.
- Use it only if an effect is clearly better in Canvas/WebGL.
- Prefer a transparent, input-disabled overlay with a narrow JS bridge.

## Lottie

Lottie plays vector animation exported from After Effects/bodymovin. It is common in polished apps for onboarding, empty states, confirmations, loaders, and micro-celebrations.

Best CloudBerry use cases:

- metadata applied confirmation
- achievement unlock pop
- profile published confirmation
- update downloaded/installing badge
- empty states that are not boring
- loading screen accent animation

How to keep it human:

- Do not use generic Lottie marketplace animations.
- Make tiny custom animations that fit the themes.
- Use Lottie for short, deliberate moments, not ambient background animation.

Theme examples:

- Matrix: tiny falling glyph checkmark.
- NieR: black square fragments assembling into a check.
- Wuthering Waves: quick glass shard shimmer.
- Glitch: one horizontal split before the confirmation locks in.

## Audio / Sound Design

CloudBerry already has procedural sound through `SoundDesignService` and `ProceduralSoundPack`. That is good for early iteration, but professional sound can go further.

WPF `SoundPlayer` is simple but limited. NAudio is a mature .NET audio library for playback, mixing, device access, decoding, and more detailed control. XAudio2 is the Windows-native game/audio path for low-latency sound, but it is lower-level.

Best CloudBerry sound moments:

- app load
- page transition soft tick
- button press
- card focus
- Cloudscape shelf move
- launch handoff
- achievement unlock
- profile publish/friend added
- metadata applied
- update ready

Recommendation:

- Keep procedural sounds for now as fallback.
- Add a `SoundPack` folder later with small `.wav` files.
- Add theme-specific sound profiles:
  - Dark: soft glass/air
  - Matrix: low digital tick
  - Glitch: tiny bitcrush click, very short
  - NieR: pale mechanical tone
  - Wuthering: airy chime/sweep
- Add volume slider and reduced audio toggle.
- Avoid long startup audio; use a 400-900 ms polished cue.

## FFmpeg and ImageMagick Helpers

The app already uses helper tooling concepts. FFmpeg and ImageMagick can materially improve CloudBerry visuals without adding runtime UI complexity.

FFmpeg use cases:

- generate short looping video thumbnails from trailers
- extract representative frames for library hero art
- normalize video previews
- create blurred preview strips
- convert animated assets into lightweight app-friendly formats

ImageMagick use cases:

- generate blurred background plates from cover art
- create dominant color palettes
- make theme-tinted image variants
- crop/resize/letterbox artwork consistently
- generate app icon sizes

Best strategy:

- Keep these as optional companion tools.
- Use them in background jobs or during media import.
- Cache results in CloudBerry's media cache.
- Never run heavy media processing during normal browsing.

## Theme-Specific Visual Direction

### CloudBerry Dark

Target feel: premium default, quiet, fast, readable.

Effects:

- soft blue/cyan pointer glow
- subtle card lift
- clean glass panels
- very light hero parallax
- smooth 240 ms page transition

Avoid:

- heavy gradients
- over-blue monotone
- constant particles

### Neon Matrix

Target feel: faithful Matrix-inspired code rain, terminal clarity, dark glass.

Effects:

- proper falling glyph rain in background
- occasional scanline sweep
- green phosphor glow on focused controls
- terminal-style selection flash
- low-opacity vertical rain behind cards

Implementation priority:

- Replace WPF brush-based Matrix rain with a SkiaSharp canvas or WebView2 Canvas effect.
- Use actual glyph columns with varied speeds, fade trails, and head glow.
- Keep UI foreground readable with dark glass panels.

Avoid:

- random green dots
- noisy background that ruins legibility
- too much bloom

### Retro Glitch

Target feel: controlled CRT/arcade energy, not messy.

Effects:

- scanlines at 4-8% opacity
- short channel-offset on page enter
- selected card chromatic rim
- one-frame horizontal displacement when applying actions
- slight vignette

Avoid:

- constant jitter
- illegible text
- random broken layout
- excessive magenta/purple dominance

### NieR Automata

Target feel: archival parchment, machine UI, elegant melancholy.

Effects:

- parchment panels
- thin black divider lines
- muted sepia/gray background art
- subtle mechanical sweep
- deletion fragment effect for major transitions
- focus states feel like terminal selection, not glossy buttons

Implementation priority:

- A NieR-specific transition pack:
  - line sweep
  - 2-3 black square fragments
  - brief opacity dropout
- Keep the app readable and restrained.

Avoid:

- generic fantasy parchment
- over-rounded cards
- bright modern gradients

### Wuthering Waves

Target feel: anime sci-fi glass, airy motion, bright but clean.

Effects:

- background art more visible than current parchment-heavy surface
- cyan/pink rim highlights
- diagonal wind/light streaks
- glass cards with cooler tint
- soft parallax on hero/art surfaces
- bright selected card edge in Cloudscape

Implementation priority:

- Replace remaining parchment surfaces with Wuthering-specific glass brushes.
- Use theme background as a real part of composition, not a dim wallpaper.
- Add light streak Skia effect at low opacity.

Avoid:

- NieR parchment carryover
- procedural-looking blobs
- opaque beige panels

## Profile Card Direction

The profile card should be one of the most premium areas in CloudBerry. The user specifically wants original Xbox 360 dashboard flavor without requiring an avatar.

Professional direction:

- big gamercard silhouette
- glass or blade-style card body
- gamer score strip
- cloud code as a clean identity badge
- online status dot/pulse
- friend count and recent achievement
- profile picture/icon search
- theme-aware edge lighting
- rich hover/focus state

Card structure:

- left: player name, presence, motto
- center: score, achievements, library size
- right: icon/avatar tile
- bottom strip: cloud code, online status, last seen

Effects:

- subtle reflective sheen on hover
- theme rim color
- selected card 3D tilt, very small
- profile publish confirmation animation
- no pill spam
- no exposed internal effect labels like "Voltage Rose"

Helper option:

- SkiaSharp profile card foil/reflection effect.
- ImageMagick generated avatar/icon crops.
- Lottie publish/check confirmation.

## Cloudscape Direction

Cloudscape should feel like a game console shell, not a stretched desktop window.

High-value improvements:

- horizontal shelves with drag inertia
- selected tile focus zoom
- animated art crossfade
- genre lanes
- favorites lane
- recent lane
- installed/ready lane
- achievements-ready lane
- per-theme shelf focus effect
- controller-first input states
- left/right shoulder category switching
- hidden cursor after inactivity in Cloudscape

The Netflix-like model:

- top hero selected game
- below: shelves
  - Continue Playing
  - Favorites
  - Recently Added
  - Genres
  - Achievement Ready
  - Installed
  - New Metadata
- each shelf has strong art, minimal text, and clear focus.

Avoid:

- admin-style filter buttons
- large static counters
- backlog category
- text-heavy cards

## Loading Screen Direction

Current load should become fast, clean, and branded.

Structure:

- brand
- one concise status line
- slim progress track
- themed animated accent
- soft startup sound
- no debug-ish operator blocks
- no clipped text

Theme-specific loads:

- Dark: soft cloud glow, blue line fill
- Matrix: code columns forming CloudBerry
- Glitch: short CRT boot blink
- NieR: minimal terminal line and deletion fragments
- Wuthering: glass streak and background art fade-in

Implementation:

- Keep load animation lightweight.
- Preload only the first visible screen resources.
- Defer heavy scans and update checks until shell is visible.
- Use a one-shot startup cue under one second.

## Metadata Center Direction

Metadata Center should feel like a pro editing bay, but still be simple.

Visual model:

- left resizable library list
- center resizable search/result column
- right resizable media/apply column
- sticky apply button
- scrollable media asset panel
- selected result summary at top
- clear source badges: IGDB, Steam, Wikipedia, local

Effects:

- selected result lift and rim
- media thumbnail hover zoom
- apply confirmation crossfade
- loading shimmer while search is running
- no empty giant blank windows

Helper ideas:

- ImageMagick dominant color extraction from selected cover to tint the preview.
- Cached blurred background generated per selected result.
- Lottie confirmation when metadata applies.

## Achievement Visual Direction

Achievement systems feel valuable when unlocks are styled well.

CloudBerry should support:

- achievement pop overlay with theme-specific styling
- game-specific achievement dashboard
- locked/unlocked comparison
- generated achievements clearly labeled if they are CloudBerry-generated
- scan/import status that is informative but not noisy

Effects:

- small achievement toast with icon, title, score, sound
- Cloudscape unlock shelf
- profile card recent unlock strip
- theme-specific pop:
  - Matrix: green terminal unlock
  - NieR: black fragment reveal
  - Wuthering: glass shimmer
  - Glitch: tiny CRT flash

## Performance Strategy

Performance is part of visual quality. A beautiful UI that stutters feels cheap.

Rules:

- Keep list virtualization on.
- Do not animate layout properties in large lists.
- Decode images to target sizes.
- Cache generated thumbnails and background plates.
- Pause ambient effects when the window is inactive/minimized.
- Lower effect intensity when CPU/GPU usage spikes.
- Add a reduced motion setting.
- Use 60fps only for small render islands or core interactive surfaces.
- Avoid thousands of WPF visual children for particle/code rain.

Render budget:

- pointer/click effects: always cheap
- selected card animation: cheap
- page transition: short and isolated
- Matrix rain: one render island
- Wuthering streaks: one render island
- NieR deletion: short burst
- glitch displacement: short burst

## Recommended Helper Architecture

Add a small visual effects layer instead of scattering special cases everywhere.

Proposed folders:

```text
src/CloudBerry.App/
  Effects/
    VisualEffectHost.cs
    ThemeEffectController.cs
    MatrixRainEffect.cs
    WutheringStreakEffect.cs
    NierDeletionEffect.cs
    GlitchPulseEffect.cs
  Motion/
    MotionTokens.cs
    TransitionPack.cs
    VisualStateAnimator.cs
  Media/
    ArtworkPipeline.cs
    DominantColorService.cs
    ThumbnailCacheService.cs
  Audio/
    SoundPackService.cs
```

Add optional tool folder:

```text
tools/visual-pipeline/
  generate_background_plates.py
  extract_palette.py
  make_icon_pack.ps1
  build_sound_pack.ps1
```

Design principle:

- WPF owns controls.
- Effect hosts own ambient visuals.
- Media pipeline owns expensive processing.
- Sound service owns cues.
- Theme controller decides intensity and behavior.

## Dependency Candidates

### Safe Near-Term Candidates

- **SkiaSharp.Views.WPF**
  - Best for custom canvas effects.
  - Good first external visual dependency.
- **NAudio**
  - Better sound playback/mixing than `SoundPlayer`.
  - Useful for polished UI sound packs.
- **ImageMagick CLI**
  - Best as optional companion tool for image processing.
- **FFmpeg**
  - Best as optional companion tool for trailer/video thumbnails.

### Medium-Term Candidates

- **WebView2**
  - Useful for one or two advanced visual islands.
  - Avoid for normal UI.
- **Lottie player**
  - Useful for confirmations and unlock animations.
  - Use custom themed animation files only.
- **Vortice.Windows**
  - Managed DirectX bindings.
  - Powerful but should be introduced only after a narrow prototype.

### Avoid For Now

- Full migration to WinUI.
- Full custom DirectX UI.
- Game-engine embedding.
- Always-on full-window blur/noise.
- Random particle systems.
- Marketplace-looking generic animations.

## Implementation Roadmap

### Pass 1: Stabilize and Standardize Motion

- Create motion tokens.
- Unify hover/press/focus styles.
- Audit all button/card animations.
- Remove layout-animation patterns.
- Add reduced motion switch.
- Add theme motion intensity values.

### Pass 2: Art Crossfade System

- Build `CrossfadeImage` control.
- Use it in Library hero, Cloudscape hero, profile, metadata preview.
- Decode images to display size.
- Cache blurred plates.

### Pass 3: Cloudscape Premium Shelves

- Replace admin category row with shelf model.
- Add genre/favorites/recent/achievement-ready shelves.
- Tune drag selection threshold.
- Add shelf inertia.
- Add selected-card focus zoom.

### Pass 4: Profile Gamercard Overhaul

- Build true gamercard layout.
- Add icon search.
- Add online status and friend identity.
- Add gamerscore band.
- Add theme-specific rim/sheens.
- Remove internal effect labels.

### Pass 5: Skia Effect Host Prototype

- Add SkiaSharp.
- Build Matrix rain effect first.
- Pause when inactive.
- Add theme intensity control.
- Verify with GPU/CPU monitoring.

### Pass 6: Theme Transition Packs

- Implement theme-specific page enter/selection/launch/apply transitions.
- Keep animations short.
- Make Matrix, Glitch, NieR, Wuthering feel genuinely different.

### Pass 7: Sound Pack Upgrade

- Keep procedural fallback.
- Add short WAV cues.
- Add theme sound profile mapping.
- Add master volume and reduced sound setting.
- Add startup cue.

### Pass 8: Metadata Center Polish

- Resizable columns.
- Sticky apply bar.
- Scrollable media rail.
- Apply confirmation.
- Dominant color tinting.
- Loading shimmer.

### Pass 9: Achievement Pop System

- Theme-specific unlock toast.
- Cloudscape unlock overlay.
- Profile recent unlock strip.
- Achievement-ready indicators in shelves.

### Pass 10: Performance QA

- Add debug overlay toggle for FPS/effect state.
- Stress test 1,000 games.
- Stress test large artwork.
- Ensure ambient effects pause.
- Verify launch/update/profile flows remain smooth.

## Concrete Next Best Build Target

The best next engineering sweep should not start with another huge XAML-only polish pass. It should create the reusable foundation that lets every future pass look intentional.

Recommended next build:

1. Add `MotionTokens` and shared transition helpers.
2. Add `CrossfadeImage`.
3. Add a proper profile gamercard layout.
4. Add SkiaSharp prototype only for Matrix rain.
5. Replace Cloudscape categories with shelves.
6. Add reduced motion and effect intensity settings.

That would make CloudBerry feel more premium immediately while creating a real path for deeper effects.

## Design Rulebook Going Forward

- If an effect does not communicate state, theme, or delight, cut it.
- If it stutters, it is not premium.
- If it looks random, reduce it or hand-design it.
- If it appears everywhere, it becomes noise.
- If it is theme-specific, it should affect color, motion, sound, and shape language.
- If it blocks browsing, launching, editing, or scanning, it is decoration in the way.
- CloudBerry should feel alive, not busy.

## 1.3.3 Theme Picker And Base Theme FX Notes

- Theme Studio now uses one full theme dropdown instead of separate preset cards, matching the cleaner shell ComboBox behavior already used by profiles.
- The selected theme now previews with live theme motion, current badge, density, FX intensity, mood copy, and material summary in one compact container.
- CloudBerry Dark gained a subtle berry/cyan aurora sweep so the default theme has identity without becoming loud.
- Blueberry Midnight now uses cool star glints, moonlit depth, and slow horizon bands.
- Mint Cloud now uses soft teal bloom, circuit-like nodes, and calm breeze arcs.
- High Contrast now uses crisp scan lines and corner registration marks with low animation pressure.
- The base ambient renderer now throttles non-heavy themes to a measured cadence, keeping motion visible without feeding unnecessary redraws.

## 1.3.4 Resizable Shell And Library Rail Notes

- The outer shell sidebar can now be resized with a narrow themed grip, then hidden and restored without losing the last visible width.
- The Library left rail can be resized against the detail pane, which helps non-maximized windows keep the game list readable.
- The Library collection strip now has a vertical split grip, so users can decide whether categories or game rows get more space.
- Resize handles use theme brushes and hover glow instead of default system dividers.
- The Library rail now carries a low-opacity theme effect layer behind the list, giving each theme a matching side-panel atmosphere without hurting selection clarity.

## 1.3.5 Theme-Specific FX Sweep Notes

- `ThemeEffectLayer` now supports an `EffectRole`, allowing shell, sidebar, chrome, card, and Cloudscape surfaces to use the same renderer with context-specific accents.
- Sidebar FX now get theme-specific edge traces, scan ticks, matrix blocks, mint nodes, and high-contrast registration marks.
- Chrome FX now add subtle theme-colored hairlines and sweep traces across the title bar.
- Card FX now add corner registration, NieR deletion rows, and soft theme glows for preview surfaces.
- Cloudscape FX now add horizon lines, Wuthering energy arcs, glitch bands, and high-contrast framing at big-picture scale.
- Left navigation and top navigation now have theme-colored trace sweeps on hover/selection.
- Primary buttons now get a clipped theme sweep on hover, making major actions feel more responsive without changing layout.
