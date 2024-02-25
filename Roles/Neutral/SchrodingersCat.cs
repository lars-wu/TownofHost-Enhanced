﻿using Hazel;
using System.Collections.Generic;

namespace TOHE.Roles.Neutral;

public static class SchrodingersCat
{
    private static readonly int Id = 6900; // used luckey's id (since luckey was removed)
    public static bool IsEnable = false;
    public static Dictionary<byte, byte> teammate = [];
    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.SchrodingersCat);
    }

    public static void Init()
    {
        teammate = [];
        IsEnable = false;
    }

    public static void Add(byte playerId)
    {
        teammate[playerId] = byte.MaxValue;
        IsEnable = true;
    }

    private static void SendRPC(byte catID)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncSchrodingerData, SendOption.Reliable, -1);
        writer.Write(catID);
        writer.Write(teammate[catID]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte catID = reader.ReadByte();
        byte teammateID = reader.ReadByte();
        teammate[catID] = teammateID;
    }
    public static string GetProgressText(byte catID) => Utils.ColorString(Utils.GetRoleColor(CustomRoles.SchrodingersCat).ShadeColor(0.25f), $"({(teammate.TryGetValue(catID, out var value) ? (value != byte.MaxValue ? "0" : "1") : "0")})");

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable) return true;
        if (killer == null || target == null) return true;
        if (teammate[target.PlayerId] != byte.MaxValue) return true;

        teammate[target.PlayerId] = killer.PlayerId;
        SendRPC(target.PlayerId);

        killer.RpcGuardAndKill(target);
        target.RpcGuardAndKill();

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

        killer.SetKillCooldown();

        return false;
    }

    public static void SchrodingerWinCondition(PlayerControl pc)
    {
        if (!IsEnable) return;
        if (pc == null) return;
        if (!pc.Is(CustomRoles.SchrodingersCat)) return;
        if (!teammate.ContainsKey(pc.PlayerId) || teammate[pc.PlayerId] == byte.MaxValue) return;
        if (CustomWinnerHolder.WinnerIds.Contains(teammate[pc.PlayerId]) || Main.PlayerStates.TryGetValue(teammate[pc.PlayerId], out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole))
        {
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.SchrodingersCat);
        }
    }
}
