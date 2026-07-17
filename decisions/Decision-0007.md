# Decision-0007: Avalonia MVVM User Interface

**Status:** Accepted
**Date:** 2026-07-17

## Decision

TWW3 Companion uses C# on a supported .NET long-term-support release, Avalonia's desktop target and Fluent theme, and Model-View-ViewModel (MVVM) for its initial user interface. Version 0.1 supports Windows 10 or later on x64 through self-contained installer and portable distributions.

The application starts on Home, opens one main window, and presents an active Workspace through a persistent sidebar, master list, and detail pane. Collection Membership knowledge and shared Mod knowledge use separate tabs, ViewModels, drafts, validation, and Save commands.

The complete navigation, editing, import, destructive-action, accessibility, theme, sizing, cancellation, and verification contract is defined in [RFC-0005](../RFC/RFC-0005.md).

## Rationale

- Avalonia and .NET support a self-contained Windows desktop application while preserving a path to later desktop platforms.
- MVVM keeps presentation state separate from domain, import, and SQLite responsibilities.
- Explicit editing and separate Membership and Mod detail boundaries make ownership visible to users.
- A persistent import preview with a Needs Attention queue supports both overview and required resolution.
- Standard controls, keyboard operation, Windows High Contrast, text scaling, and assistive-technology announcements are release requirements rather than later polish.

## Consequences

- The initial supported platform is Windows 10 or later on x64; ARM64 and non-Windows packages remain deferred.
- The minimum supported display is 1280 × 720 at 100% Windows scaling, with enough effective space for a 1024 × 640 logical-pixel window at increased scaling.
- Version 0.1 uses one main window and does not expose later-milestone destinations as placeholders.
- Application services mediate all domain, import, and persistence operations; Views and ViewModels do not access SQLite directly.
- The implementation plan must pin .NET, Avalonia, and dependency versions and must obtain Product Owner approval for the proposed .NET SQLite data-access library before storage code lands.
