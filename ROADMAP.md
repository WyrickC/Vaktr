# Vaktr Roadmap

## Planned

### CPU Temperature Monitoring
CPU temperature sensors on AMD and Intel require kernel-level access (PCI config space / MSR registers) that Windows restricts to signed kernel drivers. Every monitoring app that shows CPU temps (HWiNFO, Core Temp, MSI Afterburner) ships a signed driver for this.

LibreHardwareMonitor supports CPU temps through the [PawnIO](https://pawnio.eu/) kernel driver. The plan is to bundle PawnIO into a Vaktr installer so CPU temperatures work out of the box with no extra setup.

**Dependencies:**
- Build a proper installer (MSI or MSIX) for Vaktr distribution
- Bundle PawnIO driver install into the Vaktr installer
- CPU temp panel already exists in the UI — just needs live data

**Why not now:** Vaktr doesn't have an installer yet. Bundling a kernel driver requires a proper install/uninstall flow. GPU temps, CPU usage, memory, disk, network, and all other metrics work without a driver.

### Vaktr Installer
Build an MSI or MSIX installer for clean distribution. This is a prerequisite for bundling PawnIO and for features like auto-update.

## Shipped

### v0 — Local Telemetry Dashboard
- Live time-series panels for CPU, GPU, memory, disk, network, system activity
- GPU temperature monitoring (AMD and Nvidia, no driver needed)
- Per-process CPU and memory breakdown
- Drag-reorderable panels with per-panel and global time range controls
- Chart zoom (click-drag selection and preset ranges from 1m to 30d)
- SQLite persistence with rolling retention and 1-minute rollups
- Dark and light themes
- Configurable scrape interval, retention, and storage path
- Launch on startup / minimize to tray
- Drive capacity gauges
