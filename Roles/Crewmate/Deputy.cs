﻿using Hazel;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class Deputy
{
    private static readonly int Id = 7800;
    private static List<byte> playerIdList = [];
    public static bool IsEnable = false;

    public static OptionItem HandcuffCooldown;
    public static OptionItem HandcuffMax;
    public static OptionItem DeputyHandcuffCDForTarget;
    

    private static int HandcuffLimit = new();

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Deputy);
        HandcuffCooldown = FloatOptionItem.Create(Id + 10, "DeputyHandcuffCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);
        DeputyHandcuffCDForTarget = FloatOptionItem.Create(Id + 14, "DeputyHandcuffCDForTarget", new(0f, 180f, 2.5f), 45f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);
        HandcuffMax = IntegerOptionItem.Create(Id + 12, "DeputyHandcuffMax", new(1, 30, 1), 15, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Times);
    }
    public static void Init()
    {
        playerIdList = [];
        HandcuffLimit = new();
        IsEnable = false;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        HandcuffLimit = HandcuffMax.GetInt();
        IsEnable = true;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static void Remove(byte playerId)
    {
        playerIdList.Remove(playerId);
    }

    private static void SendRPC()
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRoleSkill, SendOption.Reliable, -1);
        writer.WritePacked((int)CustomRoles.Deputy);
        writer.Write(HandcuffLimit);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        HandcuffLimit = reader.ReadInt32();
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = HandcuffCooldown.GetFloat();
    public static bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && HandcuffLimit >= 1;
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(CustomRoles.SerialKiller)) return true;
        if (HandcuffLimit < 1) return false;
        if (CanBeHandcuffed(target))
        {
            HandcuffLimit--;
            SendRPC();

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("DeputyHandcuffedPlayer")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("HandcuffedByDeputy")));

          //  target.ResetKillCooldown();
            target.SetKillCooldownV3(DeputyHandcuffCDForTarget.GetFloat());
            if (!DisableShieldAnimations.GetBool()) killer.RpcGuardAndKill(target);
            if (!DisableShieldAnimations.GetBool()) target.RpcGuardAndKill(target);

            if (HandcuffLimit < 0)
                HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
            Logger.Info($"{killer.GetNameWithRole()} : 剩余{HandcuffLimit}次招募机会", "Deputy");
            return true;
        }
        
        if (HandcuffLimit < 0)
            HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("DeputyInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole()} : 剩余{HandcuffLimit}次招募机会", "Deputy");
        return false;
    }
    public static string GetHandcuffLimit() => Utils.ColorString(HandcuffLimit >= 1 ? Utils.GetRoleColor(CustomRoles.Deputy) : Color.gray, $"({HandcuffLimit})");
    public static bool CanBeHandcuffed(this PlayerControl pc)
    {
        return pc != null && !pc.Is(CustomRoles.Deputy)
        && !(
            false
            );
    }
}
