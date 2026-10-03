using System;
using System.Collections.Generic;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;

namespace BeastGloveMod
{
    public class BeastGloveScript : Script
    {
        // Knuckle Duster hash (WEAPON_KNUCKLE = 0xD8DF3C3C)
        private const WeaponHash KnuckleDusterHash = WeaponHash.KnuckleDuster;

        // Configurable settings
        private float launchSpeed = 38.0f;
        private float launchAngleDeg = 45.0f;
        private float forceMultiplier = 40.0f;
        private float spinTorque = 3.5f;
        private int punchDamage = 25;
        private bool playShockwaveFx = true;
        private bool playSoundFx = true;
        private bool cameraShake = true;
        private bool affectVehicles = true;
        private float vehicleSpeedMultiplier = 0.85f;

        // Cooldown tracking to prevent double launches
        private readonly Dictionary<int, int> recentHits = new Dictionary<int, int>();
        private bool ptfxLoaded = false;

        public BeastGloveScript()
        {
            LoadConfig();
            Tick += OnTick;

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
                    launchSpeed = config.GetValue("Physics", "LaunchSpeed", 38.0f);
                    launchAngleDeg = config.GetValue("Physics", "LaunchAngle", 45.0f);
                    forceMultiplier = config.GetValue("Physics", "ForceMultiplier", 40.0f);
                    spinTorque = config.GetValue("Physics", "SpinTorque", 3.5f);
                    punchDamage = config.GetValue("Combat", "PunchDamage", 25);

                    playShockwaveFx = config.GetValue("Effects", "PlayShockwaveFx", true);
                    playSoundFx = config.GetValue("Effects", "PlaySoundFx", true);
                    cameraShake = config.GetValue("Effects", "CameraShake", true);

                    affectVehicles = config.GetValue("Vehicles", "AffectVehicles", true);
                    vehicleSpeedMultiplier = config.GetValue("Vehicles", "VehicleSpeedMultiplier", 0.85f);
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
                    writer.WriteLine("; Natural Euphoria Launch Physics for Knuckle Duster");
                    writer.WriteLine("; ==================================================");
                    writer.WriteLine();
                    writer.WriteLine("[Physics]");
                    writer.WriteLine("; Initial launch speed in m/s");
                    writer.WriteLine("LaunchSpeed = 38.0");
                    writer.WriteLine("; Launch angle in degrees (45.0 for maximum natural projectile arc)");
                    writer.WriteLine("LaunchAngle = 45.0");
                    writer.WriteLine("; Impulse multiplier applied to ragdoll bones");
                    writer.WriteLine("ForceMultiplier = 40.0");
                    writer.WriteLine("; Natural rotational torque so targets spin and tumble realistically");
                    writer.WriteLine("SpinTorque = 3.5");
                    writer.WriteLine();
                    writer.WriteLine("[Combat]");
                    writer.WriteLine("; Non-fatal base damage so target stays alive during flight");
                    writer.WriteLine("PunchDamage = 25");
                    writer.WriteLine();
                    writer.WriteLine("[Effects]");
                    writer.WriteLine("; Sonic air-burst particle ring (no fire/explosions)");
                    writer.WriteLine("PlayShockwaveFx = true");
                    writer.WriteLine("; Impact audio blast");
                    writer.WriteLine("PlaySoundFx = true");
                    writer.WriteLine("; Punch camera shake");
                    writer.WriteLine("CameraShake = true");
                    writer.WriteLine();
                    writer.WriteLine("[Vehicles]");
                    writer.WriteLine("; Launch vehicles when punched with Knuckle Duster");
                    writer.WriteLine("AffectVehicles = true");
                    writer.WriteLine("VehicleSpeedMultiplier = 0.85");
                }
            }
            catch
            {
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.IsAlive)
            {
                return;
            }

            // Only run when Knuckle Duster is currently equipped
            if (player.Weapons.Current.Hash != KnuckleDusterHash)
            {
                return;
            }

            int currentTime = Game.GameTime;

            // Ensure particle asset is available
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

            // Clean up expired cooldowns (2.0s cooldown per target)
            List<int> expired = new List<int>();
            foreach (var kvp in recentHits)
            {
                if (currentTime - kvp.Value > 2000)
                {
                    expired.Add(kvp.Key);
                }
            }
            foreach (int id in expired)
            {
                recentHits.Remove(id);
            }

