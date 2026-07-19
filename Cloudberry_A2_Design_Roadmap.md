# CLOUDBERRY: Visual & FX Design Roadmap
**Target:** Agent 2 (A2) - Visual and FX Lead
**Project:** Cloudberry - The Modern-Nostalgic Game Library App
**Document Purpose:** Deep-dive instructions, aesthetic pillars, reference lists, and structural roadmap for designing the Cloudberry front-end and visual FX.

---

## 1. THE CORE IDENTITY: "Modern Nostalgia"
Cloudberry is not just a game launcher; it is a time machine wrapped in a state-of-the-art spacecraft. The goal is to evoke the visceral, emotional memory of booting up a console in the mid-2000s to late 2010s, but executing it with the precision, high refresh rates, and minimalist elegance of modern UI/UX design (2026 standards). 

We are combining the deep, metadata-rich functionality of PC launchers with the highly stylized, identity-driven interfaces of classic consoles.

### Visual Metaphor
Imagine the **Xbox 360 Blades** made of frosted glass (glassmorphism), sliding smoothly over a dynamic, ambient background reminiscent of the **PlayStation 3 XMB or PS5**, while managing a library as robustly as **Playnite**. 

---

## 2. INSPIRATION & REFERENCE MAPPING
A2, you must deeply analyze these references and extract the specific elements listed below.

### A. The Xbox 360 Dashboard (Primary Structural Inspiration)
* **The "Blades" Era (2005):** * *Extract:* The physical, tactile sense of navigation. The sharp, swooshing transitions. The clear categorization (Media, Games, System, Profile) that felt like flipping through a futuristic physical folder.
* **The NXE Era (New Xbox Experience - 2008):** * *Extract:* The "cover flow" style presentation of games. The dedicated 'Game Details' stage where the background changes to match the selected game's art. The bold, chunky, yet clean typography.
* **Modern Translation:** Do not use heavy skeuomorphism or flat matte colors. Translate the "blades" or "panels" into sleek, semi-transparent frosted acrylic panels.

### B. Sony PlayStation UI (Primary FX & Ambient Inspiration)
* **PS3 / PSP (XMB - CrossMediaBar):**
    * *Extract:* The infinite, fluid horizontal/vertical cross navigation. Most importantly: the **ambient wave backgrounds**. The UI should feel like it's floating in a living, breathing space.
* **PS5:**
    * *Extract:* High-end particle effects, subtle glowing focus states (the golden/white aura around selected items). The way game hubs instantly take over the screen with high-res art and bespoke ambient audio. Smooth, non-jarring micro-animations.

### C. Playnite & Launchbox (Primary Functional Inspiration)
* **Playnite (Fullscreen Mode):**
    * *Extract:* Grid customization, filtering elegance. The ability to handle massive libraries (1000+ games) without the UI lagging.
* **Launchbox (Big Box Mode):**
    * *Extract:* The "Arcade Cabinet" love. We want to display metadata beautifully—release year, developer, genre, achievements, and user playtime.

---

## 3. COLOR PALETTE & TYPOGRAPHY

### The "Cloudberry" Palette
* **Base/Backgrounds:** Deep, cosmic twilights. Midnight purples (`#1A0B2E`), deep indigo (`#111827`), and dark slate.
* **Accents (The Berry):** Vibrant, electric magenta (`#D92662`), neon cyan (`#00F0FF` for selection highlights), and soft sunset orange for warnings/tertiary alerts.
* **Materials:** Dark Glassmorphism. Panels should have a backdrop blur (`backdrop-filter: blur(20px)`) with a very faint, 1px bright border to simulate edge-lit acrylic.

### Typography
* **Headers/Game Titles:** Bold, slightly extended sans-serifs that feel tech-forward but legible (e.g., *Space Grotesk*, *Montserrat*, or *Rajdhani*).
* **Body/Metadata:** Ultra-clean, highly readable modern sans-serifs (e.g., *Inter*, *SF Pro*, or *Roboto*).
* **UI Details:** Use all-caps with wide tracking for small category labels (e.g., `G E N R E`, `P L A Y T I M E`).

---

## 4. FX DESIGN & ANIMATION INSTRUCTIONS (For A2)

A2, the FX are what will make Cloudberry feel "alive". Pay close attention to these rules:

1.  **The Ambient Layer:** The background should never be completely static. Implement a slow-moving, procedural shader—perhaps a soft "aurora borealis" effect or slowly drifting cosmic dust (particles) tinted in the Cloudberry palette.
    * *Dynamic Shift:* When a user hovers over a game, the ambient background should smoothly crossfade into that game's fanart/wallpaper, but maintain the particle overlay to keep the Cloudberry branding intact.
2.  **Navigation Physics (Elasticity):** When moving between blades/tabs, use a spring-based animation curve (e.g., `ease-out-elastic`). It should snap into place with a tiny bit of bounce, replicating that satisfying mechanical feel of the Xbox 360 blades.
3.  **Focus States (The PS5 Glow):** When a game cover is selected in the grid, it should slightly scale up (`1.05x`). Apply a subtle, pulsating drop shadow or outer glow (cyan or magenta) to indicate active focus.
4.  **Transition FX (The Swoosh):** Moving from the main grid into a "Game Detail" page should feel like stepping into a portal. The UI panels should slide away rapidly, the game art pushes to the forefront, and the metadata slides in from the bottom/sides with staggered delays (100ms, 150ms, 200ms).

---

## 5. ARCHITECTURAL ROADMAP FOR A2

### Phase 1: Structural Wireframing (The Blade/Grid Hybrid)
* **Task:** Design the main navigation framework.
* **Requirement:** Create a top-level horizontal navigation bar (The Blades). Categories: `Home`, `Library`, `Collections`, `Settings`.
* **Requirement:** Design the sub-level vertical/horizontal grid (The XMB/Cover Flow) for the game covers.

### Phase 2: Game Detail View (The Hub)
* **Task:** Design the individual game page.
* **Requirement:** Hero image background. Prominent "PLAY" button. Clear arrangement of metadata (time played, last played, developer, achievements).
* **Vibe:** Should feel like the NXE game pages where the game's identity temporarily takes over the console.

### Phase 3: Visual Detailing & Glassmorphism
* **Task:** Apply the Cloudberry color palette, typography, and material properties.
* **Requirement:** Ensure contrast ratios remain high despite the dark/transparent themes. Use gradients sparingly—mostly for active states.

### Phase 4: FX & Micro-interactions
* **Task:** Define the animation timings, hover states, and procedural background effects.
* **Requirement:** Provide exact CSS/Shader pseudocode or animation curve parameters (Bezier curves) for all movements. Ensure particle FX don't distract from readability.

---

## 6. A2 DIRECTIVE SUMMARY
A2, your output must not just be flat images or static ideas. Whenever you generate a design concept, you must explain the **motion**, the **feel**, and the **z-index depth** of the elements. Make the user *feel* the nostalgia through the layout, but let them appreciate the modern era through your sleek FX and frictionless transitions. Begin your design thinking by visualizing the snap of an Xbox blade combined with the flow of a PlayStation wave.
