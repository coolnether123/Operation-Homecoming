using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using OperationHomecoming.Core;

namespace OperationHomecoming.TestFixture
{
    public static class BehaviorFixtureActions
    {
        private const string Prefix = "[Operation Homecoming Fixture] ";
        private static readonly List<Pawn> kidnappedPawns = new List<Pawn>();

        [DebugAction("Operation Homecoming Fixture", "Set up next kidnapped pawn", actionType = DebugActionType.Action)]
        public static void SetupKidnappedPawn()
        {
            Pawn pawn = Find.ColonistBar.GetColonistsInOrder()
                .FirstOrDefault(p => !kidnappedPawns.Contains(p));
            Faction captor = Find.FactionManager.AllFactions.FirstOrDefault(
                f => f != null && f != Faction.OfPlayer &&
                     f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));
            if (pawn == null || captor == null)
            {
                Log.Message(Prefix + "setup skipped: no free colonist and hostile humanlike captor.");
                return;
            }

            pawn.SetFaction(captor);
            pawn.guest.SetGuestStatus(captor, GuestStatus.Prisoner);
            kidnappedPawns.Add(pawn);
            Log.Message(Prefix + "kidnapped pawn=" + pawn.LabelShort +
                        " id=" + pawn.thingIDNumber +
                        " captor=" + captor.def.defName +
                        " prisoner=" + pawn.IsPrisoner);
        }

        [DebugAction("Operation Homecoming Fixture", "Trigger accelerated contact", actionType = DebugActionType.Action)]
        public static void TriggerAcceleratedContact()
        {
            Pawn pawn = kidnappedPawns.LastOrDefault();
            CompKidnapContactTracker tracker = pawn?.GetComp<CompKidnapContactTracker>();
            if (tracker == null)
            {
                Log.Error(Prefix + "no tracked kidnapped pawn; run setup first.");
                return;
            }

            ModSettings_OperationHomecoming settings = OperationHomecomingMod.Settings;
            settings.baseSuccessPercent = 100;
            settings.maxSuccessPercent = 100;
            settings.contactAttemptDelayMinDays = 1;
            settings.contactAttemptDelayMaxDays = 1;

            FieldInfo nextAttempt = typeof(CompKidnapContactTracker).GetField(
                "nextAttemptTick", BindingFlags.Instance | BindingFlags.NonPublic);
            nextAttempt.SetValue(tracker, Find.TickManager.TicksGame - 1);
            tracker.CompTick();
            // A pawn added by the fixture can receive its component after the
            // first tick; run the production tracker once more so both the
            // initialization and accelerated-attempt seams are exercised.
            nextAttempt.SetValue(tracker, Find.TickManager.TicksGame - 1);
            tracker.CompTick();
            LogLifecycle("after-contact", pawn);
        }

        [DebugAction("Operation Homecoming Fixture", "End latest rescue as success", actionType = DebugActionType.Action)]
        public static void EndLatestRescueSuccess()
        {
            Quest quest = LatestRescueQuest();
            if (quest == null)
            {
                Log.Error(Prefix + "no rescue quest available.");
                return;
            }

            quest.End(QuestEndOutcome.Success);
            LogLifecycle("after-success", kidnappedPawns.LastOrDefault());
        }

        [DebugAction("Operation Homecoming Fixture", "End latest rescue as failure", actionType = DebugActionType.Action)]
        public static void EndLatestRescueFailure()
        {
            OperationHomecomingMod.Settings.postFailureRaidChance = 0f;
            Quest quest = LatestRescueQuest();
            if (quest == null)
            {
                Log.Error(Prefix + "no rescue quest available.");
                return;
            }

            quest.End(QuestEndOutcome.Fail);
            LogLifecycle("after-failure-queued", kidnappedPawns.LastOrDefault());
        }

        [DebugAction("Operation Homecoming Fixture", "End latest rescue as return raid", actionType = DebugActionType.Action)]
        public static void EndLatestRescueReturnRaid()
        {
            OperationHomecomingMod.Settings.postFailureRaidChance = 1f;
            Quest quest = LatestRescueQuest();
            if (quest == null)
            {
                Log.Error(Prefix + "no rescue quest available.");
                return;
            }

            quest.End(QuestEndOutcome.Fail);
            LogLifecycle("after-raid-queued", kidnappedPawns.LastOrDefault());
        }