            // Check for actual registered damage hits
            CheckMeleeDamageHits(player, currentTime);
        }

        private void CheckMeleeDamageHits(Ped player, int currentTime)
        {
            Vector3 playerPos = player.Position;

            // 1. Check Peds - ONLY trigger when the ped HAS ACTUALLY TAKEN DAMAGE from the player
            Ped[] nearbyPeds = World.GetNearbyPeds(player, 2.8f);
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

                // Check distance
                if (ped.Position.DistanceTo(playerPos) > 2.5f)
                {
                    continue;
                }

                // CRITICAL: ONLY trigger when damage is confirmed registered on the victim from the player!
                // This completely eliminates premature launching on lock-on or during lunge animation.
                bool hasDamaged = ped.HasBeenDamagedBy(player) ||
                                  Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, ped.Handle, player.Handle, true);

                if (hasDamaged)
                {
                    LaunchPed(player, ped, currentTime);
                }
            }

            // 2. Check Vehicles - ONLY trigger when damage is confirmed
            if (affectVehicles)
            {
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(player, 3.2f);
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

                    if (veh.Position.DistanceTo(playerPos) > 2.8f)
                    {
                        continue;
                    }

                    bool hasDamaged = Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, veh.Handle, player.Handle, true);
                    if (hasDamaged)
                    {
                        LaunchVehicle(player, veh, currentTime);
                    }
                }
            }
        }

        private void LaunchPed(Ped player, Ped victim, int currentTime)
        {
            // Lock out double-triggers immediately
            recentHits[victim.Handle] = currentTime;

            // Clear engine damage state
            victim.ClearLastWeaponDamage();
            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, victim.Handle);

            // Compute horizontal punch direction
            Vector3 playerPos = player.Position;
            Vector3 victimPos = victim.Position;

            float dx = victimPos.X - playerPos.X;
            float dy = victimPos.Y - playerPos.Y;
            float hDist = (float)Math.Sqrt(dx * dx + dy * dy);

            float nx, ny;
            if (hDist > 0.08f)
            {
                nx = dx / hDist;
                ny = dy / hDist;
            }
            else
            {
                Vector3 fwd = player.ForwardVector;
                nx = fwd.X;
                ny = fwd.Y;
                float fLen = (float)Math.Sqrt(nx * nx + ny * ny);
                if (fLen > 0.01f) { nx /= fLen; ny /= fLen; } else { nx = 1f; ny = 0f; }
            }

            // Natural 45° launch vector
            float angleRad = (float)(launchAngleDeg * (Math.PI / 180.0));
            float cosA = (float)Math.Cos(angleRad);
            float sinA = (float)Math.Sin(angleRad);

            Vector3 launchDir = new Vector3(nx * cosA, ny * cosA, sinA);

            // ================================================================
            // PURE NATURAL EUPHORIA RAGDOLL:
            // - Ragdoll type 0 (CTaskNMRelax)
            // - bForceScriptControl = false: Lets ERO simulate completely free!
            //   No script freezes, full limb flailing, head physics, natural body roll.
            // ================================================================
            victim.CanRagdoll = true;
            Function.Call(Hash.SET_PED_TO_RAGDOLL, victim.Handle, 6000, 6000, 0, false, false, false);
            Function.Call(Hash.RESET_PED_RAGDOLL_TIMER, victim.Handle);
            Function.Call(Hash.SET_PED_RAGDOLL_FORCE_FALL, victim.Handle);

            // Elevate slightly off ground (0.25m) so feet don't drag
            victim.Position = new Vector3(victimPos.X, victimPos.Y, victimPos.Z + 0.25f);

            // Initial launch velocity
            victim.Velocity = launchDir * launchSpeed;

            // Apply physical bone impulse at upper chest (generates natural backflip & tumble)
            float totalForce = launchSpeed * forceMultiplier;
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                victim.Handle,
                1, // MaxForceRot
                launchDir.X * totalForce,
                launchDir.Y * totalForce,
                launchDir.Z * totalForce,
                0f, 0f, 0.35f, // Offset above center of mass creates authentic backflip torque!
                0,
                false, // World coordinates
                true,  // Ignore up
                true,  // Scale by mass
                false,
                true
            );

            // Natural backflip and corkscrew spin in local coordinate space
            victim.LocalRotationVelocity = new Vector3(-spinTorque, 0.4f, 0.6f);

            // Controlled non-fatal damage
            if (punchDamage > 0)
            {
                victim.ApplyDamage(punchDamage);
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
            if (hDist > 0.08f)
            {
                nx = dx / hDist;
                ny = dy / hDist;
            }
            else
            {
                Vector3 fwd = player.ForwardVector;
                nx = fwd.X;
                ny = fwd.Y;
                float fLen = (float)Math.Sqrt(nx * nx + ny * ny);
                if (fLen > 0.01f) { nx /= fLen; ny /= fLen; } else { nx = 1f; ny = 0f; }
            }

            float angleRad = (float)(launchAngleDeg * (Math.PI / 180.0));
            float cosA = (float)Math.Cos(angleRad);
            float sinA = (float)Math.Sin(angleRad);

            Vector3 launchDir = new Vector3(nx * cosA, ny * cosA, sinA);
            float speed = launchSpeed * vehicleSpeedMultiplier;

            // Elevate slightly
            veh.Position = new Vector3(vehPos.X, vehPos.Y, vehPos.Z + 0.35f);
            veh.Velocity = launchDir * speed;

            // Apply force with offset to make car tumble / roll in the air
            float vehForce = speed * forceMultiplier * 1.5f;
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                veh.Handle,
                1,
                launchDir.X * vehForce,
                launchDir.Y * vehForce,
                launchDir.Z * vehForce,
                -ny * 0.5f, nx * 0.5f, 0.4f, // Offset off-center creates barrel-roll / spin!
                0,
                false,
                true,
                true,
                false,
                true
            );

            // Natural pitch and roll spin for launched vehicles
            veh.LocalRotationVelocity = new Vector3(-spinTorque * 0.7f, 0.6f, 0.4f);

            TriggerBeastImpactEffects(veh.Position);
        }

        private void TriggerBeastImpactEffects(Vector3 impactPos)
        {
            Ped player = Game.Player.Character;

            // Camera Shake on punch impact
            if (cameraShake)
            {
                Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "SMALL_EXPLOSION_SHAKE", 0.30f);
            }

            // Punch Impact Audio Blast (deep parachute pop sound)
            if (playSoundFx && player != null)
            {
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "BASEJUMP_PULL_CHUTE", player.Handle, "HUD_AWARDS", 0, 0);
            }

            // Sonic Air-Blast PTFX (dust ring without fiery explosion)
            if (playShockwaveFx && ptfxLoaded)
            {
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, "core");
                Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD,
                    "bul_sand_dust",
                    impactPos.X, impactPos.Y, impactPos.Z,
                    0f, 0f, 0f,
                    1.4f,
                    false, false, false
                );
            }
        }
    }
}
