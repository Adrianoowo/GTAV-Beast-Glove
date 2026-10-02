using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace BeastGloveMod
{
    public class BeastGloveScript : Script
    {
        // Weapon Hash for Knuckle Duster (WEAPON_KNUCKLE = 0xD8DF3C3C)
        private const WeaponHash KnuckleDusterHash = WeaponHash.KnuckleDuster;

        // Configurable settings
        private float launchSpeed = 48.0f;
        private float launchAngleDeg = 45.0f;
        private float forceMultiplier = 55.0f;
        private int ragdollDurationMs = 8000;
        private int punchDamage = 20;

        private bool playShockwaveFx = true;
        private bool playSoundFx = true;
        private bool cameraShake = true;
        private bool affectVehicles = true;
        private float vehicleSpeedMultiplier = 0.85f;
        private bool showEquipNotification = true;
        private bool showHitNotification = true;
        private Keys equipHotkey = Keys.B;

        // Internal tracking
        private readonly Dictionary<int, int> recentHits = new Dictionary<int, int>();
        private readonly List<ActiveFlight> activeFlights = new List<ActiveFlight>();
        private bool ptfxLoaded = false;
        private int lastAttackTime = 0;
        private bool notifiedEquipped = false;

        private class ActiveFlight
        {
            public Entity Target;
            public Vector3 LaunchDir;
            public float Speed;
            public int StartTime;
            public int BoostTicksLeft;
            public bool IsPed;
        }

        public BeastGloveScript()
        {
            LoadConfig();

            Tick += OnTick;
            KeyDown += OnKeyDown;

            // Pre-request PTFX asset
            Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "core");
        }

        private void LoadConfig()
        {
            try
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "BeastGlove.ini");
                if (!File.Exists(iniPath))
                {
                    iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BeastGlove.ini");
                }

                if (File.Exists(iniPath))
                {
                    ScriptSettings config = ScriptSettings.Load(iniPath);
                    launchSpeed = config.GetValue("Physics", "LaunchSpeed", 48.0f);
                    launchAngleDeg = config.GetValue("Physics", "LaunchAngle", 45.0f);
                    forceMultiplier = config.GetValue("Physics", "ForceMultiplier", 55.0f);
                    ragdollDurationMs = config.GetValue("Physics", "RagdollDurationMs", 8000);
                    punchDamage = config.GetValue("Combat", "PunchDamage", 20);

                    playShockwaveFx = config.GetValue("Effects", "PlayShockwaveFx", true);
                    playSoundFx = config.GetValue("Effects", "PlaySoundFx", true);
                    cameraShake = config.GetValue("Effects", "CameraShake", true);

                    affectVehicles = config.GetValue("Vehicles", "AffectVehicles", true);
                    vehicleSpeedMultiplier = config.GetValue("Vehicles", "VehicleSpeedMultiplier", 0.85f);

                    showEquipNotification = config.GetValue("General", "ShowEquipNotification", true);
                    showHitNotification = config.GetValue("General", "ShowHitNotification", true);
                    equipHotkey = config.GetValue("General", "EquipHotkey", Keys.B);
                }
                else
                {
                    SaveDefaultConfig(iniPath);
                }
            }
            catch
            {
            }
        }

        private void SaveDefaultConfig(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (StreamWriter writer = new StreamWriter(path, false))
                {
                    writer.WriteLine("; ==================================================");
                    writer.WriteLine("; GTA V Legacy - BEAST GLOVE MOD (ERO Compatible)");
                    writer.WriteLine("; Knuckle Duster super launch effect (~100m @ 45°)");
                    writer.WriteLine("; ==================================================");
                    writer.WriteLine();
                    writer.WriteLine("[Physics]");
                    writer.WriteLine("; Launch speed in m/s (48.0 delivers ~100m arc under GTA V drag)");
                    writer.WriteLine("LaunchSpeed = 48.0");
                    writer.WriteLine("; Launch angle in degrees (45.0 for maximum range)");
                    writer.WriteLine("LaunchAngle = 45.0");
                    writer.WriteLine("; Physics impulse multiplier for ragdoll bones");
                    writer.WriteLine("ForceMultiplier = 55.0");
                    writer.WriteLine("; Flight ragdoll duration in milliseconds");
                    writer.WriteLine("RagdollDurationMs = 8000");
                    writer.WriteLine();
                    writer.WriteLine("[Combat]");
                    writer.WriteLine("; Non-fatal base damage so target stays alive during flight");
                    writer.WriteLine("PunchDamage = 20");
                    writer.WriteLine();
                    writer.WriteLine("[Effects]");
                    writer.WriteLine("; Sonic air-blast particle ring (clean, no fire/explosion)");
                    writer.WriteLine("PlayShockwaveFx = true");
                    writer.WriteLine("; Heavy impact audio blast");
                    writer.WriteLine("PlaySoundFx = true");
                    writer.WriteLine("; Camera punch shake");
                    writer.WriteLine("CameraShake = true");
                    writer.WriteLine();
                    writer.WriteLine("[Vehicles]");
                    writer.WriteLine("; Launch vehicles when punched with Knuckle Duster");
                    writer.WriteLine("AffectVehicles = true");
                    writer.WriteLine("VehicleSpeedMultiplier = 0.85");
                    writer.WriteLine();
                    writer.WriteLine("[General]");
                    writer.WriteLine("ShowEquipNotification = true");
                    writer.WriteLine("ShowHitNotification = true");
                    writer.WriteLine("EquipHotkey = B");
                }
            }
            catch
            {
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == equipHotkey)
            {
                Ped player = Game.Player.Character;
                if (player != null && player.IsAlive)
                {
                    if (!player.Weapons.HasWeapon(KnuckleDusterHash))
                    {
                        player.Weapons.Give(KnuckleDusterHash, 1, true, true);
                    }
                    else
                    {
                        player.Weapons.Select(KnuckleDusterHash, true);
                    }

                    Notification.PostTicker("~r~BEAST GLOVE~s~ equipped! Punch targets to launch them ~y~100m~s~ at ~g~45°~s~.", false, false);
                }
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.IsAlive)
            {
                return;
            }

            int currentTime = Game.GameTime;

            // Ensure PTFX asset is loaded
            if (!ptfxLoaded)
            {
                if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, "core"))
                {
                    ptfxLoaded = true;
                }
                else
                {
                    Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "core");
                }
            }

            // Check if Knuckle Duster is currently equipped
            bool isKnuckleEquipped = (player.Weapons.Current.Hash == KnuckleDusterHash);

            if (isKnuckleEquipped)
            {
                if (!notifiedEquipped)
                {
                    if (showEquipNotification)
                    {
                        Notification.PostTicker("~r~BEAST GLOVE active!~s~ Knuckle Duster launches targets 100m.", false, false);
                    }
                    notifiedEquipped = true;
                }

                // Check melee combat status
                bool isMelee = player.IsPerformingMeleeAction ||
                               player.IsInMeleeCombat ||
                               Game.IsControlJustPressed(GTA.Control.Attack) ||
                               Game.IsControlPressed(GTA.Control.Attack) ||
                               Function.Call<bool>(Hash.IS_PED_PERFORMING_MELEE_ACTION, player.Handle);

                if (isMelee)
                {
                    lastAttackTime = currentTime;
                }

                // 400ms detection window to catch impact frames
                if (currentTime - lastAttackTime < 400)
                {
                    CheckAndLaunchTargets(player, currentTime);
                }
            }
            else
            {
                notifiedEquipped = false;
            }

            // Clean up recent hits cooldown list (1.5s cooldown per entity)
            List<int> toRemoveHits = new List<int>();
            foreach (var kvp in recentHits)
            {
                if (currentTime - kvp.Value > 1500)
                {
                    toRemoveHits.Add(kvp.Key);
                }
            }
            foreach (int key in toRemoveHits)
            {
                recentHits.Remove(key);
            }

            // Apply multi-frame velocity boost and monitor flight
            ProcessActiveFlights(currentTime);
        }

        private void CheckAndLaunchTargets(Ped player, int currentTime)
        {
            Vector3 playerPos = player.Position;
            Vector3 forward = player.ForwardVector;

            // Check direct engine melee target
            Ped targetPed = player.MeleeTarget;
            if (targetPed != null && targetPed.Exists() && targetPed.Handle != player.Handle && !recentHits.ContainsKey(targetPed.Handle))
            {
                LaunchPed(player, targetPed, currentTime);
                return;
            }

            // Check nearby peds
            Ped[] nearbyPeds = World.GetNearbyPeds(player, 3.5f);
            foreach (Ped ped in nearbyPeds)
            {
                if (ped == null || !ped.Exists() || ped.Handle == player.Handle)
                {
                    continue;
                }

                if (recentHits.ContainsKey(ped.Handle))
                {
                    continue;
                }

                Vector3 toPed = ped.Position - playerPos;
                float dist = toPed.Length();
                if (dist > 3.0f)
                {
                    continue;
                }

                Vector3 toPedNorm = toPed;
                toPedNorm.Normalize();
                float dot = forward.X * toPedNorm.X + forward.Y * toPedNorm.Y;

                bool hasDamaged = ped.HasBeenDamagedBy(player) || Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, ped.Handle, player.Handle, true);
                bool inFrontOfPlayer = (dist < 2.2f && dot > 0.35f);

                if (hasDamaged || inFrontOfPlayer)
                {
                    LaunchPed(player, ped, currentTime);
                }
            }

            // Check nearby vehicles
            if (affectVehicles)
            {
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(player, 3.5f);
                foreach (Vehicle veh in nearbyVehicles)
                {
                    if (veh == null || !veh.Exists())
                    {
                        continue;
                    }

                    if (recentHits.ContainsKey(veh.Handle))
                    {
                        continue;
                    }

                    Vector3 toVeh = veh.Position - playerPos;
                    float dist = toVeh.Length();
                    if (dist > 3.2f)
                    {
                        continue;
                    }

                    Vector3 toVehNorm = toVeh;
                    toVehNorm.Normalize();
                    float dot = forward.X * toVehNorm.X + forward.Y * toVehNorm.Y;

                    bool hasDamaged = Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, veh.Handle, player.Handle, true);
                    bool inFront = (dist < 2.5f && dot > 0.4f);

                    if (hasDamaged || inFront)
                    {
                        LaunchVehicle(player, veh, currentTime);
                    }
                }
            }
        }

        private void LaunchPed(Ped player, Ped victim, int currentTime)
        {
            recentHits[victim.Handle] = currentTime;

            // Clear damage state to prevent double triggers
            victim.ClearLastWeaponDamage();
            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, victim.Handle);

            // Compute 45-degree launch vector away from punch
            Vector3 playerPos = player.Position;
            Vector3 victimPos = victim.Position;

            float dx = victimPos.X - playerPos.X;
            float dy = victimPos.Y - playerPos.Y;
            float hDist = (float)Math.Sqrt(dx * dx + dy * dy);

            float nx, ny;
            if (hDist > 0.1f)
            {
                nx = dx / hDist;
                ny = dy / hDist;
            }
            else
            {
                Vector3 fwd = player.ForwardVector;
                nx = fwd.X;
                ny = fwd.Y;
                float fwdLen = (float)Math.Sqrt(nx * nx + ny * ny);
                if (fwdLen > 0.01f) { nx /= fwdLen; ny /= fwdLen; } else { nx = 1f; ny = 0f; }
            }

            // 45 degrees: cos(45) = sin(45) = sqrt(2)/2 = 0.7071068
            float angleRad = (float)(launchAngleDeg * (Math.PI / 180.0));
            float cosA = (float)Math.Cos(angleRad);
            float sinA = (float)Math.Sin(angleRad);

            Vector3 launchDir = new Vector3(nx * cosA, ny * cosA, sinA);

            // ========================================================
            // ERO COMPATIBILITY & RAGDOLL SETUP:
            // 1. Force relaxed physics ragdoll (type 0: CTaskNMRelax).
            //    This disables ERO's stumble-balancer joint torques so
            //    the ped doesn't try to plant their feet on the ground.
            // 2. Clear Euphoria balance timers so NM tasks don't fight velocity.
            // ========================================================
            victim.CanRagdoll = true;
            Function.Call(Hash.SET_PED_TO_RAGDOLL, victim.Handle, ragdollDurationMs, ragdollDurationMs, 0, false, false, false);
            Function.Call(Hash.RESET_PED_RAGDOLL_TIMER, victim.Handle);
            Function.Call(Hash.SET_PED_RAGDOLL_FORCE_FALL, victim.Handle);

            // Lift ped slightly off ground to instantly break ground friction
            victim.Position = new Vector3(victimPos.X, victimPos.Y, victimPos.Z + 0.35f);

            // Set root entity velocity
            victim.Velocity = launchDir * launchSpeed;
            Function.Call(Hash.SET_ENTITY_VELOCITY, victim.Handle, launchDir.X * launchSpeed, launchDir.Y * launchSpeed, launchDir.Z * launchSpeed);

            // Apply massive impulse to ragdoll bones (ScaleByMass = true)
            float totalForce = launchSpeed * forceMultiplier;
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                victim.Handle,
                1, // MaxForceRot
                launchDir.X * totalForce,
                launchDir.Y * totalForce,
                launchDir.Z * totalForce,
                0f, 0f, 0f,
                0,     // Pelvis / root
                false, // World coordinates
                true,  // Ignore up
                true,  // Scale by mass
                false,
                true
            );

            // Add rotational tumble impulse for dramatic flight
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                victim.Handle,
                1,
                0f, 0f, 0f,
                0.5f, 0f, 0.5f,
                0,
                true,
                true,
                true,
                false,
                true
            );

            // Apply non-fatal punch damage so target stays alive during flight
            if (punchDamage > 0)
            {
                victim.ApplyDamage(punchDamage);
            }

            // Queue for multi-frame initial velocity boost (guarantees takeoff)
            activeFlights.Add(new ActiveFlight
            {
                Target = victim,
                LaunchDir = launchDir,
                Speed = launchSpeed,
                StartTime = currentTime,
                BoostTicksLeft = 8,
                IsPed = true
            });

            if (showHitNotification)
            {
                Notification.PostTicker("~r~BEAST PUNCH!~s~ Target launched ~y~100m~s~.", false, false);
            }

            TriggerBeastImpactEffects(victim.Position);
        }

        private void LaunchVehicle(Ped player, Vehicle veh, int currentTime)
        {
            recentHits[veh.Handle] = currentTime;

            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, veh.Handle);

            Vector3 playerPos = player.Position;
            Vector3 vehPos = veh.Position;

            float dx = vehPos.X - playerPos.X;
            float dy = vehPos.Y - playerPos.Y;
            float hDist = (float)Math.Sqrt(dx * dx + dy * dy);

            float nx, ny;
            if (hDist > 0.1f)
            {
                nx = dx / hDist;
                ny = dy / hDist;
            }
            else
            {
                Vector3 fwd = player.ForwardVector;
                nx = fwd.X;
                ny = fwd.Y;
                float fwdLen = (float)Math.Sqrt(nx * nx + ny * ny);
                if (fwdLen > 0.01f) { nx /= fwdLen; ny /= fwdLen; } else { nx = 1f; ny = 0f; }
            }

            float angleRad = (float)(launchAngleDeg * (Math.PI / 180.0));
            float cosA = (float)Math.Cos(angleRad);
            float sinA = (float)Math.Sin(angleRad);

            Vector3 launchDir = new Vector3(nx * cosA, ny * cosA, sinA);
            float speed = launchSpeed * vehicleSpeedMultiplier;

            veh.Position = new Vector3(vehPos.X, vehPos.Y, vehPos.Z + 0.45f);
            veh.Velocity = launchDir * speed;
            Function.Call(Hash.SET_ENTITY_VELOCITY, veh.Handle, launchDir.X * speed, launchDir.Y * speed, launchDir.Z * speed);

            float vehForce = speed * forceMultiplier * 1.5f;
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                veh.Handle,
                1,
                launchDir.X * vehForce,
                launchDir.Y * vehForce,
                launchDir.Z * vehForce,
                0f, 0f, 0f,
                0,
                false,
                true,
                true,
                false,
                true
            );

            activeFlights.Add(new ActiveFlight
            {
                Target = veh,
                LaunchDir = launchDir,
                Speed = speed,
                StartTime = currentTime,
                BoostTicksLeft = 6,
                IsPed = false
            });

            TriggerBeastImpactEffects(veh.Position);
        }

        private void TriggerBeastImpactEffects(Vector3 impactPos)
        {
            Ped player = Game.Player.Character;

            // Camera Shake on punch impact
            if (cameraShake)
            {
                Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "SMALL_EXPLOSION_SHAKE", 0.35f);
            }

            // Punch Impact Audio Blast (heavy pop / boom)
            if (playSoundFx && player != null)
            {
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "BASEJUMP_PULL_CHUTE", player.Handle, "HUD_AWARDS", 0, 0);
            }

            // Sonic Air-Blast PTFX (Supersonic dust ring without fire/explosion)
            if (playShockwaveFx && ptfxLoaded)
            {
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, "core");
                // "bul_sand_dust" produces a clean, high-velocity dust shockwave ring
                Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD,
                    "bul_sand_dust",
                    impactPos.X, impactPos.Y, impactPos.Z,
                    0f, 0f, 0f,
                    1.6f,
                    false, false, false
                );
            }
        }

        private void ProcessActiveFlights(int currentTime)
        {
            for (int i = activeFlights.Count - 1; i >= 0; i--)
            {
                ActiveFlight flight = activeFlights[i];
                if (flight.Target == null || !flight.Target.Exists())
                {
                    activeFlights.RemoveAt(i);
                    continue;
                }

                // Initial boost frames: keep pushing to completely overcome ground drag
                if (flight.BoostTicksLeft > 0)
                {
                    flight.BoostTicksLeft--;
                    flight.Target.Velocity = flight.LaunchDir * flight.Speed;
                }

                int elapsed = currentTime - flight.StartTime;

                if (flight.IsPed)
                {
                    Ped ped = (Ped)flight.Target;

                    // Ensure ped stays in ragdoll during flight
                    if (elapsed > 200 && elapsed < ragdollDurationMs - 1000)
                    {
                        if (ped.HeightAboveGround > 1.2f)
                        {
                            if (!ped.IsRagdoll)
                            {
                                Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 4000, 4000, 0, false, false, false);
                            }
                        }
                    }

                    if (elapsed > ragdollDurationMs)
                    {
                        activeFlights.RemoveAt(i);
                    }
                }
                else
                {
                    if (elapsed > 7000)
                    {
                        activeFlights.RemoveAt(i);
                    }
                }
            }
        }
    }
}
