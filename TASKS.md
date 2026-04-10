# TASKS.md - Vaktr v1.0.1

**Release focus:** Performance, UI/UX, and CI/CD practices.

---

## Table of Contents

1. [Performance](#1-performance)
2. [UI/UX - Chart & Panel Behavior](#2-uiux---chart--panel-behavior)
3. [UI/UX - Visual Design & Polish](#3-uiux---visual-design--polish)
4. [UI/UX - New Features](#4-uiux---new-features)
5. [CI/CD](#5-cicd)
6. [Testing](#6-testing)
7. [Security](#7-security)
8. [Documentation](#8-documentation)
9. [Stretch Goals](#9-stretch-goals---things-to-consider)

---

## 1. Performance

### 1.1 - Reduce CPU and memory usage during long-running sessions
**Priority:** Critical
**Area:** `Vaktr.App` (ViewModels, Controls), `Vaktr.Store`

With 2+ days of accumulated metrics, zooming out to view all data causes Vaktr to consume upwards of 9% of an 8-core CPU. This is unacceptable for a monitoring tool that should remain lightweight in the background.

**Investigation areas:**
- `MetricPanelViewModel` series buffer growth over time - are old points being trimmed when outside the retention window?
- `TelemetryChart.Redraw()` downsampling efficiency - is the point budget appropriate for large datasets?
- Memory allocations per render cycle - look for `List<>` / `IReadOnlyList<>` copies that could be reduced
- GC pressure from frequent snapshot processing (`ApplySnapshot` runs every scrape interval)
- Consider object pooling for `MetricPoint`, `MetricSample`, and chart rendering geometry
- Profile `LoadHistoryAsync` UNION query performance with large datasets
- Evaluate whether `ObservableCollection` change notifications cascade unnecessary UI work

### 1.2 - Fix app hitching and stuttering on long time ranges
**Priority:** Critical
**Area:** `Vaktr.App/Controls/TelemetryChart.cs`, `Vaktr.App/ShellWindow.Polished.cs`

With 2 days of built-up metrics, the app hitches and stutters with every metrics scrape. The UI thread is likely blocked by chart rendering or data processing.

**Investigation areas:**
- `ApplyQueuedSnapshot()` runs on the dispatcher - measure how long it takes with large datasets
- `RefreshPresentation()` on every panel after every snapshot - can this be throttled or made incremental?
- `TelemetryChart.Redraw()` is called for every visible panel on every snapshot - consider dirty-checking to skip unchanged panels
- Chart canvas operations (geometry building, point projection) may need to move to a background thread with results marshalled back
- `SplitAtGaps()` and `Downsample()` complexity with thousands of points
- Multiple panels redrawing simultaneously - consider staggering or virtualizing off-screen panels

### 1.3 - Improve theme switching performance
**Priority:** Medium
**Area:** `Vaktr.App/App.cs`, `Vaktr.App/ShellWindow.Polished.Ui.cs`

Theme switching triggers a full refresh of all controls, gradient cache clearing, and UI rebuilds.

**Investigation areas:**
- `_gradientCache` is cleared and fully rebuilt - can this be done lazily?
- `RefreshThemeResources()` is called on every control individually - batch or defer updates
- `RenderControlDeckSummary()` / `RenderEditableControlDeck()` full rebuilds on theme change - can brush bindings update in place?
- Consider pre-computing both palettes at startup and swapping references instead of recreating brushes

### 1.4 - Improve time range switching performance
**Priority:** Medium
**Area:** `Vaktr.App/ViewModels/DashboardViewModels.cs`, `Vaktr.App/Controls/TelemetryChart.cs`

Switching between time range presets triggers `ApplyRangePreset` on every panel followed by `RefreshPresentation` and full chart redraws.

**Investigation areas:**
- `RefreshPresentation()` rebuilds `VisibleSeries` from scratch - can it filter in-place?
- All panels refresh even if not visible on screen - skip off-screen panels
- `LoadHistoryAsync` may re-query the database on range changes - check if this can use cached data
- Chart `Redraw()` does full canvas clear + redraw - consider incremental rendering for range shifts

### 1.5 - Improve window resize smoothness
**Priority:** Medium
**Area:** `Vaktr.App/ShellWindow.Polished.cs`

Window resizing triggers `OnRootLayoutSizeChanged` which recalculates column counts and may trigger `RefreshDashboardPanels()` and chart redraws.

**Investigation areas:**
- Debounce resize events to avoid redundant layout recalculations
- `DetermineDashboardColumns()` has hysteresis but chart redraws may not be throttled
- Grid re-layout with many panels causes visual jank - consider freezing chart rendering during resize and redrawing once on resize end
- Summary card rebuilds during resize

### 1.6 - Improve scroll smoothness
**Priority:** Medium
**Area:** `Vaktr.App/ShellWindow.Polished.cs`, `Vaktr.App/Controls/TelemetryPanelCard.cs`

Scrolling the main window should be buttery smooth. The `_isRenderingSuspended` flag exists but may not be sufficient.

**Investigation areas:**
- `SetPanelRenderingSuspended` is toggled on `OnScrollHostViewChanged` - verify it actually prevents all rendering work
- Panel cards outside the viewport should skip all processing, not just chart rendering
- Legend and process table updates may still fire during scroll
- Consider UI virtualization for panels far outside the viewport

### 1.7 - Improve process chart toggle performance
**Priority:** Medium
**Area:** `Vaktr.App/ViewModels/DashboardViewModels.cs` (`MetricPanelViewModel`)

Toggling the "Chart" button on CPU/Memory process panels to show per-process chart series causes noticeable lag.

**Investigation areas:**
- `PerProcessChartsEnabled` toggle triggers `RefreshPresentation()` which rebuilds all series
- `MaxProcessChartSeries` is 5 - verify this cap is enforced before series construction
- Per-process series may create many small `ChartSeriesViewModel` objects
- Historical per-process data may not be available - verify `LoadHistoryAsync` includes process series, or handle the gap gracefully

---

## 2. UI/UX - Chart & Panel Behavior

### 2.1 - Fix chart line flickering and false spikes on longer time ranges
**Priority:** Critical
**Area:** `Vaktr.App/Controls/TelemetryChart.cs`, `Vaktr.App/ViewModels/DashboardViewModels.cs`

When viewing longer time ranges, chart lines flicker and display false spikes that disappear on the next scrape. For example, zooming in on 2-day-old data shows a 50% CPU spike that changes to a different value on the next scrape.

**Root cause candidates:**
- `Downsample()` algorithm may select different representative points on each render when the window shifts by one scrape interval
- Rollup data (`metric_rollups_1m` averages) being mixed with raw data at the boundary creates value discontinuities
- The 6-hour `RawResolutionWindow` boundary shifts with each render, causing points to switch between raw and rolled-up values
- `SplitAtGaps()` gap threshold (3x expected interval) may be too aggressive for rolled-up data which has 60s intervals
- Series buffer trimming during `AppendSample` may remove points that are still visible in the current window

**Fix approach:**
- Ensure downsampling is deterministic (same input range = same output points)
- Stabilize the raw/rollup boundary so points don't flip between sources
- Consider snapping the query boundary to minute boundaries

### 2.2 - Freeze panels when user is zoomed into a specific range
**Priority:** High
**Area:** `Vaktr.App/ViewModels/DashboardViewModels.cs`, `Vaktr.App/ShellWindow.Polished.cs`

When a user zooms into a specific historical time range to inspect data, the panel should not update or "move the needle" with new scrapes. The panel should remain static so the user can review data without it shifting. Updates should resume only when the user zooms back out to real-time view.

**Implementation approach:**
- `MetricPanelViewModel.IsZoomed` already exists - use it to gate `AppendSample()` and `RefreshPresentation()` calls
- New samples should still be buffered (appended to the backing data) but the visible series and chart should not re-render
- When the user resets zoom (`ResetZoom()`), apply all buffered updates and resume live rendering
- Summary cards should continue updating regardless of zoom state
- Status text or a visual indicator should show that a panel is in "frozen/inspection" mode

### 2.3 - Show date in panel timestamps
**Priority:** High
**Area:** `Vaktr.App/Controls/TelemetryChart.cs`

Panel timestamps currently show only the time. When viewing historical data from previous days, the date portion of the timestamp needs to be visible so users know which day they're looking at.

**Implementation approach:**
- Chart X-axis labels should show date when the visible window spans more than 24 hours, or when the data is from a different day than today
- Hover tooltips should always include the full date and time
- Format examples: "Apr 8 14:30" for multi-day views, "14:30:05" for same-day views

### 2.4 - Fix zoom-to-selection accuracy for historical data
**Priority:** High
**Area:** `Vaktr.App/Controls/TelemetryChart.cs`

Zooming in on a panel's past history for older metrics should zoom into exactly where the user highlighted, similar to Grafana. Currently the zoom selection may not map accurately to the intended time range.

**Investigation areas:**
- `OnPointerPressed` / `OnPointerReleased` selection rectangle → time range conversion in `Project()` / reverse projection
- The `MinimumSelectionWidth` (18px) threshold may reject valid small selections
- Verify that `ZoomSelectionRequested` event args contain the correct UTC timestamps corresponding to the visual selection
- Check that `ZoomToWindow(start, end)` applies the exact requested range without snapping or rounding

### 2.5 - Sort CPU cores in numerical order
**Priority:** Medium
**Area:** `Vaktr.App/ViewModels/DashboardViewModels.cs` or `Vaktr.Collector/WindowsMetricCollector.cs`

The CPU per-core panel should list cores in order: Core 0, Core 1, Core 2, etc. Currently they may appear in arbitrary order.

**Investigation areas:**
- `AddCpuUsage()` uses `TryGetArrayValues()` which returns a `Dictionary<string, double>` - dictionary ordering is not guaranteed
- Series are added in iteration order from the PDH array results
- The legend in `TelemetryPanelCard` renders series in the order they appear in `VisibleSeries`
- Fix: sort the PDH counter instances numerically before creating `MetricSample` entries, or sort `VisibleSeries` in the ViewModel

### 2.6 - Fix max value display accuracy on panels
**Priority:** Medium
**Area:** `Vaktr.App/ViewModels/DashboardViewModels.cs`

Panel max values (shown in subtle gray boxes) need to display the total system maximum rather than a dynamic value. For example, the memory panel shows "28Gi" as max but the system has 32 GiB of RAM, and the value fluctuates.

**Investigation areas:**
- `ChartCeilingValue` and `ScaleLabel` in `MetricPanelViewModel` - how are these computed?
- Memory: should always show total physical RAM (from `GlobalMemoryStatusEx.TotalPhys`)
- CPU: should always be 100%
- Disk: should show total drive capacity
- GPU VRAM: should show total VRAM
- Network/throughput: dynamic ceiling is fine (no fixed max)
- Temperature: dynamic ceiling is fine
- The ceiling value may be calculated from visible data range rather than hardware limits

---

## 3. UI/UX - Visual Design & Polish

### 3.1 - Add threshold-based color coding for utilization
**Priority:** Medium
**Area:** `Vaktr.App/Controls/TelemetryChart.cs`, `Vaktr.App/Controls/TelemetryPanelCard.cs`, `Vaktr.App/Controls/UsageGauge.cs`

Add color-coded thresholds for utilization metrics:
- **Under 75%**: Green (healthy)
- **75-90%**: Yellow (warning)
- **Over 90%**: Orange/red (critical)

**Applies to:**
- CPU utilization (total and per-core if not a significant performance hit)
- GPU utilization
- Memory utilization
- Disk utilization (per-drive)

**Implementation considerations:**
- Summary card gauges already have some color logic (yellow >75%, orange >90%) - standardize and extend
- Chart line color could shift based on value, or use colored background bands/zones
- Panel card border or badge could reflect current threshold state
- Avoid excessive per-frame color recalculation - cache threshold state and only update on value change
- Per-core CPU threshold coloring could be expensive with many cores - profile before enabling

### 3.2 - Improve panel hover and button hover smoothness
**Priority:** Medium
**Area:** `Vaktr.App/Controls/ActionChip.cs`, `Vaktr.App/Controls/TelemetryPanelCard.cs`

Hover interactions on panels and buttons should feel smooth and responsive without visual hitches.

**Investigation areas:**
- `ActionChip.UpdateVisualState()` creates new brush objects on every state change - cache brushes
- Hover transitions may be instant (no animation) - consider subtle opacity or color transitions
- Panel card hover effects may trigger layout recalculations
- Pointer enter/leave events during rapid mouse movement

### 3.3 - Add subtle design touches for panel readability
**Priority:** Low
**Area:** `Vaktr.App/Controls/TelemetryPanelCard.cs`, `Vaktr.App/Controls/TelemetryChart.cs`

General polish pass on panels to improve readability and design quality.

**Ideas to evaluate:**
- Subtle separator lines between chart area, legend, and process table
- Improved legend layout for panels with many series
- Better visual distinction between the active/selected range button and inactive ones
- Consistent padding and spacing across all panel sections
- Fade edges on chart area to indicate data continues beyond visible window
- Subtle animation when new data points arrive (only in real-time mode, not when zoomed)

---

## 4. UI/UX - New Features

### 4.1 - Add 2d and 5d quick range presets
**Priority:** High
**Area:** `Vaktr.Core/Models/Enums.cs`, `Vaktr.App/ShellWindow.Polished.cs`, `Vaktr.App/Controls/TelemetryPanelCard.cs`, `Vaktr.App/ViewModels/DashboardViewModels.cs`

Add 2-day and 5-day options to the quick range preset buttons for further drill-down into historical data.

**Implementation:**
- Add `TwoDays = 2880` and `FiveDays = 7200` to `TimeRangePreset` enum
- Add "2d" and "5d" buttons to the global range editor and per-panel range buttons
- Ensure `LoadHistoryAsync` and chart rendering handle these ranges efficiently (see performance tasks)

### 4.2 - Allow selecting individual series for focused drill-down
**Priority:** Medium
**Area:** `Vaktr.App/Controls/TelemetryPanelCard.cs`, `Vaktr.App/ViewModels/DashboardViewModels.cs`

Allow users to click on a subfield (e.g., Up/Down on network, Core 0/Core 3 on CPU, a specific process) to isolate that single series on the panel chart.

**Implementation approach:**
- Clicking a legend entry toggles that series as the "focused" series
- When a series is focused, other series are hidden or shown at very low opacity
- Clicking again or clicking a "Show All" option restores all series
- For process panels, clicking a process name in the table isolates that process on the chart
- The chart should rescale the Y-axis to fit the focused series for better visibility

### 4.3 - Add click-to-pin static tooltip on chart data points
**Priority:** Medium
**Area:** `Vaktr.App/Controls/TelemetryChart.cs`

Allow clicking on a line in a graph to display a pinned/static tooltip showing the exact timestamp, date, and value at that point, similar to Grafana's click-to-inspect behavior.

**Implementation approach:**
- Single click on a data point (or nearest point on the line) pins a tooltip at that position
- Pinned tooltip shows: full date + time, value with unit, series name
- Multiple tooltips could be pinned simultaneously for comparison
- Click on empty area or press Escape to dismiss pinned tooltips
- Pinned tooltips should not scroll with data updates (they're anchored to a specific timestamp)
- Visual: small marker dot at the pinned point + tooltip card with details

### 4.4 - Allow viewing historic per-process usage
**Priority:** Medium
**Area:** `Vaktr.App/ViewModels/DashboardViewModels.cs`, `Vaktr.Collector/WindowsMetricCollector.cs`, `Vaktr.Store/Persistence/SqliteMetricStore.cs`

When the "Chart" toggle is enabled on CPU/Memory process panels, users should be able to see historical per-process usage, not just live data.

**Implementation considerations:**
- Per-process data is already collected as `ProcessActivitySample` in `LiveBoardDetails`
- Need to verify per-process samples are being stored in `metric_samples` with appropriate panel/series keys
- Historical per-process data could be very large (many processes x many scrapes) - consider:
  - Only storing top N processes per scrape
  - Aggressive rollup/compaction for per-process data
  - Lazy loading of per-process history only when the chart toggle is activated
- Performance: loading and rendering dozens of process series over days of data must remain lightweight

---

## 5. CI/CD

### 5.1 - Review CI/CD practices for coverage
**Priority:** High
**Area:** `.github/workflows/ci.yml`

Review the current CI pipeline to ensure adequate build and test coverage.

**Checklist:**
- [ ] CI builds on all supported platforms (currently only x64 - should it also build ARM64?)
- [ ] CI runs tests with code coverage reporting (coverlet is present but coverage thresholds may not be enforced)
- [ ] CI validates the installer build (currently only done in release workflow)
- [ ] Consider adding a lint or format check step (dotnet format)
- [ ] Consider adding build warnings as errors in CI
- [ ] Verify artifact retention policies are appropriate
- [ ] Consider adding a smoke test that launches the built app and verifies it starts

### 5.2 - Review security CI actions
**Priority:** High
**Area:** `.github/workflows/security.yml`

Review the security pipeline for coverage gaps.

**Checklist:**
- [ ] `dotnet list --vulnerable` checks transitive dependencies - verify it catches all severity levels
- [ ] Gitleaks scan uses `continue-on-error: true` - should this fail the pipeline? Yes.
- [ ] Code quality job uses `TreatWarningsAsErrors=false` - consider enabling for security-relevant analyzers
- [ ] Consider adding SAST (static application security testing) beyond the built-in Roslyn analyzers
- [ ] Consider adding binary scanning of the published output
- [ ] Verify the Gitleaks config covers all sensitive patterns relevant to this project
- [ ] Consider adding dependency license compliance checking
- [ ] CODEOWNERS file for security-sensitive paths

### 5.3 - Support installers for all CPU architectures
**Priority:** Medium
**Area:** `.github/workflows/release.yml`, `installer/vaktr-setup.iss`

The app supports x64, ARM64, and x86 architectures, but the release workflow and installer only build for x64.

**Implementation:**
- Add a build matrix to `release.yml` for win-x64, win-arm64 (and optionally win-x86)
- Create separate publish outputs per architecture
- Either create per-architecture installer exes or a single installer that detects architecture
- Update the Inno Setup script to handle multiple architecture outputs
- Update the GitHub Release to attach all architecture variants
- Consider whether x86 support is worth maintaining (Windows 10+ on 32-bit is rare)

---

## 6. Testing

### 6.1 - Expand unit test coverage
**Priority:** Medium
**Area:** `Vaktr.Tests/`

The current test suite has a single test (`Normalize_Resets_Out_Of_Range_Values`). Expand coverage significantly for v1.0.1.

**Recommended test areas:**

**Vaktr.Core:**
- [ ] `VaktrConfig.Normalize()` - edge cases (max values, boundary conditions, null storage paths)
- [ ] `VaktrConfig.TryParseRetentionWindow()` - all formats ("5m", "24h", "7d"), invalid inputs, edge cases
- [ ] `VaktrConfig.FormatRetentionInput()` - round-trip consistency
- [ ] `VaktrConfig.GetRetentionWindow()` - interaction between RetentionInputText and MaxRetentionHours
- [ ] `VaktrConfig.CreateDefault()` - verify default values match documentation

**Vaktr.Store:**
- [ ] `SqliteMetricStore.InitializeAsync()` - schema creation, re-initialization with same/different path
- [ ] `SqliteMetricStore.AppendSnapshotAsync()` - single sample, multiple samples, empty snapshot
- [ ] `SqliteMetricStore.LoadHistoryAsync()` - empty database, data within range, data outside range, raw + rollup boundary
- [ ] `SqliteMetricStore.PruneAsync()` - verify old data is deleted, recent data preserved
- [ ] `CompactAndPruneAsync()` - rollup accuracy (averages match), boundary handling
- [ ] `JsonConfigStore.LoadAsync()` - missing file, corrupted JSON, legacy path migration
- [ ] `JsonConfigStore.SaveAsync()` - round-trip consistency, directory creation

**Vaktr.Collector:**
- [ ] `CollectorService` lifecycle - start/stop/restart, concurrent start calls, dispose during collection
- [ ] `CollectorService` event emission - SnapshotCollected fires, CollectionFailed fires on error
- [ ] `TemperatureSensorReader` - graceful failure when no sensors available

**Vaktr.App (ViewModels only - no UI dependencies):**
- [ ] `MainViewModel.BuildConfig()` / `ApplyConfig()` - round-trip consistency
- [ ] `MainViewModel.ApplySnapshot()` - creates panels, updates existing panels
- [ ] `MainViewModel.ApplyPanelVisibility()` - toggle visibility correctly
- [ ] `MetricPanelViewModel.AppendSample()` - adds to correct series
- [ ] `MetricPanelViewModel` sorting - `SortBucket` ordering

---

## 7. Security

### 7.1 - Audit for security issues and vulnerabilities
**Priority:** High
**Area:** Entire codebase

Perform a security review of the codebase.

**Checklist:**
- [ ] **SQL injection**: `SqliteMetricStore` uses parameterized queries (`$panelKey`, etc.) - verify no string interpolation in any SQL
- [ ] **Path traversal**: `StorageDirectory` from user input - verify it's validated/normalized before use as a file path
- [ ] **Registry access**: `AutoLaunchService` writes to HKCU Run key - verify no injection via process path
- [ ] **P/Invoke safety**: All kernel32/pdh.dll calls - verify handle cleanup (no leaks), buffer size validation
- [ ] **Process handle leaks**: `OpenProcess` calls in `EnumerateProcesses` - verify every handle is closed via `CloseHandle`
- [ ] **Unvalidated config**: `JsonConfigStore.LoadAsync` deserializes user-editable JSON - verify all values are normalized before use
- [ ] **Exception information disclosure**: Error messages in UI or logs - verify no sensitive system info is exposed
- [ ] **Dependency vulnerabilities**: Run `dotnet list --vulnerable` locally and address any findings
- [ ] **LibreHardwareMonitor**: Runs with user privileges - verify it doesn't escalate or expose kernel driver attack surface
- [ ] **Installer security**: Verify the `taskkill /F` in installer can't be exploited (e.g., killing wrong process)
- [ ] **File permissions**: Verify `%LocalAppData%\Vaktr\` files are created with appropriate permissions (user-only)

---

## 8. Documentation

### 8.1 - Update README.md
**Priority:** Low
**Area:** `README.md`

Review and update the README for v1.0.1 changes.

**Checklist:**
- [ ] Update version references from 1.0.0 to 1.0.1
- [ ] Document new features (2d/5d ranges, series isolation, click-to-pin tooltips, threshold colors)
- [ ] Update screenshots if UI has changed significantly
- [ ] Update the defaults table if any defaults changed
- [ ] Add any new keyboard shortcuts or interactions
- [ ] Update architecture section if data flow changed

---

## 9. Stretch Goals - Things to Consider

These are additional improvements worth evaluating for v1.0.1 if time permits. They address gaps not explicitly listed above but would meaningfully improve the product.

### 9.1 - Panel virtualization
Panels far off-screen currently still exist in the visual tree and may receive updates. True UI virtualization (only creating panel controls when they scroll into view) would significantly improve performance with many panels.

### 9.2 - Incremental chart rendering
Instead of full canvas clear + redraw on every update, consider appending new points to the existing canvas path and only redrawing the newly visible portion. This would dramatically reduce rendering cost for real-time updates.

### 9.3 - Background data processing
Move snapshot processing (`ApplySnapshot`, `RefreshPresentation`, downsampling) off the UI thread entirely. Compute the final render state on a background thread, then marshal only the final canvas commands to the UI thread.

### 9.4 - Memory-mapped or streaming history queries
For very long time ranges (7d, 30d), loading all history into memory at once is expensive. Consider streaming results from SQLite and downsampling on-the-fly, or using memory-mapped access for the database.

### 9.5 - Single-instance enforcement
The installer checks for a running mutex, but the app itself should enforce single-instance behavior to prevent duplicate collection and database contention. Consider using a named mutex in `Program.Main`.

### 9.6 - Graceful degradation when sensors are unavailable
If a GPU is removed (e.g., eGPU disconnect), or sensors become unavailable, the app should handle this gracefully rather than showing stale or errored panels. Consider marking panels as "sensor unavailable" and dimming them.

### 9.7 - Keyboard shortcuts
Add keyboard shortcuts for common actions:
- `Escape` to reset zoom on all panels
- `1`-`9` for quick time range presets
- `T` to toggle theme
- `S` to open settings

### 9.8 - Database size monitoring
Show the current database file size somewhere in the Control Deck or status bar. Users with long retention windows should be aware of storage impact. Consider a warning when the database exceeds a threshold (e.g., 500 MB, 1 GB).
