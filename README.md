# CutCraft - Accessible C# Desktop Video Editor

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![UI-WPF](https://img.shields.io/badge/UI-WPF-0078D7)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Media-FFmpeg](https://img.shields.io/badge/Engine-FFmpeg-007808)](https://ffmpeg.org/)
[![Audio-NAudio](https://img.shields.io/badge/Audio-NAudio-orange)](https://github.com/naudio/NAudio)
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
│   │   ├── Models/              # Project, Track, Clip, Effect, Transition, Caption, MediaAsset
│   │   ├── Commands/            # Command Pattern (Undo/Redo with live accessibility history)
│   │   ├── Subtitles/           # SubRip (.srt) parser and generator
│   │   ├── Serialization/       # Plain JSON project file serializer
│   │   ├── Timeline/            # TimelineEngine, timecodes, snapping, ripple edit
│   │   └── Accessibility/       # Screen reader spoken announcement & audio cues generator
│   │
│   ├── CutCraft.Media/          # Media engine abstraction
│   │   ├── Analysis/            # FFprobe asset analyzer (resolution, FPS, audio)
│   │   ├── Export/              # FFmpeg filter graph builder & out-of-process MP4 exporter
│   │   ├── Audio/               # NAudio waveform extractor & mixer
│   │   └── Playback/            # Preview playback abstraction (LibVLCSharp / FFmpeg)
│   │
│   └── CutCraft.Wpf/            # WPF UI presentation layer (MVVM)
│       ├── ViewModels/          # MainViewModel with CommunityToolkit.Mvvm
│       ├── MainWindow.xaml      # Accessible layout, timeline, time ruler, player, captions tab
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
- [x] **Phase 2: Transitions & Visual Effects**
  - Transitions: Crossfade (`fade`), Fade to Black (`fadeblack`), Fade to White (`fadewhite`), Wipes, Slides, Dissolve
  - Visual filters: Brightness, Contrast, Saturation, Black & White, Sepia, Invert, Blur, Sharpen, Vignette, Hue
  - Playback speed controls: 0.25x slow motion to 4.0x timelapse with multi-stage chained audio `atempo`
- [x] **Phase 3: Multi-Track PiP & Audio Mixing**
  - Multi-track Picture-in-Picture (PiP) and video overlays with custom positioning, scaling, and opacity
  - Dedicated background music / audio tracks with automated delays and mixing
  - Audio detachment: detach video audio to dedicated audio track
  - NAudio waveform extraction
- [x] **Phase 4: Captions & Subtitles Engine**
  - Subtitle parser & generator supporting SubRip (`.srt`) format
  - Caption track in project model and WPF UI
  - FFmpeg burn-in caption renderer with styled background boxes
- [x] **Phase 5: Accessibility & Audio Cues**
  - Full keyboard accessibility with clip navigation (`Alt+Left` / `Alt+Right`)
  - Screen reader live announcements (`AutomationProperties.LiveSetting="Assertive"`)
  - Distinct auditory cues (`SystemAudioCueService`) for split cuts, boundaries, seeking, and undo/redo
- [x] **Phase 6: Video Preview Playback Engine**
  - Real-time video preview playback engine (`MediaElement` with high-precision `DispatcherTimer`)
  - Active video clip switching and timeline playhead synchronization
  - Transport controls (`Space` / `K` Play-Pause, `J` / `L` frame stepping, `Home` / `End`)
  - Live SubRip subtitle/caption overlay rendering
- [x] **Phase 7: Auto-Updater (Version 0.0)**
  - Built-in `UpdateManager` for automated update manifest checks & downloads
  - Version 0.0 release update packaging (`update-manifest.json` & `updater.bat`)
  - One-click update check badge and screen reader announcements
- [x] **Phase 8: Advanced Audio Engine**
  - Audio Fade-In & Fade-Out envelope controls for both video audio and background music
  - Automated Audio Ducking (`sidechaincompress`) reducing music volume during speech/dialogue
  - Broadcast-standard Loudness Normalization (EBU R128 `loudnorm=I=-16:TP=-1.5:LRA=11`)
- [x] **Phase 9: Keyframe Transformation & Orientation**
  - Orientation & 90° clockwise rotation (`transpose=1`, `transpose=2`)
  - Horizontal & Vertical flip mirroring (`hflip`, `vflip`)
  - Ken Burns Pan & Zoom presets (`ZoomIn`, `ZoomOut`, `PanLeftToRight`, `PanRightToLeft`) via `zoompan`
- [x] **Phase 10: Interactive Markers & Magnetic Snapping Engine**
  - Interactive timeline markers with keyboard shortcuts (`M` to add marker, `Alt+M` for next marker)
  - Magnetic timeline snapping to clip cut boundaries and marker points
  - Accessible announcements and toolbar controls
- [x] **Phase 11: Multi-Format Social Presets & GIF Exporter**
  - YouTube Shorts / TikTok / Reels vertical 9:16 export preset (1080x1920)
  - Instagram 1:1 Square export preset (1080x1080)
  - High-Quality Animated GIF export via two-pass `palettegen` & `paletteuse`
  - Audio-Only MP3 extraction preset (192 kbps)
- [x] **Phase 12: Subtitles, Captions & Animated Title Cards**
  - Dual-format subtitle support: SubRip (`.srt`) and WebVTT (`.vtt`) import/export
  - Subtitle timing & editing operations: time shifting (`+0.5s` / `-0.5s`), split caption at playhead, merge adjacent captions, and re-indexing
  - Subtitle QA validation engine: automated detection of overlapping cues, invalid timestamps, and reading speed warnings (> 26 chars/sec)
  - Stylized Title Cards library: 6 turnkey templates (*Lower Third*, *Cinematic Header*, *Bold Headline*, *Minimalist*, *Neon Cyber Glow*, *Vintage Retro*)
  - Motion typography animations: smooth Fade, Slide Up, Slide Left, and Zoom In with dynamic FFmpeg `drawtext` alpha & motion expressions
  - Full Undo/Redo command architecture (`CaptionCommands` & `TextOverlayCommands`) integrated with screen reader accessibility
  - Real-time preview player burn-in with live animated transforms and typography rendering
- [x] **Phase 13: Ripple Edit & Gap Closing Engine**
  - Timeline Ripple Edit Mode toggle (`Alt+R`) with toolbar state indicator
  - Ripple Delete (`Shift+Delete`): deletes clip and automatically pulls subsequent clips back to eliminate empty timeline holes
  - Ripple In/Out Trimming: trims clip boundary and shifts all following clips synchronously
  - Timeline Gap Analyzer: detects blank gaps between clips and before the opening clip
  - Close Gap at Playhead (`Alt+G`): single-action gap elimination
  - Auto-Pack (Close All Gaps): condenses all clips on track consecutively with zero blank frames
  - Ripple Insert: inserts media at any playhead position, cleanly bisecting existing footage and rippling following clips without overwrite

---

## Keyboard Shortcuts

| Shortcut | Action | Screen Reader Announcement |
|---|---|---|
| `Space` | Play / Pause Preview | Announces playback state |
| `S` | Split Clip at Playhead | "Split clip {name} into two parts" + Cut Audio Cue |
| `I` | Set Clip In-Point | "Trimmed Start of {name} to {time}" + Cut Audio Cue |
| `O` | Set Clip Out-Point | "Trimmed End of {name} to {time}" + Cut Audio Cue |
| `M` | Add Marker at Playhead | "Added marker at {time}" + Cut Audio Cue |
| `Alt+M` | Jump to Next Marker | "Jumped to {marker} at {time}" |
| `N` | Toggle Magnetic Snapping | "Timeline magnetic snapping enabled/disabled" |
| `Ctrl+R` | Rotate Selected Clip 90° | "Rotated '{name}' to {degrees} degrees" |
| `Alt+Left` | Select Previous Clip | "Selected clip {i} of {n}: {name}" |
| `Alt+Right` | Select Next Clip | "Selected clip {i} of {n}: {name}" |
| `Delete` | Remove Selected Clip | "Removed clip {name}" |
| `Alt+R` | Toggle Ripple Edit Mode | "Timeline ripple edit mode enabled/disabled" |
| `Shift+Delete` | Ripple Delete Selected Clip | "Ripple deleted clip {name}, closing gap" |
| `Alt+G` | Close Gap at Playhead | "Closed gap of {duration}s at {time}" |
| `Ctrl+Z` | Undo | Announces undone command + Undo Audio Cue |
| `Ctrl+Y` | Redo | Announces redone command + Redo Audio Cue |
| `Left / Right` | Step 1 Frame Backward/Forward | Announces current timecode & active clip |
| `Ctrl+M` | Add Music at Current Point | "Added music '{name}' at {time} to Audio track" |
| `Ctrl+E` | Export Video / Audio / GIF | Opens export presets dialog |
| `Ctrl+S` | Save Project | Saves project to JSON |

---

## Contributors

- [@matingohil11-ops](https://github.com/matingohil11-ops)
- [@swarup-developer](https://github.com/swarup-developer)
- [@ashu-choudhury](https://github.com/ashu-choudhury)

See [CONTRIBUTORS.md](CONTRIBUTORS.md) for the contributor list.

