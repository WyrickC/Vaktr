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
- [ ] Hover states on all interactive elements (chips, buttons, panels) should feel responsive and consistent
- [ ] Ensure keyboard accessibility — tab order through controls, enter/space to activate buttons
- [ ] Chart hover tooltips should track smoothly and dismiss cleanly
- [ ] Time range chip selection should give immediate visual feedback (active state)

### Chart & data display
- [x] Show gaps in chart lines when data is missing (no connecting lines across time gaps) — gaps detected via median interval × 3 threshold
- [ ] Add subtle gridline fade at chart edges for depth
- [ ] Improve chart axis label legibility — ensure labels don't overlap at narrow widths
- [ ] Add a subtle glow or highlight on the most recent data point

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
- [ ] Profile memory usage over 1 hour of running — ensure series buffers are trimmed and not leaking
- [ ] Verify CPU overhead of Vaktr itself stays under 1% during normal 2s scrape interval
- [x] Ensure snapshot processing on the UI thread is fast — uses `DispatcherQueuePriority.Low` with coalescing
- [x] Verify that panels not currently visible (scrolled offscreen) don't do unnecessary rendering work — `SetRenderingSuspended` pauses during scroll
- [ ] Test with high panel counts (10+ visible panels) — grid layout and refresh should stay smooth

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