        [DebugAction("Operation Homecoming Fixture", "Process latest queued return", actionType = DebugActionType.Action)]
        public static void ProcessLatestQueuedReturn()
        {
            WorldComponent_OperationHomecoming world = Find.World
                .GetComponent<WorldComponent_OperationHomecoming>();
            HomecomingRecord record = world?.Records.LastOrDefault(
                r => r.returnAtTick >= 0 && HomecomingLifecycleContract.IsTerminal(r.stage));
            if (record == null)
            {
                Log.Error(Prefix + "no queued return is available.");
                return;
            }

            record.returnAtTick = Find.TickManager.TicksGame - 1;
            world.WorldComponentTick();
            LogLifecycle("after-return-processed", record.pawn);
        }

        [DebugAction("Operation Homecoming Fixture", "Force latest camp expiry", actionType = DebugActionType.Action)]
        public static void ForceLatestCampExpiry()
        {
            Quest quest = LatestRescueQuest();
            QuestPart_OperationHomecomingCamp part = quest?.PartsListForReading
                .OfType<QuestPart_OperationHomecomingCamp>().FirstOrDefault();
            KidnapCampSiteComp comp = part?.site?.GetComponent<KidnapCampSiteComp>();
            if (comp == null)
            {
                Log.Message(Prefix + "expiry skipped: no active kidnap camp.");
                return;
            }

            FieldInfo creationTick = typeof(KidnapCampSiteComp).GetField(
                "creationTick", BindingFlags.Instance | BindingFlags.NonPublic);
            creationTick.SetValue(comp, Find.TickManager.TicksGame -
                (OperationHomecomingMod.Settings.rescueWindowDays + 1) * GenDate.TicksPerDay);
            comp.CompTick();
            LogLifecycle("after-expiry", part.captivePawn);
        }

        [DebugAction("Operation Homecoming Fixture", "Report lifecycle state", actionType = DebugActionType.Action)]
        public static void ReportLifecycleState()
        {
            WorldComponent_OperationHomecoming world = Find.World
                .GetComponent<WorldComponent_OperationHomecoming>();
            foreach (HomecomingRecord record in world.Records)
            {
                Log.Message(Prefix + "record pawn=" + record.pawn?.LabelShort +
                            " stage=" + record.stage +
                            " quest=" + (record.quest?.root?.defName ?? "none") +
                            " site=" + (record.site?.def?.defName ?? "none") +
                            " returnAt=" + record.returnAtTick +
                            " returnRaid=" + record.returnRaid);
            }
        }

        [DebugAction("Operation Homecoming Fixture", "Open Operation Homecoming settings", actionType = DebugActionType.Action)]
        public static void OpenOperationHomecomingSettings()
        {
            Mod mod = LoadedModManager.GetMod<OperationHomecomingMod_Instance>();
            if (mod != null)
            {
                Find.WindowStack.Add(new Dialog_ModSettings(mod));
                Log.Message(Prefix + "opened settings window");
            }
        }

