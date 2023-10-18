using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Epic.OnlineServices;
using TOHE.Modules;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Nightmare
    {
        private static readonly int Id = 6729803;

        public static List<byte> playerIdList = new();
        public static bool IsEnable = false;

        private static OptionItem ShapeshiftCooldown;
        private static OptionItem ShapeshiftDuration;
        private static OptionItem HideTwistedPlayerNames;

        private static List<PlayerControl> PlayersInNightmare;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Twister);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 10, "TwisterCooldown", new(1f, 180f, 1f), 20f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Twister])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDuration = FloatOptionItem.Create(Id + 11, "ShapeshiftDuration", new(1f, 999f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Twister])
                    .SetValueFormat(OptionFormat.Seconds);
            HideTwistedPlayerNames = BooleanOptionItem.Create(Id + 12, "TwisterHideTwistedPlayerNames", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Twister]);
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
            IsEnable = false;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            IsEnable = true;
        }

        public static Vector2 GetNightmareRoomPS()
        {
            return Main.NormalOptions.MapId switch
            {
                0 => new Vector2(0f, 42f), // The Skeld
                1 => new Vector2(-11.4f, 8.2f), // MIRA HQ
                2 => new Vector2(42.6f, -19.9f), // Polus
                4 => new Vector2(-16.8f, -6.2f), // Airship
                _ => throw new System.NotImplementedException(),
            };
        }

        public static void OnShapeshift(PlayerControl pc, PlayerControl target)
        {
            var targetOriginPs = target.GetTruePosition();
            target.RpcTeleport(GetNightmareRoomPS());
            target.RPCPlayCustomSound("Teleport");

            var pcOriginPs = pc.GetTruePosition();
            pc.RpcTeleport(GetNightmareRoomPS());
            pc.RPCPlayCustomSound("Teleport");


        }

        public static void OnFixedUpdate()
        {
            //if (!GameStates.IsInTask)
            //{
            //    if (eatenList.Any())
            //    {
            //        eatenList.Clear();
            //        SyncEatenList(byte.MaxValue);
            //    }
            //    return;
            //}

            //foreach (var pc in eatenList)
            //{
            //    foreach (var tar in pc.Value)
            //    {
            //        var target = Utils.GetPlayerById(tar);
            //        if (target == null) continue;
            //        var pos = GetBlackRoomPS();
            //        var dis = Vector2.Distance(pos, target.GetTruePosition());
            //        if (dis < 1f) continue;
            //        target.RpcTeleport(new Vector2(pos.x, pos.y));
            //        Utils.NotifyRoles(SpecifySeer: target, ForceLoop: false);
            //    }
            //}
        }
    }
}
