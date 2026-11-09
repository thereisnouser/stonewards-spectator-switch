# Stonewards Spectator Switch

**Switch your spectator view between living dwarves after death using mouse clicks or a compact in-game HUD.**

## ✨ Features

- **Switch between teammates** — cycle through living dwarves without moving or controlling their characters.
- **Spectator HUD** — a small dark-and-gold panel shows the selected player's name, player count, and clickable previous/next buttons.
- **Free viewing angle** — the mod no longer forces the camera to face the selected dwarf; it retains the game's current camera rotation.
- **Automatic fallback** — optionally switch to another living player when the current target dies or disconnects.
- **Client-side operation** — only the player using spectator switching needs the plugin.

## 🎮 How to use

After your dwarf dies, use these controls while the game's death-view input state is active:

| Control | Action |
| --- | --- |
| **Left mouse button (LMB)** | Watch the next living dwarf |
| **Right mouse button (RMB)** | Watch the previous living dwarf |
| **Hold Alt + click an on-screen arrow** | Temporarily unlock the cursor and use the HUD buttons |

LMB/RMB work as shortcuts while the cursor is locked for normal camera input. Release **Alt** to return to the game's previous cursor mode.

The first switch selects a player; dying does not automatically select one. Dead and disconnected teammates are skipped. If nobody else is alive, the HUD displays that status and disables its buttons. The panel is hidden during other menus and after revival.

## ⚙️ Configuration

BepInEx creates `BepInEx/config/com.thereisnouser.stonewards.spectatorswitch.cfg` after the plugin starts.

| Setting | Default | Purpose |
| --- | --- | --- |
| `General/Enabled` | `true` | Enable spectator switching |
| `General/AutoAdvanceWhenTargetDies` | `true` | Pick another living dwarf if the watched player is lost |
| `UI/ShowButtons` | `true` | Show or hide the spectator HUD and buttons |
| `Camera/OrbitDistance` | `0` | Optional camera pullback along the current viewing direction |
| `Camera/EyeHeight` | `1.55` | Camera height above the selected dwarf's position |

The mouse controls and **Alt** modifier are fixed in the current source; they are not configurable hotkeys.

## 📦 Installation

### Mod manager

**The Thunderstore package has not been published yet.** For development testing, use a Stonewards profile with **BepInEx 5** enabled in r2modman or a compatible mod manager, then build and install the plugin from source.

### Build from source

Requirements: a local Stonewards installation, a BepInEx 5-enabled profile, and the .NET SDK.

Clone this repository, open PowerShell in the repository directory, and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\build-and-install.ps1 `
    -GameDir "<Stonewards installation directory>" `
    -ProfileDir "<r2modman profile directory>"
```

The script builds the project and copies `StonewardsSpectatorSwitch.dll` into `<ProfileDir>\BepInEx\plugins\StonewardsSpectatorSwitch\`.

## 🔧 Compatibility

- **Game:** Stonewards. Compatibility of the latest unreleased HUD/camera changes still needs an in-game check.
- **Mod loader:** BepInEx 5.
- **Multiplayer:** Client-side. Teammates and the host do not need the mod for a spectator to use it; at least one other living dwarf is required for switching.

## ⚠️ Known limitations

- The **initial mouse-based switching** was tested in-game, but the newer free-angle camera behavior and clickable HUD **have not yet been verified together in-game**.
- This is a **local render-camera position redirect**, not the game's native spectator-target system. The view does not claim to reproduce another player's exact camera rotation.
- The panel is only active when the local player is dead and the game's input state is `Dead`; it is not available inside other menus.

## 🛠️ Development

The plugin uses BepInEx 5, Unity's Input System and UI Toolkit. The HUD is attached to Stonewards' existing `Hud` document, while Unity camera rendering callbacks temporarily redirect the local view to a living teammate. No multiplayer commands or player teleports are sent.

Source files are in `src/StonewardsSpectatorSwitch/`. The project's version remains `0.1.0` until its first release.

## 🔗 Links

[Report an issue](https://github.com/thereisnouser/stonewards-spectator-switch/issues) · [Changelog](CHANGELOG.md)

---

*Made by thereisnouser*