        [DebugAction("Operation Homecoming Fixture", "Open quest list", actionType = DebugActionType.Action)]
        public static void OpenQuestList()
        {
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Quests);
            Log.Message(Prefix + "opened quest list");
        }

        [DebugAction("Operation Homecoming Fixture", "Report settings boundary", actionType = DebugActionType.Action)]
        public static void ReportSettingsBoundary()
        {
            ModSettings_OperationHomecoming s = OperationHomecomingMod.Settings;
            Log.Message(Prefix + "settings=" +
                "min=" + s.contactAttemptDelayMinDays +
                ",max=" + s.contactAttemptDelayMaxDays +
                ",interval=" + s.attemptIntervalDays +
                ",base=" + s.baseSuccessPercent +
                ",increment=" + s.perSkillIncrementPer4pts +
                ",cap=" + s.maxSuccessPercent +
                ",rumor=" + s.rumorFrequencyDays +
                ",camp=" + s.campStrengthFactor.ToString("F1") +
                ",window=" + s.rescueWindowDays +
                ",prisonBreak=" + s.prisonBreakToggle +
                ",raid=" + s.postFailureRaidChance.ToString("F2") +
                ",illness=" + s.illnessSeverityMultiplier.ToString("F1"));
        }

        [DebugAction("Operation Homecoming Fixture", "Set settings to lower bounds", actionType = DebugActionType.Action)]
        public static void SetSettingsToLowerBounds()
        {
            ModSettings_OperationHomecoming s = OperationHomecomingMod.Settings;
            s.contactAttemptDelayMinDays = 1;
            s.contactAttemptDelayMaxDays = 1;
            s.attemptIntervalDays = 2;
            s.baseSuccessPercent = 5;
            s.perSkillIncrementPer4pts = 5;
            s.maxSuccessPercent = 30;
            s.rumorFrequencyDays = 1;
            s.campStrengthFactor = 0.4f;
            s.rescueWindowDays = 10;
            s.prisonBreakToggle = false;
            s.postFailureRaidChance = 0f;
            s.illnessSeverityMultiplier = 0f;
            ReportSettingsBoundary();
        }

        [DebugAction("Operation Homecoming Fixture", "Set settings to upper bounds", actionType = DebugActionType.Action)]
        public static void SetSettingsToUpperBounds()
        {
            ModSettings_OperationHomecoming s = OperationHomecomingMod.Settings;
            s.contactAttemptDelayMinDays = 10;
            s.contactAttemptDelayMaxDays = 20;
            s.attemptIntervalDays = 14;
            s.baseSuccessPercent = 90;
            s.perSkillIncrementPer4pts = 20;
            s.maxSuccessPercent = 100;
            s.rumorFrequencyDays = 10;
            s.campStrengthFactor = 1.5f;
            s.rescueWindowDays = 90;
            s.prisonBreakToggle = true;
            s.postFailureRaidChance = 1f;
            s.illnessSeverityMultiplier = 3f;
            ReportSettingsBoundary();
        }

        [DebugAction("Operation Homecoming Fixture", "Exercise investigation branch", actionType = DebugActionType.Action)]
        public static void ExerciseInvestigationBranch()
        {
            Pawn pawn = kidnappedPawns.LastOrDefault();
            CompKidnapContactTracker tracker = pawn?.GetComp<CompKidnapContactTracker>();
            if (tracker == null)
            {
                Log.Message(Prefix + "investigation skipped: no tracked kidnapped pawn.");
                return;
            }

            ModSettings_OperationHomecoming settings = OperationHomecomingMod.Settings;
            settings.baseSuccessPercent = 0;
            settings.maxSuccessPercent = 0;
            FieldInfo nextAttempt = typeof(CompKidnapContactTracker).GetField(
                "nextAttemptTick", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 4; i++)
            {
                nextAttempt.SetValue(tracker, Find.TickManager.TicksGame - 1);
                tracker.CompTick();
            }

            Site investigation = Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .LastOrDefault(s => s.parts.Any(p => p.def?.defName == "OH_Investigation"));
            Log.Message(Prefix + "investigation site=" + (investigation != null) +
                        " tile=" + (investigation?.Tile.ToString() ?? "none") +
                        " parts=" + (investigation?.parts.Count ?? 0) +
                        " stage=" + Find.World.GetComponent<WorldComponent_OperationHomecoming>()
                            ?.Records.LastOrDefault(r => r.pawn == pawn)?.stage);
        }

        [DebugAction("Operation Homecoming Fixture", "Exercise prison-break seam", actionType = DebugActionType.Action)]
        public static void ExercisePrisonBreakSeam()
        {
            Pawn pawn = kidnappedPawns.LastOrDefault();
            if (pawn == null)
            {
                Log.Message(Prefix + "prison-break seam skipped: no fixture pawn.");
                return;
            }

            Quest quest = OHQuestUtility.CreatePrisonBreakQuest(pawn);
            bool hasExecutableParts = quest?.PartsListForReading.Any() == true;
            Log.Message(Prefix + "prison-break toggle=" + OperationHomecomingMod.Settings.prisonBreakToggle +
                        " quest=" + (quest != null) +
                        " root=" + (quest?.root?.defName ?? "none") +
                        " executableParts=" + hasExecutableParts +
                        " disposition=" + (hasExecutableParts ? "IMPLEMENTED_SEAM" : "NOT_IMPLEMENTED_DESIGN_BLOCKED"));
            if (quest != null && quest.State == QuestState.Ongoing)
            {
                quest.End(QuestEndOutcome.Fail);
            }
        }

        [DebugAction("Operation Homecoming Fixture", "Generate latest camp map lifecycle", actionType = DebugActionType.Action)]
        public static void GenerateLatestCampMapLifecycle()
        {
            WorldComponent_OperationHomecoming world = Find.World
                .GetComponent<WorldComponent_OperationHomecoming>();
            HomecomingRecord record = world?.Records.LastOrDefault(
                r => r.site != null && !r.site.Destroyed && r.site.def != null &&
                     r.site.parts.Any(p => p.def?.defName == "OH_KidnapCamp"));
            if (record?.site == null)
            {
                Log.Message(Prefix + "site-map skipped: no active kidnap camp.");
                return;
            }

            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(
                record.site.Tile, record.site.PreferredMapSize, null);
            KidnapCampSiteComp comp = record.site.GetComponent<KidnapCampSiteComp>();
            int hostilePawns = map?.mapPawns.AllPawnsSpawned.Count(
                p => p.Faction != Faction.OfPlayer && p.HostileTo(Faction.OfPlayer)) ?? 0;
            Log.Message(Prefix + "site-map generated=" + (map != null) +
                        " parentMatches=" + (map?.Parent == record.site) +
                        " comp=" + (comp != null) +
                        " roster=" + (comp?.GuardRoster?.Count ?? 0) +
                        " captiveSpawned=" + (comp?.CaptivePawn?.Spawned ?? false) +
                        " guardsSpawned=" + hostilePawns +
                        " mapId=" + (map?.uniqueID.ToString() ?? "none"));
        }

        [DebugAction("Operation Homecoming Fixture", "Open latest camp map", actionType = DebugActionType.Action)]
        public static void OpenLatestCampMap()
        {
            HomecomingRecord record = Find.World.GetComponent<WorldComponent_OperationHomecoming>()
                ?.Records.LastOrDefault(r => r.site?.Map != null);
            Map map = record?.site?.Map;
            if (map == null)
            {
                Log.Message(Prefix + "site-map open skipped: no generated map.");
                return;
            }

            Current.Game.CurrentMap = map;
            Find.CameraDriver.JumpToCurrentMapLoc(map.Center);
            Log.Message(Prefix + "site-map opened mapId=" + map.uniqueID +
                        " pawns=" + map.mapPawns.AllPawnsSpawned.Count +
                        " playerPawns=" + map.mapPawns.AllPawnsSpawned.Count(p => p.Faction == Faction.OfPlayer));
        }

        [DebugAction("Operation Homecoming Fixture", "Cleanup latest generated site map", actionType = DebugActionType.Action)]
        public static void CleanupLatestGeneratedSiteMap()
        {
            WorldComponent_OperationHomecoming world = Find.World
                .GetComponent<WorldComponent_OperationHomecoming>();
            HomecomingRecord record = world?.Records.LastOrDefault(r => r.site?.Map != null);
            Map map = record?.site?.Map;
            if (map == null)
            {
                Log.Message(Prefix + "site-map cleanup skipped: no generated site map.");
                return;
            }

            Current.Game.DeinitAndRemoveMap(map, true);
            Log.Message(Prefix + "site-map cleaned=" + (record.site.Map == null));
        }

        [DebugAction("Operation Homecoming Fixture", "Cleanup investigation sites", actionType = DebugActionType.Action)]
        public static void CleanupInvestigationSites()
        {
            List<Site> investigations = Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .Where(s => !s.Destroyed && s.parts.Any(p => p.def?.defName == "OH_Investigation"))
                .ToList();
            foreach (Site site in investigations)
            {
                site.Destroy();
            }

            int remaining = Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .Count(s => !s.Destroyed && s.parts.Any(p => p.def?.defName == "OH_Investigation"));
            Log.Message(Prefix + "investigation cleanup destroyed=" + investigations.Count +
                        " remaining=" + remaining);
        }

        private static Quest LatestRescueQuest()
        {
            return Find.QuestManager.QuestsListForReading.LastOrDefault(
                q => q.root?.defName == "OH_RescueCaptive" &&
                     (q.State == QuestState.Ongoing || q.State == QuestState.NotYetAccepted));
        }

        private static void LogLifecycle(string action, Pawn pawn)
        {
            WorldComponent_OperationHomecoming world = Find.World
                .GetComponent<WorldComponent_OperationHomecoming>();
            HomecomingRecord record = world.Records.FirstOrDefault(r => r.pawn == pawn);
            Log.Message(Prefix + action +
                        " pawn=" + pawn?.LabelShort +
                        " faction=" + pawn?.Faction?.def?.defName +
                        " host=" + pawn?.HostFaction?.def?.defName +
                        " prisoner=" + (pawn?.IsPrisoner ?? false) +
                        " stage=" + record?.stage +
                        " quests=" + Find.QuestManager.QuestsListForReading.Count(q => q.root?.defName == "OH_RescueCaptive") +
                        " sites=" + Find.WorldObjects.Sites.Count(s => s.GetComponent<KidnapCampSiteComp>() != null) +
                        " allSites=" + Find.WorldObjects.AllWorldObjects.OfType<Site>().Count() +
                        " spawned=" + (record?.site?.Spawned ?? false) +
                        " destroyed=" + (record?.site?.Destroyed ?? false));
        }
    }
}
