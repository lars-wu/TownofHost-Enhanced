﻿using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Crewmate;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

internal static class Eraser
{
    private static readonly int Id = 24200;
    private static List<byte> playerIdList = [];
    public static bool IsEnable = false;

    private static OptionItem EraseLimitOpt;
    public static OptionItem HideVote;

    private static List<byte> didVote = [];
    public static Dictionary<byte, int> EraseLimit = [];
    private static List<byte> PlayerToErase = [];
    public static Dictionary<byte, int> TempEraseLimit = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Eraser);
        EraseLimitOpt = IntegerOptionItem.Create(Id + 10, "EraseLimit", new(1, 15, 1), 2, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser])
            .SetValueFormat(OptionFormat.Times);
        HideVote = BooleanOptionItem.Create(Id + 11, "EraserHideVote", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);
    }
    public static void Init()
    {
        playerIdList = [];
        EraseLimit = [];
        PlayerToErase = [];
        didVote = [];
        TempEraseLimit = [];
        IsEnable = false;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        EraseLimit.Add(playerId, EraseLimitOpt.GetInt());
        IsEnable = true;

        Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole()} : 剩余{EraseLimit[playerId]}次", "Eraser");
    }
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRoleSkill, SendOption.Reliable, -1);
        writer.WritePacked((int)CustomRoles.Eraser);
        writer.Write(playerId);
        writer.Write(EraseLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        int limit = reader.ReadInt32();
        if (!EraseLimit.ContainsKey(playerId))
        {
            EraseLimit.Add(playerId , limit);
        }
        else
            EraseLimit[playerId] = limit;
    }
    public static string GetProgressText(byte playerId) => Utils.ColorString(EraseLimit[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.Eraser) : Color.gray, EraseLimit.TryGetValue(playerId, out var x) ? $"({x})" : "Invalid");

    public static void OnVote(PlayerControl player, PlayerControl target)
    {
        if (!IsEnable) return;
        if (player == null || target == null) return;
        if (target.Is(CustomRoles.Eraser)) return;
        if (EraseLimit[player.PlayerId] <= 0) return;

        if (didVote.Contains(player.PlayerId)) return;
        didVote.Add(player.PlayerId);

        Logger.Info($"{player.GetCustomRole()} votes for {target.GetCustomRole()}", "Vote Eraser");

        if (target.PlayerId == player.PlayerId)
        {
            Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
            return;
        }

        var targetRole = target.GetCustomRole();
        if (targetRole.IsTasklessCrewmate() || targetRole.IsNeutral() || Main.TasklessCrewmate.Contains(target.PlayerId) || CopyCat.playerIdList.Contains(target.PlayerId) || target.Is(CustomRoles.Stubborn))
        {
            Utils.SendMessage(string.Format(GetString("EraserEraseBaseImpostorOrNeutralRoleNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
            return;
        }

        EraseLimit[player.PlayerId]--;
        SendRPC(player.PlayerId);

        if (!PlayerToErase.Contains(target.PlayerId))
            PlayerToErase.Add(target.PlayerId);

        Utils.SendMessage(string.Format(GetString("EraserEraseNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));

        Utils.NotifyRoles(SpecifySeer: player);
    }
    public static void OnReportDeadBody()
    {
        foreach (var eraserId in playerIdList.ToArray())
        {
            TempEraseLimit[eraserId] = EraseLimit[eraserId];
        }

        PlayerToErase = [];
        didVote = [];
    }
    public static void AfterMeetingTasks(bool notifyPlayer = false)
    {
        if (notifyPlayer)
        {
            foreach (var pc in PlayerToErase.ToArray())
            {
                var player = Utils.GetPlayerById(pc);
                if (player == null) continue;

                player.Notify(GetString("LostRoleByEraser"));
            }
        }
        else
        {
            foreach (var pc in PlayerToErase.ToArray())
            {
                var player = Utils.GetPlayerById(pc);
                if (player == null) continue;
                if (!Main.ErasedRoleStorage.ContainsKey(player.PlayerId))
                {
                    Main.ErasedRoleStorage.Add(player.PlayerId, player.GetCustomRole());
                    Logger.Info($"Added {player.GetNameWithRole()} to ErasedRoleStorage", "Eraser");
                }
                else
                {
                    Logger.Info($"Canceled {player.GetNameWithRole()} Eraser bcz already erased.", "Eraser");
                    return;
                }
                player.RpcSetCustomRole(CustomRolesHelper.GetErasedRole(player.GetCustomRole().GetRoleTypes(), player.GetCustomRole()));
                player.ResetKillCooldown();
                player.SetKillCooldown();
                Logger.Info($"{player.GetNameWithRole()} Erase by Eraser", "Eraser");
            }
            Utils.MarkEveryoneDirtySettings();
        }
    }
}
