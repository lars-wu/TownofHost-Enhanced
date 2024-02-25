using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public static class Blackmailer
{
    private static readonly int Id = 24600;
    private static List<byte> playerIdList = [];
    public static OptionItem SkillCooldown;
    //public static OptionItem BlackmailerMax;
    public static Dictionary<byte, int> BlackmailerMaxUp = [];
    public static List<byte> ForBlackmailer = [];
    public static bool IsEnable = false;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Blackmailer);
        SkillCooldown = FloatOptionItem.Create(Id + 42, "BlackmailerSkillCooldown", new(2.5f, 900f, 2.5f), 20f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Blackmailer])
           .SetValueFormat(OptionFormat.Seconds);
        //BlackmailerMax = FloatOptionItem.Create(Id + 43, "BlackmailerMax", new(2.5f, 900f, 2.5f), 20f, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Blackmailer])
        //    .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = [];
        BlackmailerMaxUp = [];
        ForBlackmailer = [];
        IsEnable = false;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        IsEnable = true;
    }
    public static void ApplyGameOptions()
    {
        AURoleOptions.ShapeshifterCooldown = SkillCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }
    public static void OnShapeshift(PlayerControl blackmailer, PlayerControl target)
    {
        if (!target.IsAlive())
        {
            blackmailer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), Translator.GetString("NotAssassin")));
            return;
        }

        ForBlackmailer.Add(target.PlayerId);
        blackmailer.Notify(Translator.GetString("RejectShapeshift.AbilityWasUsed"), time: 2f);
    }
    public static void AfterMeetingTasks()
    {
        ForBlackmailer.Clear();
    }
}