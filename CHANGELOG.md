# Changelog

## 0.1.1 (experimental)

- Removed fixed behind-the-dwarf camera orientation; the game now owns the viewing angle while switching teammates.
- Moved the default view to the observed dwarf's eye position; third-person orbit pullback is optional.
- Replaced 0.1.0 camera config keys so upgrades do not inherit the old locked-camera settings.
- Kept left/right mouse switching, dead-player filtering, and revive cleanup unchanged.

## 0.1.0 (experimental)

- Cycle living teammates with left/right mouse clicks while dead.
- Local third-person observation camera; multiplayer clients need no plugin.
- Auto advance if selected teammate dies or disconnects.
- Restore camera after rendering and on revival.
- Configurable camera distance and height.
