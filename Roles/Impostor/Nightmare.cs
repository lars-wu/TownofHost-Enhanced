using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using Epic.OnlineServices;
using MS.Internal.Xml.XPath;
using TOHE.Modules;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;
using static UnityEngine.UI.Image;

namespace TOHE.Roles.Impostor
{
    public static class Nightmare
    {
        private static readonly int Id = 6729803;

        public static List<byte> playerIdList = new();
        public static bool IsEnable = false;

        private static OptionItem ShapeshiftCooldown;
        private static OptionItem ShapeshiftDuration;
        private static OptionItem NightmareRoomRadius;
        private static OptionItem SendDeadPlayerOutsideNightmare;

        private static List<PlayerControl> PlayersInNightmare;
        private static Dictionary<byte, Vector2> OriginPs;

        private static Vector2 NightmareRoomLocation = new Vector2(0f, 42f);
        private static List<Vector2> NightmareRoomPositions = new List<Vector2>
        {
            new Vector2(-4, NightmareRoomLocation.y),
            new Vector2(4, NightmareRoomLocation.y),
            new Vector2(0, NightmareRoomLocation.y -4),
            new Vector2(0, NightmareRoomLocation.y +4),
        };

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Nightmare);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 10, "TwisterCooldown", new(1f, 180f, 1f), 20f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Nightmare])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDuration = FloatOptionItem.Create(Id + 11, "ShapeshiftDuration", new(1f, 999f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Nightmare])
                    .SetValueFormat(OptionFormat.Seconds);
            NightmareRoomRadius = FloatOptionItem.Create(Id + 12, "PitfallTrapRadius", new(4f, 10f, 0.5f), 6f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Nightmare])
                .SetValueFormat(OptionFormat.Multiplier);
            SendDeadPlayerOutsideNightmare = BooleanOptionItem.Create(Id + 13, "TwisterHideTwistedPlayerNames", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Nightmare]);
        }
        public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = ShapeshiftDuration.GetFloat();
        }

        public static void Init()
        {
            playerIdList = new();
            PlayersInNightmare = new();
            OriginPs = new();
            IsEnable = false;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            IsEnable = true;
        }

        //public static Vector2 GetNightmareRoomPS()
        //{
        //    return Main.NormalOptions.MapId switch
        //    {
        //        0 => new Vector2(0f, 42f), // The Skeld
        //        1 => new Vector2(0f, 42f), // MIRA HQ
        //        2 => new Vector2(0f, 42f), // Polus
        //        4 => new Vector2(0f, 42f), // Airship
        //        _ => throw new System.NotImplementedException(),
        //    };
        //}

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!PlayersInNightmare.Any(a => a.PlayerId == target.PlayerId) || !SendDeadPlayerOutsideNightmare.GetBool()) return true;

            target.RpcTeleport(OriginPs[target.PlayerId]);
            target.SetRealKiller(killer);
            target.RpcMurderPlayerV3(target);
            Main.PlayerStates[target.PlayerId].SetDead();

            killer.SetKillCooldown();
            killer.SyncSettings();

            return false;
        }

        public static void OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
        {
            if (shapeshifting)
            {
                ReportDeadBodyPatch.CanReport[target.PlayerId] = false;

                OriginPs.Add(target.PlayerId, target.GetTruePosition());
                OriginPs.Add(pc.PlayerId, pc.GetTruePosition());

                var rd = IRandom.Instance;

                var targetPosition = NightmareRoomPositions[rd.Next(0, NightmareRoomPositions.Count - 1)];
                target.RpcTeleport(targetPosition);
                target.RPCPlayCustomSound("Teleport");
                target.Notify("Run from the Nightmare!");

                var filteredPositions = NightmareRoomPositions.Where(a => a != targetPosition).ToArray();
                var pcPosition = filteredPositions[rd.Next(0, filteredPositions.Length - 1)];
                pc.RpcTeleport(pcPosition);
                pc.RPCPlayCustomSound("Teleport");

                PlayersInNightmare.Add(pc);
                PlayersInNightmare.Add(target);
            }
            else
            {
                foreach (var player in PlayersInNightmare)
                {
                    if (player.Data.IsDead) continue;

                    player.RpcTeleport(OriginPs[player.PlayerId]);
                    player.RPCPlayCustomSound("Teleport");
                }

                PlayersInNightmare.Clear();
                OriginPs.Clear();

                ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
            }

            target.MarkDirtySettings();
            pc.MarkDirtySettings();
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!player.IsAlive() || !player.Is(CustomRoles.Nightmare) || !GameStates.IsInTask) return;

            foreach (var pc in PlayersInNightmare)
            {
                if (pc == null || pc.Data.IsDead) continue;
                var pos = NightmareRoomLocation;
                var dis = Vector2.Distance(pos, pc.GetTruePosition());
                if (dis < NightmareRoomRadius.GetFloat()) continue;
                pc.RpcTeleport(pos);
            }
        }

        public static void SetNightmareVision(IGameOptions opt, PlayerControl target)
        {
            if (PlayersInNightmare.Any(a => a.PlayerId == target.PlayerId))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, 0.25f);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0.25f);
            }
        }
    }
}
