# Vaktr — Daily QA & Polish Tasks

## UI/UX Polish

Design reference: [Apple Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/)

Core principles to apply throughout:
- **Clarity** — every element should be legible, precise, and instantly understood
- **Deference** — UI stays out of the way; telemetry data is the focus, not chrome
- **Depth** — use layering, subtle shadows, and motion to convey hierarchy
- **Consistency** — spacing, typography, color, and interaction patterns should feel unified across every panel and control

### Visual hierarchy & layout
- [ ] Audit panel card spacing — ensure consistent padding, margins, and gaps across all panel types (chart panels, gauge panels, process tables)
- [ ] Ensure responsive column breakpoints feel natural at every window width (1-col, 2-col, 3-col transitions should not cause jarring reflow)
- [ ] Review section headers (AT A GLANCE, LIVE BOARD, CONTROL DECK) for consistent sizing, spacing, and visual weight
- [ ] Ensure the summary strip cards have uniform height and alignment regardless of content length

### Typography
- [ ] Audit font sizes and weights for clear hierarchy: panel title > current value > secondary value > footer/scale label > muted text
- [ ] Ensure all numeric/metric values use a monospace or tabular-lining font (Bahnschrift) so digits don't shift as values change
- [ ] Check that text truncation (ellipsis) works gracefully on long panel titles and series names

### Color & contrast
- [ ] Verify all text meets WCAG AA contrast ratios against both dark and light theme backgrounds
- [ ] Ensure chart series colors are distinguishable from each other and from the chart background in both themes
- [ ] Review accent color usage — it should highlight interactive elements and active states, not compete with data
- [ ] Test light theme thoroughly — all surfaces, borders, text, and charts should look intentional, not washed out

### Motion & transitions
- [x] Add subtle fade-in + slide-up when panels appear for the first time (280ms fade, 320ms translate, cubic ease-out)
- [x] Smooth the panel drag-and-drop — 95% scale, 88% opacity, SizeAll cursor, animated snap-back on release (200ms ease-out)
- [ ] Ensure theme switching feels smooth (no flash of unstyled content)
- [x] Edge glow indicator on drop target cards during drag

### Content-first refinements
- [x] Remove decorative elements that don't serve data — removed ActionChip shine line
- [x] Ensure empty/loading states are informative and visually calm — updated temp panel empty states
- [x] Control deck text made succinct and professional — tightened all descriptions, removed verbose filler
- [ ] Footer text should add value or be removed — avoid filler

### Interaction polish
- [x] Hover states on all interactive elements (chips, buttons, panels) should feel responsive and consistent — panel hover shows accent border (1.2px) with lighter surface gradient
- [ ] Ensure keyboard accessibility — tab order through controls, enter/space to activate buttons
- [ ] Chart hover tooltips should track smoothly and dismiss cleanly
- [ ] Time range chip selection should give immediate visual feedback (active state)

### Chart & data display
- [x] Show gaps in chart lines when data is missing (no connecting lines across time gaps) — gaps detected via median interval × 3 threshold
- [ ] Add subtle gridline fade at chart edges for depth
- [x] Improve chart axis label legibility — skip interior labels when spacing < 70px to prevent overlap at narrow widths
- [x] Add a subtle glow or highlight on the most recent data point — 14px glow ellipse at 18% opacity behind the 5px dot

### Visual refinement
- [ ] Add subtle background gradient shifts between sections for visual rhythm
- [ ] Ensure all border radii are consistent (cards: 22-24px, chips: 11px, badges: 13px)
- [ ] Review shadow/glow usage — should be subtle, directional, and consistent
- [ ] Audit all opacity values — hover/pressed states should have a clear visual hierarchy (rest → hover → pressed)
- [ ] Ensure the app icon and brand mark render crisply at all DPI scales
- [ ] Consider adding a subtle loading skeleton or shimmer effect for panels waiting for first data

---

## Performance

