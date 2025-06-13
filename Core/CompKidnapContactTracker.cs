// ────────────────────────────────────────────────────────────────────────────────
// CompKidnapContactTracker.cs  –  RimWorld 1.5+
// Tracks kidnapped pawns and handles contact attempts based on skills
// ────────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OperationHomecoming.Core
{
    public class CompKidnapContactTracker : ThingComp
    {
        public int DaysCaptured => daysCaptured;
        public int AttemptCount => attemptIndex;
        public int DaysUntilNextAttempt => nextAttemptTick == -1 ? -1 :
            Mathf.Max(0, (nextAttemptTick - Find.TickManager.TicksGame) / GenDate.TicksPerDay);

        private int nextAttemptTick = -1;
        private int attemptIndex = 0;
        private int daysCaptured = 0;
        private SkillDef highestSkillCache;
        private int highestSkillLevelCache = 0;
        private bool hasBeenKidnapped = false;

        public CompProperties_KidnapContactTracker Props => (CompProperties_KidnapContactTracker)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // Only proceed if we have a valid game state
            if (Current.Game == null || Find.TickManager == null)
                return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            if (!respawningAfterLoad && pawn.IsKidnapped())
            {
                InitializeTracking();
            }
        }

        public override void CompTick()
        {
            var pawn = parent as Pawn;
            if (pawn == null) return;

            // Check if pawn just became kidnapped
            if (pawn.IsKidnapped() && !hasBeenKidnapped)
            {
                InitializeTracking();
                return;
            }

            // If no longer kidnapped, stop tracking
            if (!pawn.IsKidnapped())
            {
                hasBeenKidnapped = false;
                return;
            }

            // Continue tracking if kidnapped
            if (hasBeenKidnapped)
            {
                // Check for contact attempts every tick, not just daily
                if (Find.TickManager.TicksGame >= nextAttemptTick)
                {
                    RollContact(pawn);
                    ScheduleNextAttempt();
                }

                // Update days captured only once per day
                if (Find.TickManager.TicksGame % GenDate.TicksPerDay == 0)
                {
                    daysCaptured++;
                }
            }
        }

        private void InitializeTracking()
        {
            hasBeenKidnapped = true;
            daysCaptured = 0;
            attemptIndex = 0;
            CacheHighestSkill();
            ScheduleNextAttempt();

            Log.Message($"[OpHomecoming] Started tracking {parent.LabelShort} - highest skill: {highestSkillCache?.label} ({highestSkillLevelCache})");
        }

        private void ScheduleNextAttempt()
        {
            var settings = OperationHomecomingMod.Settings;
            int delayDays;

            if (attemptIndex == 0)
            {
                // First attempt uses initial delay range
                int minDays = settings?.contactAttemptDelayMinDays ?? 3;
                int maxDays = settings?.contactAttemptDelayMaxDays ?? 7;
                delayDays = Rand.RangeInclusive(minDays, maxDays);
            }
            else
            {
                // Subsequent attempts use interval
                delayDays = settings?.attemptIntervalDays ?? 5;
            }

            nextAttemptTick = Find.TickManager.TicksGame + (delayDays * GenDate.TicksPerDay);
        }

        private void CacheHighestSkill()
        {
            var pawn = parent as Pawn;
            if (pawn?.skills?.skills == null) return;

            highestSkillCache = null;
            highestSkillLevelCache = 0;

            foreach (var skillRecord in pawn.skills.skills)
            {
                if (skillRecord.Level > highestSkillLevelCache)
                {
                    highestSkillLevelCache = skillRecord.Level;
                    highestSkillCache = skillRecord.def;
                }
            }
        }

        private void RollContact(Pawn pawn)
        {
            attemptIndex++;

            var settings = OperationHomecomingMod.Settings;
            float successChance = CalculateSuccessChance();

            Log.Message($"[OpHomecoming] {pawn.LabelShort} contact attempt #{attemptIndex}, chance: {successChance:F1}%");

            if (Rand.Chance(successChance / 100f))
            {
                // Success - emit signal for quest creation
                Find.SignalManager.SendSignal(new Signal("Signal_ContactSuccess", pawn.Named("PAWN")));
                Log.Message($"[OpHomecoming] Contact successful for {pawn.LabelShort}!");
            }
            else
            {
                // Failure - emit signal for rumor/investigation
                Find.SignalManager.SendSignal(new Signal("Signal_ContactFailed", pawn.Named("PAWN"), attemptIndex.Named("ATTEMPT")));
                Log.Message($"[OpHomecoming] Contact failed for {pawn.LabelShort}");
            }
        }

        private float CalculateSuccessChance()
        {
            var settings = OperationHomecomingMod.Settings;

            float baseChance = settings?.baseSuccessPercent ?? 30f;
            float skillBonus = 0f;

            if (highestSkillCache != null && highestSkillLevelCache > 0)
            {
                // +10% per 4 skill levels by default
                float increment = settings?.perSkillIncrementPer4pts ?? 10f;
                skillBonus = (highestSkillLevelCache / 4f) * increment;
            }

            float totalChance = baseChance + skillBonus;
            float maxChance = settings?.maxSuccessPercent ?? 80f;
            return Math.Min(totalChance, maxChance);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAttemptTick, "nextAttemptTick", -1);
            Scribe_Values.Look(ref attemptIndex, "attemptIndex", 0);
            Scribe_Values.Look(ref daysCaptured, "daysCaptured", 0);
            Scribe_Values.Look(ref hasBeenKidnapped, "hasBeenKidnapped", false);
            Scribe_Values.Look(ref highestSkillLevelCache, "highestSkillLevelCache", 0);
            Scribe_Defs.Look(ref highestSkillCache, "highestSkillCache");
        }

        public override string CompInspectStringExtra()
        {
            if (!hasBeenKidnapped) return null;

            var settings = OperationHomecomingMod.Settings;
            int daysUntilNext = Mathf.Max(0, (nextAttemptTick - Find.TickManager.TicksGame) / GenDate.TicksPerDay);

            return $"Days captive: {daysCaptured}\nNext contact attempt: {daysUntilNext} days\nAttempts made: {attemptIndex}";
        }
    }

    public class CompProperties_KidnapContactTracker : CompProperties
    {
        public CompProperties_KidnapContactTracker()
        {
            compClass = typeof(CompKidnapContactTracker);
        }
    }
}

namespace OperationHomecoming
{
    public static class PawnExtensions
    {
        /// <summary>Returns true if pawn is a prisoner of a non-player faction (i.e., kidnapped).</summary>
        public static bool IsKidnapped(this Pawn pawn)
        {
            // Add safety checks
            if (pawn?.guest == null || Faction.OfPlayer == null)
                return false;

            return pawn.IsPrisoner && pawn.Faction != Faction.OfPlayer;
        }
    }
}