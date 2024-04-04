﻿using Hazel;
using System.Collections.Generic;
using System;
using System.Linq;
using TOHE.Modules;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Quizmaster
{
    private static readonly int Id = 27000;
    //public static List<byte> playerIdList = [];
    public static bool IsEnable = false;
    public static PlayerControl Player;
    public static OptionItem QuestionDifficulty;
    public static OptionItem CanKillAfterMark;
    public static OptionItem CanVentAfterMark;
    public static OptionItem NumOfKillAfterMark;
    public static OptionItem CanGiveQuestionsAboutPastGames;
    public static QuizQuestionBase Question = new SetAnswersQuestion { Stage = 0, Answer = "Select Me", PossibleAnswers = { "Select me", "Die", "Die", "Die" }, Question = "This question is to prevent crashes answer the letter with the answer \"Select me\"", HasAnswersTranslation = false, HasQuestionTranslation = false };
    public static QuizQuestionBase previousQuestion = new SetAnswersQuestion { Stage = 0, Answer = "Select Me", PossibleAnswers = { "Select me", "Die", "Die", "Die" }, Question = "This question is to prevent crashes answer the letter with the answer \"Select me\"", HasAnswersTranslation = false, HasQuestionTranslation = false };
    public static Sabotages lastSabotage = Sabotages.None;
    public static Sabotages firstSabotageOfRound = Sabotages.None;
    public static int killsForRound = 0;
    public static bool allowedKilling = false;
    //public static bool allowedVenting = true;
    public static bool AlreadyMarked = false;
    public static byte MarkedPlayer = byte.MaxValue;
    public static string lastExiledColor = "None";
    public static string lastReportedColor = "None";
    public static string thisReportedColor = "None";
    public static string lastButtonPressedColor = "None";
    public static string thisButtonPressedColor = "None";
    public static int meetingNum = 0;
    public static int diedThisRound = 0;
    public static int buttonMeeting = 0;

    public static bool InExperimental = true;
    public static void SetupCustomOption()
    {
        TabGroup tab = InExperimental ? TabGroup.OtherRoles : TabGroup.NeutralRoles;

        SetupSingleRoleOptions(Id, tab, CustomRoles.Quizmaster, 1);
        QuestionDifficulty = IntegerOptionItem.Create(Id + 10, "QuizmasterSettings.QuestionDifficulty", new(1, 4, 1), 1, tab, false).SetParent(CustomRoleSpawnChances[CustomRoles.Quizmaster]);

        CanVentAfterMark = BooleanOptionItem.Create(Id + 11, "QuizmasterSettings.CanVentAfterMark", true, tab, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Quizmaster]);
        CanKillAfterMark = BooleanOptionItem.Create(Id + 12, "QuizmasterSettings.CanKillAfterMark", false, tab, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Quizmaster]);
        NumOfKillAfterMark = IntegerOptionItem.Create(Id + 13, "QuizmasterSettings.NumOfKillAfterMark", new(1, 15, 1), 1, tab, false)
            .SetValueFormat(OptionFormat.Players)
            .SetParent(CanKillAfterMark);
        CanGiveQuestionsAboutPastGames = BooleanOptionItem.Create(Id + 14, "QuizmasterSettings.CanGiveQuestionsAboutPastGames", false, tab, false)
           .SetParent(CustomRoleSpawnChances[CustomRoles.Quizmaster]);
    }
    public static void Init()
    {
        //playerIdList = new();
        Player = null;
        firstSabotageOfRound = Sabotages.None;
        killsForRound = 0;
        allowedKilling = false;
        //allowedVenting = true;
        AlreadyMarked = false;
        MarkedPlayer = byte.MaxValue;

        if (!CanGiveQuestionsAboutPastGames.GetBool())
        {
            lastExiledColor = "None";
            lastReportedColor = "None";
            lastButtonPressedColor = "None";
            lastSabotage = Sabotages.None;
        }

        thisReportedColor = "None";
        thisButtonPressedColor = "None";
        diedThisRound = 0;
        meetingNum = 0;
        buttonMeeting = 0;
        IsEnable = false;
    }
    public static void Add(byte playerId)
    {
        //playerIdList.Add(playerId);
        MarkedPlayer = byte.MaxValue;
        IsEnable = true;
    }
    private static void SendRPC(byte targetId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.QuizmasterMarkPlayer, SendOption.Reliable, -1);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte targetId = reader.ReadByte();

        if (targetId != byte.MaxValue)
        {
            //allowedVenting = false;
            AlreadyMarked = true;
            MarkedPlayer = targetId;

            allowedKilling = CanKillAfterMark.GetBool();
        }
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = 15;
    public static bool CanUseKillButton(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return false;

        return true;
    }

    public static bool CanUseVentButton(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return false;
       
        bool canVent = false;
        if (CanVentAfterMark.GetBool() && MarkedPlayer != byte.MaxValue)
        {
            canVent = true;
        }

        return canVent;
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (AlreadyMarked == false)
        {
            //allowedVenting = false;
            AlreadyMarked = true;
            MarkedPlayer = target.PlayerId;
            SendRPC(target.PlayerId);

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);

            allowedKilling = CanKillAfterMark.GetBool();

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.MarkDirtySettings();
            killer.RPCPlayCustomSound("Clothe");

            return false;
        }
        return allowedKilling && AlreadyMarked;
    }

    static QuizQuestionBase GetRandomQuestion(List<QuizQuestionBase> qt)
    {
        List<QuizQuestionBase> questions = qt.Where(a => a.Stage <= QuestionDifficulty.GetInt()).ToList();
        var rnd = IRandom.Instance;
        QuizQuestionBase question = questions[rnd.Next(0, questions.Count)];
        if (question == previousQuestion)
        {
            question = questions[rnd.Next(0, questions.Count)];
        }
        if (question == null)
            question = new PlrColorQuestion { Stage = 1, Question = "LastReportPlayerColor", QuizmasterQuestionType = QuizmasterQuestionType.ReportColorQuestion };

        previousQuestion = question;
        question.FixUnsetAnswers();
        return question;
    }

    static CustomRoles GetRandomRole(List<CustomRoles> roles, bool AllowAddons)
    {
        var rnd = IRandom.Instance;
        CustomRoles chosenRole = roles[rnd.Next(0, roles.Count)];
        if (chosenRole.IsAdditionRole() && !AllowAddons)
        {
            for (int s = 0; s < -1; s++)
            {
                if (chosenRole.IsAdditionRole() && !AllowAddons)
                {
                    chosenRole = roles[rnd.Next(0, roles.Count)];
                }
                else
                {
                    s = -1;
                    break;
                }
            }
        }
        return chosenRole;
    }

    public static void OnButtonPress(PlayerControl player)
    {
        if (player == null) return;

        buttonMeeting++;
        meetingNum++;
        lastButtonPressedColor = thisButtonPressedColor;
        thisButtonPressedColor = player.Data.GetPlayerColorString();
        DoQuestion();
    }

    public static void OnReportDeadBody(GameData.PlayerInfo targetInfo)
    {
        if (targetInfo == null) return;

        lastReportedColor = thisReportedColor;
        thisReportedColor = targetInfo.GetPlayerColorString();
        meetingNum++;
        DoQuestion();
    }

    public static void DoQuestion()
    {
        Player = Utils.GetPlayerByRole(CustomRoles.Quizmaster);
        if (MarkedPlayer != byte.MaxValue)
        {
            CustomRoles randomRole = GetRandomRole([.. CustomRolesHelper.AllRoles], false);
            CustomRoles randomRoleWithAddon = GetRandomRole([.. CustomRolesHelper.AllRoles], false);
            List<QuizQuestionBase> Questions =
            [
                new SabotageQuestion { Stage = 1, Question = "LastSabotage",/* JSON ENTRIES */ QuizmasterQuestionType = QuizmasterQuestionType.LatestSabotageQuestion },
                new SabotageQuestion { Stage = 1, Question = "FirstRoundSabotage", QuizmasterQuestionType = QuizmasterQuestionType.FirstRoundSabotageQuestion },
                new PlrColorQuestion { Stage = 1, Question = "LastEjectedPlayerColor", QuizmasterQuestionType = QuizmasterQuestionType.EjectionColorQuestion },
                new PlrColorQuestion { Stage = 1, Question = "LastReportPlayerColor", QuizmasterQuestionType = QuizmasterQuestionType.ReportColorQuestion },
                new PlrColorQuestion { Stage = 1, Question = "LastButtonPressedPlayerColor", QuizmasterQuestionType = QuizmasterQuestionType.LastMeetingColorQuestion },

                new CountQuestion { Stage = 2, Question = "MeetingPassed", QuizmasterQuestionType = QuizmasterQuestionType.MeetingCountQuestion },
                new SetAnswersQuestion { Stage = 2, Question = "HowManyFactions", Answer = "Three", PossibleAnswers = { "One", "Two", "Three", "Four", "Five" }, QuizmasterQuestionType = QuizmasterQuestionType.FactionQuestion },
                new SetAnswersQuestion { Stage = 2, Question = GetString("BasisOfRole").Replace("{QMROLE}", randomRoleWithAddon.ToString()), HasQuestionTranslation = false, Answer = CustomRolesHelper.GetCustomRoleTypes(randomRoleWithAddon).ToString(), PossibleAnswers = { "Crewmate", "Impostor", "Neutral", "Addon" }, QuizmasterQuestionType = QuizmasterQuestionType.RoleBasisQuestion },
                new SetAnswersQuestion { Stage = 2, Question = GetString("FactionOfRole").Replace("{QMROLE}", randomRole.ToString()), HasQuestionTranslation = false, Answer = CustomRolesHelper.GetRoleTypes(randomRole).ToString(), PossibleAnswers = { "Crewmate", "Impostor", "Neutral" }, QuizmasterQuestionType = QuizmasterQuestionType.RoleFactionQuestion },

                new SetAnswersQuestion { Stage = 3, Question = "FactionRemovedName", Answer = "Coven", PossibleAnswers = { "Sabotuer", "Sorcerers", "Coven", "Killer" }, QuizmasterQuestionType = QuizmasterQuestionType.RemovedFactionQuestion },
                new SetAnswersQuestion { Stage = 3, Question = "WhatDoesEOgMeansInName", Answer = "Edited", PossibleAnswers = { "Edition", "Experimental", "Enhanced", "Edited" }, QuizmasterQuestionType = QuizmasterQuestionType.NameOriginQuestion },
                new CountQuestion { Stage = 3, Question = "HowManyDiedFirstRound", QuizmasterQuestionType = QuizmasterQuestionType.DiedFirstRoundCountQuestion },
                new CountQuestion { Stage = 3, Question = "ButtonPressedBefore", QuizmasterQuestionType = QuizmasterQuestionType.ButtonPressedBeforeThisQuestion },

                new DeathReasonQuestion { Stage = 4, Question = "PlrDieReason", QuizmasterQuestionType = QuizmasterQuestionType.PlrDeathReasonQuestion},
                new DeathReasonQuestion { Stage = 4, Question = "PlrDieMethod", QuizmasterQuestionType = QuizmasterQuestionType.PlrDeathMethodQuestion},
                new SetAnswersQuestion { Stage = 4, Question = "LastAddedRoleForKarped", Answer = "Pacifist", PossibleAnswers = { "Pacifist", "Vampire", "Snitch", "Vigilante", "Jackal", "Mole", "Sniper" }, QuizmasterQuestionType = QuizmasterQuestionType.RoleAddedQuestion },
                new DeathReasonQuestion { Stage = 4, Question = "PlrDieFaction", QuizmasterQuestionType = QuizmasterQuestionType.PlrDeathKillerFactionQuestion},
            ];
            
            Question = GetRandomQuestion(Questions);
            _ = new LateTask(() =>
            {
                ShowQuestion(Main.AllPlayerControls[MarkedPlayer]);
                Utils.SendMessage(GetString("QuizmasterChat.Marked").Replace("{QMTARGET}", Utils.GetPlayerById(MarkedPlayer).GetRealName()), Player.PlayerId, GetString("QuizmasterChat.Title"));
                foreach (var plr in Main.AllPlayerControls)
                {
                    if (plr.PlayerId != Player.PlayerId && MarkedPlayer != plr.PlayerId)
                    {
                        Utils.SendMessage(GetString("QuizmasterChat.MarkedPublic").Replace("{QMCOLOR}", Utils.GetRoleColorCode(CustomRoles.Quizmaster)).Replace("{QMTARGET}", Utils.GetPlayerById(MarkedPlayer).GetRealName()), plr.PlayerId, GetString("QuizmasterChat.Title"));
                    }
                }
            }, 6.1f, "Quizmaster Chat Notice");
        }
    }

    public static void OnPlayerExile(GameData.PlayerInfo exiled)
    {
        lastExiledColor = exiled.GetPlayerColorString();
    }

    public static void OnMeetingEnd() /* NEW ROUND START */
    {
        firstSabotageOfRound = Sabotages.None;
        killsForRound = 0;
        //allowedVenting = true;
        allowedKilling = false;
        diedThisRound = 0;
        if (MarkedPlayer != byte.MaxValue)
            KillPlayer(Utils.GetPlayerById(MarkedPlayer));

        ResetMarkedPlayer(true);
    }

    public static void ResetMarkedPlayer(bool canMarkAgain = true)
    {
        if (canMarkAgain == true)
            AlreadyMarked = false;
        MarkedPlayer = byte.MaxValue;
    }

    public static void OnPlayerDead(PlayerControl target)
    {
        diedThisRound++;
        if (target.PlayerId == MarkedPlayer) ResetMarkedPlayer(false);
    }

    public static void SetKillButtonText(HudManager instance)
    {
        if (allowedKilling)
            instance.KillButton.OverrideText(GetString("KillButtonText"));
        else
            instance.KillButton.OverrideText(GetString("QuizmasterKillButtonText"));
    }

    public static string TargetMark(PlayerControl seer, PlayerControl target)
        => (seer != null && seer.PlayerId != target.PlayerId && MarkedPlayer == target.PlayerId) ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Quizmaster), " ?!") : "";

    public static void OnSabotageCall(SystemTypes systemType)
    {
        if (!Main.MeetingIsStarted
            && systemType is
                SystemTypes.HeliSabotage or
                SystemTypes.Laboratory or
                SystemTypes.Reactor or
                SystemTypes.Electrical or
                SystemTypes.LifeSupp or
                SystemTypes.Comms or
                SystemTypes.MushroomMixupSabotage)
        {
            switch (systemType)
            {
                case SystemTypes.HeliSabotage: //The Airhip
                case SystemTypes.Laboratory: //Polus
                case SystemTypes.Reactor: //Other maps
                    lastSabotage = Sabotages.Reactor;
                    break;
                case SystemTypes.Electrical:
                    lastSabotage = Sabotages.Lights;
                    break;
                case SystemTypes.LifeSupp:
                    lastSabotage = Sabotages.O2;
                    break;
                case SystemTypes.Comms:
                    lastSabotage = Sabotages.Communications;
                    break;
                case SystemTypes.MushroomMixupSabotage:
                    lastSabotage = Sabotages.MushroomMixup;
                    break;
            }

            if (firstSabotageOfRound == Sabotages.None)
                firstSabotageOfRound = lastSabotage;
        }
    }

    public static void KillPlayer(PlayerControl plrToKill)
    {
        plrToKill.Data.IsDead = true;
        Main.PlayerStates[plrToKill.PlayerId].deathReason = PlayerState.DeathReason.WrongAnswer;
        Main.PlayerStates[plrToKill.PlayerId].SetDead();
        plrToKill.RpcExileV2();
        ResetMarkedPlayer(true);
    }

    public static void RightAnswer(PlayerControl target)
    {
        lastReportedColor = thisReportedColor;
        foreach (var plr in Main.AllPlayerControls)
        {
            if (plr.PlayerId != Player.PlayerId && target.PlayerId != plr.PlayerId)
            {
                Utils.SendMessage(GetString("QuizmasterChat.CorrectPublic").Replace("{QMCOLOR}", Utils.GetRoleColorCode(CustomRoles.Quizmaster)).Replace("{QMTARGET}", target.GetRealName()), plr.PlayerId, GetString("QuizmasterChat.Title"));
            }
        }
        Utils.SendMessage(GetString("QuizmasterChat.CorrectTarget"), target.PlayerId, GetString("QuizmasterChat.Title"));
        Utils.SendMessage(GetString("QuizmasterChat.Correct").Replace("{QMTARGET}", target.GetRealName()), Player.PlayerId, GetString("QuizmasterChat.Title"));
        ResetMarkedPlayer(true);
    }

    public static void WrongAnswer(PlayerControl target, string wrongAnswer, string rightAnswer)
    {
        lastReportedColor = thisReportedColor;
        KillPlayer(target);
        foreach (var plr in Main.AllPlayerControls)
        {
            if (plr.PlayerId != Player.PlayerId && target.PlayerId != plr.PlayerId)
            {
                Utils.SendMessage(GetString("QuizmasterChat.WrongPublic").Replace("{QMCOLOR}", Utils.GetRoleColorCode(CustomRoles.Quizmaster)).Replace("{QMTARGET}", target.GetRealName()), plr.PlayerId, GetString("QuizmasterChat.Title"));
            }
        }
        Utils.SendMessage(GetString("QuizmasterChat.Wrong").Replace("{QMTARGET}", target.GetRealName()), Player.PlayerId, GetString("QuizmasterChat.Title"));
        Utils.SendMessage(GetString("QuizmasterChat.WrongTarget").Replace("{QMWRONG}", wrongAnswer).Replace("{QMRIGHT}", rightAnswer).Replace("{QM}", Player.GetRealName()), target.PlayerId, GetString("QuizmasterChat.Title"));
    }
    public static void AnswerByChat(PlayerControl plr, string[] args)
    {
        if (MarkedPlayer == plr.PlayerId)
        {
            var answerSyntaxValid = args.Length == 2;
            if (answerSyntaxValid)
            {
                string answer = args[1].ToUpper();
                var answerValid = (answer == "A" || answer == "B" || answer == "C");
                var rightAnswer = Question.AnswerLetter.Trim().ToUpper();

                if (answerValid)
                {
                    if (rightAnswer == answer)
                        RightAnswer(plr);
                    else
                        WrongAnswer(plr, answer, rightAnswer);
                }
                else
                {
                    Utils.SendMessage(GetString("QuizmasterAnswerNotValid"), plr.PlayerId, GetString("QuizmasterChat.Title"));
                }
            }
            else
            {
                Utils.SendMessage(GetString("QuizmasterSyntaxNotValid"), plr.PlayerId, GetString("QuizmasterChat.Title"));
            }
        }
        else if (plr.GetCustomRole() is CustomRoles.Quizmaster)
        {
            Utils.SendMessage(GetString("QuizmasterCantAnswer"), plr.PlayerId, GetString("QuizmasterChat.Title"));
        }
    }

    public static void ShowQuestion(PlayerControl plr)
    {
        if (plr.PlayerId == MarkedPlayer)
        {
            Utils.SendMessage(GetString("QuizmasterChat.MarkedBy").Replace("{QMCOLOR}", Utils.GetRoleColorCode(CustomRoles.Quizmaster)).Replace("{QMQUESTION}", Question.HasQuestionTranslation ? GetString("QuizmasterQuestions." + Question.Question) : Question.Question), MarkedPlayer, GetString("QuizmasterChat.Title"));
            Utils.SendMessage(GetString("QuizmasterChat.Answers").Replace("{QMA}", Question.HasAnswersTranslation ? GetString(Question.Answers[0], showInvalid: Question.ShowInvalid) : Question.Answers[0]).Replace("{QMB}", Question.HasAnswersTranslation ? GetString(Question.Answers[1], showInvalid: Question.ShowInvalid) : Question.Answers[1]).Replace("{QMC}", Question.HasAnswersTranslation ? GetString(Question.Answers[2], showInvalid: Question.ShowInvalid) : Question.Answers[2]), MarkedPlayer, GetString("QuizmasterChat.Title"));
        }
    }

    public static void OnVotedOut()
    {
        ResetMarkedPlayer(false);
    }
}

