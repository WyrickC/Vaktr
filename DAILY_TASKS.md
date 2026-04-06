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
- [ ] Add subtle fade or slide when panels appear for the first time after startup
- [ ] Smooth the panel drag-and-drop — dragged card should feel lifted (scale + shadow), drop target should clearly invite the drop
- [ ] Ensure theme switching feels smooth (no flash of unstyled content)
- [ ] Panel expand/collapse overlay should animate in/out rather than appearing instantly

### Content-first refinements
- [ ] Remove any decorative elements that don't serve the data (unnecessary borders, redundant labels, ornamental glows that distract)
- [ ] Ensure empty/loading states are informative and visually calm, not alarming
- [ ] Review the control deck — settings should be scannable at a glance, not a wall of options
- [ ] Footer text should add value or be removed — avoid filler

### Interaction polish
- [ ] Hover states on all interactive elements (chips, buttons, panels) should feel responsive and consistent
- [ ] Ensure keyboard accessibility — tab order through controls, enter/space to activate buttons
- [ ] Chart hover tooltips should track smoothly and dismiss cleanly
- [ ] Time range chip selection should give immediate visual feedback (active state)

---

## Performance

### Startup
- [ ] Profile startup time — target: window visible with shell chrome in under 1 second
- [ ] Defer history load to after the first paint so the window appears immediately
- [ ] Defer non-critical work (icon loading, tray icon setup, brand image) to idle frames
- [ ] Verify the collector starts on a background thread and doesn't block the UI thread

### Runtime
- [ ] Profile memory usage over 1 hour of running — ensure series buffers are trimmed and not leaking
- [ ] Verify CPU overhead of Vaktr itself stays under 1% during normal 2s scrape interval
- [ ] Ensure snapshot processing on the UI thread is fast — no dropped frames during updates
- [ ] Verify that panels not currently visible (scrolled offscreen) don't do unnecessary rendering work
- [ ] Test with high panel counts (10+ visible panels) — grid layout and refresh should stay smooth

### Shutdown
- [ ] App should close within 2 seconds of the user clicking close
- [ ] Collector service should cancel promptly without waiting for a full scrape cycle
- [ ] SQLite connection should close cleanly with no WAL file left behind unnecessarily
- [ ] Verify no orphan processes remain after closing (check Task Manager)

---

## Code Cleanup

### Remove leftover codex directories
- [ ] Delete `.bin-scratch/` — codex scratch build output
- [ ] Delete `.dotnet/` — codex-local dotnet SDK/NuGet/template cache
- [ ] Delete `.obj-scratch/` — codex scratch obj output
- [ ] Delete `.pydeps/` — codex Python dependencies (Pillow, not used by Vaktr)
- [ ] Delete `.vs/` — Visual Studio cache (regenerates automatically on next open)
- [ ] Delete `scripts/` — only contains `build-codex.ps1`
- [ ] Add all six directories to `.gitignore` if not already covered

### Code structure
- [ ] Audit for dead code — unused methods, unreachable branches, commented-out blocks
- [ ] Remove excluded-but-still-on-disk source files that are no longer needed (e.g. `TemperatureBridge.cs`, old `MainViewModel.cs`, `ShellWindow.cs`, `ShellWindow.Minimal.cs`, old XAML code-behinds)
- [ ] Verify every `<Compile Remove>` in `Vaktr.App.csproj` is still necessary — remove entries for files that no longer exist
- [ ] Ensure no duplicate type definitions across active source files
- [ ] Review `Class1.cs` assembly marker files in Core/Collector/Store — consolidate or remove if not needed
- [ ] Verify `Directory.Build.props` and `Directory.Build.targets` are clean (no codex workarounds)

---

## Background Scraping

### New feature: "Scrape in background" toggle
- [ ] Add a checkbox to the control deck: "Collect metrics when minimized" (default: off)
- [ ] When off: pause the collector when the window is minimized or closed to tray — no DB writes, no CPU usage
- [ ] When on: collector keeps running in the background so historical data accumulates
- [ ] When the user reopens Vaktr after background collection, load the accumulated history and render it seamlessly
- [ ] Persist this setting in `vaktr-settings.json`
- [ ] Ensure the tray icon tooltip reflects whether background collection is active
- [ ] When the user fully exits Vaktr (right-click tray > Exit, or close without minimize-to-tray), always stop the collector regardless of this setting

---

## Retention Verification

### Configuration
- [ ] Verify all retention input formats work: `30m`, `6h`, `7d`, `90d`
- [ ] Verify edge cases: `1m`, `1h`, `1d`, `365d`
- [ ] Verify invalid inputs are rejected gracefully with a clear message
- [ ] Verify retention changes apply immediately — lowering retention should prune data on the next maintenance cycle

### Pruning correctness
- [ ] With retention set to `30m`: after 35 minutes of running, confirm no raw samples older than 30 minutes exist in the DB
- [ ] With retention set to `1h`: confirm raw samples older than 1h are deleted and no rollups exist (retention < 6h raw window)
- [ ] With retention set to `24h`: confirm raw samples older than 6h are rolled up to 1-minute aggregates, and nothing older than 24h exists
- [ ] With retention set to `7d`: confirm rollups cover the full 7-day window and raw data is only the last 6 hours
- [ ] Verify `PRAGMA incremental_vacuum` keeps the DB file size bounded after pruning

### Background + retention interaction
- [ ] When background scraping is on and retention is `1h`: confirm the DB doesn't grow unbounded overnight
- [ ] When background scraping is off: confirm no new data is written while the app is minimized
- [ ] When reopening after background collection: verify the time range selector correctly shows the available data range

### Custom retention values
- [ ] Verify arbitrary user-entered retention values work correctly: `15m`, `45m`, `2h`, `3d`, `14d`, `60d`, `180d`, `365d`
- [ ] Verify retention values shorter than the 6h raw window (`15m`, `30m`, `1h`, `3h`) skip rollup compaction and delete raw samples directly at the retention cutoff
- [ ] Verify retention values longer than the 6h raw window (`12h`, `2d`, `30d`) compact raw samples into 1-minute rollups before deleting
- [ ] Verify very short retention (`1m`, `5m`) doesn't break the pruning loop or leave stale data
- [ ] Verify very long retention (`365d`) doesn't cause excessive DB size or slow queries
- [ ] Verify switching retention from long to short (e.g. `30d` to `1h`) triggers an immediate prune of the excess data
- [ ] Verify switching retention from short to long (e.g. `1h` to `7d`) doesn't lose data that's still within the new window (already-pruned data is gone, but everything still in the DB should be preserved)

### History load
- [ ] Verify startup loads the full retention window of history (not just the graph window)
- [ ] With a large history (24h+ of data at 2s intervals): verify startup doesn't take more than a few seconds
- [ ] Verify the rollup/raw UNION query doesn't return duplicate data points in the overlap window
