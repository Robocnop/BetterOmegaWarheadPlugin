using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Warhead;
using Respawning;
using MEC;
using System.Collections.Generic;
using UnityEngine;
using Map = Exiled.API.Features.Map;

namespace BetterOmegaWarhead
{
    class EventHandlers
    {
        public bool OmegaActivated = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();
        public List<Player> HelikopterSurvivors = new List<Player>();

        public void OnRestartingRound()
        {
            Log.Debug("Restarting round. Resetting Omega Warhead state.");
            foreach (var coroutine in Coroutines)
                Timing.KillCoroutines(coroutine);
            HelikopterSurvivors.Clear();
            OmegaActivated = false;
            Coroutines.Clear();
        }

        public void OnWarheadStart(StartingEventArgs ev)
        {
            if (Plugin.Singleton.Config.ReplaceAlpha)
            {
                Log.Debug("Alpha Warhead is being replaced by Omega Warhead.");
                ev.IsAllowed = false;
                OmegaWarhead();
            }
            if (OmegaActivated)
            {
                Log.Debug("Omega Warhead is already activated. Preventing start.");
                ev.IsAllowed = false;
            }
        }

        public void OnWarheadStop(StoppingEventArgs ev)
        {
            // Chance sur 10 d'activer l'Omega Warhead
            if (Plugin.Singleton.Config.ReplaceAlpha && Random.Range(0, 10) == 0)
            {
                Log.Debug("Activating Omega Warhead after Alpha Warhead stop.");
                OmegaWarhead();
            }
            else
            {
                Log.Debug("Omega Warhead not activated after Alpha Warhead stop.");
            }
        }

        public void StopOmega()
        {
            Log.Debug("Stopping Omega Warhead.");
            OmegaActivated = false;
            Cassie.Clear();
            HelikopterSurvivors.Clear();
            Cassie.Message(Plugin.Singleton.Config.StopCassie, false, false);
            foreach (var coroutine in Plugin.Singleton.handler.Coroutines)
                Timing.KillCoroutines(coroutine);
            foreach (Room room in Room.List)
                room.ResetColor();
        }

        public void OmegaWarhead()
        {
            Log.Debug("Omega Warhead activated.");
            OmegaActivated = true;
            foreach (Room room in Room.List)
                room.Color = Color.cyan;

            Cassie.Message(Plugin.Singleton.Config.Cassie, false, false);
            Map.Broadcast(10, Plugin.Singleton.Config.ActivatedMessage);

            Coroutines.Add(Timing.CallDelayed(150, () =>
            {
                Log.Debug("Opening checkpoints for evacuation.");
                foreach (Door checkpoint in Door.List)
                {
                    if (checkpoint.Type == DoorType.CheckpointEzHczA || checkpoint.Type == DoorType.CheckpointEzHczB || checkpoint.Type == DoorType.CheckpointLczA || checkpoint.Type == DoorType.CheckpointLczB)
                    {
                        checkpoint.IsOpen = true;
                        checkpoint.Lock(69420, DoorLockType.Warhead);
                    }
                }
            }));

            Coroutines.Add(Timing.CallDelayed(179, () =>
            {
                Log.Debug("Executing post-warhead activation effects.");
                Timing.CallDelayed(4, () =>
                {
                    foreach (Player Helikopter in Player.List)
                        if (HelikopterSurvivors.Contains(Helikopter))
                        {
                            Helikopter.DisableAllEffects();
                            Helikopter.Scale = new Vector3(1, 1, 1);
                            Helikopter.Position = new Vector3(178, 1000, -59);
                            Timing.CallDelayed(2, HelikopterSurvivors.Clear);
                        }
                });
                foreach (Player People in Player.List)
                {
                    if (People.CurrentRoom.Type == RoomType.EzShelter)
                    {
                        Log.Debug($"Player {People.Nickname} is in the EZ Shelter and will receive God Mode.");
                        People.IsGodModeEnabled = true;
                        Timing.CallDelayed(0.2f, () =>
                        {
                            People.IsGodModeEnabled = false;
                            People.EnableEffect(EffectType.Flashed, 2);
                            People.Position = new Vector3(-53, 988, -50);
                            People.EnableEffect(EffectType.Blinded, 5);
                            Warhead.Detonate();
                            Warhead.Shake();
                        });
                    }
                    else if (!HelikopterSurvivors.Contains(People))
                    {
                        Log.Debug($"Player {People.Nickname} is killed by Omega Warhead.");
                        People.Kill("Omega Warhead.");
                    }
                }

                foreach (Room room in Room.List)
                    room.Color = Color.blue;
            }));

            // Compte à rebours pour l'évasion en hélicoptère
            Coroutines.Add(Timing.CallDelayed(158, () =>
            {
                for (int i = 10; i > 0; i--)
                    Map.Broadcast(1, Plugin.Singleton.Config.HelicopterMessage + i);
                Timing.CallDelayed(12, () =>
                {
                    Vector3 HelicopterZone = new Vector3(178, 993, -59);
                    foreach (Player player in Player.List)
                        // Distance check updated to 50 units
                        if (Vector3.Distance(player.Position, HelicopterZone) <= 50)
                        {
                            Log.Debug($"Player {player.Nickname} is within the helicopter zone. Initiating escape.");
                            player.Broadcast(4, Plugin.Singleton.Config.HelicopterEscape);
                            player.Position = new Vector3(293, 978, -52);
                            player.Scale = new Vector3(0, 0, 0);
                            player.EnableEffect(EffectType.Flashed, 12);
                            HelikopterSurvivors.Add(player);
                            Timing.CallDelayed(0.5f, () =>
                            {
                                player.EnableEffect(EffectType.Ensnared);
                            });
                        }
                        else
                        {
                            Log.Debug($"Player {player.Nickname} is not within the helicopter zone.");
                        }
                });
                RespawnEffectsController.ExecuteAllEffects(RespawnEffectsController.EffectType.Selection, SpawnableTeamType.NineTailedFox);
            }));
        }
    }
}