abstract public class QuizQuestionBase
{
    public int Stage { get; set; }
    public QuizmasterQuestionType QuizmasterQuestionType { get; set; }

    public string Question { get; set;  }
    public string Answer { get; set; }
    public string AnswerLetter { get; set; }
    public List<string> Answers { get; set; }
    public List<string> PossibleAnswers { get; set; } = [];
    public bool HasAnswersTranslation { get; set; } = true;
    public bool HasQuestionTranslation { get; set; } = true;
    public bool ShowInvalid { get; set; } = true;
    public abstract void FixUnsetAnswers();
}

class PlrColorQuestion : QuizQuestionBase
{
    public override void FixUnsetAnswers()
    {
        Answers = [];

        foreach (PlayerControl plr in Main.AllPlayerControls)
        {
            if (!PossibleAnswers.Contains(plr.Data.GetPlayerColorString())) 
                PossibleAnswers.Add(plr.Data.GetPlayerColorString());
        }

        var rnd = IRandom.Instance;
        int positionForRightAnswer = rnd.Next(3);

        Answer = QuizmasterQuestionType switch
        {
            QuizmasterQuestionType.EjectionColorQuestion => Quizmaster.lastExiledColor,
            QuizmasterQuestionType.ReportColorQuestion => Quizmaster.lastReportedColor,
            QuizmasterQuestionType.LastMeetingColorQuestion => Quizmaster.lastButtonPressedColor,
            _ => "None"
        };

        HasAnswersTranslation = false;

        if (PossibleAnswers.Contains(Answer))
            PossibleAnswers.Remove(Answer);

        for (int numOfQuestionsDone = 0; numOfQuestionsDone < 3; numOfQuestionsDone++)
        {
            var prefix = "";
            if (numOfQuestionsDone == positionForRightAnswer)
            {
                AnswerLetter = new List<string> { "A", "B", "C" }[positionForRightAnswer];
                if (Answer == "None") prefix = "Quizmaster.";
                if (prefix != "")
                    Answer = GetString(prefix + Answer);
                Answers.Add(prefix + Answer);
            }
            else
            {
                string thatAnswer = PossibleAnswers[rnd.Next(PossibleAnswers.Count)];
                if (thatAnswer == "None") prefix = "Quizmaster.";
                if (prefix != "")
                    thatAnswer = GetString(prefix + thatAnswer);
                Answers.Add(prefix + thatAnswer);
                PossibleAnswers.Remove(thatAnswer);
            }
        }
    }
}

