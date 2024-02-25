﻿using System.Collections.Generic;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public static class Camouflager
{
    private static readonly int Id = 2900;
    public static bool IsEnable = false;

    private static OptionItem CamouflageCooldownOpt;
    private static OptionItem CamouflageDurationOpt;
    private static OptionItem CanUseCommsSabotagOpt;
    private static OptionItem DisableReportWhenCamouflageIsActiveOpt;

    public static bool AbilityActivated = false;
    public static bool ShapeshiftIsHidden = false;
    private static float CamouflageCooldown;
    private static float CamouflageDuration;
    public static bool CanUseCommsSabotage;
    public static bool DisableReportWhenCamouflageIsActive;

    private static Dictionary<byte, long> Timer = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Camouflager);
        CamouflageCooldownOpt = FloatOptionItem.Create(Id + 2, "CamouflageCooldown", new(1f, 180f, 1f), 25f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
            .SetValueFormat(OptionFormat.Seconds);
        CamouflageDurationOpt = FloatOptionItem.Create(Id + 4, "CamouflageDuration", new(1f, 180f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager])
            .SetValueFormat(OptionFormat.Seconds);
        CanUseCommsSabotagOpt = BooleanOptionItem.Create(Id + 6, "CanUseCommsSabotage", false, TabGroup.ImpostorRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager]);
        DisableReportWhenCamouflageIsActiveOpt = BooleanOptionItem.Create(Id + 8, "DisableReportWhenCamouflageIsActive", false, TabGroup.ImpostorRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Camouflager]);

    }
    public static void Init()
    {
        Timer = [];
        AbilityActivated = false;
        IsEnable = false;
    }
    public static void Add()
    {
        CamouflageCooldown = CamouflageCooldownOpt.GetFloat();
        CamouflageDuration = CamouflageDurationOpt.GetFloat();
        CanUseCommsSabotage = CanUseCommsSabotagOpt.GetBool();
        DisableReportWhenCamouflageIsActive = DisableReportWhenCamouflageIsActiveOpt.GetBool();
        
        ShapeshiftIsHidden = Options.DisableShapeshiftAnimations.GetBool();
        IsEnable = true;
    }
    public static void ApplyGameOptions()
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftIsHidden && AbilityActivated ? CamouflageDuration : CamouflageCooldown;
        AURoleOptions.ShapeshifterDuration = CamouflageDuration;
    }
    public static void SetAbilityButtonText(HudManager __instance)
    {
        if (AbilityActivated)
            __instance.AbilityButton.OverrideText(GetString("CamouflagerShapeshiftTextAfterDisguise"));
        else
            __instance.AbilityButton.OverrideText(GetString("CamouflagerShapeshiftTextBeforeDisguise"));


        __instance.ReportButton.OverrideText(GetString("ReportButtonText"));
    }
    public static void OnShapeshift(PlayerControl camouflager = null, bool shapeshiftIsHidden = false)
    {
        AbilityActivated = true;
        
        var timer = 1.2f;
        if (shapeshiftIsHidden)
        {
            timer = 0.1f;
            camouflager.SyncSettings();
        }

        _ = new LateTask(() =>
        {
            if (!Main.MeetingIsStarted && GameStates.IsInTask)
            {
                Camouflage.CheckCamouflage();

                if (camouflager != null && shapeshiftIsHidden)
                {
                    Timer.Add(camouflager.PlayerId, Utils.GetTimeStamp());
                }
            }
        }, timer, "Camouflager Use Shapeshift");
    }
    public static void OnReportDeadBody()
    {
        ClearCamouflage();
        Timer = [];
    }
    public static void IsDead()
    {
        if (GameStates.IsMeeting) return;

        ClearCamouflage();
    }
    private static void ClearCamouflage()
    {
        AbilityActivated = false;
        Camouflage.CheckCamouflage();
    }
    public static void OnFixedUpdate(PlayerControl camouflager)
    {
        if (camouflager == null || !camouflager.IsAlive())
        {
            Timer.Remove(camouflager.PlayerId);
            ClearCamouflage();
            camouflager.SyncSettings();
            camouflager.RpcResetAbilityCooldown();
            return;
        }
        if (!Timer.TryGetValue(camouflager.PlayerId, out var oldTime)) return;

        var nowTime = Utils.GetTimeStamp();
        if (nowTime - oldTime >= CamouflageDuration)
        {
            Timer.Remove(camouflager.PlayerId);
            ClearCamouflage();
            camouflager.SyncSettings();
            camouflager.RpcResetAbilityCooldown();
        }
    }
}
