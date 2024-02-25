namespace TOHE.Roles.Crewmate
{
    using AmongUs.GameOptions;
    using HarmonyLib;
    using Hazel;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using TOHE.Modules;
    using TOHE.Roles.Neutral;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Translator;

    public static class Alchemist
    {
        private static readonly int Id = 6400;
        private static List<byte> playerIdList = [];

        public static Dictionary<byte, byte> BloodlustList = [];
        private static Dictionary<byte, int> ventedId = [];
        private static Dictionary<byte, long> InvisTime = [];

        public static byte PotionID = 10;
        public static string PlayerName = string.Empty;
        public static bool VisionPotionActive = false;
        public static bool FixNextSabo = false;
        public static bool IsProtected = false;

        public static OptionItem VentCooldown;
        public static OptionItem ShieldDuration;
        public static OptionItem Vision;
        public static OptionItem VisionOnLightsOut;
        public static OptionItem VisionDuration;
        public static OptionItem Speed;
        public static OptionItem InvisDuration;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Alchemist, 1);
            VentCooldown = FloatOptionItem.Create(Id + 11, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            ShieldDuration = FloatOptionItem.Create(Id + 12, "AlchemistShieldDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            Vision = FloatOptionItem.Create(Id + 16, "AlchemistVision", new(0f, 1f, 0.05f), 0.85f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Multiplier);
            VisionOnLightsOut = FloatOptionItem.Create(Id + 17, "AlchemistVisionOnLightsOut", new(0f, 1f, 0.05f), 0.4f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Multiplier);
            VisionDuration = FloatOptionItem.Create(Id + 18, "AlchemistVisionDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            Speed = FloatOptionItem.Create(Id + 19, "AlchemistSpeed", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                 .SetValueFormat(OptionFormat.Multiplier);
            InvisDuration = FloatOptionItem.Create(Id + 20, "AlchemistInvisDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            OverrideTasksData.Create(Id + 21, TabGroup.CrewmateRoles, CustomRoles.Alchemist);
        }
        public static void Init()
        {
            playerIdList = [];
            BloodlustList = [];
            PotionID = 10;
            PlayerName = string.Empty;
            ventedId = [];
            InvisTime = [];
            FixNextSabo = false;
            VisionPotionActive = false;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            PlayerName = Utils.GetPlayerById(playerId).GetRealName();
        }
        public static bool IsEnable => playerIdList.Count > 0;

        public static void OnTaskComplete(PlayerControl pc)
        {
            PotionID = (byte)HashRandom.Next(1, 8);

            switch (PotionID)
            {
                case 1: // Shield
                    pc.Notify(GetString("AlchemistGotShieldPotion"), 15f);
                    break;
                case 2: // Suicide
                    pc.Notify(GetString("AlchemistGotSuicidePotion"), 15f);
                    break;
                case 3: // TP to random player
                    pc.Notify(GetString("AlchemistGotTPPotion"), 15f);
                    break;
                case 4: // Speed
                    pc.Notify(GetString("AlchemistGotSpeedPotion"), 15f);
                    break;
                case 5: // Quick fix next sabo
                    FixNextSabo = true;
                    PotionID = 10;
                    pc.Notify(GetString("AlchemistGotQFPotion"), 15f);
                    break;
                case 6: // Bloodlust
                    pc.Notify(GetString("AlchemistGotBloodlustPotion"), 15f);
                    break;
                case 7: // Increased vision
                    pc.Notify(GetString("AlchemistGotSightPotion"), 15f);
                    break;
                case 8:
                    pc.Notify(GetString("AlchemistGotInvisibility"), 15f);
                    break;
                default: // just in case
                    break;
            }
        }

        public static void OnEnterVent(PlayerControl player, int ventId)
        {
            if (!player.Is(CustomRoles.Alchemist)) return;

            switch (PotionID)
            {
                case 1: // Shield
                    IsProtected = true;
                    player.Notify(GetString("AlchemistShielded"), ShieldDuration.GetInt());

                    _ = new LateTask(() =>
                    {
                        IsProtected = false;
                        player.Notify(GetString("AlchemistShieldOut"));

                    }, ShieldDuration.GetInt(), "Alchemist Shield Is Out");
                    break;
                case 2: // Suicide
                    player.MyPhysics.RpcBootFromVent(ventId);
                    _ = new LateTask(() =>
                    {
                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Poison;
                        player.SetRealKiller(player);
                        player.RpcMurderPlayerV3(player);

                    }, 1f, "Alchemist Is Poisoned");
                    break;
                case 3: // TP to random player
                    _ = new LateTask(() =>
                    {
                        var rd = IRandom.Instance;
                        List<PlayerControl> AllAlivePlayer = [.. Main.AllAlivePlayerControls.Where(x => x.CanBeTeleported()).ToArray()];
                        var tar1 = AllAlivePlayer[player.PlayerId];
                        AllAlivePlayer.Remove(tar1);
                        var tar2 = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];
                        tar1.RpcTeleport(tar2.GetCustomPosition());
                        tar1.RPCPlayCustomSound("Teleport");
                    }, 2f, "Alchemist teleported to random player");
                    break;
                case 4: // Increased speed.;
                    int SpeedDuration = 10;
                    player.Notify(GetString("AlchemistHasSpeed"));
                    var tempSpeed = Main.AllPlayerSpeed[player.PlayerId];
                    Main.AllPlayerSpeed[player.PlayerId] = Speed.GetFloat();
                    player.MarkDirtySettings();
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerSpeed[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId] - Speed.GetFloat() + tempSpeed;
                        player.Notify(GetString("AlchemistSpeedOut"));
                        player.MarkDirtySettings();
                    }, SpeedDuration, "Alchemist: Set Speed to default");
                    break;
                case 5: // Quick fix next sabo
                    // Done when making the potion
                    break;
                case 6: // Bloodlust
                    player.Notify(GetString("AlchemistPotionBloodlust"));
                    if (!BloodlustList.ContainsKey(player.PlayerId))
                    {
                        BloodlustList[player.PlayerId] = player.PlayerId;
                    }
                    break;
                case 7: // Increased vision
                    VisionPotionActive = true;
                    player.MarkDirtySettings();
                    player.Notify(GetString("AlchemistHasVision"), VisionDuration.GetFloat());
                    _ = new LateTask(() =>
                    { 
                        VisionPotionActive = false;
                        player.MarkDirtySettings();
                        player.Notify(GetString("AlchemistVisionOut"));

                    }, VisionDuration.GetFloat(), "Alchemist Vision Is Out");
                    break;
                case 8:
                    // Invisibility
                    // Handled in CoEnterVent
                    break;
                case 10:
                    player.Notify("NoPotion");
                    break;
                default: // just in case
                    player.Notify("NoPotion");
                    break;
            }

            PotionID = 10;

            NameNotifyManager.Notice.Remove(player.PlayerId);
        }
        public static bool IsInvis(byte id) => InvisTime.ContainsKey(id);
        private static void SendRPC(PlayerControl pc)
        {
            if (pc.AmOwner) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAlchemistTimer, SendOption.Reliable, pc.GetClientId());
            writer.Write((InvisTime.TryGetValue(pc.PlayerId, out var x) ? x : -1).ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            InvisTime = [];
            long invis = long.Parse(reader.ReadString());
            if (invis > 0) InvisTime.Add(PlayerControl.LocalPlayer.PlayerId, invis);
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!BloodlustList.ContainsKey(player.PlayerId)) return;

            if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
            {
                BloodlustList.Remove(player.PlayerId);
            }
            else
            {
                Vector2 bloodlustPos = player.transform.position;
                Dictionary<byte, float> targetDistance = [];
                float dis;
                foreach (var target in Main.AllAlivePlayerControls)
                {
                    if (target.PlayerId != player.PlayerId && !target.Is(CustomRoles.Pestilence))
                    {
                        dis = Vector2.Distance(bloodlustPos, target.transform.position);
                        targetDistance.Add(target.PlayerId, dis);
                    }
                }
                if (targetDistance.Count > 0)
                {
                    var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();
                    PlayerControl target = Utils.GetPlayerById(min.Key);
                    var KillRange = NormalGameOptionsV07.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                    if (min.Value <= KillRange && player.CanMove && target.CanMove)
                    {
                        if (player.RpcCheckAndMurder(target, true))
                        {
                            var bloodlustId = BloodlustList[player.PlayerId];
                            RPC.PlaySoundRPC(bloodlustId, Sounds.KillSound);
                            target.SetRealKiller(Utils.GetPlayerById(bloodlustId));
                            player.RpcMurderPlayerV3(target);
                            player.MarkDirtySettings();
                            target.MarkDirtySettings();
                            BloodlustList.Remove(player.PlayerId);
                            Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(bloodlustId), SpecifyTarget: player, ForceLoop: true);
                        }
                    }
                }
            }
        }
        private static long lastFixedTime;
        private static bool InviseIsActive = false;
        public static void OnFixedUpdateINV(PlayerControl player)
        {
            if (GameStates.IsMeeting) return;

            var now = Utils.GetTimeStamp();

            if (lastFixedTime != now)
            {
                lastFixedTime = now;
                Dictionary<byte, long> newList = [];
                List<byte> refreshList = [];
                foreach (var it in InvisTime)
                {
                    var pc = Utils.GetPlayerById(it.Key);
                    if (pc == null) continue;
                    var remainTime = it.Value + (long)InvisDuration.GetFloat() - now;
                    if (remainTime < 0)
                    {
                        pc?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(pc.PlayerId, out var id) ? id : Main.LastEnteredVent[pc.PlayerId].Id);
                        ventedId.Remove(pc.PlayerId);
                        pc.Notify(GetString("ChameleonInvisStateOut"));
                        pc.RpcResetAbilityCooldown();
                        SendRPC(pc);
                        InviseIsActive = false;
                        continue;
                    }
                    else if (remainTime <= 10)
                    {
                        if (!pc.IsModClient()) pc.Notify(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
                    }
                    newList.Add(it.Key, it.Value);
                }
                InvisTime.Where(x => !newList.ContainsKey(x.Key)).Do(x => refreshList.Add(x.Key));
                InvisTime = newList;
                refreshList.Do(x => SendRPC(Utils.GetPlayerById(x)));
            }
        }
        public static void OnReportDeadBody()
        {
            lastFixedTime = new();
            BloodlustList = [];
            InvisTime = [];

            if (InviseIsActive)
            {
                foreach (var alchemistId in playerIdList.ToArray())
                {
                    if (!ventedId.ContainsKey(alchemistId)) continue;
                    var alchemist = Utils.GetPlayerById(alchemistId);
                    if (alchemist == null) return;

                    alchemist?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(alchemistId, out var id) ? id : Main.LastEnteredVent[alchemistId].Id);
                    SendRPC(alchemist);
                }
                InviseIsActive = false;
            }

            ventedId = [];
        }

        public static void OnCoEnterVent(PlayerPhysics __instance, int ventId)
        {
            PotionID = 10;
            var pc = __instance.myPlayer;
            NameNotifyManager.Notice.Remove(pc.PlayerId);
            if (!AmongUsClient.Instance.AmHost) return;
            _ = new LateTask(() =>
            {
                ventedId.Remove(pc.PlayerId);
                ventedId.Add(pc.PlayerId, ventId);

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, pc.GetClientId());
                writer.WritePacked(ventId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                InvisTime.Add(pc.PlayerId, Utils.GetTimeStamp());
                SendRPC(pc);
                InviseIsActive = true;
                NameNotifyManager.Notify(pc, GetString("ChameleonInvisState"), InvisDuration.GetFloat());
            }, 0.5f, "Alchemist Invis");
        }

        public static string GetHudText(PlayerControl pc)
        {
            if (pc == null || GameStates.IsMeeting || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
            var str = new StringBuilder();
            if (IsInvis(pc.PlayerId))
            {
                var remainTime = InvisTime[pc.PlayerId] + (long)InvisDuration.GetFloat() - Utils.GetTimeStamp();
                str.Append(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
            }
            else 
            {
                switch (PotionID)
                {
                    case 1: // Shield
                        str.Append(GetString("PotionStore") + GetString("StoreShield"));
                        break;
                    case 2: // Suicide
                        str.Append(GetString("PotionStore") + GetString("StoreSuicide"));
                        break;
                    case 3: // TP to random player
                        str.Append(GetString("PotionStore") + GetString("StoreTP"));
                        break;
                    case 4: // Increased Speed
                        str.Append(GetString("PotionStore") + GetString("StoreSP"));
                        break;
                    case 5: // Quick fix next sabo
                        str.Append(GetString("PotionStore") + GetString("StoreQF"));
                        break;
                    case 6: // Bloodlust
                        str.Append(GetString("PotionStore") + GetString("StoreBL"));
                        break;
                    case 7: // Increased vision
                        str.Append(GetString("PotionStore") + GetString("StoreNS"));
                        break;
                    case 8: // Invisibility
                        str.Append(GetString("PotionStore") + GetString("StoreINV"));
                        break;
                    case 10:
                        str.Append(GetString("PotionStore") + GetString("StoreNull"));
                        break;
                    default: // just in case
                        break;
                }
                if (FixNextSabo) str.Append(GetString("WaitQFPotion"));
            }
            return str.ToString();
        }
        public static string GetProgressText(int playerId)
        {
            if (Utils.GetPlayerById(playerId) == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
            var str = new StringBuilder();
            switch (PotionID)
            {
                case 1: // Shield
                    str.Append("<color=#00ff97>✚</color>");
                    break;
                case 2: // Suicide
                    str.Append("<color=#478800>⁂</color>");
                    break;
                case 3: // TP to random player
                    str.Append("<color=#42d1ff>§</color>");
                    break;
                case 4: // Increased Speed
                    str.Append("<color=#77e0cb>»</color>");
                    break;
                case 5: // Quick fix next sabo
                    str.Append("<color=#3333ff>★</color>");
                    break;
                case 6: // Bloodlust
                    str.Append("<color=#691a2e>乂</color>");
                    break;
                case 7: // Increased vision
                    str.Append("<color=#663399>◉</color>");
                    break;
                case 8: //Invisibility
                    str.Append("<color=#b3b3b3>◌</color>");
                    break;
                default:
                    break;
            }
            if (FixNextSabo) str.Append("<color=#3333ff>★</color>");
            return str.ToString();
        }
        public static void UpdateSystem(SystemTypes systemType, byte amount)
        {
            FixNextSabo = false;
            switch (systemType)
            {
                case SystemTypes.Reactor:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 16);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 17);
                    }
                    break;
                case SystemTypes.Laboratory:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 67);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 66);
                    }
                    break;
                case SystemTypes.LifeSupp:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 67);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 66);
                    }
                    break;
                case SystemTypes.Comms:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 17);
                    }
                    break;
            }
        }
    }
}