class DeathReasonQuestion : QuizQuestionBase
{
    public override void FixUnsetAnswers()
    {
        Answers = [];

        var rnd = IRandom.Instance;

        PlayerControl chosenPlayer = null;

        if (QuizmasterQuestionType == QuizmasterQuestionType.PlrDeathReasonQuestion)
        {
            PossibleAnswers.Add("None");
            PossibleAnswers.Add(PlayerState.DeathReason.etc.ToString());
            PossibleAnswers.Add(PlayerState.DeathReason.Vote.ToString());
        }
        else if (QuizmasterQuestionType == QuizmasterQuestionType.PlrDeathMethodQuestion)
        {
            PossibleAnswers.Add(PlayerState.DeathReason.Disconnected.ToString());
            PossibleAnswers.Add(PlayerState.DeathReason.Vote.ToString());
            PossibleAnswers.Add(PlayerState.DeathReason.Kill.ToString());
        }
        else if (QuizmasterQuestionType == QuizmasterQuestionType.PlrDeathKillerFactionQuestion)
        {
            PossibleAnswers.Add("");
            PossibleAnswers.Add(PlayerState.DeathReason.Vote.ToString());
            PossibleAnswers.Add(PlayerState.DeathReason.Kill.ToString());
        }

        chosenPlayer = Main.AllPlayerControls[rnd.Next(Main.AllPlayerControls.Length)];

        foreach (PlayerControl plr in Main.AllPlayerControls)
        {
            if (QuizmasterQuestionType == QuizmasterQuestionType.PlrDeathReasonQuestion)
            {
                if (plr.Data.IsDead && !PossibleAnswers.Contains(Main.PlayerStates[chosenPlayer.PlayerId].deathReason.ToString()))
                    PossibleAnswers.Add(Main.PlayerStates[chosenPlayer.PlayerId].deathReason.ToString());
            }
        }

        int positionForRightAnswer = rnd.Next(0, 3);

        HasQuestionTranslation = false; //doing this do i can just change the player name in question
        Question = GetString("QuizmasterQuestions." + Question).Replace("{PLR}", chosenPlayer.GetRealName());

        ShowInvalid = false;

        Answer = QuizmasterQuestionType switch
        {
            QuizmasterQuestionType.PlrDeathReasonQuestion => chosenPlayer.Data.IsDead ? Main.PlayerStates[chosenPlayer.PlayerId].deathReason.ToString() : "None",
            QuizmasterQuestionType.PlrDeathMethodQuestion => chosenPlayer.Data.Disconnected ? PlayerState.DeathReason.Disconnected.ToString() : (Main.PlayerStates[chosenPlayer.PlayerId].deathReason == PlayerState.DeathReason.Vote ? PlayerState.DeathReason.Vote.ToString() : PlayerState.DeathReason.Kill.ToString()),
            QuizmasterQuestionType.PlrDeathKillerFactionQuestion => CustomRolesHelper.GetRoleTypes(chosenPlayer.GetRealKiller().GetCustomRole()).ToString(),
            _ => "None"
        };

        PossibleAnswers.Remove(Answer);
        for (int numOfQuestionsDone = 0; numOfQuestionsDone < 3; numOfQuestionsDone++)
        {
            var prefix = "";
            if (QuizmasterQuestionType == QuizmasterQuestionType.PlrDeathKillerFactionQuestion) prefix = "Type.";
            if (numOfQuestionsDone == positionForRightAnswer)
            {
                AnswerLetter = new List<string> { "A", "B", "C" }[positionForRightAnswer];
                if (Answer == "None") prefix = "Quizmaster.";
                if (prefix != "")
                    Answer = GetString(prefix + Answer);
                Answers.Add(prefix + Answer);
            }
            else
            {
                string thatAnswer = PossibleAnswers[rnd.Next(0, PossibleAnswers.Count)];
                if (thatAnswer == "None") prefix = "Quizmaster.";
                if (prefix != "")
                    thatAnswer = GetString(prefix + thatAnswer);
                Answers.Add(prefix + thatAnswer);
                PossibleAnswers.Remove(thatAnswer);
            }
        }
    }
}