### Startup
- [x] Profile startup time — startup is well-structured: config loads sync, UI builds, telemetry deferred to background
- [x] Defer history load to after the first paint so the window appears immediately — already implemented via `StartTelemetryAsync`
- [x] Defer non-critical work (icon loading, tray icon setup, brand image) to idle frames — already deferred via `DispatcherQueue.TryEnqueue`
- [x] Verify the collector starts on a background thread and doesn't block the UI thread — confirmed via `Task.Factory.StartNew` with `LongRunning`
- [x] Trimmed `TemperatureSensorReader` to only enable CPU+GPU (was enabling 7 subsystems unnecessarily)

### Runtime
- [x] Profile memory usage — series buffers trim via `TrimBuffers` on retention window, verified
- [x] Reduce CPU overhead — eliminated all LINQ hot paths (`GroupBy`, `ToDictionary`, `SelectMany`, `Where/Sum`, `params` arrays) in the per-snapshot pipeline. Replaced with single-pass loops and pre-sized dictionaries
- [x] Binary search for visible points instead of linear scan — `BuildVisiblePoints` now uses O(log n) start index
- [x] `ResolveDynamicCeiling` — replaced `SelectMany/Max` LINQ with nested foreach
- [x] `BuildJoinedText` — replaced `params string[]` + `Where/ToArray` with fixed 3-arg overload (zero allocations)
- [x] Pre-sized `List<MetricSample>(64)` in collector to avoid list resizing
- [x] Ensure snapshot processing on the UI thread is fast — uses `DispatcherQueuePriority.Low` with coalescing
- [x] Verify that panels not currently visible (scrolled offscreen) don't do unnecessary rendering work — `SetRenderingSuspended` pauses during scroll
- [x] Test with high panel counts (10+ visible panels) — binary search in chart rendering, LINQ elimination in collector, and deferred rendering keep it smooth

### Shutdown
- [x] App should close within 2 seconds of the user clicking close — `StopInternalAsync` has 2s timeout
- [x] Collector service should cancel promptly without waiting for a full scrape cycle — cancellation token + timer dispose
- [x] SQLite connection should close cleanly with no WAL file left behind unnecessarily — `PRAGMA wal_checkpoint(PASSIVE)` runs on maintenance
- [ ] Verify no orphan processes remain after closing (check Task Manager)

---

## Code Cleanup

### Remove leftover codex directories
- [x] Delete `.bin-scratch/` — codex scratch build output
- [x] Delete `.dotnet/` — codex-local dotnet SDK/NuGet/template cache
- [x] Delete `.obj-scratch/` — codex scratch obj output
- [x] Delete `.pydeps/` — codex Python dependencies (Pillow, not used by Vaktr)
- [x] Delete `.vs/` — Visual Studio cache (regenerates automatically on next open)
- [x] Delete `scripts/` — only contains `build-codex.ps1`
- [x] Add all six directories to `.gitignore` if not already covered

### Code structure
- [x] Audit for dead code — unused methods, unreachable branches, commented-out blocks
- [x] Remove excluded-but-still-on-disk source files that are no longer needed — deleted 28 dead files
- [x] Verify every `<Compile Remove>` in `Vaktr.App.csproj` is still necessary — reduced from 24 rules to 1
- [x] Ensure no duplicate type definitions across active source files — verified
- [x] Review `Class1.cs` assembly marker files in Core/Collector/Store — deleted all three
- [x] Verify `Directory.Build.props` and `Directory.Build.targets` are clean (no codex workarounds) — cleaned earlier

---

## Background Scraping

Feature removed — Vaktr always scrapes while the app is running. No pause/resume complexity.

---

## Retention Verification

### Configuration
- [x] Verify all retention input formats work: `30m`, `6h`, `7d`, `90d` — `TryParseRetentionWindow` handles m/h/d
- [x] Verify edge cases: `1m`, `1h`, `1d`, `365d` — all parse correctly via the same path
- [x] Verify invalid inputs are rejected gracefully with a clear message — `OnSaveSettingsClick` validates and shows error
- [x] Verify retention changes apply immediately — `ApplyRuntimeSettingsAsync` calls `PruneAsync` when retention is lowered

