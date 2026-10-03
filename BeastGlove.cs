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
        private float launchSpeed = 68.0f;
        private float launchAngleDeg = 28.0f;
        private float spinTorque = 12.0f;
        private int punchDamage = 1;
        private bool playShockwaveFx = true;
        private bool playSoundFx = true;
        private bool cameraShake = true;
        private bool affectVehicles = true;
        private float vehicleLaunchSpeed = 18.0f;
        private float vehicleSpinTorque = 2.2f;

        // Airborne ped tracking
        private class AirbornePedInfo
        {
            public int StartTime;
            public int ExpireTime;
            public int NextScreamTime;
            public int ReactionType;
        }

        // Tracking dictionaries
        private readonly Dictionary<int, int> recentHits = new Dictionary<int, int>();
        private readonly Dictionary<int, AirbornePedInfo> airbornePeds = new Dictionary<int, AirbornePedInfo>();
        private readonly Random rng = new Random();
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
                    launchSpeed = config.GetValue("Physics", "LaunchSpeed", 68.0f);
                    launchAngleDeg = config.GetValue("Physics", "LaunchAngle", 28.0f);
                    spinTorque = config.GetValue("Physics", "SpinTorque", 12.0f);
                    punchDamage = config.GetValue("Combat", "PunchDamage", 1);

                    playShockwaveFx = config.GetValue("Effects", "PlayShockwaveFx", true);
                    playSoundFx = config.GetValue("Effects", "PlaySoundFx", true);
                    cameraShake = config.GetValue("Effects", "CameraShake", true);

                    affectVehicles = config.GetValue("Vehicles", "AffectVehicles", true);
                    vehicleLaunchSpeed = config.GetValue("Vehicles", "VehicleLaunchSpeed", 18.0f);
                    vehicleSpinTorque = config.GetValue("Vehicles", "VehicleSpinTorque", 2.2f);
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
                    writer.WriteLine("; Initial ped launch speed in m/s (28 m/s ~ 100 km/h produces an authentic 70-80m arc)");
                    writer.WriteLine("LaunchSpeed = 68.0");
                    writer.WriteLine("; Launch angle in degrees (45.0 for maximum natural projectile arc)");
                    writer.WriteLine("LaunchAngle = 28.0");
                    writer.WriteLine("; Natural rotational torque so targets somersault realistically in the air");
                    writer.WriteLine("SpinTorque = 12.0");
                    writer.WriteLine();
                    writer.WriteLine("[Combat]");
                    writer.WriteLine("; Punch damage dealt to target (default 1 to keep peds alive so Euphoria active ragdoll flails in the air)");
                    writer.WriteLine("PunchDamage = 1");
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
                    writer.WriteLine("; Vehicle launch speed in m/s (18 m/s gives a realistic 30-40m dramatic airborne tumble)");
                    writer.WriteLine("VehicleLaunchSpeed = 18.0");
                    writer.WriteLine("; Vehicle spin torque for airborne barrel-rolls");
                    writer.WriteLine("VehicleSpinTorque = 2.2");
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

            // Process airborne peds: maintain 100% active Euphoria ragdoll, flailing, and screaming
            List<int> landed = new List<int>();
            foreach (var kvp in airbornePeds)
            {
                int pedHandle = kvp.Key;
                AirbornePedInfo info = kvp.Value;

                bool exists = Function.Call<bool>(Hash.DOES_ENTITY_EXIST, pedHandle);
                if (!exists)
                {
                    landed.Add(pedHandle);
                    continue;
                }

                Ped p = Entity.FromHandle(pedHandle) as Ped;

                // Check if ped has completed flight (hit the ground, crashed into an obstacle, or flight time elapsed)
                bool minFlightElapsed = currentTime >= (info.StartTime + 800);
                bool inAir = Function.Call<bool>(Hash.IS_ENTITY_IN_AIR, pedHandle);
                bool isSlow = p != null && p.Velocity.Length() < 2.5f;
                bool timeExpired = currentTime >= info.ExpireTime;

                bool hasLanded = timeExpired || (minFlightElapsed && (!inAir || isSlow));

                if (hasLanded)
                {
                    landed.Add(pedHandle);

                    // Lethal terminal impact: kill the ped upon landing so they stay permanently down
                    Function.Call(Hash.SET_ENTITY_INVINCIBLE, pedHandle, false);
                    Function.Call(Hash.SET_PED_DIES_WHEN_INJURED, pedHandle, true);
                    Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, pedHandle, true);
                    Function.Call(Hash.APPLY_DAMAGE_TO_PED, pedHandle, 1000, false);
                    Function.Call(Hash.SET_ENTITY_HEALTH, pedHandle, 0, 0);

                    if (p != null && p.Exists() && p.IsAlive)
                    {
                        p.Kill();
                    }
                }
                else
                {
                    // While airborne: guarantee alive state so Euphoria never turns into a corpse
                    bool isDead = Function.Call<bool>(Hash.IS_ENTITY_DEAD, pedHandle, false);
                    if (isDead)
                    {
                        if (p != null)
                        {
                            p.Resurrect();
                            p.IsCollisionEnabled = true;
                            p.MaxHealth = 200;
                            p.Health = 200;
                            p.CanRagdoll = true;
                            Function.Call(Hash.SET_ENTITY_INVINCIBLE, pedHandle, true);
                            Function.Call(Hash.SET_PED_TO_RAGDOLL_WITH_FALL, pedHandle, 6000, 6000, 0, 0f, 0f, -1f, 1.0f, 0f, 0f, 0f, 0f, 0f, 0f);
                        }
                    }
                    else
                    {
                        // Reset ragdoll timer to keep physics active throughout flight
                        Function.Call(Hash.RESET_PED_RAGDOLL_TIMER, pedHandle);

                        int currentHealth = Function.Call<int>(Hash.GET_ENTITY_HEALTH, pedHandle);
                        if (currentHealth < 100)
                        {
                            Function.Call(Hash.SET_ENTITY_HEALTH, pedHandle, 200, 0);
                        }

                        // Play continuous terrified screaming while airborne
                        if (currentTime >= info.NextScreamTime && currentTime < info.ExpireTime - 800)
                        {
                            Function.Call(Hash.PLAY_PAIN, pedHandle, 6, 0.0f, false);
                            info.NextScreamTime = currentTime + 750;
                        }
                    }
                }
            }
            foreach (int id in landed)
            {
                airbornePeds.Remove(id);
            }

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

            // 1. Check Peds - ONLY trigger when the ped has actually registered damage from the player
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

                // Check distance
                if (ped.Position.DistanceTo(playerPos) > 3.0f)
                {
                    continue;
                }

                // Trigger ONLY when damage is confirmed registered on the victim from the player
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

                    if (veh.Position.DistanceTo(playerPos) > 3.0f)
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

            // Clear engine damage state immediately
            victim.ClearLastWeaponDamage();
            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, victim.Handle);

            // Ensure the victim is 100% alive and healthy for active Euphoria flight
            if (victim.IsDead)
            {
                victim.Resurrect();
                victim.IsCollisionEnabled = true;
            }

            // Boost max health and health to 200 so they stay conscious
            victim.MaxHealth = Math.Max(victim.MaxHealth, 200);
            victim.Health = Math.Max(victim.Health, 200);
            victim.CanSufferCriticalHits = false;
            Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, victim.Handle, false);
            Function.Call(Hash.SET_PED_DIES_WHEN_INJURED, victim.Handle, false);

            // Temporarily protect from impact insta-death while airborne (restored on landing)
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, victim.Handle, true);

            // Pick a random airborne reaction type for natural variety
            // 0: Wild high-fall flailing & arm windmills
            // 1: Terrified cowering / shielding face and tucking
            // 2: Adaptive windmilling & leg pedaling
            int reactionType = rng.Next(3);

            // Track victim for the airborne flight duration
            airbornePeds[victim.Handle] = new AirbornePedInfo
            {
                StartTime = currentTime,
                ExpireTime = currentTime + 5000,
                NextScreamTime = currentTime + 600,
                ReactionType = reactionType
            };

            // Break any melee animation lock between player and victim
            Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, victim.Handle);

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
            // PURE ERO / EUPHORIA HIGH FALL RAGDOLL:
            // - Uses SET_PED_TO_RAGDOLL_WITH_FALL (Hash 0xD76632D99E4966C8)
            // - Fall type 0 (TYPE_FROM_HIGH) initiates CTaskNMHighFall directly,
            //   engaging ERO's tuned air flailing and panic simulation!
            // ================================================================
            victim.CanRagdoll = true;
            Function.Call(Hash.SET_PED_TO_RAGDOLL_WITH_FALL,
                victim.Handle,
                6000, // minTime
                6000, // maxTime
                0,    // nFallType: 0 = TYPE_FROM_HIGH
                launchDir.X,
                launchDir.Y,
                launchDir.Z,
                1.0f, // fGroundHeight
                0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f
            );

            // ACTIVATE SPECIFIC NATURALMOTION EUPHORIA REACTION
            try
            {
                if (reactionType == 0)
                {
                    // Reaction 0: Wild high-fall flailing & arm windmills
                    var hf = victim.Euphoria.HighFall;
                    hf.BodyStiffness = 7.0f;
                    hf.ArmAngSpeed = 24.0f;
                    hf.ArmAmplitude = 4.0f;
                    hf.LegAngSpeed = 16.0f;
                    hf.AdaptiveCircling = true;
                    hf.OrientateBodyToFallDirection = true;
                    hf.AlanRickman = false;
                    hf.ArmsUp = 2.5f;
                    hf.Start();
                }
                else if (reactionType == 1)
                {
                    // Reaction 1: Terrified cowering / shielding face and tucking
                    var ubf = victim.Euphoria.UpperBodyFlinch;
                    ubf.ProtectHeadToggle = true;
                    ubf.UseLeftArm = true;
                    ubf.UseRightArm = true;
                    ubf.BackBendAmount = 0.85f;
                    ubf.BodyStiffness = 12.0f;
                    ubf.Start();

                    var bru = victim.Euphoria.BodyRollUp;
                    bru.Stiffness = 10.0f;
                    bru.ArmReachAmount = 0.3f;
                    bru.Start();
                }
                else
                {
                    // Reaction 2: Adaptive windmilling and leg pedaling
                    var wm = victim.Euphoria.ArmsWindmillAdaptive;
                    wm.AngSpeed = 26.0f;
                    wm.Amplitude = 3.8f;
                    wm.BodyStiffness = 7.0f;
                    wm.Start();

                    var pl = victim.Euphoria.PedalLegs;
                    pl.AngularSpeed = 18.0f;
                    pl.LegStiffness = 7.0f;
                    pl.Start();
                }
            }
            catch
            {
            }

            // Direct linear launch velocity
            victim.Velocity = launchDir * launchSpeed;

            // Apply impulse at upper torso / chest level (0.35m above center of mass)
            // This produces a natural backflip somersault tumble purely through ragdoll physics
            // WITHOUT artificially locking the entity's angular velocity!
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                victim.Handle,
                3, // MaxForceRot2 (external impulse)
                launchDir.X * launchSpeed * 0.4f,
                launchDir.Y * launchSpeed * 0.4f,
                launchDir.Z * launchSpeed * 0.4f,
                0.0f, -0.1f, 0.35f, // Upper chest offset
                0,     // boneIndex
                false, // World coordinate force
                true,  // Ignore up
                true,  // Local offset
                false, // isPush
                true   // isVelChange
            );

            // Scream sound effect from victim
            Function.Call(Hash.PLAY_PAIN, victim.Handle, 7, 0.0f, false);

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

            // Wake up physics simulation
            veh.IsPositionFrozen = false;

            // Direct linear velocity
            veh.Velocity = launchDir * vehicleLaunchSpeed;

            // Apply force with lateral and vertical offset to induce dramatic barrel-roll and pitch-flip
            // Local offset: X = 1.2f (side), Y = -0.6f (rear), Z = 0.5f (roof)
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                veh.Handle,
                3, // MaxForceRot2
                launchDir.X * vehicleLaunchSpeed * 0.5f,
                launchDir.Y * vehicleLaunchSpeed * 0.5f,
                launchDir.Z * vehicleLaunchSpeed * 0.5f,
                1.2f, -0.6f, 0.5f, // Local offset induces roll + pitch flip
                0,
                false, // World coordinates force
                true,  // Ignore up
                true,  // Local offset
                false, // isPush
                true   // isVelChange
            );

            // World angular velocity for dramatic airborne tumble
            Vector3 angVel = new Vector3(-vehicleSpinTorque * 0.8f, vehicleSpinTorque * 1.2f, 0.4f);
            Function.Call(Hash.SET_ENTITY_ANGULAR_VELOCITY, veh.Handle, angVel.X, angVel.Y, angVel.Z);

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
                    "ent_sht_dust",
                    impactPos.X, impactPos.Y, impactPos.Z + 0.3f,
                    0.0f, 0.0f, 0.0f,
                    2.5f,
                    false, false, false
                );
            }
        }
    }
}