class CountQuestion : QuizQuestionBase
{
    public override void FixUnsetAnswers()
    {
        var rnd = IRandom.Instance;

        Answer = QuizmasterQuestionType switch
        {
            QuizmasterQuestionType.MeetingCountQuestion => Quizmaster.meetingNum.ToString(),
            QuizmasterQuestionType.ButtonPressedBeforeThisQuestion => (Quizmaster.buttonMeeting - 1).ToString(),
            QuizmasterQuestionType.DiedFirstRoundCountQuestion => Quizmaster.diedThisRound.ToString(),
            _ => "None"
        };

        Answers = [];
        int ans = int.Parse(Answer);
        if (ans < 1)
        {
            PossibleAnswers.Add((ans + rnd.Next(1, 3)).ToString());
            PossibleAnswers.Add((ans + rnd.Next(3, 5)).ToString());
        }
        else
        {
            PossibleAnswers.Add((ans + rnd.Next(1, 3)).ToString());
            PossibleAnswers.Add((ans - 1).ToString());
        }

        HasAnswersTranslation = false;

        int positionForRightAnswer = rnd.Next(0, 3);

        PossibleAnswers.Remove(Answer);
        for (int numOfQuestionsDone = 0; numOfQuestionsDone < 3; numOfQuestionsDone++)
        {
            if (numOfQuestionsDone == positionForRightAnswer)
            {
                AnswerLetter = new List<string> { "A", "B", "C" }[positionForRightAnswer];
                Answers.Add(Answer);
            }
            else
            {
                string thatAnswer = PossibleAnswers[rnd.Next(0, PossibleAnswers.Count)];
                Answers.Add(thatAnswer);
                PossibleAnswers.Remove(thatAnswer);
            }
        }
    }
}

