# BEAST Glove Mod for GTA V (Legacy Edition)

Authentic **BEAST Glove** physics simulation for **Grand Theft Auto V Legacy** (`v1.0.3889.0`), integrated natively with **ERO (Euphoria Ragdoll Overhaul 1.9.4)**.

---

## 🥊 Features

- **Confirmed Hit Detection**: Triggers **only** when a punch with the Knuckle Duster (`WEAPON_KNUCKLE`) physically connects and inflicts damage. Zero premature launches during lock-on, lunges, or wind-ups; zero double launches.
- **Natural Euphoria Ragdoll**: Peds are launched into an upward parabolic arc with authentic backflip torque and corkscrew rotation. Euphoria is given 100% free reign (`bForceScriptControl = false`), so limbs flail dynamically in the air and realistic tumble physics play out upon impact.
- **Dynamic Vehicle Launches**: Punching vehicles launches them with off-center rotational torque, producing dramatic aerial spins, barrel rolls, and high-impact crashes.
- **ERO (Euphoria Ragdoll Overhaul 1.9.4) Active**:
  - `behaviours.xml` and `physicstasks.ymt` installed directly into `mods/update/update.rpf`, `mods/common.rpf`, and `mods/x64a.rpf`.
  - Loaded dynamically by `RageOpenV.asi` and `dinput8.dll`.
  - Peds tumble, roll down slopes, and react brutally upon landing using ERO's authentic physics.
- **Audio & Visuals**:
  - Clean supersonic dust shockwave ring (`bul_sand_dust`).
  - Punch impact boom sound (`BASEJUMP_PULL_CHUTE`).
  - Physical camera shake on hit (`SMALL_EXPLOSION_SHAKE`).
- **Clean & Immersive**: No on-screen notification spam and no hotkey overrides. Simply equip the Knuckle Duster from your weapon wheel or trainer and punch.

---

## 📂 Installation Layout

All files are installed in your GTA V directory (`D:\SteamLibrary\steamapps\common\Grand Theft Auto V`):

| File / Folder | Description |
|---|---|
| `args.txt` | Disables BattlEye anticheat for singleplayer modding |
| `dinput8.dll` | ASI loader |
| `ScriptHookV.dll` | ScriptHookV runtime (v1.0.3889.0) |
| `ScriptHookVDotNet.asi` | ScriptHookV .NET v3.7.0 runtime bridge |
| `ScriptHookVDotNet2.dll` / `ScriptHookVDotNet3.dll` | SHVDN API assemblies |
| `ScriptHookVDotNet.ini` | SHVDN configuration (Console: `F5`, Reload: `Insert`) |
| `NativeTrainer.asi` | ScriptHookV Native Trainer (Menu: `F4`) |
| `RageOpenV.asi` | Open-source RPF archive loader for the `mods` folder |
| `mods/update/update.rpf` | Rebuilt archive containing ERO 1.9.4 behaviours & physicstasks |
| `mods/common.rpf` | Rebuilt archive containing ERO 1.9.4 behaviours.xml |
| `mods/x64a.rpf` | Rebuilt archive containing ERO 1.9.4 physicstasks.ymt |
| `scripts/BeastGlove.dll` | Precompiled Beast Glove mod library |
| `scripts/BeastGlove.cs` | Source script |
| `scripts/BeastGlove.ini` | User settings for speeds, angles, and effects |

---

## ⚙️ Configuration (`scripts/BeastGlove.ini`)

```ini
[Physics]
; Initial launch speed in m/s
LaunchSpeed = 38.0

; Launch angle in degrees (45.0 for maximum natural projectile arc)
LaunchAngle = 45.0

; Impulse multiplier applied to ragdoll bones
ForceMultiplier = 40.0

; Natural rotational spin torque so targets backflip and corkscrew
SpinTorque = 3.5

[Combat]
; Base punch damage (keeps target alive so Euphoria flailing is visible)
PunchDamage = 25

[Effects]
; Sonic air-burst particle ring (no fire/explosions)
PlayShockwaveFx = true

; Heavy impact audio blast
PlaySoundFx = true

; Punch camera shake
CameraShake = true

[Vehicles]
; Launch vehicles when punched with Knuckle Duster
AffectVehicles = true

; Vehicle speed multiplier
VehicleSpeedMultiplier = 0.85
```

---

## 🚀 How to Play

1. Start **GTA V Single Player**.
2. Equip the **Knuckle Duster** from your weapon wheel (or using `F4` Native Trainer).
3. Punch any pedestrian or vehicle.
4. The target will only launch when the punch connects, flipping and spinning naturally through the air with full Euphoria ragdoll physics!
