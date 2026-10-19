⚠️ This whole mod is fully vibecoded. Proceed at your own risk and morals, i do not care any more than that
# BEAST Glove Mod for GTA V (Legacy Edition)

Authentic **BEAST Glove** physics simulation for **Grand Theft Auto V Legacy** (`v1.0.3889.0`), integrated natively with **ERO (Euphoria Ragdoll Overhaul 1.9.4)**.

---

## 🥊 Features

- **Confirmed Damage Hit Detection**: Triggers **only** when a punch with the Knuckle Duster (`WEAPON_KNUCKLE`) physically connects and inflicts damage. Zero premature launches during lock-on, lunges, or wind-ups; zero double launches.
- **Natural Euphoria Ragdoll**: Peds are launched into an upward parabolic arc with authentic backflip torque and corkscrew rotation. Euphoria is given 100% free reign (`bForceScriptControl = false`), with dynamic procedural flailing (`UpperBodyFlinch`, `BodyRollUp`, `ArmsWindmillAdaptive`, `PedalLegs`) and realistic tumble physics upon impact.
- **Dynamic Vehicle Launches**: Punching vehicles launches them with off-center rotational torque, producing dramatic aerial spins, barrel rolls, and high-impact crashes.
- **Audio & Visuals**:
  - Clean supersonic dust shockwave ring (`ent_sht_dust`).
  - Heavy punch impact audio blast (`BASEJUMP_PULL_CHUTE`).
  - Screaming victim audio upon launch.
  - Physical camera shake on hit (`SMALL_EXPLOSION_SHAKE`).
- **Clean & Immersive**: No on-screen notification spam and no hotkey overrides. Simply equip the Knuckle Duster from your weapon wheel or trainer and punch.
- **ERO (Euphoria Ragdoll Overhaul) Compatible**: Fully tested and optimized to work alongside ERO 1.9.4 and `RageOpenV.asi`.

---

## 📦 Quick Installation

1. Download the latest `BeastGlove-v1.0.0.zip` from the **[Releases](../../releases)** tab.
2. Extract the contents directly into your **Grand Theft Auto V root folder** (where `GTA5.exe` is located):
   - Example: `C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V`
3. If prompted to overwrite files, select **Replace**.

### File Structure in GTA V Root:
```
Grand Theft Auto V/
├── args.txt                  (Disables BattlEye for singleplayer modding)
├── commandline.txt           (Disables BattlEye for singleplayer modding)
├── dinput8.dll               (ASI Loader)
├── ScriptHookV.dll           (ScriptHookV runtime)
├── NativeTrainer.asi         (F4 menu to spawn Knuckle Duster & vehicles)
├── ScriptHookVDotNet.asi     (SHVDN v3.7.0 bridge)
├── ScriptHookVDotNet.ini     (SHVDN config)
├── ScriptHookVDotNet2.dll    (SHVDN API)
├── ScriptHookVDotNet3.dll    (SHVDN API)
├── RageOpenV.asi             (Open-source RPF archive loader for mods)
├── RageOpenV.ini             (RageOpenV config)
├── ERO1.9.4.oiv              (Euphoria Ragdoll Overhaul package)
└── scripts/
    ├── BeastGlove.dll        (Precompiled mod binary)
    ├── BeastGlove.ini        (Mod settings)
    └── BeastGlove.cs         (Source code)
```

---

## ⚙️ Configuration (`scripts/BeastGlove.ini`)

All physics, damage, and effects are fully customizable via `scripts/BeastGlove.ini`:

```ini
; ==================================================
; GTA V Legacy - BEAST GLOVE MOD (ERO Compatible)
; Natural Euphoria Launch Physics for Knuckle Duster
; ==================================================

[Physics]
; Initial ped launch speed in m/s (68 m/s produces an authentic high-velocity arc)
LaunchSpeed = 68.0
; Launch angle in degrees (28.0 for forward-punch trajectory)
LaunchAngle = 28.0
; Natural rotational torque so targets somersault realistically in the air
SpinTorque = 12.0

[Combat]
; Punch damage dealt to target (1 keeps peds alive so Euphoria active ragdoll flails dynamically)
PunchDamage = 1

[Effects]
; Sonic air-burst particle ring (no fire/explosions)
PlayShockwaveFx = true
; Impact audio blast
PlaySoundFx = true
; Punch camera shake
CameraShake = true

[Vehicles]
; Launch vehicles when punched with Knuckle Duster
AffectVehicles = true
; Vehicle launch speed in m/s (18 m/s gives a realistic dramatic airborne tumble)
VehicleLaunchSpeed = 18.0
; Vehicle spin torque for airborne barrel-rolls
VehicleSpinTorque = 2.2
```

---

## 🚀 How to Play

1. Launch **GTA V Story Mode** (BattlEye is automatically bypassed by `commandline.txt`).
2. Equip the **Knuckle Duster** (`WEAPON_KNUCKLE`) from your weapon wheel or press `F4` to open Native Trainer (`Weapons -> Get All Weapons`).
3. Punch any pedestrian or vehicle.
4. Watch them fly with complete Euphoria ragdoll physics and tumbling somersaults!
5. Reload mod at any time in-game by pressing `Insert` (SHVDN reload key).

---

## 🛠️ Building from Source

To compile `BeastGlove.dll` from `BeastGlove.cs`, run:
```cmd
build.bat
```
This uses Microsoft .NET Framework 4.8 `csc.exe` (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) and references `ScriptHookVDotNet3.dll`. No Visual Studio install required.

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).