class SetAnswersQuestion : QuizQuestionBase
{
    public override void FixUnsetAnswers()
    {
        Answers = [];

        var rnd = IRandom.Instance;
        int positionForRightAnswer = rnd.Next(0, 3);

        PossibleAnswers.Remove(Answer);
        for (int numOfQuestionsDone = 0; numOfQuestionsDone < 3; numOfQuestionsDone++)
        {
            var prefix = QuizmasterQuestionType switch
            {
                QuizmasterQuestionType.RoleBasisQuestion or QuizmasterQuestionType.RoleFactionQuestion or QuizmasterQuestionType.FactionQuestion or QuizmasterQuestionType.NameOriginQuestion or QuizmasterQuestionType.RemovedFactionQuestion or QuizmasterQuestionType.RoleAddedQuestion => "QuizmasterAnswers.",
                _ => ""
            };

            if (numOfQuestionsDone == positionForRightAnswer)
            {
                AnswerLetter = new List<string> { "A", "B", "C" }[positionForRightAnswer];
                if (Answer == "None") prefix = "Quizmaster.";
                Answers.Add(prefix + Answer);

                ShowInvalid = false;
            }
            else
            {
                string thatAnswer = PossibleAnswers[rnd.Next(0, PossibleAnswers.Count)];
                if (thatAnswer == "None") prefix = "Quizmaster.";
                Answers.Add(prefix + thatAnswer);
                PossibleAnswers.Remove(thatAnswer);
            }
        }
    }
}

