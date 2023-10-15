using System;
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

namespace TOHE.Roles.AddOns.Common
{
    public static class Sick
    {
        private static readonly int Id = 97979;

        public static Dictionary<byte, Dictionary<byte, float>> TransmitTimer = new();

        public static OptionItem TransmitTime;

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
                        targetPlayer.RpcSetCustomRole(CustomRoles.Sick); // sick
                        Utils.NotifyRoles();
                        MarkEveryoneDirtySettings();
                    }
                    else
                        TransmitTimer[player.PlayerId][targetPlayer.PlayerId] += Time.fixedDeltaTime;
                }
                else
                    TransmitTimer[player.PlayerId].Add(targetPlayer.PlayerId, 0f);
            }
        }
    }
}
