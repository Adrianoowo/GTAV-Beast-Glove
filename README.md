# BEAST Glove Mod for GTA V (Legacy Edition)

An authentic simulation of the **BEAST Glove** (inspired by glove combat games like Slap Battles) for **Grand Theft Auto V Legacy** (`v1.0.3889.0`), built with native **ERO (Euphoria Ragdoll Overhaul 1.9.4)** compatibility.

---

## 🥊 Features

- **Knuckle Duster Integration**: Uses the Knuckle Duster (`WEAPON_KNUCKLE`) as the Beast Glove.
- **Physics-Accurate 45° Launch**: Catapults any target struck approximately **100 meters** into the air at a precise **45-degree angle**, following a parabolic flight arc.
- **ERO (Euphoria Ragdoll Overhaul) Compatible**:
  - Automatically suppresses ERO's stumble-balancer joint resistance at the moment of impact so targets aren't pinned down or slowed by ground friction.
  - Maintains relaxed ragdoll (`CTaskNMRelax`, type 0) throughout the high-velocity flight arc.
  - Re-engages ERO's NaturalMotion behaviours on ground impact, allowing full Euphoria tumbling, slope rolling, and post-crash wound reactions.
- **Punch Feedback**:
  - **Shockwave PTFX**: Non-damaging air-burst dust shockwave effect (`exp_grd_grenade_lod`) spawned at the strike point.
  - **Audio Blast**: Deep air-pop impact boom (`BASEJUMP_PULL_CHUTE`).
  - **Screen Shake**: High-impact camera shake (`SMALL_EXPLOSION_SHAKE`).
- **Vehicle Knockback**: Punching vehicles also launches them through the air with tuned vehicle mass multipliers.
- **Quick-Equip Hotkey**: Press **`B`** (configurable) to instantly acquire and equip the Knuckle Duster / Beast Glove.
- **Fully Configurable**: All speeds, angles, durations, effects, and hotkeys can be customized in `BeastGlove.ini`.

---

## 📂 File Installation Status

All core modding prerequisites and script files have been placed directly into your GTA V directory (`D:\SteamLibrary\steamapps\common\Grand Theft Auto V`):

| File / Folder | Purpose |
|---|---|
| `args.txt` | Disables BattlEye anticheat (`-nobattleye -noBE`) so singleplayer mods load smoothly |
| `dinput8.dll` | Standard ASI loader |
| `ScriptHookV.dll` | ScriptHookV runtime compatible with GTA V v1.0.3889.0 |
| `ScriptHookVDotNet.asi` | ScriptHookV .NET v3.7.0 runtime bridge |
| `ScriptHookVDotNet2.dll` / `ScriptHookVDotNet3.dll` | SHVDN API assemblies |
| `ScriptHookVDotNet.ini` | SHVDN configuration (Console: `F5`, Reload: `Insert`) |
| `NativeTrainer.asi` | ScriptHookV Native Trainer (Menu: `F4`, Numpad navigation) |
| `RageOpenV.asi` | Open-source RPF archive loader for the `mods` folder |
| `scripts/BeastGlove.dll` | Precompiled Beast Glove mod library |
| `scripts/BeastGlove.cs` | Source script for Beast Glove |
| `scripts/BeastGlove.ini` | User settings for speed, angle, damage, FX, and hotkey |
| `ERO 1.9.4 by MiGGousT/` | Euphoria Ragdoll Overhaul package moved from Downloads |
| `ERO1.9.4.oiv` | 1-Click OIV package for ERO |
| `OpenIVSetup.exe` | OpenIV 4.1 offline installer |

---

## ⚙️ Configuration (`scripts/BeastGlove.ini`)

```ini
[Physics]
; Launch speed in meters per second (46.0 achieves ~100m under GTA V ragdoll drag)
LaunchSpeed = 46.0

; Upward angle in degrees (45.0 degrees is optimal projectile angle)
LaunchAngle = 45.0

; How long the ped stays in ragdoll during flight (in milliseconds)
RagdollDurationMs = 7000

[Combat]
; Damage dealt per punch
PunchDamage = 100

[Effects]
; Visual air-burst dust shockwave particle effect
PlayShockwaveFx = true

; Heavy impact sound effect
PlaySoundFx = true

; Physical camera shake
CameraShake = true

[Vehicles]
; Whether vehicles also get launched when hit
AffectVehicles = true

; Velocity multiplier for vehicles
VehicleSpeedMultiplier = 0.75

[General]
; Notification when equipping Knuckle Duster
ShowEquipNotification = true

; Hotkey to quickly spawn/equip Knuckle Duster (default: B)
EquipHotkey = B
```

---

## 🚀 How to Play

1. Launch **Grand Theft Auto V** (Single Player).
2. Press **`B`** (or select the **Knuckle Duster** from your weapon wheel).
3. A notification will appear: `BEAST GLOVE active!`.
4. Punch any ped or vehicle.
5. Watch them launch **100 meters** into the air at a **45° angle** and crash down with ERO Euphoria physics!
