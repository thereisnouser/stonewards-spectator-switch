# Stonewards Spectator Switch

A client-only BepInEx 5 mod for Stonewards. After your dwarf dies, switch between living teammates without changing the game world or other players' state.

**Release version: 0.1.0 (not released yet).**

## Controls

- **LMB** — next living dwarf, when the mouse is locked for camera look
- **RMB** — previous living dwarf, when the mouse is locked
- **Hold ALT** — temporarily reveal/unlock the pointer, then **left-click the on-screen < or > buttons** to switch. Release ALT to return to the game's cursor mode

A small dark-and-gold HUD panel appears only while you are dead and the game's death input state is active. It shows the observed dwarf's name, the current position in the list, and both clickable arrow buttons. If no living teammates remain, it displays that status and disables the buttons.

The mod preserves the game's current camera **rotation** instead of forcibly pointing the view at the observed dwarf. By default, it redirects only the camera's render position to the chosen dwarf's eye level. It does **not** change the game's native spectator target, send network commands, teleport anyone, or require other players to install this mod.

## Build and install

Requires: Stonewards (Mono), BepInEx 5 installed in your r2modman profile, and the .NET SDK.

```powershell
$repo = "X:\projects\stonewards-spectator-switch"
$gameDir = "F:\Steam\steamapps\common\Stonewards"
$profileDir = Join-Path $env:APPDATA "r2modmanPlus-local\Stonewards\profiles\stonewards-test"

git -C $repo pull --ff-only
powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File "$repo\build-and-install.ps1" `
    -GameDir $gameDir `
    -ProfileDir $profileDir
```

The DLL is installed in `<ProfileDir>\BepInEx\plugins\StonewardsSpectatorSwitch\`.

## Configuration

BepInEx generates `BepInEx/config/com.thereisnouser.stonewards.spectatorswitch.cfg`.

- `General/Enabled` — turn mod on/off
- `General/AutoAdvanceWhenTargetDies` — switch automatically if the observed player dies or disconnects
- `UI/ShowButtons` — show/hide clickable arrows and spectator status
- `Camera/OrbitDistance` — optional third-person offset along the existing look direction (default `0`)
- `Camera/EyeHeight` — height of the observed player's eye position (default `1.55`)

## Test status

The initial 0.1.0 code was confirmed to build and switch teammates, but it imposed a fixed view angle. The camera-direction fix and clickable HUD have since been added **before the first release**. These latest changes still need compilation and in-game verification. Check `BepInEx/LogOutput.log` for `Spectator Switch v0.1.0 initialized`.

## Notes

This mod intentionally uses the game's HUD document for buttons and a local render-position redirect for observation. The proper in-game spectator-target API has not yet been verified. When another game menu is open, the mod hides its buttons and avoids interfering with that menu's cursor.
