using AmongUs.Data;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using TOHE.Modules;
using TOHE.Modules.ChatManager;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Double;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;
using TOHE.Roles.AddOns.Common;
using System.Threading.Channels;

namespace TOHE;

public static class Utils
{
    private static readonly DateTime timeStampStartTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static long GetTimeStamp(DateTime? dateTime = null) => (long)((dateTime ?? DateTime.Now).ToUniversalTime() - timeStampStartTime).TotalSeconds;
    public static void ErrorEnd(string text)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            Logger.Fatal($"{text} error, triggering anti-blackout measures", "Anti-black");
            ChatUpdatePatch.DoBlockChat = true;
            Main.OverrideWelcomeMsg = GetString("AntiBlackOutNotifyInLobby");
            
            _ = new LateTask(() =>
            {
                Logger.SendInGame(GetString("AntiBlackOutLoggerSendInGame"));
            }, 3f, "Anti-Black Msg SendInGame 3");
            
            _ = new LateTask(() =>
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                GameManager.Instance.LogicFlow.CheckEndCriteria();
                RPC.ForceEndGame(CustomWinner.Error);
            }, 5.5f, "Anti-Black End Game 3");
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AntiBlackout, SendOption.Reliable);
            writer.Write(text);
            writer.EndMessage();
            if (Options.EndWhenPlayerBug.GetBool())
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd"));
                }, 3f, "Anti-Black Msg SendInGame 4");
            }
            else
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutHostRejectForceEnd"));
                }, 3f, "Anti-Black Msg SendInGame 5");
                
                _ = new LateTask(() =>
                {
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                    Logger.Fatal($"{text} 错误，已断开游戏", "Anti-black");
                }, 8f, "Anti-Black Exit Game 4");
            }
        }
    }
    public static ClientData GetClientById(int id)
    {
        try
        {
            var client = AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(cd => cd.Id == id);
            return client;
        }
        catch
        {
            return null;
        }
    }
    public static bool IsActive(SystemTypes type)
    {
        if (GameStates.IsHideNSeek) return false;

        // if ShipStatus not have current SystemTypes, return false
        if (!ShipStatus.Instance.Systems.ContainsKey(type))
        {
            return false;
        }

        int mapId = GetActiveMapId();
        /*
            The Skeld    = 0
            MIRA HQ      = 1
            Polus        = 2
            Dleks        = 3
            The Airship  = 4
            The Fungle   = 5
        */

        //Logger.Info($"{type}", "SystemTypes");

        switch (type)
        {
            case SystemTypes.Electrical:
                {
                    if (mapId == 5) return false; // if The Fungle return false
                    var SwitchSystem = ShipStatus.Instance.Systems[type].Cast<SwitchSystem>();
                    return SwitchSystem != null && SwitchSystem.IsActive;
                }
            case SystemTypes.Reactor:
                {
                    if (mapId == 2) return false; // if Polus return false
                    else
                    {
                        var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                        return ReactorSystemType != null && ReactorSystemType.IsActive;
                    }
                }
            case SystemTypes.Laboratory:
                {
                    if (mapId != 2) return false; // Only Polus
                    var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                    return ReactorSystemType != null && ReactorSystemType.IsActive;
                }
            case SystemTypes.LifeSupp:
                {
                    if (mapId is 2 or 4 or 5) return false; // Only Skeld & Dleks & Mira HQ
                    var LifeSuppSystemType = ShipStatus.Instance.Systems[type].Cast<LifeSuppSystemType>();
                    return LifeSuppSystemType != null && LifeSuppSystemType.IsActive;
                }
            case SystemTypes.HeliSabotage:
                {
                    if (mapId != 4) return false; // Only Airhip
                    var HeliSabotageSystem = ShipStatus.Instance.Systems[type].Cast<HeliSabotageSystem>();
                    return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                }
            case SystemTypes.Comms:
                {
                    if (mapId is 1 or 5) // Only Mira HQ & The Fungle
                    {
                        var HqHudSystemType = ShipStatus.Instance.Systems[type].Cast<HqHudSystemType>();
                        return HqHudSystemType != null && HqHudSystemType.IsActive;
                    }
                    else
                    {
                        var HudOverrideSystemType = ShipStatus.Instance.Systems[type].Cast<HudOverrideSystemType>();
                        return HudOverrideSystemType != null && HudOverrideSystemType.IsActive;
                    }
                }
            case SystemTypes.MushroomMixupSabotage:
                {
                    if (mapId != 5) return false; // Only The Fungle
                    var MushroomMixupSabotageSystem = ShipStatus.Instance.Systems[type].TryCast<MushroomMixupSabotageSystem>();
                    return MushroomMixupSabotageSystem != null && MushroomMixupSabotageSystem.IsActive;
                }
            default:
                return false;
        }
    }
    public static SystemTypes GetCriticalSabotageSystemType() => GetActiveMapName() switch
    {
        MapNames.Polus => SystemTypes.Laboratory,
        MapNames.Airship => SystemTypes.HeliSabotage,
        _ => SystemTypes.Reactor,
    };

    public static MapNames GetActiveMapName() => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
    public static byte GetActiveMapId() => GameOptionsManager.Instance.CurrentGameOptions.MapId;

    public static void SetVision(this IGameOptions opt, bool HasImpVision)
    {
        if (HasImpVision)
        {
            opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod) * 5);
            }
            return;
        }
        else
        {
            opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
            }
            return;
        }
    }
    //誰かが死亡したときのメソッド
    public static void SetVisionV2(this IGameOptions opt)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod));
        if (IsActive(SystemTypes.Electrical))
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
        }
        return;
    }
    
    public static void TargetDies(PlayerControl killer, PlayerControl target)
    {
        if (!target.Data.IsDead || GameStates.IsMeeting) return;

        foreach (var seer in Main.AllPlayerControls)
        {
            if (KillFlashCheck(killer, target, seer))
            {
                seer.KillFlash();
                continue;
            }
            else if (target.Is(CustomRoles.CyberStar))
            {
                if (!Options.ImpKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsImpostor()) continue;
                if (!Options.NeutralKnowCyberStarDead.GetBool() && seer.GetCustomRole().IsNeutral()) continue;
                seer.KillFlash();
                seer.Notify(ColorString(GetRoleColor(CustomRoles.CyberStar), GetString("OnCyberStarDead")));
            }
            else if (target.Is(CustomRoles.Cyber))
            {
                if (!Cyber.ImpKnowCyberDead.GetBool() && seer.GetCustomRole().IsImpostor()) continue;
                if (!Cyber.NeutralKnowCyberDead.GetBool() && seer.GetCustomRole().IsNeutral()) continue;
                if (!Cyber.CrewKnowCyberDead.GetBool() && seer.GetCustomRole().IsCrewmate()) continue;
                seer.KillFlash();
                seer.Notify(ColorString(GetRoleColor(CustomRoles.Cyber), GetString("OnCyberDead"))); 
            } 
        }
        if (target.Is(CustomRoles.CyberStar) && !Main.CyberStarDead.Contains(target.PlayerId)) Main.CyberStarDead.Add(target.PlayerId);
        if (target.Is(CustomRoles.Cyber) && !Cyber.CyberDead.Contains(target.PlayerId)) Cyber.CyberDead.Add(target.PlayerId);
    }
    public static bool KillFlashCheck(PlayerControl killer, PlayerControl target, PlayerControl seer)
    {
        if (seer.Is(CustomRoles.GM) || seer.Is(CustomRoles.Seer)) return true;
        if (seer.Data.IsDead || killer == seer || target == seer) return false;
        if (seer.Is(CustomRoles.EvilTracker)) return EvilTracker.KillFlashCheck();
        return false;
    }
    public static void KillFlash(this PlayerControl player)
    {
        // Kill flash (blackout flash + reactor flash)
        bool ReactorCheck = IsActive(GetCriticalSabotageSystemType());

        var Duration = Options.KillFlashDuration.GetFloat();
        if (ReactorCheck) Duration += 0.2f; //リアクター中はブラックアウトを長くする

        //実行
        Main.PlayerStates[player.PlayerId].IsBlackOut = true; //ブラックアウト
        if (player.AmOwner)
        {
            FlashColor(new(1f, 0f, 0f, 0.3f));
            if (Constants.ShouldPlaySfx()) RPC.PlaySound(player.PlayerId, Sounds.KillSound);
        }
        else if (player.IsModClient())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.KillFlash, SendOption.Reliable, player.GetClientId());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else if (!ReactorCheck) player.ReactorFlash(0f); //リアクターフラッシュ
        player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = false; //ブラックアウト解除
            player.MarkDirtySettings();
        }, Options.KillFlashDuration.GetFloat(), "Remove Kill Flash");
    }
    public static void BlackOut(this IGameOptions opt, bool IsBlackOut)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
        if (IsBlackOut)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0);
            opt.SetFloat(FloatOptionNames.CrewLightMod, 0);
        }
        return;
    }
    public static string GetDisplayRoleAndSubName(byte seerId, byte targetId, bool notShowAddOns = false)
    {
        var TextData = GetRoleAndSubText(seerId, targetId, notShowAddOns);
        return ColorString(TextData.Item2, TextData.Item1);
    }
    public static string GetRoleName(CustomRoles role, bool forUser = true)
    {
        return GetRoleString(Enum.GetName(typeof(CustomRoles), role), forUser);
    }
    public static string GetRoleMode(CustomRoles role, bool parentheses = true)
    {
        if (Options.HideGameSettings.GetBool() && Main.AllPlayerControls.Length > 1)
            return string.Empty;

        string mode = GetString($"Chance{role.GetMode()}").RemoveHtmlTags();
        if (role.Is(CustomRoles.Lovers)) mode = GetString($"Chance{Options.LoverSpawnChances.GetInt()}");
        else if (role.IsAdditionRole() && Options.CustomAdtRoleSpawnRate.ContainsKey(role))
        {
            mode = GetString($"Chance{Options.CustomAdtRoleSpawnRate[role].GetFloat()}");
            
        }
        
        return parentheses ? $"({mode})" : mode;
    }
    public static string GetDeathReason(PlayerState.DeathReason status)
    {
        return GetString("DeathReason." + Enum.GetName(typeof(PlayerState.DeathReason), status));
    }
    public static Color GetRoleColor(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = "#ffffff";
        _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
        return c;
    }
    public static string GetRoleColorCode(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = "#ffffff";
        return hexColor;
    }
    public static (string, Color) GetRoleAndSubText(byte seerId, byte targetId, bool notShowAddOns = false)
    {
        string RoleText = "Invalid Role";
        Color RoleColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

        var targetMainRole = Main.PlayerStates[targetId].MainRole;
        var targetSubRoles = Main.PlayerStates[targetId].SubRoles;

        RoleText = GetRoleName(targetMainRole);
        RoleColor = GetRoleColor(targetMainRole);

        try
        {
            if (targetSubRoles.Count > 0)
            {
                var seer = GetPlayerById(seerId);
                var target = GetPlayerById(targetId);

                if (seer == null || target == null) return (RoleText, RoleColor);

                // if player last imp
                if (LastImpostor.currentId == targetId)
                    RoleText = GetRoleString("Last-") + RoleText;

                if (Options.NameDisplayAddons.GetBool() && !notShowAddOns)
                {
                    var seerPlatform = seer.GetClient()?.PlatformData.Platform;
                    var addBracketsToAddons = Options.AddBracketsToAddons.GetBool();

                    // if the player is playing on a console platform
                    if (seerPlatform is Platforms.Playstation or Platforms.Xbox or Platforms.Switch)
                    {
                        // By default, censorship is enabled on consoles
                        // Need to set add-ons colors without endings "</color>"

                        // colored role
                        RoleText = ColorStringWithoutEnding(GetRoleColor(targetMainRole), RoleText);

                        // colored add-ons
                        foreach (var subRole in targetSubRoles.Where(subRole => subRole.ShouldBeDisplayed() && seer.ShowSubRoleTarget(target, subRole)).ToArray())
                            RoleText = ColorStringWithoutEnding(GetRoleColor(subRole), addBracketsToAddons ? $"({GetString($"Prefix.{subRole}")}) " : $"{GetString($"Prefix.{subRole}")} ") + RoleText;
                    }
                    // default
                    else
                    {
                        foreach (var subRole in targetSubRoles.Where(subRole => subRole.ShouldBeDisplayed() && seer.ShowSubRoleTarget(target, subRole)).ToArray())
                            RoleText = ColorString(GetRoleColor(subRole), addBracketsToAddons ? $"({GetString($"Prefix.{subRole}")}) " : $"{GetString($"Prefix.{subRole}")} ") + RoleText;
                    }
                }

                foreach (var subRole in targetSubRoles.ToArray())
                {
                    if (seer.ShowSubRoleTarget(target, subRole))
                        switch (subRole)
                        {
                            case CustomRoles.Madmate:
                            case CustomRoles.Recruit:
                            case CustomRoles.Charmed:
                            case CustomRoles.Soulless:
                            case CustomRoles.Infected:
                            case CustomRoles.Contagious:
                            case CustomRoles.Admired:
                                RoleColor = GetRoleColor(subRole);
                                RoleText = GetRoleString($"{subRole}-") + RoleText;
                                break;

                        }
                }
            }

            return (RoleText, RoleColor);
        }
        catch
        {
            return (RoleText, RoleColor);
        }
    }
    public static string GetKillCountText(byte playerId, bool ffa = false)
    {
        int count = Main.PlayerStates.Count(x => x.Value.GetRealKiller() == playerId);
        if (count < 1 && !ffa) return "";
        return ColorString(new Color32(255, 69, 0, byte.MaxValue), string.Format(GetString("KillCount"), count));
    }
    public static string GetVitalText(byte playerId, bool RealKillerColor = false)
    {
        var state = Main.PlayerStates[playerId];
        string deathReason = state.IsDead ? GetString("DeathReason." + state.deathReason) : GetString("Alive");
        if (RealKillerColor)
        {
            var KillerId = state.GetRealKiller();
            Color color = KillerId != byte.MaxValue ? Main.PlayerColors[KillerId] : GetRoleColor(CustomRoles.Doctor);
            if (state.deathReason == PlayerState.DeathReason.Disconnected) color = new Color(255, 255, 255, 50);
            deathReason = ColorString(color, deathReason);
        }
        return deathReason;
    }

    public static bool HasTasks(GameData.PlayerInfo p, bool ForRecompute = true)
    {
        if (GameStates.IsLobby) return false;
        //Tasksがnullの場合があるのでその場合タスク無しとする
        if (p.Tasks == null) return false;
        if (p.Role == null) return false;

        var hasTasks = true;
        var States = Main.PlayerStates[p.PlayerId];
        if (p.Disconnected) return false;
        if (p.Role.IsImpostor)
            hasTasks = false; //タスクはCustomRoleを元に判定する

        if (Options.CurrentGameMode == CustomGameMode.FFA) return false;

        if (p.IsDead && Options.GhostIgnoreTasks.GetBool()) hasTasks = false;
        var role = States.MainRole;
        switch (role)
        {
            case CustomRoles.GM:
            case CustomRoles.Sheriff:
            case CustomRoles.Vigilante:
            case CustomRoles.Jailer:
            case CustomRoles.CopyCat:
            case CustomRoles.Shaman:
            case CustomRoles.Arsonist:
            case CustomRoles.Jackal:
            case CustomRoles.Bandit:
            case CustomRoles.Doppelganger:
            case CustomRoles.Sidekick:
            case CustomRoles.Poisoner:
            case CustomRoles.Necromancer:
            case CustomRoles.SerialKiller:
            case CustomRoles.Pyromaniac:
            case CustomRoles.Werewolf:
            case CustomRoles.Traitor:
            case CustomRoles.Huntsman:
            case CustomRoles.Glitch:
            case CustomRoles.Pickpocket:
            case CustomRoles.Maverick:
            case CustomRoles.Agitater:
            case CustomRoles.Jinx:
            case CustomRoles.SoulCollector:
            case CustomRoles.SchrodingersCat:
            case CustomRoles.Parasite:
            case CustomRoles.Minion:
            case CustomRoles.Nemesis:
            case CustomRoles.Crusader:
            case CustomRoles.Refugee:
            case CustomRoles.Jester:
            case CustomRoles.Pirate:
            case CustomRoles.Pixie:
            case CustomRoles.PlagueDoctor:
            case CustomRoles.Shroud:
            case CustomRoles.Mario:
            case CustomRoles.Vulture:
            case CustomRoles.God:
            case CustomRoles.SwordsMan:
            case CustomRoles.Innocent:
            case CustomRoles.Pelican:
            case CustomRoles.Medusa:
            case CustomRoles.Revolutionist:
            case CustomRoles.Hater:
            case CustomRoles.Gamer:
            case CustomRoles.HexMaster:
            //case CustomRoles.Occultist:
            case CustomRoles.Wraith:
            case CustomRoles.Juggernaut:
            case CustomRoles.Reverie:
            case CustomRoles.PotionMaster:
            case CustomRoles.DarkHide:
            case CustomRoles.Collector:
            case CustomRoles.ImperiusCurse:
            case CustomRoles.Provocateur:
            case CustomRoles.Medic:
            case CustomRoles.BloodKnight:
            case CustomRoles.Camouflager:
            case CustomRoles.Totocalcio:
            case CustomRoles.Succubus:
            case CustomRoles.CursedSoul:
            case CustomRoles.Admirer:
            case CustomRoles.Amnesiac:
            case CustomRoles.Imitator:
            case CustomRoles.Infectious:
            case CustomRoles.Monarch:
            case CustomRoles.Deputy:
            case CustomRoles.Investigator:
            case CustomRoles.Virus:
            case CustomRoles.Farseer:
            case CustomRoles.Counterfeiter:
            case CustomRoles.Witness:
            case CustomRoles.Pursuer:
            case CustomRoles.Spiritcaller:
            case CustomRoles.PlagueBearer:
            case CustomRoles.Pestilence:
            case CustomRoles.Masochist:
            case CustomRoles.Executioner:
            case CustomRoles.Lawyer:
            case CustomRoles.Doomsayer:
            case CustomRoles.Seeker:
            case CustomRoles.Romantic:
            case CustomRoles.VengefulRomantic:
            case CustomRoles.RuthlessRomantic:
            case CustomRoles.Quizmaster:
                hasTasks = false;
                break;
            case CustomRoles.Workaholic:
            case CustomRoles.Terrorist:
            case CustomRoles.Sunnyboy:
            case CustomRoles.Convict:
            case CustomRoles.Opportunist:
            case CustomRoles.Taskinator:
            case CustomRoles.Phantom:
                if (ForRecompute)
                    hasTasks = false;
                    break;
            case CustomRoles.Crewpostor:
                if (ForRecompute && !p.IsDead)
                    hasTasks = false;
                if (p.IsDead)
                    hasTasks = false;
                break;
            default:
                if (role.IsImpostor() || role.IsNK()) hasTasks = false;
                break;
        }

        foreach (var subRole in States.SubRoles.ToArray())
            switch (subRole)
            {
                case CustomRoles.Madmate:
                case CustomRoles.Charmed:
                case CustomRoles.Recruit:
                case CustomRoles.Egoist:
                case CustomRoles.Infected:
                case CustomRoles.EvilSpirit:
                case CustomRoles.Contagious:
                case CustomRoles.Soulless:
                case CustomRoles.Rascal:
                    //Lovers don't count the task as a win
                    hasTasks &= !ForRecompute;
                    break;
                case CustomRoles.Mundane:
                    if (!hasTasks) hasTasks = !ForRecompute;
                    break;

            }
        if (CopyCat.playerIdList.Contains(p.PlayerId)) hasTasks = false;
        if (Main.TasklessCrewmate.Contains(p.PlayerId)) hasTasks = false;

        return hasTasks;
    }
  
    public static string GetProgressText(PlayerControl pc)
    {
        try
        {
            if (!Main.playerVersion.ContainsKey(AmongUsClient.Instance.HostId)) return string.Empty; //ホストがMODを入れていなければ未記入を返す
            var taskState = pc.GetPlayerTaskState();
            var Comms = false;
            if (taskState.hasTasks)
            {
                if (IsActive(SystemTypes.Comms)) Comms = true;
                if (Camouflager.AbilityActivated) Comms = true;
                //if (PlayerControl.LocalPlayer.myTasks.ToArray().Any(x => x.TaskType == TaskTypes.FixComms)) Comms = true;
            }
            return GetProgressText(pc.PlayerId, Comms);
        }
        catch (Exception error)
        {
            Logger.Error(error.ToString(), $"GetProgressText(PlayerControl pc) - PlayerId: {pc.PlayerId}, Role: {Main.PlayerStates[pc.PlayerId].MainRole}");
            return "Error1";
        }
    }
    public static string GetProgressText(byte playerId, bool comms = false)
    {
        try
        {
            if (!Main.playerVersion.ContainsKey(AmongUsClient.Instance.HostId)) return ""; //ホストがMODを入れていなければ未記入を返す
            var ProgressText = new StringBuilder();
            var role = Main.PlayerStates[playerId].MainRole;
            switch (role)
            {
                case CustomRoles.Arsonist:
                    var doused = GetDousedPlayerCount(playerId);
                    if (!Options.ArsonistCanIgniteAnytime.GetBool()) ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), $"({doused.Item1}/{doused.Item2})"));
                    else ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), $"({doused.Item1}/{Options.ArsonistMaxPlayersToIgnite.GetInt()})"));
                    break;
                case CustomRoles.SoulCollector:
                    ProgressText.Append(SoulCollector.GetProgressText(playerId));
                    break;
                case CustomRoles.Pixie:
                    ProgressText.Append(Pixie.GetProgressText(playerId));
                    break;
                case CustomRoles.Sheriff:
                    if (Sheriff.ShowShotLimit.GetBool()) ProgressText.Append(Sheriff.GetShotLimit(playerId));
                    break;
                case CustomRoles.President:
                    ProgressText.Append(President.GetEndLimit(playerId));
                    break;
                case CustomRoles.Veteran:
                    var taskState2 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor2;
                    var TaskCompleteColor2 = Color.green;
                    var NonCompleteColor2 = Color.yellow;
                    var NormalColor2 = taskState2.IsTaskFinished ? TaskCompleteColor2 : NonCompleteColor2;
                    TextColor2 = comms ? Color.gray : NormalColor2;
                    string Completed2 = comms ? "?" : $"{taskState2.CompletedTasksCount}";
                    Color TextColor21;
                    if (Main.VeteranNumOfUsed[playerId] < 1) TextColor21 = Color.red;
                    else TextColor21 = Color.white;
                    ProgressText.Append(ColorString(TextColor2, $"({Completed2}/{taskState2.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor21, $" <color=#ffffff>-</color> {Math.Round(Main.VeteranNumOfUsed[playerId], 1)}"));
                    break;
                case CustomRoles.Grenadier:
                    var taskState3 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor3;
                    var TaskCompleteColor3 = Color.green;
                    var NonCompleteColor3 = Color.yellow;
                    var NormalColor3 = taskState3.IsTaskFinished ? TaskCompleteColor3 : NonCompleteColor3;
                    TextColor3 = comms ? Color.gray : NormalColor3;
                    string Completed3 = comms ? "?" : $"{taskState3.CompletedTasksCount}";
                    Color TextColor31;
                    if (Main.GrenadierNumOfUsed[playerId] < 1) TextColor31 = Color.red;
                    else TextColor31 = Color.white;
                    ProgressText.Append(ColorString(TextColor3, $"({Completed3}/{taskState3.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor31, $" <color=#ffffff>-</color> {Math.Round(Main.GrenadierNumOfUsed[playerId], 1)}"));
                    break;
                case CustomRoles.Bastion:
                    var taskState15 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor15;
                    var TaskCompleteColor15 = Color.green;
                    var NonCompleteColor15 = Color.yellow;
                    var NormalColor15 = taskState15.IsTaskFinished ? TaskCompleteColor15 : NonCompleteColor15;
                    TextColor15 = comms ? Color.gray : NormalColor15;
                    string Completed15 = comms ? "?" : $"{taskState15.CompletedTasksCount}";
                    Color TextColor151;
                    if (Main.BastionNumberOfAbilityUses < 1) TextColor151 = Color.red;
                    else TextColor151 = Color.white;
                    ProgressText.Append(ColorString(TextColor15, $"({Completed15}/{taskState15.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor151, $" <color=#777777>-</color> {Math.Round(Main.BastionNumberOfAbilityUses, 1)}"));
                    break;
                case CustomRoles.Divinator:
                    var taskState4 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor4;
                    var TaskCompleteColor4 = Color.green;
                    var NonCompleteColor4 = Color.yellow;
                    var NormalColor4 = taskState4.IsTaskFinished ? TaskCompleteColor4 : NonCompleteColor4;
                    TextColor4 = comms ? Color.gray : NormalColor4;
                    string Completed4 = comms ? "?" : $"{taskState4.CompletedTasksCount}";
                    Color TextColor41;
                    if (Divinator.CheckLimit[playerId] < 1) TextColor41 = Color.red;
                    else TextColor41 = Color.white;
                    ProgressText.Append(ColorString(TextColor4, $"({Completed4}/{taskState4.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor41, $" <color=#ffffff>-</color> {Math.Round(Divinator.CheckLimit[playerId])}"));
                    break;
                case CustomRoles.DovesOfNeace:
                    var taskState5 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor5;
                    var TaskCompleteColor5 = Color.green;
                    var NonCompleteColor5 = Color.yellow;
                    var NormalColor5 = taskState5.IsTaskFinished ? TaskCompleteColor5 : NonCompleteColor5;
                    TextColor5 = comms ? Color.gray : NormalColor5;
                    string Completed5 = comms ? "?" : $"{taskState5.CompletedTasksCount}";
                    Color TextColor51;
                    if (Main.DovesOfNeaceNumOfUsed[playerId] < 1) TextColor51 = Color.red;
                    else TextColor51 = Color.white;
                    ProgressText.Append(ColorString(TextColor5, $"({Completed5}/{taskState5.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor51, $" <color=#ffffff>-</color> {Math.Round(Main.DovesOfNeaceNumOfUsed[playerId], 1)}"));
                    break;
                case CustomRoles.TimeMaster:
                    var taskState6 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor6;
                    var TaskCompleteColor6 = Color.green;
                    var NonCompleteColor6 = Color.yellow;
                    var NormalColor6 = taskState6.IsTaskFinished ? TaskCompleteColor6 : NonCompleteColor6;
                    TextColor6 = comms ? Color.gray : NormalColor6;
                    string Completed6 = comms ? "?" : $"{taskState6.CompletedTasksCount}";
                    Color TextColor61;
                    if (Main.TimeMasterNumOfUsed[playerId] < 1) TextColor61 = Color.red;
                    else TextColor61 = Color.white;
                    ProgressText.Append(ColorString(TextColor6, $"({Completed6}/{taskState6.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor61, $" <color=#ffffff>-</color> {Math.Round(Main.TimeMasterNumOfUsed[playerId], 1)}"));
                    break;
                case CustomRoles.Mediumshiper:
                    var taskState7 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor7;
                    var TaskCompleteColor7 = Color.green;
                    var NonCompleteColor7 = Color.yellow;
                    var NormalColor7 = taskState7.IsTaskFinished ? TaskCompleteColor7 : NonCompleteColor7;
                    TextColor7 = comms ? Color.gray : NormalColor7;
                    string Completed7 = comms ? "?" : $"{taskState7.CompletedTasksCount}";
                    Color TextColor71;
                    if (Mediumshiper.ContactLimit[playerId] < 1) TextColor71 = Color.red;
                    else TextColor71 = Color.white;
                    ProgressText.Append(ColorString(TextColor7, $"({Completed7}/{taskState7.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor71, $" <color=#ffffff>-</color> {Math.Round(Mediumshiper.ContactLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Inspector:
                    var taskState8 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor8;
                    var TaskCompleteColor8 = Color.green;
                    var NonCompleteColor8 = Color.yellow;
                    var NormalColor8 = taskState8.IsTaskFinished ? TaskCompleteColor8 : NonCompleteColor8;
                    TextColor8 = comms ? Color.gray : NormalColor8;
                    string Completed8 = comms ? "?" : $"{taskState8.CompletedTasksCount}";
                    Color TextColor81;
                    if (Inspector.MaxCheckLimit[playerId] < 1) TextColor81 = Color.red;
                    else TextColor81 = Color.white;
                    ProgressText.Append(ColorString(TextColor8, $"({Completed8}/{taskState8.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor81, $" <color=#ffffff>-</color> {Math.Round(Inspector.MaxCheckLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Oracle:
                    var taskState9 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor9;
                    var TaskCompleteColor9 = Color.green;
                    var NonCompleteColor9 = Color.yellow;
                    var NormalColor9 = taskState9.IsTaskFinished ? TaskCompleteColor9 : NonCompleteColor9;
                    TextColor9 = comms ? Color.gray : NormalColor9;
                    string Completed9 = comms ? "?" : $"{taskState9.CompletedTasksCount}";
                    Color TextColor91;
                    if (Oracle.CheckLimit[playerId] < 1) TextColor91 = Color.red;
                    else TextColor91 = Color.white;
                    ProgressText.Append(ColorString(TextColor9, $"({Completed9}/{taskState9.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor91, $" <color=#ffffff>-</color> {Math.Round(Oracle.CheckLimit[playerId], 1)}"));
                    break;
                case CustomRoles.SabotageMaster:
                    var taskState10 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor10;
                    var TaskCompleteColor10 = Color.green;
                    var NonCompleteColor10 = Color.yellow;
                    var NormalColor10 = taskState10.IsTaskFinished ? TaskCompleteColor10 : NonCompleteColor10;
                    TextColor10 = comms ? Color.gray : NormalColor10;
                    string Completed10 = comms ? "?" : $"{taskState10.CompletedTasksCount}";
                    Color TextColor101;
                    if (SabotageMaster.SkillLimit.GetFloat() - SabotageMaster.UsedSkillCount[playerId] < 1) TextColor101 = Color.red;
                    else TextColor101 = Color.white;
                    ProgressText.Append(ColorString(TextColor10, $"({Completed10}/{taskState10.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor101, $" <color=#ffffff>-</color> {Math.Round(SabotageMaster.SkillLimit.GetFloat() - SabotageMaster.UsedSkillCount[playerId], 1)}"));
                    break;
                case CustomRoles.Tracker:
                    var taskState11 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor11;
                    var TaskCompleteColor11 = Color.green;
                    var NonCompleteColor11 = Color.yellow;
                    var NormalColor11 = taskState11.IsTaskFinished ? TaskCompleteColor11 : NonCompleteColor11;
                    TextColor11 = comms ? Color.gray : NormalColor11;
                    string Completed11 = comms ? "?" : $"{taskState11.CompletedTasksCount}";
                    Color TextColor111;
                    if (Tracker.TrackLimit[playerId] < 1) TextColor111 = Color.red;
                    else TextColor111 = Color.white;
                    ProgressText.Append(ColorString(TextColor11, $"({Completed11}/{taskState11.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor111, $" <color=#ffffff>-</color> {Math.Round(Tracker.TrackLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Bloodhound:
                    var taskState12 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor12;
                    var TaskCompleteColor12 = Color.green;
                    var NonCompleteColor12 = Color.yellow;
                    var NormalColor12 = taskState12.IsTaskFinished ? TaskCompleteColor12 : NonCompleteColor12;
                    TextColor12 = comms ? Color.gray : NormalColor12;
                    string Completed12 = comms ? "?" : $"{taskState12.CompletedTasksCount}";
                    Color TextColor121;
                    if (Bloodhound.UseLimit[playerId] < 1) TextColor121 = Color.red;
                    else TextColor121 = Color.white;
                    ProgressText.Append(ColorString(TextColor12, $"({Completed12}/{taskState12.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor121, $" <color=#ffffff>-</color> {Math.Round(Bloodhound.UseLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Alchemist:
                    ProgressText.Append(Alchemist.GetProgressText(playerId));
                    break;
                case CustomRoles.PlagueDoctor:
                    ProgressText.Append(PlagueDoctor.GetProgressText());
                    break;
                case CustomRoles.Chameleon:
                    var taskState13 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor13;
                    var TaskCompleteColor13 = Color.green;
                    var NonCompleteColor13 = Color.yellow;
                    var NormalColor13 = taskState13.IsTaskFinished ? TaskCompleteColor13 : NonCompleteColor13;
                    TextColor13 = comms ? Color.gray : NormalColor13;
                    string Completed13 = comms ? "?" : $"{taskState13.CompletedTasksCount}";
                    Color TextColor131;
                    if (Chameleon.UseLimit[playerId] < 1) TextColor131 = Color.red;
                    else TextColor131 = Color.white;
                    ProgressText.Append(ColorString(TextColor13, $"({Completed13}/{taskState13.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor131, $" <color=#ffffff>-</color> {Math.Round(Chameleon.UseLimit[playerId], 1)}"));
                    break;
                case CustomRoles.Lighter:
                    var taskState14 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor14;
                    var TaskCompleteColor14 = Color.green;
                    var NonCompleteColor14 = Color.yellow;
                    var NormalColor14 = taskState14.IsTaskFinished ? TaskCompleteColor14 : NonCompleteColor14;
                    TextColor14 = comms ? Color.gray : NormalColor14;
                    string Completed14 = comms ? "?" : $"{taskState14.CompletedTasksCount}";
                    Color TextColor141;
                    if (Main.LighterNumOfUsed[playerId] < 1) TextColor141 = Color.red;
                    else TextColor141 = Color.white;
                    ProgressText.Append(ColorString(TextColor14, $"({Completed14}/{taskState14.AllTasksCount})"));
                    ProgressText.Append(ColorString(TextColor141, $" <color=#ffffff>-</color> {Math.Round(Main.LighterNumOfUsed[playerId], 1)}"));
                    break;
                case CustomRoles.TaskManager:
                    var taskState1 = Main.PlayerStates?[playerId].TaskState;
                    Color TextColor1;
                    var TaskCompleteColor1 = Color.green;
                    var NonCompleteColor1 = Color.yellow;
                    var NormalColor1 = taskState1.IsTaskFinished ? TaskCompleteColor1 : NonCompleteColor1;
                    TextColor1 = comms ? Color.gray : NormalColor1;
                    string Completed1 = comms ? "?" : $"{taskState1.CompletedTasksCount}";
                    string totalCompleted1 = comms ? "?" : $"{GameData.Instance.CompletedTasks}";
                    ProgressText.Append(ColorString(TextColor1, $"({Completed1}/{taskState1.AllTasksCount})"));
                    ProgressText.Append($" <color=#777777>-</color> <color=#00ffa5>{totalCompleted1}/{GameData.Instance.TotalTasks}</color>");
                    break;
                case CustomRoles.Pirate:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Pirate).ShadeColor(0.25f), $"({Pirate.NumWin}/{Pirate.SuccessfulDuelsToWin.GetInt()})"));
                    break;
                case CustomRoles.Crusader:
                    ProgressText.Append(Crusader.GetSkillLimit(playerId));
                    break;
                case CustomRoles.Jailer:
                    ProgressText.Append(Jailer.GetProgressText(playerId));
                    break;
                /*     case CustomRoles.CopyCat:
                         ProgressText.Append(ColorString(GetRoleColor(CustomRoles.CopyCat).ShadeColor(0.25f), $"({(CopyCat.MiscopyLimit.TryGetValue(playerId, out var count2) ? count2 : 0)})"));
                         break; */
                case CustomRoles.PlagueBearer:
                    var plagued = PlagueBearer.PlaguedPlayerCount(playerId);
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.PlagueBearer).ShadeColor(0.25f), $"({plagued.Item1}/{plagued.Item2})"));
                    break;
                case CustomRoles.Doomsayer:
                    var doomsayerguess = Doomsayer.GuessedPlayerCount(playerId);
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Doomsayer).ShadeColor(0.25f), $"({doomsayerguess.Item1}/{doomsayerguess.Item2})"));
                    break;
                case CustomRoles.Seeker:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Seeker).ShadeColor(0.25f), $"({Seeker.TotalPoints[playerId]}/{Seeker.PointsToWin.GetInt()})"));
                    break;
                case CustomRoles.SchrodingersCat:
                    ProgressText.Append(SchrodingersCat.GetProgressText(playerId));
                    break;

                case CustomRoles.Sniper:
                    ProgressText.Append(Sniper.GetBulletCount(playerId));
                    break;
                case CustomRoles.EvilTracker:
                    ProgressText.Append(EvilTracker.GetMarker(playerId));
                    break;
                case CustomRoles.TimeThief:
                    ProgressText.Append(TimeThief.GetProgressText(playerId));
                    break;
                case CustomRoles.Mario:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Mario).ShadeColor(0.25f), $"({(Main.MarioVentCount.TryGetValue(playerId, out var count) ? count : 0)}/{Options.MarioVentNumWin.GetInt()})"));
                    break;
                case CustomRoles.Vulture:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Vulture).ShadeColor(0.25f), $"({(Vulture.BodyReportCount.TryGetValue(playerId, out var count1) ? count1 : 0)}/{Vulture.NumberOfReportsToWin.GetInt()})"));
                    break;
                case CustomRoles.Masochist:
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Masochist).ShadeColor(0.25f), $"({(Main.MasochistKillMax.TryGetValue(playerId, out var count3) ? count3 : 0)}/{Options.MasochistKillMax.GetInt()})"));
                    break;
                case CustomRoles.Kamikaze:
                    ProgressText.Append(Kamikaze.GetMarkedLimit(playerId));
                    break;
                case CustomRoles.QuickShooter:
                    ProgressText.Append(QuickShooter.GetShotLimit(playerId));
                    break;
                case CustomRoles.SwordsMan:
                    ProgressText.Append(SwordsMan.GetKillLimit(playerId));
                    break;
                case CustomRoles.Pelican:
                    ProgressText.Append(Pelican.GetProgressText(playerId));
                    break;
                case CustomRoles.Counterfeiter:
                    ProgressText.Append(Counterfeiter.GetSeelLimit(playerId));
                    break;
                case CustomRoles.Pursuer:
                    ProgressText.Append(Pursuer.GetSeelLimit(playerId));
                    break;
                case CustomRoles.Revolutionist:
                    var draw = GetDrawPlayerCount(playerId, out var _);
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Revolutionist).ShadeColor(0.25f), $"({draw.Item1}/{draw.Item2})"));
                    break;
                case CustomRoles.Gangster:
                    ProgressText.Append(Gangster.GetRecruitLimit(playerId));
                    break;
                case CustomRoles.Medic:
                    ProgressText.Append(Medic.GetSkillLimit(playerId));
                    break;
                case CustomRoles.CursedWolf:
                    int SpellCount = Main.CursedWolfSpellCount[playerId];
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.CursedWolf), $"({SpellCount})"));
                    break;
                case CustomRoles.Jinx:
                    int JinxSpellCount = Main.JinxSpellCount[playerId];
                    ProgressText.Append(ColorString(GetRoleColor(CustomRoles.Jinx), $"({JinxSpellCount})"));
                    break;
                case CustomRoles.Collector:
                    ProgressText.Append(Collector.GetProgressText(playerId));
                    break;
                case CustomRoles.Taskinator:
                    ProgressText.Append(Taskinator.GetProgressText(playerId));
                    break;
                case CustomRoles.Benefactor:
                    ProgressText.Append(Benefactor.GetProgressText(playerId));
                    break;
                case CustomRoles.Eraser:
                    ProgressText.Append(Eraser.GetProgressText(playerId));
                    break;
                case CustomRoles.Cleanser:
                    ProgressText.Append(Cleanser.GetProgressText(playerId));
                    break;
                case CustomRoles.Keeper:
                    ProgressText.Append(Keeper.GetProgressText(playerId, comms));
                    break;
                case CustomRoles.Anonymous:
                    ProgressText.Append(Anonymous.GetHackLimit(playerId));
                    break;
                case CustomRoles.Killer:
                    ProgressText.Append(FFAManager.GetDisplayScore(playerId));
                    break;
                case CustomRoles.Totocalcio:
                    ProgressText.Append(Totocalcio.GetProgressText(playerId));
                    break;
                case CustomRoles.Romantic:
                    ProgressText.Append(Romantic.GetProgressText(playerId));
                    break;
                case CustomRoles.VengefulRomantic:
                    ProgressText.Append(VengefulRomantic.GetProgressText(playerId));
                    break;
                case CustomRoles.Succubus:
                    ProgressText.Append(Succubus.GetCharmLimit());
                    break;
                case CustomRoles.CursedSoul:
                    ProgressText.Append(CursedSoul.GetCurseLimit());
                    break;
                case CustomRoles.Retributionist:
                    ProgressText.Append(Retributionist.GetRetributeLimit(playerId));
                    break;
                case CustomRoles.Nemesis:
                    ProgressText.Append(Nemesis.GetRevengeLimit(playerId));
                    break;
                case CustomRoles.Warden:
                    ProgressText.Append(Warden.GetNotifyLimit(playerId));
                    break;
                case CustomRoles.Admirer:
                    ProgressText.Append(Admirer.GetAdmireLimit(playerId));
                    break;
                case CustomRoles.Infectious:
                    ProgressText.Append(Infectious.GetBiteLimit());
                    break;
                case CustomRoles.Monarch:
                    ProgressText.Append(Monarch.GetKnightLimit());
                    break;
                case CustomRoles.Deputy:
                    ProgressText.Append(Deputy.GetHandcuffLimit());
                    break;
                case CustomRoles.Investigator:
                    ProgressText.Append(Investigator.GetInvestigateLimit(playerId));
                    break;
                case CustomRoles.Virus:
                    ProgressText.Append(Virus.GetInfectLimit());
                    break;
                case CustomRoles.EvilDiviner:
                    ProgressText.Append(EvilDiviner.GetDivinationCount(playerId));
                    break;
                case CustomRoles.PotionMaster:
                    ProgressText.Append(PotionMaster.GetRitualCount(playerId));
                    break;
                case CustomRoles.Jackal:
                    if (Jackal.CanRecruitSidekick.GetBool())
                        ProgressText.Append(Jackal.GetRecruitLimit(playerId));
                    break;
                case CustomRoles.Bandit:
                    ProgressText.Append(Bandit.GetStealLimit(playerId));
                    break;
                case CustomRoles.Doppelganger:
                    ProgressText.Append(Doppelganger.GetStealLimit(playerId));
                    break;
                case CustomRoles.Spiritcaller:
                    ProgressText.Append(Spiritcaller.GetSpiritLimit());
                    break;
                case CustomRoles.Swapper:
                    ProgressText.Append(Swapper.GetSwappermax(playerId));
                    break;
                case CustomRoles.ChiefOfPolice:
                    ProgressText.Append(ChiefOfPolice.GetSkillLimit(playerId));
                    break;
                case CustomRoles.Spy:
                    ProgressText.Append(Spy.GetProgressText(playerId, comms));
                    break;
                case CustomRoles.NiceMini:
                case CustomRoles.EvilMini:
                    ProgressText.Append(Mini.GetAge(playerId));
                    break;
                default:
                    //タスクテキスト
                    var taskState = Main.PlayerStates?[playerId].TaskState;
                    if (taskState.hasTasks)
                    {
                        Color TextColor;
                        var info = GetPlayerInfoById(playerId);
                        var TaskCompleteColor = HasTasks(info) ? Color.green : GetRoleColor(role).ShadeColor(0.5f); //タスク完了後の色
                        var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white; //カウントされない人外は白色

                        if (Workhorse.IsThisRole(playerId))
                            NonCompleteColor = Workhorse.RoleColor;

                        var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
                        if (Main.PlayerStates.TryGetValue(playerId, out var ps) && ps.MainRole == CustomRoles.Crewpostor)
                            NormalColor = Color.red;

                        TextColor = comms ? Color.gray : NormalColor;
                        string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";
                        ProgressText.Append(ColorString(TextColor, $"({Completed}/{taskState.AllTasksCount})"));
                    }
                    break;
            }
            if (ProgressText.Length != 0)
                ProgressText.Insert(0, " "); //空じゃなければ空白を追加

            return ProgressText.ToString();
        }
        catch (Exception error)
        {
            Logger.Error(error.ToString(), $"GetProgressText(byte playerId, bool comms = false) - PlayerId: {playerId}, Role: {Main.PlayerStates[playerId].MainRole}");
            return "Error2";
        }
    }
    public static void ShowActiveSettingsHelp(byte PlayerId = byte.MaxValue)
    {
        SendMessage(GetString("CurrentActiveSettingsHelp") + ":", PlayerId);

        if (Options.DisableDevices.GetBool()) { SendMessage(GetString("DisableDevicesInfo"), PlayerId); }
        if (Options.SyncButtonMode.GetBool()) { SendMessage(GetString("SyncButtonModeInfo"), PlayerId); }
        if (Options.SabotageTimeControl.GetBool()) { SendMessage(GetString("SabotageTimeControlInfo"), PlayerId); }
        if (Options.RandomMapsMode.GetBool()) { SendMessage(GetString("RandomMapsModeInfo"), PlayerId); }
        if (Main.EnableGM.Value) { SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId); }
        
        foreach (var role in CustomRolesHelper.AllRoles)
        {
            if (role.IsEnable() && !role.IsVanilla()) SendMessage(GetRoleName(role) + GetRoleMode(role) + GetString(Enum.GetName(typeof(CustomRoles), role) + "InfoLong"), PlayerId);
        }

        if (Options.NoGameEnd.GetBool()) { SendMessage(GetString("NoGameEndInfo"), PlayerId); }
    }
    public static void ShowActiveSettings(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }

        var sb = new StringBuilder();
        sb.Append(" ★ " + GetString("TabGroup.SystemSettings"));
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Tab is TabGroup.SystemSettings && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            sb.Append($"\n{opt.GetName(true)}: {opt.GetString()}");
            //ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append("\n\n ★ " + GetString("TabGroup.GameSettings"));
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Tab is TabGroup.GameSettings && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            sb.Append($"\n{opt.GetName(true)}: {opt.GetString()}");
            //ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        SendMessage(sb.ToString(), PlayerId);
    }
    
    public static void ShowAllActiveSettings(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }
        var sb = new StringBuilder();

        sb.Append(GetString("Settings")).Append(':');
        foreach (var role in Options.CustomRoleCounts.Keys.ToArray())
        {
            if (!role.IsEnable()) continue;
            string mode = GetString($"Chance{role.GetMode()}");
            sb.Append($"\n【{GetRoleName(role)}:{mode} ×{role.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id > 59999 && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            if (opt.Name is "KillFlashDuration" or "RoleAssigningAlgorithm")
                sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
            else
                sb.Append($"\n【{opt.GetName(true)}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        SendMessage(sb.ToString(), PlayerId);
    }
    public static void CopyCurrentSettings()
    {
        var sb = new StringBuilder();
        if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
        {
            ClipboardHelper.PutClipboardString(GetString("Message.HideGameSettings"));
            return;
        }
        sb.Append($"━━━━━━━━━━━━【{GetString("Roles")}】━━━━━━━━━━━━");
        foreach (var role in Options.CustomRoleCounts.Keys.ToArray())
        {
            if (!role.IsEnable()) continue;
            string mode = GetString($"Chance{role.GetMode()}");
            sb.Append($"\n【{GetRoleName(role)}:{mode} ×{role.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━【{GetString("Settings")}】━━━━━━━━━━━━");
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id > 59999 && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            if (opt.Name == "KillFlashDuration")
                sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
            else
                sb.Append($"\n【{opt.GetName(true)}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        ClipboardHelper.PutClipboardString(sb.ToString());
    }
    public static void ShowActiveRoles(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }

        List<string> impsb = [];
        List<string> neutralsb = [];
        List<string> crewsb = [];
        List<string> addonsb = [];

        foreach (var role in CustomRolesHelper.AllRoles)
        {
            string mode = GetString($"Chance{role.GetMode()}");
            if (role.IsEnable())
            {
                if (role.Is(CustomRoles.Lovers)) mode = GetString($"Chance{Options.LoverSpawnChances.GetInt()}");
                else if (role.IsAdditionRole() && Options.CustomAdtRoleSpawnRate.ContainsKey(role))
                {
                    mode = GetString($"Chance{Options.CustomAdtRoleSpawnRate[role].GetFloat()}");

                }
                var roleDisplay = $"{GetRoleName(role)}: {mode} x{role.GetCount()}";
                if (role.IsAdditionRole()) addonsb.Add(roleDisplay);
                else if (role.IsCrewmate()) crewsb.Add(roleDisplay);
                else if (role.IsImpostor() || role.IsMadmate()) impsb.Add(roleDisplay);
                else if (role.IsNeutral()) neutralsb.Add(roleDisplay);
            }
        }

        impsb.Sort();
        crewsb.Sort();
        neutralsb.Sort();
        addonsb.Sort();
        
        SendMessage(string.Join("\n", impsb), PlayerId, ColorString(GetRoleColor(CustomRoles.Impostor), GetString("ImpostorRoles")));
        SendMessage(string.Join("\n", crewsb), PlayerId, ColorString(GetRoleColor(CustomRoles.Crewmate), GetString("CrewmateRoles")));
        SendMessage(string.Join("\n", neutralsb), PlayerId, GetString("NeutralRoles"));
        SendMessage(string.Join("\n", addonsb), PlayerId, GetString("AddonRoles"));
    }
    public static void ShowChildrenSettings(OptionItem option, ref StringBuilder sb, int deep = 0, bool command = false)
    {
        foreach (var opt in option.Children.Select((v, i) => new { Value = v, Index = i + 1 }).ToArray())
        {
            if (command)
            {
                sb.Append("\n\n");
                command = false;
            }

            if (opt.Value.Name == "Maximum") continue; //Maximumの項目は飛ばす
            if (opt.Value.Name == "DisableSkeldDevices" && !GameStates.SkeldIsActive && !GameStates.DleksIsActive) continue;
            if (opt.Value.Name == "DisableMiraHQDevices" && !GameStates.MiraHQIsActive) continue;
            if (opt.Value.Name == "DisablePolusDevices" && !GameStates.PolusIsActive) continue;
            if (opt.Value.Name == "DisableAirshipDevices" && !GameStates.AirshipIsActive) continue;
            if (opt.Value.Name == "PolusReactorTimeLimit" && !GameStates.PolusIsActive) continue;
            if (opt.Value.Name == "AirshipReactorTimeLimit" && !GameStates.AirshipIsActive) continue;
            if (deep > 0)
            {
                sb.Append(string.Concat(Enumerable.Repeat("┃", Mathf.Max(deep - 1, 0))));
                sb.Append(opt.Index == option.Children.Count ? "┗ " : "┣ ");
            }
            sb.Append($"{opt.Value.GetName(true)}: {opt.Value.GetString()}\n");
            if (opt.Value.GetBool()) ShowChildrenSettings(opt.Value, ref sb, deep + 1);
        }
    }
    public static void ShowLastRoles(byte PlayerId = byte.MaxValue)
    {
        if (AmongUsClient.Instance.IsGameStarted)
        {
            SendMessage(GetString("CantUse.lastroles"), PlayerId);
            return;
        }

        var sb = new StringBuilder();

        sb.Append($"<#ffffff>{GetString("RoleSummaryText")}</color><size=70%>");

        List<byte> cloneRoles = new(Main.PlayerStates.Keys);
        foreach (byte id in Main.winnerList.ToArray())
        {
            if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
            sb.Append($"\n<#c4aa02>★</color> ").Append(EndGamePatch.SummaryText[id]/*.RemoveHtmlTags()*/);
            cloneRoles.Remove(id);
        }
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.FFA:
                List<(int, byte)> listFFA = [];
                foreach (byte id in cloneRoles.ToArray())
                {
                    listFFA.Add((FFAManager.GetRankOfScore(id), id));
                }
                listFFA.Sort();
                foreach ((int, byte) id in listFFA.ToArray())
                {
                    sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id.Item2]);
                }
                break;
            default: // Normal game
                foreach (byte id in cloneRoles.ToArray())
                {
                    if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>"))
                        continue;
                    sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id]);
                }
                break;
        }
        sb.Append("</size>");
        string lr = sb.ToString();
        if (lr.Length > 1200) lr = lr.RemoveHtmlTags();
        SendMessage("\n", PlayerId, lr);
    }
    public static void ShowKillLog(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.killlog"), PlayerId);
            return;
        }
        if (EndGamePatch.KillLog != "") 
        {
            string kl = EndGamePatch.KillLog;
            if (Options.OldKillLog.GetBool() || kl.Length > 1200) kl = kl.RemoveHtmlTags();
            SendMessage(kl, PlayerId); 
        }
    }
    public static void ShowLastResult(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.lastresult"), PlayerId);
            return;
        }
        var sb = new StringBuilder();
        if (SetEverythingUpPatch.LastWinsText != "") sb.Append($"{GetString("LastResult")}: {SetEverythingUpPatch.LastWinsText}");
        if (SetEverythingUpPatch.LastWinsReason != "") sb.Append($"\n{GetString("LastEndReason")}: {SetEverythingUpPatch.LastWinsReason}");
        if (sb.Length > 0 && Options.CurrentGameMode != CustomGameMode.FFA) SendMessage(sb.ToString(), PlayerId);
    }
    public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
    {
        var SubRoles = Main.PlayerStates[id].SubRoles;
        if (SubRoles.Count == 0 && intro == false) return "";
        var sb = new StringBuilder();

        if (summary)
            sb.Append(' ');

        foreach (var role in SubRoles.ToArray())
        {
            if (role is CustomRoles.NotAssigned or
                        CustomRoles.LastImpostor) continue;
            if (summary && role is CustomRoles.Madmate or CustomRoles.Charmed or CustomRoles.Recruit or CustomRoles.Admired or CustomRoles.Infected or CustomRoles.Contagious or CustomRoles.Soulless) continue;

            var RoleColor = GetRoleColor(role);
            var RoleText = disableColor ? GetRoleName(role) : ColorString(RoleColor, GetRoleName(role));
            
            if (summary)
                sb.Append($"{ColorString(RoleColor, "(")}{RoleText}{ColorString(RoleColor, ")")}");
            else
                sb.Append($"{ColorString(Color.white, " + ")}{RoleText}");
        }

        if (intro && !SubRoles.Contains(CustomRoles.Lovers) && !SubRoles.Contains(CustomRoles.Ntr) && CustomRoles.Ntr.RoleExist())
        {
            var RoleText = disableColor ? GetRoleName(CustomRoles.Lovers) : ColorString(GetRoleColor(CustomRoles.Lovers), GetRoleName(CustomRoles.Lovers));
            sb.Append($"{ColorString(Color.white, " + ")}{RoleText}");
        }

        return sb.ToString();
    }

    public static byte MsgToColor(string text, bool isHost = false)
    {
        text = text.ToLowerInvariant();
        text = text.Replace("色", string.Empty);
        int color;
        try { color = int.Parse(text); } catch { color = -1; }
        switch (text)
        {
            case "0":
            case "红":
            case "紅":
            case "red":
            case "Red":
            case "vermelho":
            case "Vermelho":
            case "крас":
            case "Крас":
            case "красн":
            case "Красн":
            case "красный":
            case "Красный":
                color = 0; break;
            case "1":
            case "蓝":
            case "藍":
            case "深蓝":
            case "blue":
            case "Blue":
            case "azul":
            case "Azul":
            case "син":
            case "Син":
            case "синий":
            case "Синий":
                color = 1; break;
            case "2":
            case "绿":
            case "綠":
            case "深绿":
            case "green":
            case "Green":
            case "verde-escuro":
            case "Verde-Escuro":
            case "Зел":
            case "зел":
            case "Зелёный":
            case "Зеленый":
            case "зелёный":
            case "зеленый":
                color = 2; break;
            case "3":
            case "粉红":
            case "pink":
            case "Pink":
            case "rosa":
            case "Rosa":
            case "Роз":
            case "роз":
            case "Розовый":
            case "розовый":
                color = 3; break;
            case "4":
            case "橘":
            case "orange":
            case "Orange":
            case "laranja":
            case "Laranja":
            case "оранж":
            case "Оранж":
            case "оранжевый":
            case "Оранжевый":
                color = 4; break;
            case "5":
            case "黄":
            case "黃":
            case "yellow":
            case "Yellow":
            case "amarelo":
            case "Amarelo":
            case "Жёлт":
            case "Желт":
            case "жёлт":
            case "желт":
            case "Жёлтый":
            case "Желтый":
            case "жёлтый":
            case "желтый":
                color = 5; break;
            case "6":
            case "黑":
            case "black":
            case "Black":
            case "preto":
            case "Preto":
            case "Чёрн":
            case "Черн":
            case "Чёрный":
            case "Черный":
            case "чёрный":
            case "черный":
                color = 6; break;
            case "7":
            case "白":
            case "white":
            case "White":
            case "branco":
            case "Branco":
            case "Белый":
            case "белый":
                color = 7; break;
            case "8":
            case "紫":
            case "purple":
            case "Purple":
            case "roxo":
            case "Roxo":
            case "Фиол":
            case "фиол":
            case "Фиолетовый":
            case "фиолетовый":
                color = 8; break;
            case "9":
            case "棕":
            case "brown":
            case "Brown":
            case "marrom":
            case "Marrom":
            case "Корич":
            case "корич":
            case "Коричневый":
            case "коричевый":
                color = 9; break;
            case "10":
            case "青":
            case "cyan":
            case "Cyan":
            case "ciano":
            case "Ciano":
            case "Голуб":
            case "голуб":
            case "Голубой":
            case "голубой":
                color = 10; break;
            case "11":
            case "黄绿":
            case "黃綠":
            case "浅绿":
            case "lime":
            case "Lime":
            case "verde-claro":
            case "Verde-Claro":
            case "Лайм":
            case "лайм":
            case "Лаймовый":
            case "лаймовый":
                color = 11; break;
            case "12":
            case "红褐":
            case "紅褐":
            case "深红":
            case "maroon":
            case "Maroon":
            case "bordô":
            case "Bordô":
            case "vinho":
            case "Vinho":
            case "Борд":
            case "борд":
            case "Бордовый":
            case "бордовый":
                color = 12; break;
            case "13":
            case "玫红":
            case "玫紅":
            case "浅粉":
            case "rose":
            case "Rose":
            case "rosa-claro":
            case "Rosa-Claro":
            case "Светло роз":
            case "светло роз":
            case "Светло розовый":
            case "светло розовый":
            case "Сирень":
            case "сирень":
            case "Сиреневый":
            case "сиреневый":
                color = 13; break;
            case "14":
            case "焦黄":
            case "焦黃":
            case "淡黄":
            case "banana":
            case "Banana":
            case "Банан":
            case "банан":
            case "Банановый":
            case "банановый":
                color = 14; break;
            case "15":
            case "灰":
            case "gray":
            case "Gray":
            case "cinza":
            case "Cinza":
            case "grey":
            case "Grey":
            case "Сер":
            case "сер":
            case "Серый":
            case "серый":
                color = 15; break;
            case "16":
            case "茶":
            case "tan":
            case "Tan":
            case "bege":
            case "Bege":
            case "Загар":
            case "загар":
            case "Загаровый":
            case "загаровый":
                color = 16; break;
            case "17":
            case "珊瑚":
            case "coral":
            case "Coral":
            case "salmão":
            case "Salmão":
            case "Корал":
            case "корал":
            case "Коралл":
            case "коралл":
            case "Коралловый":
            case "коралловый":
                color = 17; break;
                
            case "18": case "隐藏": case "?": color = 18; break;
        }
        return !isHost && color == 18 ? byte.MaxValue : color is < 0 or > 18 ? byte.MaxValue : Convert.ToByte(color);
    }

    public static void ShowHelpToClient(byte ID)
    {
        SendMessage(
            GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /xf {GetString("Command.solvecover")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
            + $"\n ○ /death {GetString("Command.death")}"
     //       + $"\n ○ /icons {GetString("Command.iconinfo")}"
            , ID);
    }
    public static void ShowHelp(byte ID)
    {
        SendMessage(
            GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
       //     + $"\n  ○ /icons {GetString("Command.iconinfo")}"
            + $"\n  ○ /death {GetString("Command.death")}"
            + "\n\n" + GetString("CommandHostList")
            + $"\n  ○ /s {GetString("Command.say")}"
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /xf {GetString("Command.solvecover")}"
            + $"\n  ○ /mw {GetString("Command.mw")}"
            + $"\n  ○ /kill {GetString("Command.kill")}"
            + $"\n  ○ /exe {GetString("Command.exe")}"
            + $"\n  ○ /level {GetString("Command.level")}"
            + $"\n  ○ /id {GetString("Command.idlist")}"
            + $"\n  ○ /qq {GetString("Command.qq")}"
            + $"\n  ○ /dump {GetString("Command.dump")}"
        //    + $"\n  ○ /iconhelp {GetString("Command.iconhelp")}"
            , ID);
    }
    public static void CheckTerroristWin(GameData.PlayerInfo Terrorist)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var taskState = GetPlayerById(Terrorist.PlayerId).GetPlayerTaskState();
        if (taskState.IsTaskFinished && (!Main.PlayerStates[Terrorist.PlayerId].IsSuicide || Options.CanTerroristSuicideWin.GetBool())) //タスクが完了で（自殺じゃない OR 自殺勝ちが許可）されていれば
        {
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc.Is(CustomRoles.Terrorist))
                {
                    if (Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote)
                    {
                        //追放された場合は生存扱い
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.etc;
                        //生存扱いのためSetDeadは必要なし
                    }
                    else
                    {
                        //キルされた場合は自爆扱い
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                    }
                }
                else if (!pc.Data.IsDead)
                {
                    //生存者は爆死
                    pc.SetRealKiller(Terrorist.Object);
                    Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    Main.PlayerStates[pc.PlayerId].SetDead();
                    pc.RpcMurderPlayerV3(pc);
                    
                }
            }
            if (!CustomWinnerHolder.CheckForConvertedWinner(Terrorist.PlayerId))
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Terrorist);
                CustomWinnerHolder.WinnerIds.Add(Terrorist.PlayerId);
            }
        }
    }
    public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "", bool logforChatManager = false, bool replay = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        // set replay to true when you want to send previous sys msg or do not want to add a sys msg in the history
        if (!replay && GameStates.IsInGame) ChatManager.AddSystemChatHistory(sendTo, text);

        if (title == "") title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";

        if (!logforChatManager)
            ChatManager.AddToHostMessage(text.RemoveHtmlTagsTemplate());

        Main.MessagesToSend.Add((text.RemoveHtmlTagsTemplate(), sendTo, title));
    }
    public static bool IsPlayerModerator(string friendCode)
    {
        if (friendCode == "") return false;
        var friendCodesFilePath = @"./TOHE-DATA/Moderators.txt";
        var friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Any(code => code.Contains(friendCode));
    }
    public static bool IsPlayerVIP(string friendCode)
    {
        if (friendCode == "") return false;
        var friendCodesFilePath = @"./TOHE-DATA/VIP-List.txt";
        var friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Any(code => code.Contains(friendCode));
    }
    public static bool CheckColorHex(string ColorCode)
    {
        Regex regex = new("^[0-9A-Fa-f]{6}$");
        if (!regex.IsMatch(ColorCode)) return false;
        return true;
    }
    public static bool CheckGradientCode(string ColorCode)
    {
        Regex regex = new(@"^[0-9A-Fa-f]{6}\s[0-9A-Fa-f]{6}$");
        if (!regex.IsMatch(ColorCode)) return false;
        return true;
    }
    public static string GradientColorText(string startColorHex, string endColorHex, string text)
    {
        if (startColorHex.Length != 6 || endColorHex.Length != 6)
        {
            Logger.Error("Invalid color hex code. Hex code should be 6 characters long (without #) (e.g., FFFFFF).", "GradientColorText");
            //throw new ArgumentException("Invalid color hex code. Hex code should be 6 characters long (e.g., FFFFFF).");
            return text;
        }

        Color startColor = HexToColor(startColorHex);
        Color endColor = HexToColor(endColorHex);

        int textLength = text.Length;
        float stepR = (endColor.r - startColor.r) / textLength;
        float stepG = (endColor.g - startColor.g) / textLength;
        float stepB = (endColor.b - startColor.b) / textLength;
        float stepA = (endColor.a - startColor.a) / textLength;

        string gradientText = "";

        for (int i = 0; i < textLength; i++)
        {
            float r = startColor.r + (stepR * i);
            float g = startColor.g + (stepG * i);
            float b = startColor.b + (stepB * i);
            float a = startColor.a + (stepA * i);


            string colorHex = ColorToHex(new Color(r, g, b, a));
            //Logger.Msg(colorHex, "color");
            gradientText += $"<color=#{colorHex}>{text[i]}</color>";
        }

        return gradientText;
    }

    private static Color HexToColor(string hex)
    {
        _ = ColorUtility.TryParseHtmlString("#" + hex, out var color);
        return color;
    }

    private static string ColorToHex(Color color)
    {
        Color32 color32 = (Color32)color;
        return $"{color32.r:X2}{color32.g:X2}{color32.b:X2}{color32.a:X2}";
    }
    public static void ApplySuffix(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null || Main.AutoMuteUs.Value) return;
        
        if (!(player.AmOwner || (player.FriendCode.GetDevUser().HasTag())))
        {
            if (!IsPlayerModerator(player.FriendCode) && !IsPlayerVIP(player.FriendCode))
            {
                string name1 = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var n1) ? n1 : "";
                if (GameStates.IsLobby && name1 != player.name && player.CurrentOutfitType == PlayerOutfitType.Default) player.RpcSetName(name1);
                return;
            }
        }
        string name = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var n) ? n : "";
        if (Main.nickName != "" && player.AmOwner) name = Main.nickName;
        if (name == "") return;
        if (AmongUsClient.Instance.IsGameStarted)
        {
            if (Options.FormatNameMode.GetInt() == 1 && Main.nickName == "") name = Palette.GetColorName(player.Data.DefaultOutfit.ColorId);
        }
        else
        {
            if (!GameStates.IsLobby) return;
            if (player.AmOwner)
            {
                if (!player.IsModClient()) return;
                {
                    if ((GameStates.IsOnlineGame || GameStates.IsLocalGame) && !Options.HideHostText.GetBool())
                        name = $"<color={GetString("HostColor")}>{GetString("HostText")}</color><color={GetString("IconColor")}>{GetString("Icon")}</color><color={GetString("NameColor")}>{name}</color>";

                    //name = $"<color=#902efd>{GetString("HostText")}</color><color=#4bf4ff>♥</color>" + name;
                }
                if (Options.CurrentGameMode == CustomGameMode.FFA)
                    name = $"<color=#00ffff><size=1.7>{GetString("ModeFFA")}</size></color>\r\n" + name;
            }
            var modtag = "";
            if (Options.ApplyVipList.GetValue() == 1 && player.FriendCode != PlayerControl.LocalPlayer.FriendCode)
            {
                if (IsPlayerVIP(player.FriendCode))
                {
                    string colorFilePath = @$"./TOHE-DATA/Tags/VIP_TAGS/{player.FriendCode}.txt";
                    //static color
                    if (!Options.GradientTagsOpt.GetBool())
                    { 
                        string startColorCode = "ffff00";
                        if (File.Exists(colorFilePath))
                        {
                            string ColorCode = File.ReadAllText(colorFilePath);
                            _ = ColorCode.Trim();
                            if (CheckColorHex(ColorCode)) startColorCode = ColorCode;
                        }
                        //"ffff00"
                        modtag = $"<color=#{startColorCode}>{GetString("VipTag")}</color>";
                        }
                    else //gradient color
                    {
                        string startColorCode = "ffff00";
                        string endColorCode = "ffff00";
                        string ColorCode = "";
                        if (File.Exists(colorFilePath))
                        {
                            ColorCode = File.ReadAllText(colorFilePath);
                            if (ColorCode.Split(" ").Length == 2)
                            {
                                startColorCode = ColorCode.Split(" ")[0];
                                endColorCode = ColorCode.Split(" ")[1];
                            }
                        }
                        if (!CheckGradientCode(ColorCode))
                        {
                            startColorCode = "ffff00";
                            endColorCode = "ffff00";
                        }
                        //"33ccff", "ff99cc"
                        if (startColorCode == endColorCode) modtag = $"<color=#{startColorCode}>{GetString("VipTag")}</color>";

                        else modtag = GradientColorText(startColorCode, endColorCode, GetString("VipTag"));
                    }
                }
            }
            if (Options.ApplyModeratorList.GetValue() == 1 && player.FriendCode != PlayerControl.LocalPlayer.FriendCode)
            {
                if (IsPlayerModerator(player.FriendCode))
                {
                    string colorFilePath = @$"./TOHE-DATA/Tags/MOD_TAGS/{player.FriendCode}.txt";
                    //static color
                    if (!Options.GradientTagsOpt.GetBool())
                    { 
                        string startColorCode = "8bbee0";
                        if (File.Exists(colorFilePath))
                        {
                            string ColorCode = File.ReadAllText(colorFilePath);
                            _ = ColorCode.Trim();
                            if (CheckColorHex(ColorCode)) startColorCode = ColorCode;
                        }
                        //"33ccff", "ff99cc"
                        modtag = $"<color=#{startColorCode}>{GetString("ModTag")}</color>";
                    }
                    else //gradient color
                    {
                        string startColorCode = "8bbee0";
                        string endColorCode = "8bbee0";
                        string ColorCode = "";
                        if (File.Exists(colorFilePath))
                        {
                            ColorCode = File.ReadAllText(colorFilePath);
                            if (ColorCode.Split(" ").Length == 2)
                            {
                                startColorCode = ColorCode.Split(" ")[0];
                                endColorCode = ColorCode.Split(" ")[1];
                            }
                        }
                        if (!CheckGradientCode(ColorCode))
                        {
                            startColorCode = "8bbee0";
                            endColorCode = "8bbee0";
                        }
                        //"33ccff", "ff99cc"
                        if (startColorCode == endColorCode) modtag = $"<color=#{startColorCode}>{GetString("ModTag")}</color>";

                        else modtag = GradientColorText(startColorCode, endColorCode, GetString("ModTag"));
                    }
                }
            }
            if (player.AmOwner)
            {
                name = Options.GetSuffixMode() switch
                {
                    SuffixModes.TOHE => name += $"\r\n<color={Main.ModColor}>TOHE v{Main.PluginDisplayVersion}</color>",
                    SuffixModes.Streaming => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Streaming")}</color></size>",
                    SuffixModes.Recording => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Recording")}</color></size>",
                    SuffixModes.RoomHost => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.RoomHost")}</color></size>",
                    SuffixModes.OriginalName => name += $"\r\n<size=1.7><color={Main.ModColor}>{DataManager.player.Customization.Name}</color></size>",
                    SuffixModes.DoNotKillMe => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.DoNotKillMe")}</color></size>",
                    SuffixModes.NoAndroidPlz => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.NoAndroidPlz")}</color></size>",
                    SuffixModes.AutoHost => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.AutoHost")}</color></size>",
                    _ => name
                };
            }

            if (!name.Contains($"\r\r") && player.FriendCode.GetDevUser().HasTag() && (player.AmOwner || player.IsModClient()))
            {
                name = player.FriendCode.GetDevUser().GetTag() + "<size=1.5>" + modtag + "</size>" + name;
            }
            else name = modtag + name;
        }
        if (name != player.name && player.CurrentOutfitType == PlayerOutfitType.Default)
            player.RpcSetName(name);
    }
    public static PlayerControl GetPlayerById(int PlayerId)
    {
        return Main.AllPlayerControls.FirstOrDefault(pc => pc.PlayerId == PlayerId);
    }
    public static PlayerControl GetPlayerByRole(CustomRoles Role)
    {
        return Main.AllPlayerControls.FirstOrDefault(pc => pc.GetCustomRole() == Role);
    }
    public static GameData.PlayerInfo GetPlayerInfoById(int PlayerId) =>
        GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == PlayerId);
    private static readonly StringBuilder SelfSuffix = new();
    private static readonly StringBuilder SelfMark = new(20);
    private static readonly StringBuilder TargetSuffix = new();
    private static readonly StringBuilder TargetMark = new(20);
    public static async void NotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool MushroomMixupIsActive = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Main.AllPlayerControls == null) return;
        if (GameStates.IsHideNSeek) return;

        //Do not update NotifyRoles during meetings
        if (GameStates.IsMeeting) return;

        //var caller = new System.Diagnostics.StackFrame(1, false);
        //var callerMethod = caller.GetMethod();
        //string callerMethodName = callerMethod.Name;
        //string callerClassName = callerMethod.DeclaringType.FullName;
        //Logger.Info($" Was called from: {callerClassName}.{callerMethodName}", "NotifyRoles");

        await DoNotifyRoles(isForMeeting, SpecifySeer, SpecifyTarget, NoCache, ForceLoop, CamouflageIsForMeeting, MushroomMixupIsActive);
    }
    public static Task DoNotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool NoCache = false, bool ForceLoop = false, bool CamouflageIsForMeeting = false, bool MushroomMixupIsActive = false)
    {
        if (!AmongUsClient.Instance.AmHost) return Task.CompletedTask;
        if (Main.AllPlayerControls == null) return Task.CompletedTask;
        if (GameStates.IsHideNSeek) return Task.CompletedTask;

        //Do not update NotifyRoles during meetings
        if (GameStates.IsMeeting) return Task.CompletedTask;

        //var logger = Logger.Handler("DoNotifyRoles");

        HudManagerPatch.NowCallNotifyRolesCount++;
        HudManagerPatch.LastSetNameDesyncCount = 0;

        PlayerControl[] seerList = SpecifySeer != null 
            ? ([SpecifySeer]) 
            : Main.AllPlayerControls;

        PlayerControl[] targetList = SpecifyTarget != null
            ? ([SpecifyTarget])
            : Main.AllPlayerControls;

        if (!MushroomMixupIsActive)
        {
            MushroomMixupIsActive = IsActive(SystemTypes.MushroomMixupSabotage);
        }

        Logger.Info($" START - Count Seers: {seerList.Length} & Count Target: {targetList.Length}", "DoNotifyRoles");

        //seer: player who updates the nickname/role/mark
        //target: seer updates nickname/role/mark of other targets
        foreach (var seer in seerList)
        {
            // Do nothing when the seer is not present in the game
            if (seer == null || seer.Data.Disconnected) continue;
            
            // Only non-modded players
            if (seer.IsModClient()) continue;

            // Size of player roles
            string fontSize = "1.5";
            if (isForMeeting && (seer.GetClient().PlatformData.Platform == Platforms.Playstation || seer.GetClient().PlatformData.Platform == Platforms.Switch)) fontSize = "70%";
            
            //logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole() + ":START");

            var seerRole = seer.GetCustomRole();

            // Hide player names in during Mushroom Mixup if seer is alive and desync impostor
            if (!CamouflageIsForMeeting && MushroomMixupIsActive && seer.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
            {
                seer.RpcSetNamePrivate("<size=0%>", true, force: NoCache);
            }
            else
            {
                // Clear marker after name seer
                SelfMark.Clear();

                // ====== Add SelfMark for seer ======

                if (seer.Is(CustomRoles.Lovers) /* || CustomRoles.Ntr.RoleExist() */)
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), "♥"));

                if (seer.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));

                if (seer.Is(CustomRoles.Cyber) && Cyber.CyberKnown.GetBool())
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Cyber), "★"));

                if (Blackmailer.ForBlackmailer.Contains(seer.PlayerId))
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Blackmailer), "╳"));

                if (BallLightning.IsEnable && BallLightning.IsGhost(seer))
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                if (Medic.IsEnable && (Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtected == seer.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() is 0 or 2))
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), "✚"));

                if (Snitch.IsEnable)
                    SelfMark.Append(Snitch.GetWarningArrow(seer));

                if (CustomRoles.Solsticer.RoleExist())
                    SelfMark.Append(Solsticer.GetWarningArrow(seer, seer));

                if (Gamer.IsEnable)
                    SelfMark.Append(Gamer.TargetMark(seer, seer));

                if (Sniper.IsEnable)
                    SelfMark.Append(Sniper.GetShotNotify(seer.PlayerId));

                if (Romantic.IsEnable)
                    SelfMark.Append(Romantic.SelfMark(seer));

                // ====== Add SelfSuffix for seer ======

                SelfSuffix.Clear();

                if (Deathpact.IsEnable)
                    SelfSuffix.Append(Deathpact.GetDeathpactPlayerArrow(seer));


                if (!isForMeeting) // Only during game
                {
                    switch (seerRole)
                    {
                        case CustomRoles.BountyHunter:
                            SelfSuffix.Append(BountyHunter.GetTargetText(seer, false));
                            SelfSuffix.Append(BountyHunter.GetTargetArrow(seer));
                            break;

                        case CustomRoles.EvilTracker:
                            SelfSuffix.Append(EvilTracker.GetTargetArrow(seer, seer));
                            break;

                        case CustomRoles.Fireworker:
                            SelfSuffix.Append(Fireworker.GetStateText(seer));
                            break;

                        case CustomRoles.AntiAdminer:
                            SelfSuffix.Append(AntiAdminer.GetSuffix());
                            break;

                        case CustomRoles.Snitch:
                            SelfSuffix.Append(Snitch.GetSnitchArrow(seer));
                            break;

                        case CustomRoles.Mortician:
                            SelfSuffix.Append(Mortician.GetTargetArrow(seer));
                            break;

                        case CustomRoles.Bloodhound:
                            SelfSuffix.Append(Bloodhound.GetTargetArrow(seer));
                            break;

                        case CustomRoles.Tracefinder:
                            SelfSuffix.Append(Tracefinder.GetTargetArrow(seer));
                            break;

                        case CustomRoles.Tracker:
                            SelfSuffix.Append(Tracker.GetTrackerArrow(seer));
                            break;

                        case CustomRoles.Spiritualist:
                            SelfSuffix.Append(Spiritualist.GetSpiritualistArrow(seer));
                            break;

                        case CustomRoles.Monitor:
                            SelfSuffix.Append(Monitor.GetSuffix());
                            break;

                        case CustomRoles.Vulture:
                            if (Vulture.ArrowsPointingToDeadBody.GetBool())
                                SelfSuffix.Append(Vulture.GetTargetArrow(seer));
                            break;

                        case CustomRoles.Witch:
                            SelfSuffix.Append(Witch.GetSpellModeText(seer, false));
                            break;

                        case CustomRoles.HexMaster:
                            SelfSuffix.Append(HexMaster.GetHexModeText(seer, false));
                            break;

                        //case CustomRoles.Occultist:
                        //    SelfSuffix.Append(Occultist.GetHexModeText(seer, false));
                        //    break;

                        case CustomRoles.PlagueDoctor:
                            SelfSuffix.Append(PlagueDoctor.GetLowerTextOthers(seer));
                            break;
                    }
                }
                else // Only during meeting
                {
                    if (seer.IsAlive())
                    {
                        if (Shroud.IsEnable && Shroud.ShroudList.ContainsKey(seer.PlayerId))
                            SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Shroud), "◈"));
                    }

                    if (Pirate.IsEnable && seer.PlayerId == Pirate.PirateTarget)
                        SelfMark.Append(Pirate.GetPlunderedMark(seer.PlayerId, true));

                    if (Witch.IsEnable)
                        SelfMark.Append(Witch.GetSpelledMark(seer.PlayerId, true));

                    if (HexMaster.IsEnable)
                        SelfMark.Append(HexMaster.GetHexedMark(seer.PlayerId, true));

                    //SelfMark.Append(Occultist.GetCursedMark(seer.PlayerId, true));
                }
                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.FFA:
                        SelfSuffix.Append(FFAManager.GetPlayerArrow(seer));
                        break;
                }


                // ====== Get SeerRealName ======

                string SeerRealName = seer.GetRealName(isForMeeting);

                if (MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool() && !isForMeeting && Options.CurrentGameMode != CustomGameMode.FFA)
                {
                    var SeerRoleInfo = seer.GetRoleInfo();

                    if (seerRole.IsImpostor())
                        SeerRealName = $"<size=110%><color=#ff1919>" + GetString("YouAreImpostor") + $"</color></size>\n<size=130%>" + SeerRoleInfo + $"</size>";

                    else if (seerRole.IsCrewmate() && !seer.Is(CustomRoles.Madmate))
                        SeerRealName = $"<size=110%><color=#8cffff>" + GetString("YouAreCrewmate") + $"</color></size>\n" + SeerRoleInfo;

                    else if (seerRole.IsNeutral() && !seerRole.IsMadmate())
                        SeerRealName = $"<size=110%><color=#7f8c8d>" + GetString("YouAreNeutral") + $"</color></size>\n<size=130%>" + SeerRoleInfo + $"</size>";

                    else if (seerRole.IsMadmate() || seerRole == CustomRoles.Madmate)
                        SeerRealName = $"<size=110%><color=#ff1919>" + GetString("YouAreMadmate") + $"</color></size>\n<size=130%>" + SeerRoleInfo + $"</size>";
                }

                // ====== Combine SelfRoleName, SelfTaskText, SelfName, SelfDeathReason for seer ======

                string SelfTaskText = GetProgressText(seer);
                string SelfRoleName = $"<size={fontSize}>{seer.GetDisplayRoleAndSubName(seer, false)}{SelfTaskText}</size>";
                string SelfDeathReason = seer.KnowDeathReason(seer) ? $"({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))})" : "";
                string SelfName = $"{ColorString(seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";

                switch (seerRole)
                {
                    case CustomRoles.PlagueBearer:
                        if (PlagueBearer.IsPlaguedAll(seer))
                        {
                            seer.RpcSetCustomRole(CustomRoles.Pestilence);
                            seer.Notify(GetString("PlagueBearerToPestilence"));
                            seer.RpcGuardAndKill(seer);
                            if (!PlagueBearer.PestilenceList.Contains(seer.PlayerId))
                                PlagueBearer.PestilenceList.Add(seer.PlayerId);
                            PlagueBearer.SetKillCooldownPestilence(seer.PlayerId);
                            PlagueBearer.playerIdList.Remove(seer.PlayerId);
                        }
                        break;

                    case CustomRoles.Arsonist:
                        if (seer.IsDouseDone())
                            SelfName = $"{ColorString(seer.GetRoleColor(), GetString("EnterVentToWin"))}";
                        break;

                    case CustomRoles.Revolutionist:
                        if (seer.IsDrawDone())
                            SelfName = $">{ColorString(seer.GetRoleColor(), string.Format(GetString("EnterVentWinCountDown"), Main.RevolutionistCountdown.TryGetValue(seer.PlayerId, out var x) ? x : 10))}";
                        break;
                }

                if (Pelican.IsEnable && Pelican.IsEaten(seer.PlayerId))
                    SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";

                if (Deathpact.IsEnable && Deathpact.IsInActiveDeathpact(seer))
                    SelfName = Deathpact.GetDeathpactString(seer);

                if (NameNotifyManager.GetNameNotify(seer, out var name))
                    SelfName = name;

                // Devourer
                if (Devourer.IsEnable)
                {
                    bool playerDevoured = Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(seer.PlayerId));
                    
                    if (playerDevoured && !CamouflageIsForMeeting)
                        SelfName = GetString("DevouredName");
                }

                // Camouflage
                if (!CamouflageIsForMeeting && ((IsActive(SystemTypes.Comms) && Camouflage.IsActive) || Camouflager.AbilityActivated))
                    SelfName = $"<size=0%>{SelfName}</size>";


                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.FFA:
                        FFAManager.GetNameNotify(seer, ref SelfName);
                        SelfName = $"<size={fontSize}>{SelfTaskText}</size>\r\n{SelfName}";
                        break;
                    default:
                        SelfName = SelfRoleName + "\r\n" + SelfName;
                        break;
                }
                SelfName += SelfSuffix.ToString() == "" ? "" : "\r\n " + SelfSuffix.ToString();

                if (!isForMeeting) SelfName += "\r\n";

                seer.RpcSetNamePrivate(SelfName, true, force: NoCache);
            }

            // Start run loop for target only if condition is "true"
            if (seer.Data.IsDead || !seer.IsAlive()
                || seerList.Length == 1
                || targetList.Length == 1
                || MushroomMixupIsActive
                || NoCache
                || ForceLoop)
            {
                foreach (var target in targetList)
                {
                    // if the target is the seer itself, do nothing
                    if (target.PlayerId == seer.PlayerId) continue;

                    //logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole() + ":START");

                    // Hide player names in during Mushroom Mixup if seer is alive and desync impostor
                    if (!CamouflageIsForMeeting && MushroomMixupIsActive && target.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
                    {
                        seer.RpcSetNamePrivate("<size=0%>", true, force: NoCache);
                    }
                    else
                    {
                        seerRole = seer.GetCustomRole();

                        // ====== Add TargetMark for target ======

                        TargetMark.Clear();

                        if (isForMeeting)
                        {
                            if (Witch.IsEnable)
                                TargetMark.Append(Witch.GetSpelledMark(target.PlayerId, true));

                            if (HexMaster.IsEnable)
                                TargetMark.Append(HexMaster.GetHexedMark(target.PlayerId, true));

                            //if (Occultist.IsEnable)
                            //    TargetMark.Append(Occultist.GetCursedMark(target.PlayerId, true));

                            if (Pirate.IsEnable)
                                TargetMark.Append(Pirate.GetPlunderedMark(target.PlayerId, true));

                            if (Shroud.IsEnable && !seer.Is(CustomRoles.Shroud) && target.IsAlive())
                                TargetMark.Append(Shroud.GetShroudMark(target.PlayerId, true));
                        }

                        if (Mini.IsEnable && Mini.EveryoneCanKnowMini.GetBool() && (target.Is(CustomRoles.NiceMini) || target.Is(CustomRoles.EvilMini)))
                        {
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Mini), Mini.Age != 18 && Mini.UpDateAge.GetBool() ? $"({Mini.Age})" : ""));
                        }

                        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetPlayerTaskState().IsTaskFinished)
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));

                        if (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetPlayerTaskState().IsTaskFinished)
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Marshall), "★"));

                        if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));

                        if (Captain.IsEnable)
                            if ((target.PlayerId != seer.PlayerId) && (target.Is(CustomRoles.Captain) && Captain.OptionCrewCanFindCaptain.GetBool()) &&
                                (target.GetPlayerTaskState().CompletedTasksCount >= Captain.OptionTaskRequiredToReveal.GetInt()) &&
                                (seer.GetCustomRole().IsCrewmate() && !seer.Is(CustomRoles.Madmate) || (seer.Is(CustomRoles.Madmate) && Captain.OptionMadmateCanFindCaptain.GetBool())))
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Captain), " ☆"));

                        if (target.Is(CustomRoles.Cyber) && Cyber.CyberKnown.GetBool())
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Cyber), "★"));

                        if (BallLightning.IsEnable && BallLightning.IsGhost(target))
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));

                        if (Snitch.IsEnable)
                            TargetMark.Append(Snitch.GetWarningMark(seer, target));

                        if (target.Is(CustomRoles.Solsticer))
                        {
                            TargetMark.Append(Solsticer.GetWarningArrow(seer, target));
                        }

                        if (Executioner.IsEnable)
                            TargetMark.Append(Executioner.TargetMark(seer, target));

                        if (Gamer.IsEnable)
                            TargetMark.Append(Gamer.TargetMark(seer, target));

                        if (Totocalcio.IsEnable)
                            TargetMark.Append(Totocalcio.TargetMark(seer, target));

                        if (Romantic.IsEnable)
                            TargetMark.Append(Romantic.TargetMark(seer, target));

                        if (Lawyer.IsEnable)
                            TargetMark.Append(Lawyer.LawyerMark(seer, target));

                        if (Deathpact.IsEnable)
                            TargetMark.Append(Deathpact.GetDeathpactMark(seer, target));


                        if (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                        {
                            TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                        }
                        else if (seer.Data.IsDead && !seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                        {
                            TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                        }
                        else if (target.Is(CustomRoles.Ntr) || seer.Is(CustomRoles.Ntr))
                        {
                            TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                        }


                        if (Medic.IsEnable)
                        {
                            if (seer.Is(CustomRoles.Medic) && (Medic.WhoCanSeeProtect.GetInt() is 0 or 1) && (Medic.InProtect(target.PlayerId) || Medic.TempMarkProtected == target.PlayerId))
                            {
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), "✚"));
                            }
                            else if (!seer.IsAlive() && !seer.Is(CustomRoles.Medic) && (Medic.InProtect(target.PlayerId) || Medic.TempMarkProtected == target.PlayerId))
                            {
                                TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Medic), "✚"));
                            }
                        }

                        if (PlagueDoctor.IsEnable)
                        {
                            TargetMark.Append(PlagueDoctor.GetMarkOthers(seer, target));
                        }

                        switch (seerRole)
                        {
                            case CustomRoles.PlagueBearer:
                                if (PlagueBearer.IsPlagued(seer.PlayerId, target.PlayerId))
                                {
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.PlagueBearer)}>●</color>");
                                }
                                break;

                            case CustomRoles.Arsonist:
                                if (seer.IsDousedPlayer(target))
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");

                                if (Main.ArsonistTimer.TryGetValue(seer.PlayerId, out var ar_kvp) && ar_kvp.Item1 == target)
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");
                                break;

                            case CustomRoles.Revolutionist:
                                if (seer.IsDrawPlayer(target))
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");

                                if (Main.RevolutionistTimer.TryGetValue(seer.PlayerId, out var re_kvp) && re_kvp.Item1 == target)
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");
                                break;

                            case CustomRoles.Farseer:
                                if (Farseer.FarseerTimer.TryGetValue(seer.PlayerId, out var fa_kvp) && fa_kvp.Item1 == target)
                                    TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");
                                break;

                            case CustomRoles.Puppeteer:
                                TargetMark.Append(Puppeteer.TargetMark(seer, target));
                                break;

                            case CustomRoles.Shroud:
                                TargetMark.Append(Shroud.TargetMark(seer, target));
                                break;
                        }


                        // ====== Seer know target role ======

                        string TargetRoleText = ExtendedPlayerControl.KnowRoleTarget(seer, target)
                                ? $"<size={fontSize}>{seer.GetDisplayRoleAndSubName(target, false)}{GetProgressText(target)}</size>\r\n" : "";


                        if (seer.IsAlive() && seer.IsRevealedPlayer(target) && target.Is(CustomRoles.Trickster))
                        {
                            TargetRoleText = Farseer.RandomRole[seer.PlayerId];
                            TargetRoleText += Farseer.GetTaskState();
                        }


                        // ====== Target player name ======

                        string TargetPlayerName = target.GetRealName(isForMeeting);


                        // ========= During Game And Meeting =========
                        switch (seerRole)
                        {
                            case CustomRoles.EvilTracker:
                                TargetMark.Append(EvilTracker.GetTargetMark(seer, target));
                                if (isForMeeting && EvilTracker.IsTrackTarget(seer, target) && EvilTracker.CanSeeLastRoomInMeeting)
                                    TargetRoleText = $"<size={fontSize}>{EvilTracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                                break;

                            case CustomRoles.Tracker:
                                TargetMark.Append(Tracker.GetTargetMark(seer, target));
                                if (isForMeeting && Tracker.IsTrackTarget(seer, target) && Tracker.CanSeeLastRoomInMeeting)
                                    TargetRoleText = $"<size={fontSize}>{Tracker.GetArrowAndLastRoom(seer, target)}</size>\r\n";
                                break;

                            case CustomRoles.Lookout:
                                if (seer.IsAlive() && target.IsAlive())
                                    TargetPlayerName = (ColorString(GetRoleColor(CustomRoles.Lookout), " " + target.PlayerId.ToString()) + " " + TargetPlayerName);
                                break;

                            case CustomRoles.Swapper:
                                if (seer.IsAlive() && target.IsAlive())
                                    TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Swapper), target.PlayerId.ToString()) + " " + TargetPlayerName;
                                break;

                            case CustomRoles.Quizmaster:
                                TargetMark.Append(Quizmaster.TargetMark(seer, target));
                                break;

                        }

                        // ========= Only During Meeting =========
                        if (isForMeeting)
                        {
                            switch (seerRole)
                            {
                                case CustomRoles.Psychic:
                                    if (target.IsRedForPsy(seer) && seer.IsAlive())
                                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Impostor), TargetPlayerName);
                                    break;

                                case CustomRoles.Judge:
                                    if (seer.IsAlive() && target.IsAlive())
                                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Judge), target.PlayerId.ToString()) + " " + TargetPlayerName;
                                    break;

                                case CustomRoles.Inspector:
                                    if (seer.IsAlive() && target.IsAlive())
                                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Inspector), target.PlayerId.ToString()) + " " + TargetPlayerName;
                                    break;

                                case CustomRoles.Councillor:
                                    if (seer.IsAlive() && target.IsAlive())
                                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Councillor), target.PlayerId.ToString()) + " " + TargetPlayerName;
                                    break;

                                case CustomRoles.Doomsayer:
                                    if (seer.IsAlive() && target.IsAlive())
                                        TargetPlayerName = ColorString(GetRoleColor(CustomRoles.Doomsayer), " " + target.PlayerId.ToString()) + " " + TargetPlayerName;
                                    break;
                            }

                            // Guesser Mode is On ID
                            if (Options.GuesserMode.GetBool())
                            {
                                // seer & target is alive
                                if (seer.IsAlive() && target.IsAlive())
                                {
                                    var GetTragetId = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;

                                    //Crewmates
                                    if (Options.CrewmatesCanGuess.GetBool() && seer.GetCustomRole().IsCrewmate() && !seer.Is(CustomRoles.Judge) && !seer.Is(CustomRoles.Inspector) && !seer.Is(CustomRoles.Lookout) && !seer.Is(CustomRoles.Swapper))
                                        TargetPlayerName = GetTragetId;

                                    else if (seer.Is(CustomRoles.NiceGuesser) && !Options.CrewmatesCanGuess.GetBool())
                                        TargetPlayerName = GetTragetId;



                                    //Impostors
                                    if (Options.ImpostorsCanGuess.GetBool() && seer.GetCustomRole().IsImpostor() && !seer.Is(CustomRoles.Councillor) && !seer.Is(CustomRoles.Nemesis))
                                        TargetPlayerName = GetTragetId;

                                    else if (seer.Is(CustomRoles.EvilGuesser) && !Options.ImpostorsCanGuess.GetBool())
                                        TargetPlayerName = GetTragetId;



                                    // Neutrals
                                    if (Options.NeutralKillersCanGuess.GetBool() && seer.GetCustomRole().IsNK())
                                        TargetPlayerName = GetTragetId;

                                    if (Options.PassiveNeutralsCanGuess.GetBool() && seer.GetCustomRole().IsNonNK() && !seer.Is(CustomRoles.Doomsayer))
                                        TargetPlayerName = GetTragetId;
                                }
                            }
                            else // Guesser Mode is Off ID
                            {
                                if (seer.IsAlive() && target.IsAlive())
                                {
                                    Logger.Warn($"Guesser condtion {(seer.Is(CustomRoles.Guesser) && !seer.Is(CustomRoles.Inspector) && !seer.Is(CustomRoles.Swapper) && !seer.Is(CustomRoles.Lookout))}", "ID BUG");
                                    if (seer.Is(CustomRoles.NiceGuesser) || seer.Is(CustomRoles.EvilGuesser) ||
                                        (seer.Is(CustomRoles.Guesser) && !seer.Is(CustomRoles.Inspector) && !seer.Is(CustomRoles.Swapper) && !seer.Is(CustomRoles.Lookout)))
                                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                                }
                            }
                        }

                        TargetPlayerName = TargetPlayerName.ApplyNameColorData(seer, target, isForMeeting);

                        // ====== Add TargetSuffix for target (TargetSuffix visible ​​only to the seer) ======
                        TargetSuffix.Clear();


                        TargetSuffix.Append(Stealth.GetSuffix(seer, target));


                        // ====== Target Death Reason for target (Death Reason visible ​​only to the seer) ======
                        string TargetDeathReason = seer.KnowDeathReason(target) 
                            ? $" ({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})" : "";

                        // Devourer
                        if (Devourer.IsEnable)
                        {
                            bool targetDevoured = Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(target.PlayerId));
                            if (targetDevoured && !CamouflageIsForMeeting)
                                TargetPlayerName = GetString("DevouredName");
                        }

                        // Camouflage
                        if (!CamouflageIsForMeeting && ((IsActive(SystemTypes.Comms) && Camouflage.IsActive) || Camouflager.AbilityActivated))
                            TargetPlayerName = $"<size=0%>{TargetPlayerName}</size>";

                        // Target Name
                        string TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}";
                        TargetName += TargetSuffix.ToString() == "" ? "" : ("\r\n" + TargetSuffix.ToString());

                        target.RpcSetNamePrivate(TargetName, true, seer, force: NoCache);
                    }
                }
            }
        }
        //Logger.Info($" Loop for Targets: {}", "DoNotifyRoles", force: true);
        Logger.Info($" END", "DoNotifyRoles");
        return Task.CompletedTask;
    }
    public static void MarkEveryoneDirtySettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
    }
    public static void SyncAllSettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
        GameOptionsSender.SendAllGameOptions();
    }
    public static void AfterMeetingTasks()
    {
        ChatManager.ClearLastSysMsg();

        if (Diseased.IsEnable) Diseased.AfterMeetingTasks();
        if (Antidote.IsEnable) Antidote.AfterMeetingTasks();

        AntiBlackout.AfterMeetingTasks();

        if (Eraser.IsEnable) Eraser.AfterMeetingTasks(notifyPlayer: false);
        if (Cleanser.IsEnable) Cleanser.AfterMeetingTasks(notifyPlayer: false);
        if (Vulture.IsEnable) Vulture.AfterMeetingTasks(notifyPlayer: false);
        if (Seeker.IsEnable) Seeker.AfterMeetingTasks(notifyPlayer: false);

        if (Collector.IsEnable) Collector.AfterMeetingTasks();
        if (Blackmailer.IsEnable) Blackmailer.AfterMeetingTasks();
        if (Swooper.IsEnable) Swooper.AfterMeetingTasks();
        if (Chameleon.IsEnable) Chameleon.AfterMeetingTasks();
        if (Wraith.IsEnable) Wraith.AfterMeetingTasks();
        if (Glitch.IsEnable) Glitch.AfterMeetingTasks();
        if (Keeper.IsEnable) Keeper.AfterMeetingTasks();
        if (BountyHunter.IsEnable) BountyHunter.AfterMeetingTasks();
        if (EvilTracker.IsEnable) EvilTracker.AfterMeetingTasks();
        if (Mercenary.IsEnable) Mercenary.AfterMeetingTasks();
        if (Spiritualist.IsEnable) Spiritualist.AfterMeetingTasks();
        if (Penguin.IsEnable) Penguin.AfterMeetingTasks();
        if (Taskinator.IsEnable) Taskinator.AfterMeetingTasks();
        if (Benefactor.IsEnable) Benefactor.AfterMeetingTasks();
        if (Retributionist.IsEnable) Retributionist.AfterMeetingTasks(); 
        if (PlagueDoctor.IsEnable) PlagueDoctor.AfterMeetingTasks();
        if (Jailer.IsEnable) Jailer.AfterMeetingTasks();
        if (CopyCat.IsEnable) CopyCat.AfterMeetingTasks();  //all crew after meeting task should be before this
        if (Pirate.IsEnable) Pirate.AfterMeetingTask();
        if (Chronomancer.IsEnable) Chronomancer.AfterMeetingTask();
        if (Solsticer.IsEnable) Solsticer.AfterMeetingTasks();
        if (RiftMaker.IsEnable) RiftMaker.AfterMeetingTasks();
        if (Councillor.IsEnable) Councillor.AfterMeetingTasks();
        if (Statue.IsEnable) Statue.AfterMeetingTasks();
        if (Burst.IsEnable) Burst.AfterMeetingTasks();

        Main.ShamanTarget = byte.MaxValue;
        Main.ShamanTargetChoosen = false;
        OverKiller.MurderTargetLateTask = [];

        if (Options.AirshipVariableElectrical.GetBool())
            AirshipElectricalDoors.Initialize();

        DoorsReset.ResetDoors();

        // Empty Deden bug support Empty vent after meeting
        var ventilationSystem = ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out var systemType) ? systemType.TryCast<VentilationSystem>() : null;
        if (ventilationSystem != null)
        {
            ventilationSystem.PlayersInsideVents.Clear();
            ventilationSystem.IsDirty = true;
        }
    }
    public static void AfterPlayerDeathTasks(PlayerControl target, bool onMeeting = false)
    {

        switch (target.GetCustomRole())
        {
            case CustomRoles.Terrorist:
                Logger.Info(target?.Data?.PlayerName + " was Terrorist", "AfterPlayerDeathTasks");
                CheckTerroristWin(target.Data);
                break;
            case CustomRoles.Executioner:
                if (Executioner.Target.ContainsKey(target.PlayerId))
                {
                    Executioner.Target.Remove(target.PlayerId);
                    Executioner.SendRPC(target.PlayerId);
                }
                break;
            case CustomRoles.Lawyer:
                if (Lawyer.Target.ContainsKey(target.PlayerId))
                {
                    Lawyer.Target.Remove(target.PlayerId);
                    Lawyer.SendRPC(target.PlayerId);
                }
                break;
            case CustomRoles.CyberStar:
                if (GameStates.IsMeeting)
                {
                    //Death Message
                    foreach (var pc in Main.AllPlayerControls)
                    {
                        if (!Options.ImpKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsImpostor()) continue;
                        if (!Options.NeutralKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsNeutral()) continue;

                        SendMessage(string.Format(GetString("CyberStarDead"), target.GetRealName()), pc.PlayerId, ColorString(GetRoleColor(CustomRoles.CyberStar), GetString("CyberStarNewsTitle")));
                    }
                }
                else
                {
                    if (!Main.CyberStarDead.Contains(target.PlayerId))
                        Main.CyberStarDead.Add(target.PlayerId);
                }
                break;
            case CustomRoles.Romantic:
                Romantic.isRomanticAlive = false;
                break;
            case CustomRoles.Pelican:
                Pelican.OnPelicanDied(target.PlayerId);
                break;
            case CustomRoles.Devourer:
                Devourer.OnDevourerDied(target.PlayerId);
                break;
        }

        var States = Main.PlayerStates[target.PlayerId];
        foreach (var subRole in States.SubRoles.ToArray())
        {
            switch (subRole)
            {
                case CustomRoles.Cyber:
                    if (GameStates.IsMeeting)
                    {
                        //网红死亡消息提示
                        foreach (var pc in Main.AllPlayerControls)
                        {
                            if (!Cyber.ImpKnowCyberDead.GetBool() && pc.GetCustomRole().IsImpostor()) continue;
                            if (!Cyber.NeutralKnowCyberDead.GetBool() && pc.GetCustomRole().IsNeutral()) continue;
                            if (!Cyber.CrewKnowCyberDead.GetBool() && pc.GetCustomRole().IsCrewmate()) continue;
                            SendMessage(string.Format(GetString("CyberDead"), target.GetRealName()), pc.PlayerId, ColorString(GetRoleColor(CustomRoles.Cyber), GetString("CyberNewsTitle")));
                        }
                    }
                    break;
            }
        }

        if (Executioner.Target.ContainsValue(target.PlayerId))
            Executioner.ChangeRoleByTarget(target);

        if (Romantic.BetPlayer.ContainsValue(target.PlayerId))
            Romantic.ChangeRole(target.PlayerId);

        if (Lawyer.Target.ContainsValue(target.PlayerId))
            Lawyer.ChangeRoleByTarget(target);

        FixedUpdateInNormalGamePatch.LoversSuicide(target.PlayerId, onMeeting);

        Tricky.AfterPlayerDeathTasks(target);
    }
    public static void ChangeInt(ref int ChangeTo, int input, int max)
    {
        var tmp = ChangeTo * 10;
        tmp += input;
        ChangeTo = Math.Clamp(tmp, 0, max);
    }
    public static void CountAlivePlayers(bool sendLog = false)
    {
        int AliveImpostorCount = Main.AllAlivePlayerControls.Count(pc => pc.Is(CustomRoleTypes.Impostor));
        if (Main.AliveImpostorCount != AliveImpostorCount)
        {
            Logger.Info("Number Impostor left: " + AliveImpostorCount, "CountAliveImpostors");
            Main.AliveImpostorCount = AliveImpostorCount;
            LastImpostor.SetSubRole();
        }

        if (sendLog)
        {
            var sb = new StringBuilder(100);
            if (Options.CurrentGameMode != CustomGameMode.FFA)
            { 
                foreach (var countTypes in EnumHelper.GetAllValues<CountTypes>())
                {
                    var playersCount = PlayersCount(countTypes);
                    if (playersCount == 0) continue;
                    sb.Append($"{countTypes}:{AlivePlayersCount(countTypes)}/{playersCount}, ");
                }
            }
            sb.Append($"All:{AllAlivePlayersCount}/{AllPlayersCount}");
            Logger.Info(sb.ToString(), "CountAlivePlayers");
        }
    }
    public static string GetVoteName(byte num)
    {
        string name = "invalid";
        var player = GetPlayerById(num);
        if (num < 15 && player != null) name = player?.GetNameWithRole();
        if (num == 253) name = "Skip";
        if (num == 254) name = "None";
        if (num == 255) name = "Dead";
        return name;
    }
    public static string PadRightV2(this object text, int num)
    {
        int bc = 0;
        var t = text.ToString();
        foreach (char c in t) bc += Encoding.GetEncoding("UTF-8").GetByteCount(c.ToString()) == 1 ? 1 : 2;
        return t?.PadRight(Mathf.Max(num - (bc - t.Length), 0));
    }
    public static void DumpLog()
    {
        string f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/TOHE-logs/";
        string t = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
        string filename = $"{f}TOHE-v{Main.PluginVersion}-{t}.log";
        if (!Directory.Exists(f)) Directory.CreateDirectory(f);
        FileInfo file = new(@$"{Environment.CurrentDirectory}/BepInEx/LogOutput.log");
        file.CopyTo(@filename);

        if (PlayerControl.LocalPlayer != null)
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.DumpfileSaved"), $"TOHE - v{Main.PluginVersion}-{t}.log"));

        SendMessage(string.Format(GetString("Message.DumpcmdUsed"), PlayerControl.LocalPlayer.GetNameWithRole()));

        ProcessStartInfo psi = new("Explorer.exe") { Arguments = "/e,/select," + @filename.Replace("/", "\\") };
        Process.Start(psi);
    }
    public static (int, int) GetDousedPlayerCount(byte playerId)
    {
        int doused = 0, all = 0; //学校で習った書き方
                                 //多分この方がMain.isDousedでforeachするより他のアーソニストの分ループ数少なくて済む
        foreach (var pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == playerId) continue; //塗れない人は除外 (死んでたり切断済みだったり あとアーソニスト自身も)

            all++;
            if (Main.isDoused.TryGetValue((playerId, pc.PlayerId), out var isDoused) && isDoused)
                //塗れている場合
                doused++;
        }

        return (doused, all);
    }
    public static (int, int) GetDrawPlayerCount(byte playerId, out List<PlayerControl> winnerList)
    {
        int draw = 0;
        int all = Options.RevolutionistDrawCount.GetInt();
        int max = Main.AllAlivePlayerControls.Length;
        if (!Main.PlayerStates[playerId].IsDead) max--;
        winnerList = [];
        if (all > max) all = max;

        foreach (var pc in Main.AllPlayerControls)
        {
            if (Main.isDraw.TryGetValue((playerId, pc.PlayerId), out var isDraw) && isDraw)
            {
                winnerList.Add(pc);
                draw++;
            }
        }
        return (draw, all);
    }
    public static string SummaryTexts(byte id, bool disableColor = true, bool check = false)
    {
        var name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
        if (id == PlayerControl.LocalPlayer.PlayerId) name = DataManager.player.Customization.Name;
        else name = GetPlayerById(id)?.Data.PlayerName ?? name;

        var taskState = Main.PlayerStates?[id].TaskState;
        string TaskCount;

        if (taskState.hasTasks)
        {
            Color CurrentСolor;
            var TaskCompleteColor = Color.green; // Color after task completion
            var NonCompleteColor = taskState.CompletedTasksCount > 0 ? Color.yellow : Color.white; // Uncountable out of person is white

            if (Workhorse.IsThisRole(id))
                NonCompleteColor = Workhorse.RoleColor;

            CurrentСolor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;

            if (Main.PlayerStates.TryGetValue(id, out var ps) && ps.MainRole.Is(CustomRoles.Crewpostor))
                CurrentСolor = Color.red;

            if (ps.SubRoles.Contains(CustomRoles.Workhorse))
                GetRoleColor(ps.MainRole).ShadeColor(0.5f);

            TaskCount = ColorString(CurrentСolor, $" ({taskState.CompletedTasksCount}/{taskState.AllTasksCount})");
        }
        else { TaskCount = GetProgressText(id); }

        string summary = $"{ColorString(Main.PlayerColors[id], name)} - {GetDisplayRoleAndSubName(id, id, true)}{GetSubRolesText(id, summary: true)}{TaskCount} {GetKillCountText(id)} ({GetVitalText(id, true)})";
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.FFA:
                summary = $"{ColorString(Main.PlayerColors[id], name)} {GetKillCountText(id, ffa: true)}";
                break;
        }
        return check && GetDisplayRoleAndSubName(id, id, true).RemoveHtmlTags().Contains("INVALID:NotAssigned")
            ? "INVALID"
            : disableColor ? summary.RemoveHtmlTags() : summary;
    }
    public static string RemoveHtmlTagsTemplate(this string str) => Regex.Replace(str, "", "");
    public static string RemoveHtmlTags(this string str) => Regex.Replace(str, "<[^>]*?>", "");
    public static void FlashColor(Color color, float duration = 1f)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud.FullScreen == null) return;
        var obj = hud.transform.FindChild("FlashColor_FullScreen")?.gameObject;
        if (obj == null)
        {
            obj = UnityEngine.Object.Instantiate(hud.FullScreen.gameObject, hud.transform);
            obj.name = "FlashColor_FullScreen";
        }
        hud.StartCoroutine(Effects.Lerp(duration, new Action<float>((t) =>
        {
            obj.SetActive(t != 1f);
            obj.GetComponent<SpriteRenderer>().color = new(color.r, color.g, color.b, Mathf.Clamp01((-2f * Mathf.Abs(t - 0.5f) + 1) * color.a / 2)); //アルファ値を0→目標→0に変化させる
        })));
    }

    public static Dictionary<string, Sprite> CachedSprites = [];
    public static Sprite LoadSprite(string path, float pixelsPerUnit = 1f)
    {
        try
        {
            if (CachedSprites.TryGetValue(path + pixelsPerUnit, out var sprite)) return sprite;
            Texture2D texture = LoadTextureFromResources(path);
            sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
            return CachedSprites[path + pixelsPerUnit] = sprite;
        }
        catch
        {
            Logger.Error($"Failed to read Texture： {path}", "LoadSprite");
        }
        return null;
    }
    public static Texture2D LoadTextureFromResources(string path)
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            ImageConversion.LoadImage(texture, ms.ToArray(), false);
            return texture;
        }
        catch
        {
            Logger.Error($"Failed to read Texture： {path}", "LoadTextureFromResources");
        }
        return null;
    }
    public static string ColorString(Color32 color, string str) => $"<color=#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}</color>";
    public static string ColorStringWithoutEnding(Color32 color, string str) => $"<color=#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}";
    /// <summary>
    /// Darkness:１の比率で黒色と元の色を混ぜる。マイナスだと白色と混ぜる。
    /// </summary>
    public static Color ShadeColor(this Color color, float Darkness = 0)
    {
        bool IsDarker = Darkness >= 0; //黒と混ぜる
        if (!IsDarker) Darkness = -Darkness;
        float Weight = IsDarker ? 0 : Darkness; //黒/白の比率
        float R = (color.r + Weight) / (Darkness + 1);
        float G = (color.g + Weight) / (Darkness + 1);
        float B = (color.b + Weight) / (Darkness + 1);
        return new Color(R, G, B, color.a);
    }

    public static void SetChatVisible()
    {
        if (!GameStates.IsInGame || !AmongUsClient.Instance.AmHost) return;
        
        MeetingHud.Instance = UnityEngine.Object.Instantiate(HudManager.Instance.MeetingPrefab);
        MeetingHud.Instance.ServerStart(PlayerControl.LocalPlayer.PlayerId);
        AmongUsClient.Instance.Spawn(MeetingHud.Instance, -2, SpawnFlags.None);
        MeetingHud.Instance.RpcClose();
    }

    public static bool TryCast<T>(this Il2CppObjectBase obj, out T casted)
    where T : Il2CppObjectBase
    {
        casted = obj.TryCast<T>();
        return casted != null;
    }
    public static int AllPlayersCount => Main.PlayerStates.Values.Count(state => state.countTypes != CountTypes.OutOfGame);
    public static int AllAlivePlayersCount => Main.AllAlivePlayerControls.Count(pc => !pc.Is(CountTypes.OutOfGame));
    public static bool IsAllAlive => Main.PlayerStates.Values.All(state => state.countTypes == CountTypes.OutOfGame || !state.IsDead);
    public static int PlayersCount(CountTypes countTypes) => Main.PlayerStates.Values.Count(state => state.countTypes == countTypes);
    public static int AlivePlayersCount(CountTypes countTypes) => Main.AllAlivePlayerControls.Count(pc => pc.Is(countTypes));
}