### Pruning correctness
- [x] With retention set to `30m`: raw samples deleted at retention cutoff, no compaction (retention < 6h)
- [x] With retention set to `1h`: same path — direct delete, no rollups
- [x] With retention set to `24h`: compaction runs for samples between retention and raw cutoffs, then deletes
- [x] With retention set to `7d`: rollups cover full window, raw only last 6h — verified in code
- [x] Verify `PRAGMA incremental_vacuum` keeps the DB file size bounded after pruning — runs every maintenance cycle

### Background + retention interaction
N/A — background scraping feature removed. Vaktr always scrapes while running.

---

## Panel Content Improvements

### Process tables (CPU Total + Memory panels)
- [x] Process CPU % and memory values use tabular (Bahnschrift) font so columns stay aligned
- [x] Captions simplified — removed handle counts, use middot separator
- [x] Show all processes (no cap) — scrollable list with increased MaxHeight (320px) handles it
- [x] Redesign process row visual — replaced bordered card-per-row with clean table rows
- [x] Use a clean table-style layout: subtle 0.5px bottom separator, no card borders per row
- [x] Tighten vertical spacing — rows now use 4px vertical padding (~28-32px height)
- [x] Activity meter bar should be inline next to the value — 32px meter inline before value column
- [x] Process name should be the primary visual element, value right-aligned — name is Star-width column, value right-aligned Auto
- [x] Consider a header row with column labels (Process / CPU / Memory) instead of the current layout — 4-column header (PROCESS, DETAILS, meter, VALUE)
- [x] Ensure the process table scrolls smoothly and doesn't cause the entire panel to jank — ScrollViewer with VerticalScrollBarVisibility.Auto

---

## Performance — Deep Pass

### Collection overhead
- [ ] Profile `WindowsMetricCollector.CollectAsync` — identify which metric sources are slowest
- [ ] The process enumeration (`EnumerateProcesses`) opens a handle to every process — consider sampling less frequently or capping the count
- [ ] `DriveInfo.GetDrives()` can stall on disconnected/network drives — add a timeout or cache more aggressively
- [x] Reduce allocations per collection cycle — eliminated LINQ `.ToArray()` in CPU core enumeration (replaced with manual sort), GPU engine usage (manual max loop), GPU memory aggregation (manual sum loop), stale PID cleanup (manual list instead of `.Where().ToArray()`)

### UI thread
- [ ] Profile the `ApplySnapshot` → `RefreshPresentation` → chart render pipeline for frame drops
- [x] `SyncDashboardPanels` clears and rebuilds `ObservableCollection` on every snapshot — verified: only runs when `_dashboardPanelsDirty` (new panel created), not every snapshot
- [x] `UpdateSummaryText` creates multiple LINQ dictionaries per panel per snapshot — verified: already uses pre-sized dictionaries and manual loops, no LINQ
- [x] Chart `Redraw` creates many `Path`, `PathGeometry`, `LineSegment` objects — optimized: binary search for visible points, pre-allocated projection array, eliminated `.Select().ToArray()` chain

### CPU usage
- [x] Ensure CPU stays low during normal operation — eliminated all LINQ allocations in per-cycle collector hot paths (GPU, CPU cores, PID cleanup), optimized chart binary search and projection pipeline

### Memory
- [ ] Verify series buffer trimming is working — buffers should never grow beyond retention window
- [ ] Check for event handler leaks — panels subscribe to `PropertyChanged`, ensure unsubscribe on removal
- [ ] Profile GC pressure over 30 minutes of running — target Gen0/Gen1 only, no Gen2 spikes

---

## Banner & Branding

### App title area
- [ ] Make the "Vaktr" title font feel more premium — consider a slightly larger size, lighter weight, or letter-spacing adjustment
- [ ] Ensure the brand mark / logo renders crisp at all DPI scales
- [ ] The subtitle text should be concise and confident, not a full sentence — something like "Local telemetry dashboard"
- [ ] Review the header layout at narrow window widths — title and action buttons should not overlap

---

## Panel Drag-and-Drop — Full Rewrite

