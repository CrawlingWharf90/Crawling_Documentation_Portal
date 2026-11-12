# Changelog

## V1.0.1

### Fixed
- Fixed Unity Package Manager (UPM) installation failing due to a missing `package.json.meta` file causing errors in Unity's immutable package cache.
- Fixed `crawling_documentation_portal.html` not being found during Unity setup by ensuring it is packaged inside the `UNITY/` directory.
- Fixed Unity Editor compilation issues by adding `DocsPortal.asmdef` to ensure scripts are excluded from final game builds.

### Changed
- Refactored `DocsPortalMenu.cs` to copy the HTML portal directly from the local package cache instead of downloading it from GitHub via a web request.
- Updated README with the correct Unity Package Manager (UPM) installation instructions utilizing the `?path=/UNITY` URL parameter.

## V1.0.0

### Added
- Initial release of the Crawling Documentation Portal.
- Single-file HTML portal with support for Markdown, LaTeX maths, Mermaid diagrams, and Excel-style sheets.
- Unity Editor integration (`DocsPortalMenu.cs`) for one-click documentation folder setup and IDE launching.
- File system access integration for editing local documentation files directly from the browser.