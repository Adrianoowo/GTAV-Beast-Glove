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

        // Physics Settings
        private float launchSpeed = 46.0f;
        private float launchAngleDeg = 45.0f;
        private int ragdollDurationMs = 7000;
        private int punchDamage = 100;

        // FX & Feedback Settings
        private bool playShockwaveFx = true;
        private bool playSoundFx = true;
        private bool cameraShake = true;
        private bool affectVehicles = true;
        private float vehicleSpeedMultiplier = 0.75f;
        private bool showEquipNotification = true;
        private Keys equipHotkey = Keys.B;

        // State tracking
        private readonly Dictionary<int, int> recentHits = new Dictionary<int, int>();
        private readonly List<LaunchedEntity> inFlightEntities = new List<LaunchedEntity>();
        private bool ptfxLoaded = false;
        private int lastMeleeActionTime = 0;
        private bool notifiedEquipped = false;

        private class LaunchedEntity
        {
            public Entity TargetEntity;
            public int LaunchTime;
            public bool IsPed;
            public bool HasLanded;
        }

        public BeastGloveScript()
        {
            LoadConfig();

            Tick += OnTick;
            KeyDown += OnKeyDown;

            // Pre-request particle assets
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
                    launchSpeed = config.GetValue("Physics", "LaunchSpeed", 46.0f);
                    launchAngleDeg = config.GetValue("Physics", "LaunchAngle", 45.0f);
                    ragdollDurationMs = config.GetValue("Physics", "RagdollDurationMs", 7000);
                    punchDamage = config.GetValue("Combat", "PunchDamage", 100);
                    playShockwaveFx = config.GetValue("Effects", "PlayShockwaveFx", true);
                    playSoundFx = config.GetValue("Effects", "PlaySoundFx", true);
                    cameraShake = config.GetValue("Effects", "CameraShake", true);
                    affectVehicles = config.GetValue("Vehicles", "AffectVehicles", true);
                    vehicleSpeedMultiplier = config.GetValue("Vehicles", "VehicleSpeedMultiplier", 0.75f);
                    showEquipNotification = config.GetValue("General", "ShowEquipNotification", true);
                    equipHotkey = config.GetValue("General", "EquipHotkey", Keys.B);
                }
                else
                {
                    SaveDefaultConfig(iniPath);
                }
            }
            catch
            {
                // Fallback to default values
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
                    writer.WriteLine("LaunchSpeed = 46.0");
                    writer.WriteLine("LaunchAngle = 45.0");
                    writer.WriteLine("RagdollDurationMs = 7000");
                    writer.WriteLine();
                    writer.WriteLine("[Combat]");
                    writer.WriteLine("PunchDamage = 100");
                    writer.WriteLine();
                    writer.WriteLine("[Effects]");
                    writer.WriteLine("PlayShockwaveFx = true");
                    writer.WriteLine("PlaySoundFx = true");
                    writer.WriteLine("CameraShake = true");
                    writer.WriteLine();
                    writer.WriteLine("[Vehicles]");
                    writer.WriteLine("AffectVehicles = true");
                    writer.WriteLine("VehicleSpeedMultiplier = 0.75");
                    writer.WriteLine();
                    writer.WriteLine("[General]");
                    writer.WriteLine("ShowEquipNotification = true");
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

                    Notification.PostTicker("~r~BEAST GLOVE~s~ equipped! Strike targets to launch them ~y~100m~s~ at ~g~45°~s~.", false, false);
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

            // Ensure particle asset is loaded
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

            // Check if Knuckle Duster is equipped
            bool isKnuckleEquipped = (player.Weapons.Current.Hash == KnuckleDusterHash);

            if (isKnuckleEquipped)
            {
                if (!notifiedEquipped)
                {
                    if (showEquipNotification)
                    {
                        Notification.PostTicker("~r~BEAST GLOVE active!~s~ Knuckle Duster hits will send targets soaring.", false, false);
                    }
                    notifiedEquipped = true;
                }

                // Check melee action states
                bool isMelee = player.IsPerformingMeleeAction || player.IsInMeleeCombat || Function.Call<bool>(Hash.IS_PED_PERFORMING_MELEE_ACTION, player.Handle);
                if (isMelee)
                {
                    lastMeleeActionTime = currentTime;
                }

                // Allow 350ms window after melee initiation to catch the hit frame
                bool isAttacking = isMelee || (currentTime - lastMeleeActionTime < 350);

                if (isAttacking)
                {
                    ProcessMeleeImpacts(player, currentTime);
                }
            }
            else
            {
                notifiedEquipped = false;
            }

            // Clean up old hit timestamps (older than 1.5 seconds)
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

            // Monitor entities in flight and manage ERO landing transition
            ProcessInFlightEntities(currentTime);
        }

        private void ProcessMeleeImpacts(Ped player, int currentTime)
        {
            Vector3 playerPos = player.Position;
            Vector3 forward = player.ForwardVector;

            // Check direct melee target first if engine already locked it
            Ped targetPed = player.MeleeTarget;
            if (targetPed != null && targetPed.Exists() && targetPed.Handle != player.Handle && !recentHits.ContainsKey(targetPed.Handle))
            {
                LaunchPed(player, targetPed, currentTime);
                return;
            }

            // 1. Process Nearby Peds
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

                // Check forward facing angle (within 75 degrees of player facing)
                Vector3 toPedNorm = toPed;
                toPedNorm.Normalize();
                float dot = forward.X * toPedNorm.X + forward.Y * toPedNorm.Y;

                bool hasDamaged = ped.HasBeenDamagedBy(player) || Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, ped.Handle, player.Handle, true);
                bool inPunchCone = (dist < 2.0f && dot > 0.35f);

                if (hasDamaged || inPunchCone)
                {
                    LaunchPed(player, ped, currentTime);
                }
            }

            // 2. Process Vehicles (if enabled)
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
                    bool inPunchCone = (dist < 2.5f && dot > 0.4f);

                    if (hasDamaged || inPunchCone)
                    {
                        LaunchVehicle(player, veh, currentTime);
                    }
                }
            }
        }

        private void LaunchPed(Ped player, Ped victim, int currentTime)
        {
            recentHits[victim.Handle] = currentTime;

            // Clear GTA damage status so it doesn't loop
            victim.ClearLastWeaponDamage();
            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, victim.Handle);

            // Calculate 45-degree launch vector away from attacker
            Vector3 playerPos = player.Position;
            Vector3 victimPos = victim.Position;

            float dx = victimPos.X - playerPos.X;
            float dy = victimPos.Y - playerPos.Y;
            float horizontalDist = (float)Math.Sqrt(dx * dx + dy * dy);

            float nx, ny;
            if (horizontalDist > 0.1f)
            {
                nx = dx / horizontalDist;
                ny = dy / horizontalDist;
            }
            else
            {
                Vector3 fwd = player.ForwardVector;
                nx = fwd.X;
                ny = fwd.Y;
                float fwdLen = (float)Math.Sqrt(nx * nx + ny * ny);
                if (fwdLen > 0.01f) { nx /= fwdLen; ny /= fwdLen; } else { nx = 1f; ny = 0f; }
            }

            // 45 degrees: cos(45) = sin(45) = sqrt(2)/2 = 0.7071
            float angleRad = (float)(launchAngleDeg * (Math.PI / 180.0));
            float horizFactor = (float)Math.Cos(angleRad);
            float vertFactor = (float)Math.Sin(angleRad);

            float vx = nx * horizFactor * launchSpeed;
            float vy = ny * horizFactor * launchSpeed;
            float vz = vertFactor * launchSpeed;

            // ========================================================
            // ERO COMPATIBILITY LAYER:
            // 1. Force ped into standard relaxed ragdoll (type 0).
            //    This bypasses ERO's stumble balancer and prevent foot drag.
            // 2. Clear Euphoria balance timers so NM tasks don't fight velocity.
            // 3. Keep script control over ragdoll.
            // ========================================================
            Function.Call(Hash.SET_PED_CAN_RAGDOLL, victim.Handle, true);
            Function.Call(Hash.SET_PED_TO_RAGDOLL, victim.Handle, ragdollDurationMs, ragdollDurationMs, 0, false, false, true);
            Function.Call(Hash.RESET_PED_RAGDOLL_TIMER, victim.Handle);
            Function.Call(Hash.SET_PED_RAGDOLL_FORCE_FALL, victim.Handle);

            // Slightly elevate to prevent instant ground friction
            victim.Position = new Vector3(victimPos.X, victimPos.Y, victimPos.Z + 0.2f);

            // Apply direct launch velocity
            Function.Call(Hash.SET_ENTITY_VELOCITY, victim.Handle, vx, vy, vz);

            // Add rotational tumble impulse for cinematic mid-air flight
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY, victim.Handle, 1, 0f, 0f, 0f, 0.4f, 0f, 0.4f, 0, true, true, true, false, true);

            // Apply punch damage
            if (punchDamage > 0)
            {
                victim.ApplyDamage(punchDamage);
            }

            // Track entity in flight
            inFlightEntities.Add(new LaunchedEntity
            {
                TargetEntity = victim,
                LaunchTime = currentTime,
                IsPed = true,
                HasLanded = false
            });

            // Trigger BEAST Glove Effects (Sound, FX, Camera)
            TriggerImpactEffects(victim.Position);
        }

        private void LaunchVehicle(Ped player, Vehicle veh, int currentTime)
        {
            recentHits[veh.Handle] = currentTime;

            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, veh.Handle);

            Vector3 playerPos = player.Position;
            Vector3 vehPos = veh.Position;

            float dx = vehPos.X - playerPos.X;
            float dy = vehPos.Y - playerPos.Y;
            float horizontalDist = (float)Math.Sqrt(dx * dx + dy * dy);

            float nx, ny;
            if (horizontalDist > 0.1f)
            {
                nx = dx / horizontalDist;
                ny = dy / horizontalDist;
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
            float horizFactor = (float)Math.Cos(angleRad);
            float vertFactor = (float)Math.Sin(angleRad);

            float speed = launchSpeed * vehicleSpeedMultiplier;
            float vx = nx * horizFactor * speed;
            float vy = ny * horizFactor * speed;
            float vz = vertFactor * speed;

            // Elevate slightly to unstick wheels
            veh.Position = new Vector3(vehPos.X, vehPos.Y, vehPos.Z + 0.35f);
            Function.Call(Hash.SET_ENTITY_VELOCITY, veh.Handle, vx, vy, vz);

            inFlightEntities.Add(new LaunchedEntity
            {
                TargetEntity = veh,
                LaunchTime = currentTime,
                IsPed = false,
                HasLanded = false
            });

            TriggerImpactEffects(veh.Position);
        }

        private void TriggerImpactEffects(Vector3 impactPos)
        {
            Ped player = Game.Player.Character;

            // Camera Shake
            if (cameraShake)
            {
                Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "SMALL_EXPLOSION_SHAKE", 0.4f);
            }

            // Audio Shockwave
            if (playSoundFx && player != null)
            {
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "BASEJUMP_PULL_CHUTE", player.Handle, "HUD_AWARDS", 0, 0);
            }

            // Visual Particle Shockwave (Air burst ring)
            if (playShockwaveFx && ptfxLoaded)
            {
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, "core");
                Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD,
                    "exp_grd_grenade_lod",
                    impactPos.X, impactPos.Y, impactPos.Z,
                    0f, 0f, 0f,
                    0.5f,
                    false, false, false
                );
            }
        }

        private void ProcessInFlightEntities(int currentTime)
        {
            for (int i = inFlightEntities.Count - 1; i >= 0; i--)
            {
                LaunchedEntity item = inFlightEntities[i];
                if (item.TargetEntity == null || !item.TargetEntity.Exists())
                {
                    inFlightEntities.RemoveAt(i);
                    continue;
                }

                int flightDuration = currentTime - item.LaunchTime;

                if (item.IsPed)
                {
                    Ped ped = (Ped)item.TargetEntity;

                    // Ensure ped stays in ragdoll while soaring through the air
                    float heightAboveGround = ped.HeightAboveGround;
                    if (flightDuration > 300 && flightDuration < 6000)
                    {
                        if (heightAboveGround > 1.2f)
                        {
                            if (!ped.IsRagdoll)
                            {
                                Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 4000, 4000, 0, false, false, true);
                            }
                        }
                        else if (!item.HasLanded && flightDuration > 1000)
                        {
                            // Ped has touched ground: allow ERO NaturalMotion behaviors to kick in!
                            item.HasLanded = true;
                            // Reset script control so ERO ground-tumble / roll reactions take over
                            Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 3000, 3000, 0, false, false, false);
                        }
                    }

                    if (flightDuration > 8000)
                    {
                        inFlightEntities.RemoveAt(i);
                    }
                }
                else
                {
                    if (flightDuration > 7000)
                    {
                        inFlightEntities.RemoveAt(i);
                    }
                }
            }
        }
    }
}
