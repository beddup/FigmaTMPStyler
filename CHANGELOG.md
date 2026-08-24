# Changelog
## [0.1.2] - 2026-08-24

### Added
- `Assets → Figma TMP Styler → Check Auto-generated TMP GameObject Under Selection` menu command that scans selected prefabs/folders and fixes auto-generated `figma_attached` child TMP overlays (snaps their RectTransform to fully overlap the parent and syncs their text)

### Fixed
- Applying a Figma font size now also sets `fontSizeMax`, so auto-sizing text honors the Figma size

## [0.1.1] - 2026-08-19

### Changed
- Drop shadow expands the underlay via `_UnderlayDilate` so its origin aligns with the outline outer edge
- Inner shadow compensates the face dilate by directionally adjusting the offset (since `_UnderlayDilate` cannot shift the inner shadow origin)
- Ratio C solver now converges monotonically with a stable break condition (removed the "Unstable Ratio C" warning)

### Fixed
- Shadow offset compensation no longer over/under-compensates diagonal offsets

### Added
- Error logs when outline or shadow values exceed the shader range

## [0.1.0] - 2026-08-17
### Add
- Support multiple drop shadows and inner shadows via layered child TextMeshPro objects
- Stroke alignment handling (only "outside" stroke align supported)
- Tolerance-based vertical/horizontal gradient direction detection

### Changed
- Material generation now produces a `Materials` list instead of single outline/drop-shadow and inner-shadow materials
- Material saving updates existing assets in place
- More stable ratio-C solver (more iterations, range validation, stability warning)

## [0.0.7] - 2026-08-14
### Add
- Refresh TMP material by its name

## [0.0.6] - 2026-08-14
### Fixed
- Find Best Ratio C for shadow effect

## [0.0.5] - 2026-07-02
### Fixed
- "Regenerate Material From TMP Component" now correctly parses integer-format material names
- Fixed material resetting to font default after regeneration (removed AssetDatabase.Refresh race)
- Material name read via fontSharedMaterial to avoid Unity "(Instance)" suffix

## [0.0.4] - 2026-07-02
- Registered on OpenUPM

## [0.0.3] - 2026-06-27
- Added material caching with deterministic naming
- Added inner shadow support via child GameObject layering
- Improved outline-to-TMP center-stroke compensation

## [0.0.2] - 2026-06-25
- Added drop shadow and linear gradient support
- Added font style mapping (underline, strikethrough, letter casing)

## [0.0.1] - 2026-06-23
- Initial release with solid-color outline support and text alignment