class SabotageQuestion : QuizQuestionBase
{
    private static readonly List<Sabotages> SkeldSabotages = [Sabotages.None, Sabotages.Lights, Sabotages.Reactor, Sabotages.O2];
    private static readonly List<Sabotages> MiraSabotages = [Sabotages.None, Sabotages.Lights, Sabotages.Reactor, Sabotages.O2, Sabotages.Communications];
    private static readonly List<Sabotages> PolusSabotages = [Sabotages.None, Sabotages.Lights, Sabotages.Reactor, Sabotages.Communications];
    private static readonly List<Sabotages> AirshitSabotages = [Sabotages.None, Sabotages.Lights, Sabotages.Reactor, Sabotages.Communications];
    private static readonly List<Sabotages> FungleSabotages = [Sabotages.None, Sabotages.Communications, Sabotages.Reactor, Sabotages.MushroomMixup];

    public override void FixUnsetAnswers()
    {
        Answers = [];

        PossibleAnswers = Utils.GetActiveMapName() switch
        {
            MapNames.Skeld => SkeldSabotages.ConvertAll(f => f.ToString()),
            MapNames.Dleks => SkeldSabotages.ConvertAll(f => f.ToString()),
            MapNames.Mira => MiraSabotages.ConvertAll(f => f.ToString()),
            MapNames.Polus => PolusSabotages.ConvertAll(f => f.ToString()),
            MapNames.Airship => AirshitSabotages.ConvertAll(f => f.ToString()),
            MapNames.Fungle => FungleSabotages.ConvertAll(f => f.ToString()),
            _ => throw new NotImplementedException(),
        };


        var rnd = IRandom.Instance;
        int positionForRightAnswer = rnd.Next(0, 3);

        Answer = QuizmasterQuestionType switch
        {
            QuizmasterQuestionType.LatestSabotageQuestion => Quizmaster.lastSabotage.ToString(),
            QuizmasterQuestionType.FirstRoundSabotageQuestion => Quizmaster.firstSabotageOfRound.ToString(),
            _ => Sabotages.None.ToString(),
        };

        PossibleAnswers.Remove(Answer);

        for (int numOfQuestionsDone = 0; numOfQuestionsDone < 3; numOfQuestionsDone++)
        {
            var prefix = "QuizmasterSabotages.";
            if (numOfQuestionsDone == positionForRightAnswer)
            {
                AnswerLetter = new List<string> { "A", "B", "C" }[positionForRightAnswer];
                if (Answer == "None") prefix = "Quizmaster.";
                Answers.Add(prefix + Answer);
            }
            else
            {
                string thatAnswer = PossibleAnswers[rnd.Next(0, PossibleAnswers.Count)];
                if (thatAnswer == "None") prefix = "Quizmaster.";
                Answers.Add(prefix + thatAnswer);
                PossibleAnswers.Remove(thatAnswer);
            }
        }
    }
}

public enum QuizmasterQuestionType
{
    FirstRoundSabotageQuestion,
    LatestSabotageQuestion,
    EjectionColorQuestion,
    ReportColorQuestion,
    LastMeetingColorQuestion,
    RoleBasisQuestion,
    RoleFactionQuestion,
    MeetingCountQuestion,
    FactionQuestion,
    RemovedFactionQuestion,
    ButtonPressedBeforeThisQuestion,
    DiedFirstRoundCountQuestion,
    NameOriginQuestion,
    PlrDeathReasonQuestion,
    PlrDeathMethodQuestion,
    RoleAddedQuestion,
    PlrDeathKillerFactionQuestion,
}

public enum Sabotages
{
    None = -1,

    Lights,
    Reactor,
    O2,
    Communications,
    MushroomMixup
}