﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using UnityEngine;
using UnityEngine.Video;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;
using MS.Internal.Xml.XPath;
using static UnityEngine.GraphicsBuffer;
using Epic.OnlineServices;

namespace TOHE.Roles.AddOns.Common
{
    public static class Sick
    {
        private static readonly int Id = 97979;

        public static Dictionary<byte, Dictionary<byte, float>> TransmitTimer = new();

        public static OptionItem TransmitTime;
        public static OptionItem CauseVision;
        public static OptionItem CanBeOnCrew;
        public static OptionItem CanBeOnImp;
        public static OptionItem CanBeOnNeutral;

        public static void SetupCustomOption()
        {
            SetupAdtRoleOptions(Id, CustomRoles.Sick, canSetNum: true);
            CanBeOnImp = BooleanOptionItem.Create(Id + 11, "ImpCanBeOiiai", true, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sick]);
            CanBeOnCrew = BooleanOptionItem.Create(Id + 12, "CrewCanBeOiiai", true, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sick]);
            CanBeOnNeutral = BooleanOptionItem.Create(Id + 13, "NeutralCanBeOiiai", true, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sick]);
            TransmitTime = FloatOptionItem.Create(Id + 14, "FarseerRevealCooldown", new(0f, 30f, 1f), 3f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sick])
                .SetValueFormat(OptionFormat.Seconds);
            CauseVision = FloatOptionItem.Create(Id + 15, "FarseerRevealTime", new(0f, 5f, 0.05f), 0.65f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sick])
                .SetValueFormat(OptionFormat.Multiplier);
        }
        public static void Init()
        {
            TransmitTimer = new();
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!player.Is(CustomRoles.Sick)) return;

            foreach (var targetPlayer in Main.AllAlivePlayerControls.Where(a => !a.Is(CustomRoles.Sick)))
            {
                float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                float dis = Vector2.Distance(player.transform.position, targetPlayer.transform.position);
                if (dis > range) continue;

                if (!TransmitTimer.ContainsKey(player.PlayerId))
                    TransmitTimer.Add(player.PlayerId, new Dictionary<byte, float>());

                if (TransmitTimer[player.PlayerId].ContainsKey(targetPlayer.PlayerId))
                {
                    float time = TransmitTimer[player.PlayerId][targetPlayer.PlayerId];
                    if (time >= TransmitTime.GetFloat())
                    {
                        TransmitTimer.Remove(player.PlayerId);
                        targetPlayer.RpcSetCustomRole(CustomRoles.Sick);
                        targetPlayer.MarkDirtySettings();
                        Utils.NotifyRoles();
                    }
                    else
                        TransmitTimer[player.PlayerId][targetPlayer.PlayerId] += Time.fixedDeltaTime;
                }
                else
                    TransmitTimer[player.PlayerId].Add(targetPlayer.PlayerId, 0f);
            }
        }

        public static void SetSickVision(PlayerControl player, IGameOptions opt)
        {
            if (player.Is(CustomRoles.Sick))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, CauseVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, CauseVision.GetFloat());
            }
        }
    }
}
