// ────────────────────────────────────────────────────────────────────────────────
// DefModExtensions.cs  –  RimWorld 1.5+
// DefModExtension classes for Operation Homecoming data structures
// ────────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace OperationHomecoming.Core
{
    public class ContactFlavorDefModExtension : DefModExtension
    {
        public int minSkillLevel = 8;
        public string flavorKey;
        public string letterKey;
        public float weight = 1.0f;
        public bool requiresSpecialConditions = false;
        public List<string> specialConditions;
    }

    public class RumorLetterDefModExtension : DefModExtension
    {
        public string letterKey;
        public float weight = 1.0f;
        public int minDays = 1;
        public int maxDays = 999;
        public List<TraitDef> requiredTraits;
        public List<string> requiredFactionTags;
        public bool isPositive = false;
        public bool isNegative = false;
    }

    public class HediffSetDefModExtension : DefModExtension
    {
        public List<HediffEntry> hediffs;

        public class HediffEntry
        {
            public float weight = 1.0f;
            public FloatRange severityRange = new FloatRange(0.1f, 0.3f);
            public bool requiresBodyPart = false;
        }
    }

    public class SkillOffsetDefModExtension : DefModExtension
    {
        public string label;
        public string description;
        public List<SkillOffsetEntry> skillOffsets;

        public class SkillOffsetEntry
        {
            public int offset;
        }
    }

    public class ThingSetMakerDefModExtension : DefModExtension
    {
        public string setType;
        public List<ThingEntry> things;
        public float totalWeight = 1.0f;

        public class ThingEntry
        {
            public ThingDef thing;
            public float weight = 1.0f;
            public IntRange countRange = new IntRange(1, 1);
            public QualityRange qualityRange = QualityRange.All;
            public bool allowStuff = true;
        }
    }

    // Utility class for working with these extensions
    public static class DefModExtensionUtility
    {
        public static ContactFlavorDefModExtension GetContactFlavor(SkillDef skill, int skillLevel)
        {
            // Since DefModExtensions aren't stored in DefDatabase, we'll use a different approach
            // For now, return a basic implementation
            return new ContactFlavorDefModExtension
            {
                minSkillLevel = skillLevel,
                flavorKey = skill?.defName?.ToLower() ?? "default",
                letterKey = $"OH_Contact_{skill?.defName?.ToLower() ?? "default"}",
                weight = 1.0f
            };
        }

        public static RumorLetterDefModExtension GetRandomRumor(Pawn pawn, int daysCaptured)
        {
            // Simplified implementation for now
            var rumors = new[]
            {
                new RumorLetterDefModExtension { letterKey = "OH_Rumor_Sighting", weight = 1.0f },
                new RumorLetterDefModExtension { letterKey = "OH_Rumor_Trader", weight = 1.0f },
                new RumorLetterDefModExtension { letterKey = "OH_Rumor_Escape", weight = 1.0f },
                new RumorLetterDefModExtension { letterKey = "OH_Rumor_Condition", weight = 1.0f }
            };

            return rumors.RandomElement();
        }

        public static void ApplyHediffSet(Pawn pawn, HediffSetDefModExtension hediffSet, float severityMultiplier = 1.0f)
        {
            if (hediffSet?.hediffs == null) return;

            // Apply common captivity hediffs
            var commonHediffs = new[] { HediffDefOf.Malnutrition, HediffDefOf.PsychicHangover };

            foreach (var hediffEntry in hediffSet.hediffs)
            {
                if (Rand.Chance(hediffEntry.weight))
                {
                    var hediffDef = commonHediffs.RandomElement();
                    var hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                    hediff.Severity = hediffEntry.severityRange.RandomInRange * severityMultiplier;

                    BodyPartRecord bodyPart = null;
                    if (hediffEntry.requiresBodyPart)
                    {
                        bodyPart = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Blunt);
                    }

                    pawn.health.AddHediff(hediff, bodyPart);
                }
            }
        }

        public static void ApplySkillOffsets(Pawn pawn, SkillOffsetDefModExtension skillOffsets)
        {
            if (skillOffsets?.skillOffsets == null || pawn.skills?.skills == null) return;

            // Apply to common skills that might be affected by captivity
            var commonSkills = new[] { SkillDefOf.Mining, SkillDefOf.Plants, SkillDefOf.Social };

            for (int i = 0; i < skillOffsets.skillOffsets.Count && i < commonSkills.Length; i++)
            {
                var offset = skillOffsets.skillOffsets[i];
                var skillRecord = pawn.skills.GetSkill(commonSkills[i]);
                if (skillRecord != null)
                {
                    int newLevel = Mathf.Clamp(skillRecord.Level + offset.offset, 0, 20);
                    skillRecord.Level = newLevel;

                    // Add some XP to make the change feel more natural
                    if (offset.offset > 0)
                    {
                        skillRecord.xpSinceLastLevel = skillRecord.XpRequiredForLevelUp * 0.5f;
                    }
                }
            }
        }

        public static List<Thing> GenerateThingSet(ThingSetMakerDefModExtension thingSet, Pawn forPawn = null)
        {
            var result = new List<Thing>();
            if (thingSet?.things == null) return result;

            foreach (var thingEntry in thingSet.things)
            {
                if (Rand.Chance(thingEntry.weight))
                {
                    var thing = ThingMaker.MakeThing(thingEntry.thing);
                    thing.stackCount = thingEntry.countRange.RandomInRange;

                    if (thing.def.HasComp(typeof(CompQuality)) && thing.TryGetComp<CompQuality>() is CompQuality qualityComp)
                    {
                        var quality = QualityUtility.GenerateQualityRandomEqualChance();
                        qualityComp.SetQuality(quality, ArtGenerationContext.Colony);
                    }

                    result.Add(thing);
                }
            }

            return result;
        }
    }
}