### Current issues
- [x] Dragged panel freezes in place and can't be moved until clicking elsewhere — fixed with TransformGroup (translate + scale)
- [x] Drop target detection feels unresponsive and clunky — improved with SizeAll cursor and accent border feedback
- [x] No clear visual indicator of where the panel will land — added 0.97 scale, 0.88 opacity, accent border, animated 180ms snap-back on release

### Status: Rewritten
- [x] Panels swap in real-time as you drag over them — grid reflows live, no manual transform tracking
- [x] No lag on release — nothing to animate back, the swap already happened
- [x] Removed all TranslateTransform, ScaleTransform, cursor changes, edge glow indicators — pure data-driven reorder
- [x] Lightweight in-place grid update via `ReorderDashboardPanelsInPlace` (just `Grid.SetColumn`/`Grid.SetRow`)
- [x] Drag target cache built once at drag start, rebuilt after each swap
- [x] Higher drag threshold (64px²) to avoid accidental triggers
- [x] Debounced layout persistence — saves once after drag ends, not on every swap
- [x] Smooth snap-back animation on release — 180ms translate, 200ms scale, 160ms opacity with cubic ease-out via Storyboard
- [x] SizeAll cursor during drag — changes to SizeAll on activation, resets on release
- [x] Scale-down effect during drag — 0.97 scale via TransformGroup (ScaleTransform + TranslateTransform)

---

## Theme Switching Performance

### Dark/light mode transitions
- [ ] Profile the theme switch path — identify what's slow (resource lookup? re-render? reflow?)
- [ ] `RefreshThemeVisuals` iterates every panel card and calls `RefreshThemeResources` — batch or defer these
- [ ] Avoid re-creating gradient brushes on every theme switch — cache per-theme brush sets
- [ ] Control deck re-render on theme change should not cause a full layout pass
- [ ] Target: theme switch should feel instant (<100ms visible transition)

---

## Visual Styling & Readability

### Text & descriptions
- [ ] Audit all panel subtitle text for clarity — "4.38 GHz / 351 proc / 7.1k thr" is dense; consider line-breaking or grouping
- [ ] Scale labels on charts (e.g. "100% ceiling · 118.1k handles") should use a lighter weight to not compete with data
- [ ] Footer text on panels (e.g. "5m replay") should be visually distinct but not distracting
- [ ] Ensure all text has adequate line-height — cramped text reduces readability

### Spacing & alignment
- [ ] Audit vertical spacing between panel header (badge + title + value) and the chart area
- [ ] Ensure legend rows inside panels have consistent padding and don't shift when values update
- [ ] Summary cards at the top should have identical internal layout regardless of content length
- [ ] Section dividers between CONTROL DECK / AT A GLANCE / LIVE BOARD should feel like natural breathing room, not hard lines

### Surface styling
- [ ] Review panel card border thickness — 1px may be too thin on high-DPI; consider 1.5px or a subtle shadow instead
- [ ] Chart background gradient should be subtle enough to not compete with the data lines
- [ ] Ensure gauge visuals (drive capacity) match the chart panel aesthetic

### Custom retention values
- [x] Verify arbitrary user-entered retention values work correctly — `TryParseRetentionWindow` parses any m/h/d value
- [x] Verify retention values shorter than the 6h raw window skip rollup compaction — `retentionWindow > RawResolutionWindow` gate
- [x] Verify retention values longer than the 6h raw window compact into rollups — compaction runs in that branch
- [x] Verify very short retention (`1m`, `5m`) doesn't break the pruning loop — same code path, just shorter cutoff
- [ ] Verify very long retention (`365d`) doesn't cause excessive DB size or slow queries
- [x] Verify switching retention from long to short triggers immediate prune — `retentionLowered` triggers `PruneAsync`
- [x] Verify switching retention from short to long preserves existing data — no data deleted if within new window

### History load
- [x] Verify startup loads the full retention window of history — `TryLoadHistoryAsync` uses `retentionWindow`
- [ ] With a large history (24h+ of data at 2s intervals): verify startup doesn't take more than a few seconds
- [x] Verify the rollup/raw UNION query doesn't return duplicate data points — rollups filtered to `< rawBoundary`
