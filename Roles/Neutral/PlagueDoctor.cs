﻿using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TOHE.Roles.Neutral;

// https://github.com/tukasa0001/TownOfHost/blob/main/Roles/Neutral/PlagueDoctor.cs
public static class PlagueDoctor
{
    private static readonly int Id = 27600;
    private static List<byte> playerIdList = [];
    private static Dictionary<byte, float> InfectInfos;
    public static bool IsEnable;

    private static OptionItem OptionInfectLimit;
    private static OptionItem OptionInfectWhenKilled;
    private static OptionItem OptionInfectTime;
    private static OptionItem OptionInfectDistance;
    private static OptionItem OptionInfectInactiveTime;
    private static OptionItem OptionInfectCanInfectSelf;
    private static OptionItem OptionInfectCanInfectVent;

    private static int InfectCount;
    private static bool InfectActive;
    private static bool LateCheckWin;
    private static int InfectLimit;
    private static bool InfectWhenKilled;
    private static float InfectTime;
    private static float InfectDistance;
    private static float InfectInactiveTime;
    private static bool CanInfectSelf;
    private static bool CanInfectVent;


    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.PlagueDoctor, 1);
        OptionInfectLimit = IntegerOptionItem.Create(Id + 10, "PlagueDoctorInfectLimit", new(1, 3, 1), 1, TabGroup.NeutralRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.PlagueDoctor])
            .SetValueFormat(OptionFormat.Times);
        OptionInfectWhenKilled = BooleanOptionItem.Create(Id + 11, "PlagueDoctorInfectWhenKilled", false, TabGroup.NeutralRoles, false)
           .SetParent(Options.CustomRoleSpawnChances[CustomRoles.PlagueDoctor]);
        OptionInfectTime = FloatOptionItem.Create(Id + 12, "PlagueDoctorInfectTime", new(3f, 20f, 1f), 8f, TabGroup.NeutralRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.PlagueDoctor])
            .SetValueFormat(OptionFormat.Seconds);
        OptionInfectDistance = FloatOptionItem.Create(Id + 13, "PlagueDoctorInfectDistance", new(0.5f, 2f, 0.25f), 1.5f, TabGroup.NeutralRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.PlagueDoctor])
            .SetValueFormat(OptionFormat.Multiplier);
        OptionInfectInactiveTime = FloatOptionItem.Create(Id + 14, "PlagueDoctorInfectInactiveTime", new(0.5f, 10f, 0.5f), 3.5f, TabGroup.NeutralRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.PlagueDoctor])
            .SetValueFormat(OptionFormat.Seconds);
        OptionInfectCanInfectSelf = BooleanOptionItem.Create(Id + 15, "PlagueDoctorCanInfectSelf", false, TabGroup.NeutralRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.PlagueDoctor]);
        OptionInfectCanInfectVent = BooleanOptionItem.Create(Id + 16, "PlagueDoctorCanInfectVent", false, TabGroup.NeutralRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.PlagueDoctor]);
    }

    public static void Init()
    {
        IsEnable = false;
        playerIdList = [];
        InfectInfos = [];
    }
    public static void Add(byte playerId)
    {
        IsEnable = true;
        InfectLimit = OptionInfectLimit.GetInt();
        InfectWhenKilled = OptionInfectWhenKilled.GetBool();
        InfectTime = OptionInfectTime.GetFloat();
        InfectDistance = OptionInfectDistance.GetFloat();
        InfectInactiveTime = OptionInfectInactiveTime.GetFloat();
        CanInfectSelf = OptionInfectCanInfectSelf.GetBool();
        CanInfectVent = OptionInfectCanInfectVent.GetBool();

        InfectCount = InfectLimit;

        InfectActive = true;

        // Fixed airship respawn selection delay
        if (Main.NormalOptions.MapId == 4)
            InfectInactiveTime += 5f;

        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;
    public static bool CanUseKillButton() => InfectCount != 0;
    public static string GetProgressText()
    {
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.PlagueDoctor).ShadeColor(0.25f), $"({InfectCount})");
    }
    public static void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }
    public static bool CanInfect(PlayerControl player)
    {
        // Not a plague doctor, or capable of self-infection and infected person created
        return player.PlayerId != playerIdList[0] || (CanInfectSelf && InfectCount == 0);
    }
    public static void SendRPC(byte targetId, float rate, bool firstInfect)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncPlagueDoctor, SendOption.Reliable, -1);
        writer.Write(firstInfect);
        writer.Write(targetId);
        writer.Write(rate);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        var firstInfect = reader.ReadBoolean();
        var targetId = reader.ReadByte();
        var rate = reader.ReadSingle();

        if (firstInfect)
        {
            InfectCount = 0;
        }
        InfectInfos[targetId] = rate;
    }
    public static bool OnPDinfect(PlayerControl killer, PlayerControl target)
    {
        if (InfectCount > 0)
        {
            InfectCount = 0;
            killer.RpcGuardAndKill(target);
            DirectInfect(target, killer);
        }
        return false;
    }
    public static void OnPDdeath(PlayerControl killer, PlayerControl target)
    {
        if (InfectWhenKilled && InfectCount > 0)
        {
            InfectCount = 0;
            DirectInfect(killer, target);
        }
    }
    public static void OnAnyMurder()
    {
        // You may win if an uninfected person dies.
        LateCheckWin = true;
    }
    public static void OnReportDeadBody()
    {
        InfectActive = false;
    }
    public static void OnCheckPlayerPosition(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (LateCheckWin)
        {
            // After hanging/killing, check the victory conditions just to be sure.
            LateCheckWin = false;
            CheckWin();
        }
        if (!player.IsAlive() || player == null || !InfectActive) return;

        if (InfectInfos.TryGetValue(player.PlayerId, out var rate) && rate >= 100)
        {
            // In case of an infected person
            var changed = false;
            var inVent = player.inVent;
            List<PlayerControl> updates = [];
            foreach (PlayerControl target in Main.AllAlivePlayerControls)
            {
                // Plague doctors are excluded if they cannot infect themselves.
                if (!CanInfect(target)) continue;
                // Excluded if inside or outside the vent
                if (!CanInfectVent && target.inVent != inVent) continue;

                InfectInfos.TryGetValue(target.PlayerId, out var oldRate);
                // Exclude infected people
                if (oldRate >= 100) continue;

                // Exclude players outside the range
                var distance = Vector3.Distance(player.transform.position, target.transform.position);
                if (distance > InfectDistance) continue;

                var newRate = oldRate + Time.fixedDeltaTime / InfectTime * 100;
                newRate = Math.Clamp(newRate, 0, 100);
                InfectInfos[target.PlayerId] = newRate;
                if ((oldRate < 50 && newRate >= 50) || newRate >= 100)
                {
                    changed = true;
                    updates.Add(target);
                    Logger.Info($"InfectRate [{target.GetNameWithRole()}]: {newRate}%", "PlagueDoctor");
                    SendRPC(target.PlayerId, newRate, false);
                }
            }
            if (changed)
            {
                //If someone is infected
                CheckWin();
                foreach (PlayerControl x in updates.ToArray())
                {
                    Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(playerIdList[0]), SpecifyTarget: x);
                }
            }
        }
    }
    public static void AfterMeetingTasks()
    {
        // You may win if a non-infected person is hanged.
        LateCheckWin = true;

        _ = new LateTask(() =>
        {
            Logger.Info("Infect Active", "PlagueDoctor");
            InfectActive = true;
        },
        InfectInactiveTime, "ResetInfectInactiveTime");
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!CanInfect(seen)) return string.Empty;
        if (!seer.Is(CustomRoles.PlagueDoctor) && seer.IsAlive()) return string.Empty;
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.PlagueDoctor), GetInfectRateCharactor(seen));
    }
    public static string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!seen.Is(CustomRoles.PlagueDoctor)) return string.Empty;
        if (!seer.Is(CustomRoles.PlagueDoctor) && seer.IsAlive()) return string.Empty;

        var str = new StringBuilder(40);
        foreach (PlayerControl player in Main.AllAlivePlayerControls)
        {
            if (!player.Is(CustomRoles.PlagueDoctor))
                str.Append(GetInfectRateCharactor(player));
        }
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.PlagueDoctor), str.ToString());
    }
    public static bool IsInfected(byte playerId)
    {
        InfectInfos.TryGetValue(playerId, out var rate);
        return rate >= 100;
    }
    public static string GetInfectRateCharactor(PlayerControl player)
    {
        if (!IsEnable) return string.Empty;
        if (!CanInfect(player) || !player.IsAlive()) return string.Empty;
        InfectInfos.TryGetValue(player.PlayerId, out var rate);
        return rate switch
        {
            < 50 => "\u2581",
            >= 50 and < 100 => "\u2584",
            >= 100 => "\u2588",
            _ => string.Empty,
        };
    }
    public static void DirectInfect(PlayerControl target, PlayerControl plague)
    {
        if (playerIdList.Count == 0 || target == null) return;
        Logger.Info($"InfectRate [{target.GetNameWithRole()}]: 100%", "PlagueDoctor");
        InfectInfos[target.PlayerId] = 100;
        SendRPC(target.PlayerId, 100, true);
        Utils.NotifyRoles(SpecifySeer: plague, SpecifyTarget: target);
        CheckWin();
    }
    public static void CheckWin()
    {
        if (!IsEnable) return;
        if (!AmongUsClient.Instance.AmHost) return;
        // Invalid if someone's victory is being processed
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

        if (Main.AllAlivePlayerControls.All(p => p.Is(CustomRoles.PlagueDoctor) || IsInfected(p.PlayerId)))
        {
            InfectActive = false;

            foreach (PlayerControl player in Main.AllAlivePlayerControls)
            {
                if (player.Is(CustomRoles.PlagueDoctor)) continue;
                Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Infected;
                player.RpcMurderPlayerV3(player);
            }
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.PlagueDoctor);
            foreach (var plagueDoctor in Main.AllPlayerControls.Where(p => p.Is(CustomRoles.PlagueDoctor)).ToArray())
            {
                CustomWinnerHolder.WinnerIds.Add(plagueDoctor.PlayerId);
            }
        }
    }
}
