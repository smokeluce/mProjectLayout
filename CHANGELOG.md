# Changelog

All notable changes to mProjectLayout will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [v1.1.0] - 2026-03-06

### Added
- Auto-detection of three input formats — Tree (box-drawing), Explicit Paths, and Indented (spaces)
- **Tree format** now correctly parses nested hierarchy using depth tracking before stripping box characters
- **Path format** support for `dir\subdir\file.cs` and `dir/subdir/file.cs` style input
- **Indented format** support with auto-detected indent unit (2-space, 4-space, etc.)
- UTF-8 glyphs in log output — `📁` directories, `📄` files, `⚠` skipped, `🔍` format detected, `🚀` start, `✅` complete, `❌` errors
- **Clear button** — resets input box, log panel, and status bar in one click
- **Generate-again guard** — warns with a confirmation dialog if you attempt to generate into the same directory twice without browsing to a new one
- Autoscroll on log panel — scrolls to latest entry automatically on each generation step
- Monospace font in both the input and log panels for clean tree rendering
- Horizontal and vertical scrollbars on input and log panels
- Flexible layout — input and log panels now stretch to fill available window height
- `KnownExtensionlessFiles` list covering `Makefile`, `Dockerfile`, `Jenkinsfile`, `LICENSE`, `.gitignore`, `.env`, `.editorconfig`, and more
- Dotfile detection (`.env`, `.gitignore`, etc.) treated correctly as files not directories

### Fixed
- **Critical:** Tree format parser was discarding indentation before measuring depth, causing all nested files and directories to land flat at the base directory instead of their correct nested locations
- File vs. directory detection no longer relies solely on presence of `.` — extensionless known filenames and dotfiles are now handled correctly
- `Dockerfile`, `Makefile`, `LICENSE` and similar extensionless files no longer silently skipped as unrecognized lines

### Changed
- Log output now prefixed with UTF-8 glyphs for visual clarity
- Status bar reflects generation result with `✅` / `❌` prefix
- Window height increased slightly to accommodate flexible layout
- `MainWindow.axaml.cs` wires autoscroll via `CaretIndex` and `Dispatcher.UIThread.Post`

---

## [v1.0.1] - 2026-02-07

### Changed
- Icon workflow polish
- Minor UI refinements

---

## [v1.0.0] - 2026-02-07

### Added
- Initial release
- Basic project skeleton generator
- Avalonia + FluentAvalonia UI
- Browse for base directory
- Paste folder structure and generate
- Log panel with generation output
- Single-file self-contained Windows EXE build
- MVVM architecture with CommunityToolkit.Mvvm

---

[v1.1.0]: https://github.com/smokeluce/mProjectLayout/releases/tag/v1.1.0
[v1.0.1]: https://github.com/smokeluce/mProjectLayout/releases/tag/v1.0.1
[v1.0.0]: https://github.com/smokeluce/mProjectLayout/releases/tag/v1.0.0
