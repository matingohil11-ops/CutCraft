# CutCraft - Accessible C# Desktop Video Editor

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![UI-WPF](https://img.shields.io/badge/UI-WPF-0078D7)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Media-FFmpeg](https://img.shields.io/badge/Engine-FFmpeg-007808)](https://ffmpeg.org/)
[![License-LGPL](https://img.shields.io/badge/License-LGPL-green)](LICENSE)

**CutCraft** is a lightweight, high-performance, and fully accessible Windows desktop video editor built with C# and .NET 10, inspired by the simplicity and workflow of Windows Movie Maker.

Designed from the ground up to be 100% operable with screen readers (NVDA/JAWS) and keyboard shortcuts.

---

## Architecture & Project Structure

```
CutCraft/
├── CutCraft.sln
├── src/
│   ├── CutCraft.Core/           # Pure C# domain logic (zero UI dependencies)
│   │   ├── Models/              # Project, Track, Clip, Effect, MediaAsset
│   │   ├── Commands/            # Command Pattern (Undo/Redo with live accessibility history)
│   │   ├── Serialization/       # Plain JSON project file serializer
│   │   ├── Timeline/            # TimelineEngine, timecodes, snapping, ripple edit
│   │   └── Accessibility/       # Screen reader spoken announcement generator
│   │
│   ├── CutCraft.Media/          # Media engine abstraction
│   │   ├── Analysis/            # FFprobe asset analyzer (resolution, FPS, audio)
│   │   ├── Export/              # FFmpeg filter graph builder & out-of-process MP4 exporter
│   │   ├── Audio/               # NAudio waveform extractor & mixer
│   │   └── Playback/            # Preview playback abstraction (LibVLCSharp / FFmpeg)
│   │
│   └── CutCraft.Wpf/            # WPF UI presentation layer (MVVM)
│       ├── ViewModels/          # MainViewModel with CommunityToolkit.Mvvm
│       ├── MainWindow.xaml      # Accessible layout, timeline, time ruler, player
│       └── App.xaml
│
└── tests/
    └── CutCraft.Core.Tests/     # Comprehensive unit tests for timeline, commands, & export graphs
```

---

## Roadmap Milestones

- [x] **Phase 0: Foundations**
  - Architecture setup (Core, Media, WPF, Tests)
  - Undo/Redo command pattern engine
  - FFmpeg filter graph generator for trimming, transitions, effects & audio mixing
  - Project JSON persistence
- [x] **Phase 1: The MVP Core**
  - Single/Multi-track video and audio timeline
  - Trimming (In/Out points) and precise Split at playhead
  - Text title overlays
  - Export MP4 with H.264 video and AAC audio
- [ ] **Phase 2: Transitions & Visual Effects**
- [ ] **Phase 3: Multi-Track PiP & Waveforms**
- [ ] **Phase 4: Captions & Subtitles (.srt)**
- [ ] **Phase 5: Accessibility & Screen-Reader Certification (NVDA)**
- [ ] **Phase 6: Proxy Media & Performance Hardening**
- [ ] **Phase 7: Packaging & Auto-Updates (Velopack & MSIX)**

---

## Keyboard Shortcuts

| Shortcut | Action | Screen Reader Announcement |
|---|---|---|
| `Space` | Play / Pause Preview | Announces playback state |
| `S` | Split Clip at Playhead | "Split clip {name} into two parts" |
| `I` | Set Clip In-Point | "Trimmed Start of {name} to {time}" |
| `O` | Set Clip Out-Point | "Trimmed End of {name} to {time}" |
| `Delete` | Remove Selected Clip | "Removed clip {name}" |
| `Ctrl+Z` | Undo | Announces undone command |
| `Ctrl+Y` | Redo | Announces redone command |
| `Left / Right` | Step 1 Frame Backward/Forward | Announces current timecode & active clip |
| `Ctrl+E` | Export MP4 | Opens export dialog |
| `Ctrl+S` | Save Project | Saves project to JSON |
