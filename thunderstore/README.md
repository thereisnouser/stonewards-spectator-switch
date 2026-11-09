# Stonewards Spectator Switch

**Switch your spectator view between living dwarves after death with mouse clicks or simple on-screen buttons.**

## ✨ Features

- **Teammate switching** — cycle through living dwarves after dying.
- **Clickable HUD** — previous/next arrows plus the watched player's name and position in the list.
- **Free viewing angle** — does not force the camera to face the watched dwarf.
- **Automatic fallback** — can switch to another living teammate when the watched player dies or disconnects.
- **Client-side** — no installation required for the host or other players.

## 🎮 How to use

While dead, click **LMB** for the next living dwarf or **RMB** for the previous one. To use the on-screen arrows, **hold Alt** to release the cursor, click a button, then release Alt.

The first click selects a teammate. Dead or disconnected dwarves are skipped. The HUD hides after revival and while other menus are open.

## ⚙️ Configuration

The BepInEx configuration file is `BepInEx/config/com.thereisnouser.stonewards.spectatorswitch.cfg`.

- `General/Enabled` — enables the mod (default: `true`).
- `General/AutoAdvanceWhenTargetDies` — automatically picks a new living player (default: `true`).
- `UI/ShowButtons` — displays the spectator panel (default: `true`).
- `Camera/OrbitDistance` — optional camera pullback (default: `0`).
- `Camera/EyeHeight` — camera height above the selected dwarf (default: `1.55`).

## 📦 Installation

Install through **r2modman** or another compatible Thunderstore mod manager when the package is published. Launch **Stonewards** through the mod manager with BepInEx 5 enabled.

## 🔧 Compatibility

- **Game:** Stonewards. The latest unreleased HUD/camera changes are awaiting an in-game check.
- **Mod loader:** BepInEx 5.
- **Multiplayer:** Client-side; only the spectator needs the mod. Switching requires another living dwarf.

## ⚠️ Known limitations

- The mouse-based switching was tested in-game, but the updated camera behavior and clickable HUD have not yet been verified together.
- Observation locally changes the view position; it does not use Stonewards' native spectator-target system or mirror another player's exact camera rotation.
- Controls are active only while dead, outside other game menus.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-spectator-switch) · [Report an issue](https://github.com/thereisnouser/stonewards-spectator-switch/issues) · [Changelog](https://github.com/thereisnouser/stonewards-spectator-switch/blob/main/CHANGELOG.md)

---

*Made by thereisnouser*
