// ────────────────────────────────────────────────────────────────────────────────
// ModSettings_OperationHomecoming.cs  –  RimWorld 1.5+
// Configurable settings for Operation Homecoming mod
// ────────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using Verse;

namespace OperationHomecoming.Core
{
    public class ModSettings_OperationHomecoming : ModSettings
    {
        // Contact timing settings
        public int contactAttemptDelayMinDays = 3;
        public int contactAttemptDelayMaxDays = 7;
        public int attemptIntervalDays = 5;

        // Success chance settings
        public int baseSuccessPercent = 30;
        public int perSkillIncrementPer4pts = 10;
        public int maxSuccessPercent = 80;

        // Quest and camp settings
        public int rumorFrequencyDays = 4;
        public float campStrengthFactor = 0.7f;
        public int rescueWindowDays = 30;

        // Feature toggles
        public bool prisonBreakToggle = true;

        // Post-failure settings
        public float postFailureRaidChance = 0.6f;
        public float illnessSeverityMultiplier = 1.0f;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref contactAttemptDelayMinDays, "contactAttemptDelayMinDays", 3);
            Scribe_Values.Look(ref contactAttemptDelayMaxDays, "contactAttemptDelayMaxDays", 7);
            Scribe_Values.Look(ref attemptIntervalDays, "attemptIntervalDays", 5);

            // Keep persisted settings valid even when an older or hand-edited
            // config contains an inverted first-contact range.
            contactAttemptDelayMinDays = Mathf.Clamp(contactAttemptDelayMinDays, 1, 10);
            contactAttemptDelayMaxDays = Mathf.Clamp(contactAttemptDelayMaxDays, contactAttemptDelayMinDays, 20);
            attemptIntervalDays = Mathf.Clamp(attemptIntervalDays, 2, 14);

            Scribe_Values.Look(ref baseSuccessPercent, "baseSuccessPercent", 30);
            Scribe_Values.Look(ref perSkillIncrementPer4pts, "perSkillIncrementPer4pts", 10);
            Scribe_Values.Look(ref maxSuccessPercent, "maxSuccessPercent", 80);

            Scribe_Values.Look(ref rumorFrequencyDays, "rumorFrequencyDays", 4);
            Scribe_Values.Look(ref campStrengthFactor, "campStrengthFactor", 0.7f);
            Scribe_Values.Look(ref rescueWindowDays, "rescueWindowDays", 30);

            Scribe_Values.Look(ref prisonBreakToggle, "prisonBreakToggle", true);
            Scribe_Values.Look(ref postFailureRaidChance, "postFailureRaidChance", 0.6f);
            Scribe_Values.Look(ref illnessSeverityMultiplier, "illnessSeverityMultiplier", 1.0f);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Contact timing section
            listing.Label("OH_Settings_ContactTiming".Translate());
            listing.Gap();

            listing.Label("OH_Settings_ContactDelayMin".Translate() + ": " + contactAttemptDelayMinDays + " " + "DaysLower".Translate());
            contactAttemptDelayMinDays = (int)listing.Slider(contactAttemptDelayMinDays, 1, 10);

            listing.Label("OH_Settings_ContactDelayMax".Translate() + ": " + contactAttemptDelayMaxDays + " " + "DaysLower".Translate());
            contactAttemptDelayMaxDays = (int)listing.Slider(contactAttemptDelayMaxDays, contactAttemptDelayMinDays, 20);

            listing.Label("OH_Settings_AttemptInterval".Translate() + ": " + attemptIntervalDays + " " + "DaysLower".Translate());
            attemptIntervalDays = (int)listing.Slider(attemptIntervalDays, 2, 14);

            listing.Gap();

            // Success chance section
            listing.Label("OH_Settings_SuccessChance".Translate());
            listing.Gap();

            listing.Label("OH_Settings_BaseSuccess".Translate() + ": " + baseSuccessPercent + "%");
            baseSuccessPercent = (int)listing.Slider(baseSuccessPercent, 5, 90);

            listing.Label("OH_Settings_SkillIncrement".Translate() + ": " + perSkillIncrementPer4pts + "%");
            perSkillIncrementPer4pts = (int)listing.Slider(perSkillIncrementPer4pts, 5, 20);

            listing.Label("OH_Settings_MaxSuccess".Translate() + ": " + maxSuccessPercent + "%");
            maxSuccessPercent = (int)listing.Slider(maxSuccessPercent, 30, 100);

            listing.Gap();

            // Quest settings section
            listing.Label("OH_Settings_QuestSettings".Translate());
            listing.Gap();

            listing.Label("OH_Settings_RumorFrequency".Translate() + ": " + rumorFrequencyDays + " " + "DaysLower".Translate());
            rumorFrequencyDays = (int)listing.Slider(rumorFrequencyDays, 1, 10);

            listing.Label("OH_Settings_CampStrength".Translate() + ": " + campStrengthFactor.ToString("F1"));
            campStrengthFactor = listing.Slider(campStrengthFactor, 0.4f, 1.5f);

            listing.Label("OH_Settings_RescueWindow".Translate() + ": " + rescueWindowDays + " " + "DaysLower".Translate());
            rescueWindowDays = (int)listing.Slider(rescueWindowDays, 10, 90);

            listing.Gap();

            // Feature toggles section
            listing.Label("OH_Settings_Features".Translate());
            listing.Gap();

            listing.CheckboxLabeled("OH_Settings_PrisonBreak".Translate(), ref prisonBreakToggle);

            listing.Gap();

            // Post-failure settings section
            listing.Label("OH_Settings_PostFailure".Translate());
            listing.Gap();

            listing.Label("OH_Settings_RaidChance".Translate() + ": " + (postFailureRaidChance * 100).ToString("F0") + "%");
            postFailureRaidChance = listing.Slider(postFailureRaidChance, 0f, 1f);

            listing.Label("OH_Settings_IllnessSeverity".Translate() + ": " + illnessSeverityMultiplier.ToString("F1"));
            illnessSeverityMultiplier = listing.Slider(illnessSeverityMultiplier, 0f, 3f);

            listing.End();
        }
    }
}
