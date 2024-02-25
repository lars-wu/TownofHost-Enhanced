﻿namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System.Collections.Generic;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Translator;

    public static class Bloodhound
    {
        private static readonly int Id = 7700;
        private static List<byte> playerIdList = [];
        public static bool IsEnable = false;

        public static HashSet<byte> UnreportablePlayers = [];
        public static Dictionary<byte, List<byte>> BloodhoundTargets = [];
        public static Dictionary<byte, float> UseLimit = [];

        public static OptionItem ArrowsPointingToDeadBody;
        public static OptionItem UseLimitOpt;
        public static OptionItem LeaveDeadBodyUnreportable;
        public static OptionItem BloodhoundAbilityUseGainWithEachTaskCompleted;
        public static OptionItem InformKillerBeingTracked;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bloodhound);
            ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "BloodhoundArrowsPointingToDeadBody", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            LeaveDeadBodyUnreportable = BooleanOptionItem.Create(Id + 11, "BloodhoundLeaveDeadBodyUnreportable", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);
            BloodhoundAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);
            InformKillerBeingTracked = BooleanOptionItem.Create(Id + 14, "BloodhoundInformKillerBeingTracked", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodhound]);
        }
        public static void Init()
        {
            IsEnable = false;
            playerIdList = [];
            UseLimit = [];
            UnreportablePlayers = [];
            BloodhoundTargets = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
            BloodhoundTargets.Add(playerId, []);
            IsEnable = true;
        }
        public static void Remove(byte playerId)
        {
            playerIdList.Remove(playerId);
            UseLimit.Remove(playerId);
            BloodhoundTargets.Remove(playerId);
        }

        private static void SendRPC(byte playerId, bool add, Vector3 loc = new())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBloodhoundArrow, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(add);
            if (add)
            {
                writer.Write(loc.x);
                writer.Write(loc.y);
                writer.Write(loc.z);
            }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void SendRPCLimit(byte playerId, int operate, byte targetId = 0xff)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRoleSkill, SendOption.Reliable, -1);
            writer.WritePacked((int)CustomRoles.Bloodhound);
            writer.Write(playerId);
            writer.Write(operate);
            writer.Write(UseLimit[playerId]);
            if (operate != 2) writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCLimit(MessageReader reader)
        {
            byte pid = reader.ReadByte();
            int opt = reader.ReadInt32();
            float limit = reader.ReadSingle();
            UseLimit[pid] = limit;
            if (opt != 2)
            {
                byte tid = reader.ReadByte();
                if (!BloodhoundTargets.ContainsKey(pid)) BloodhoundTargets[pid] = [];
                BloodhoundTargets[pid].Add(tid);
                if (opt == 1) UnreportablePlayers.Add(tid);
            }
        }
        public static void SendRPCKiller(byte playerId, byte killerId, bool add)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBloodhoundkKillerArrow, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(killerId);
            writer.Write(add);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

        }
        public static void ReceiveRPCKiller(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            byte killerId = reader.ReadByte();
            bool add = reader.ReadBoolean();
            if (add)
            {
                BloodhoundTargets[playerId].Add(killerId);
                TargetArrow.Add(playerId, killerId);
            }
            else
            {
                BloodhoundTargets[playerId].Remove(killerId);
                TargetArrow.Remove(playerId, killerId);
            }
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            bool add = reader.ReadBoolean();
            if (add)
                LocateArrow.Add(playerId, new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            else
            { 
                LocateArrow.RemoveAllTarget(playerId);
                if (BloodhoundTargets.ContainsKey(playerId)) BloodhoundTargets[playerId].Clear();
            }
        }

        public static void Clear()
        {
            foreach (var apc in playerIdList.ToArray())
            {
                LocateArrow.RemoveAllTarget(apc);
                SendRPC(apc, false);
            }

            foreach (var bloodhound in BloodhoundTargets)
            {
                foreach (var target in bloodhound.Value.ToArray())
                {
                    TargetArrow.Remove(bloodhound.Key, target);
                    SendRPCKiller(bloodhound.Key, target, add: false);
                }

                BloodhoundTargets[bloodhound.Key].Clear();
            }
        }

        public static void OnPlayerDead(PlayerControl target)
        {
            if (!ArrowsPointingToDeadBody.GetBool()) return;

            foreach (var pc in playerIdList.ToArray())
            {
                var player = Utils.GetPlayerById(pc);
                if (player == null || !player.IsAlive()) continue;
                LocateArrow.Add(pc, target.transform.position);
                SendRPC(pc, true, target.transform.position);
            }
        }

        public static void OnReportDeadBody(PlayerControl pc, GameData.PlayerInfo target, PlayerControl killer)
        {
            if (BloodhoundTargets[pc.PlayerId].Contains(killer.PlayerId))
            {
                return;
            }

            LocateArrow.Remove(pc.PlayerId, target.Object.transform.position);
            SendRPC(pc.PlayerId, false);

            if (UseLimit[pc.PlayerId] >= 1)
            {
                BloodhoundTargets[pc.PlayerId].Add(killer.PlayerId);
                TargetArrow.Add(pc.PlayerId, killer.PlayerId);
                SendRPCKiller(pc.PlayerId, killer.PlayerId, add: true);

                pc.Notify(GetString("BloodhoundTrackRecorded"));
                UseLimit[pc.PlayerId] -= 1;
                int operate = 0;
                if (LeaveDeadBodyUnreportable.GetBool())
                {
                    UnreportablePlayers.Add(target.PlayerId);
                    operate = 1;
                }
                SendRPCLimit(pc.PlayerId, operate, targetId: target.PlayerId);

                if (InformKillerBeingTracked.GetBool())
                {
                    killer.Notify(GetString("BloodhoundIsTrackingYou"));
                }
            }
            else
            {
                pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }

        public static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (!seer.Is(CustomRoles.Bloodhound)) return "";
            if (target != null && seer.PlayerId != target.PlayerId) return "";
            if (GameStates.IsMeeting) return "";
            if (BloodhoundTargets.ContainsKey(seer.PlayerId) && BloodhoundTargets[seer.PlayerId].Count > 0)
            {
                var arrows = "";
                foreach (var targetId in BloodhoundTargets[seer.PlayerId])
                {
                    var arrow = TargetArrow.GetArrows(seer, targetId);
                    arrows += Utils.ColorString(seer.GetRoleColor(), arrow);
                }
                return arrows;
            }
            return Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }
    }
}
