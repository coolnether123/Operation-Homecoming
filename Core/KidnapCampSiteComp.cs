// ────────────────────────────────────────────────────────────────────────────────
// KidnapCampSiteComp.cs  –  RimWorld 1.5+
// WorldObjectComp for kidnap camp sites with layout and guard management
// ────────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace OperationHomecoming.Core
{
    public class WorldObjectCompProperties_KidnapCamp : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_KidnapCamp()
        {
            compClass = typeof(KidnapCampSiteComp);
        }
    }

    public class KidnapCampSiteComp : WorldObjectComp
    {
        private Pawn captivePawn;
        private int layoutSeed;
        private List<Pawn> guardRoster = new List<Pawn>();
        private int creationTick;
        private bool isReinforced = false;
        private string contactFlavor;
        private Quest rescueQuest;
        private bool expiryHandled;

        public Pawn CaptivePawn => captivePawn;
        public int LayoutSeed => layoutSeed;
        public List<Pawn> GuardRoster => guardRoster;
        public bool IsReinforced => isReinforced;
        public string ContactFlavor => contactFlavor;
        public Quest RescueQuest => rescueQuest;

        public void Initialize(Pawn pawn, string flavor = null)
        {
            if (captivePawn == pawn && guardRoster.Count > 0)
            {
                contactFlavor = flavor ?? contactFlavor ?? "default";
                return;
            }

            captivePawn = pawn;
            contactFlavor = flavor ?? "default";
            layoutSeed = Rand.Int;
            creationTick = Find.TickManager.TicksGame;

            GenerateGuardRoster();

            Log.Message($"[OpHomecoming] Initialized kidnap camp for {pawn.LabelShort} with {guardRoster.Count} guards");
        }

        public void AttachQuest(Quest quest)
        {
            rescueQuest = quest;
        }

        private void GenerateGuardRoster()
        {
            var settings = OperationHomecomingMod.Settings;

            // Base guard count based on camp strength factor
            int baseGuards = Rand.RangeInclusive(3, 6);
            int adjustedGuards = Mathf.RoundToInt(baseGuards * settings.campStrengthFactor);
            adjustedGuards = Mathf.Clamp(adjustedGuards, 2, 10);

            // Find hostile faction for guards
            var hostileFaction = FindHostileFaction();
            if (hostileFaction == null)
            {
                Log.Warning("[OpHomecoming] Could not find hostile faction for guard generation");
                return;
            }

            // Generate guards
            for (int i = 0; i < adjustedGuards; i++)
            {
                var pawnKind = GetGuardPawnKind(hostileFaction, i == 0); // First guard is leader
                var guard = PawnGenerator.GeneratePawn(pawnKind, hostileFaction);

                if (guard != null)
                {
                    guardRoster.Add(guard);
                }
            }

            // Add special guards based on contact flavor
            AddFlavorSpecificGuards(hostileFaction);
        }

        private Faction FindHostileFaction()
        {
            // Try to find a faction that would realistically kidnap colonists
            var candidates = new List<Faction>();

            foreach (var faction in Find.FactionManager.AllFactions)
            {
                if (faction.HostileTo(Faction.OfPlayer) &&
                    !faction.def.hidden &&
                    faction.def.humanlikeFaction &&
                    faction.def.pawnGroupMakers != null)
                {
                    candidates.Add(faction);
                }
            }

            return candidates.Count > 0 ? candidates.RandomElement() : null;
        }

        private PawnKindDef GetGuardPawnKind(Faction faction, bool isLeader)
        {
            if (faction?.def?.pawnGroupMakers == null) return PawnKindDefOf.Villager;

            foreach (var groupMaker in faction.def.pawnGroupMakers)
            {
                if (groupMaker.kindDef == PawnGroupKindDefOf.Combat)
                {
                    var options = groupMaker.options;
                    if (options != null && options.Count > 0)
                    {
                        if (isLeader)
                        {
                            // Try to find a leader-type pawn
                            var leaders = new List<PawnGenOption>();
                            foreach (var option in options.Where(o => o != null && o.kind != null))
                            {
                                var label = (option.kind.labelPlural ?? option.kind.label ?? string.Empty).ToLower();
                                if (label.Contains("leader") || label.Contains("boss") || label.Contains("chief"))
                                {
                                    leaders.Add(option);
                                }
                            }
                            if (leaders.Count > 0)
                            {
                                var weightedLeaders = leaders.Where(o => o.selectionWeight > 0f).ToList();
                                return (weightedLeaders.Count > 0
                                    ? weightedLeaders.RandomElementByWeight(o => o.selectionWeight)
                                    : leaders[0]).kind;
                            }
                        }

                        var validOptions = options.Where(o => o != null && o.kind != null).ToList();
                        if (validOptions.Count > 0)
                        {
                            var weightedOptions = validOptions.Where(o => o.selectionWeight > 0f).ToList();
                            return (weightedOptions.Count > 0
                                ? weightedOptions.RandomElementByWeight(o => o.selectionWeight)
                                : validOptions[0]).kind;
                        }
                    }
                }
            }

            return PawnKindDefOf.Villager;
        }

        private void AddFlavorSpecificGuards(Faction faction)
        {
            // Add special guards based on the contact flavor
            switch (contactFlavor)
            {
                case "mass_food_poisoning":
                    // Add 2 extra turrets worth of guards
                    for (int i = 0; i < 2; i++)
                    {
                        var guard = PawnGenerator.GeneratePawn(GetGuardPawnKind(faction, false), faction);
                        if (guard != null)
                        {
                            guardRoster.Add(guard);
                        }
                    }
                    break;

                case "structural_flaw_note":
                    // Remove one guard (wall collapse creates alternate entry)
                    if (guardRoster.Count > 2)
                    {
                        guardRoster.RemoveAt(guardRoster.Count - 1);
                    }
                    break;

                case "medical":
                    // Add an extra downed neutral pawn
                    Faction neutralFaction = null;
                    foreach (var f in Find.FactionManager.AllFactions)
                    {
                        if (!f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction)
                        {
                            neutralFaction = f;
                            break;
                        }
                    }
                    if (neutralFaction != null)
                    {
                        var prisoner = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager, neutralFaction);
                        if (prisoner != null)
                        {
                            // Make them downed
                            prisoner.health.AddHediff(HediffDefOf.Anesthetic);
                            guardRoster.Add(prisoner); // Will be handled specially in map generation
                        }
                    }
                    break;
            }
        }

        public void SetReinforced(bool reinforced)
        {
            isReinforced = reinforced;
            if (reinforced)
            {
                // Add extra guards for reinforcement
                var hostileFaction = guardRoster.FirstOrDefault()?.Faction;
                if (hostileFaction != null)
                {
                    var extraGuard = PawnGenerator.GeneratePawn(GetGuardPawnKind(hostileFaction, false), hostileFaction);
                    if (extraGuard != null)
                    {
                        guardRoster.Add(extraGuard);
                    }
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            // Check for quest expiry
            var settings = OperationHomecomingMod.Settings;
            int daysSinceCreation = (Find.TickManager.TicksGame - creationTick) / GenDate.TicksPerDay;

            if (!expiryHandled && daysSinceCreation >= settings.rescueWindowDays)
            {
                expiryHandled = true;
                // Quest expired - trigger failure
                HandleQuestExpiry();
            }

            Site site = parent as Site;
            if (rescueQuest != null && rescueQuest.State == QuestState.Ongoing &&
                site?.HasMap == true && captivePawn != null &&
                guardRoster.Count > 0 &&
                guardRoster.All(guard => guard == null || guard.Destroyed || guard.Dead))
            {
                rescueQuest.End(QuestEndOutcome.Success);
            }
        }

        private void HandleQuestExpiry()
        {
            Log.Message($"[OpHomecoming] Kidnap camp quest expired for {captivePawn?.LabelShort}");

            Find.World.GetComponent<WorldComponent_OperationHomecoming>()
                ?.TryResolve(captivePawn, HomecomingLifecycleStage.Expired);

            if (rescueQuest != null && rescueQuest.State == QuestState.Ongoing)
            {
                rescueQuest.End(QuestEndOutcome.Fail);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref captivePawn, "captivePawn");
            Scribe_Values.Look(ref layoutSeed, "layoutSeed");
            Scribe_Values.Look(ref creationTick, "creationTick");
            Scribe_Values.Look(ref isReinforced, "isReinforced");
            Scribe_Values.Look(ref contactFlavor, "contactFlavor");
            Scribe_References.Look(ref rescueQuest, "rescueQuest");
            Scribe_Values.Look(ref expiryHandled, "expiryHandled");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                guardRoster.Clear();
                Site site = parent as Site;
                if (site?.Map != null)
                {
                    guardRoster.AddRange(site.Map.mapPawns.AllPawnsSpawned
                        .Where(p => p != captivePawn && p.Faction != null && p.Faction != Faction.OfPlayer));
                }
                else if (captivePawn != null)
                {
                    GenerateGuardRoster();
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (captivePawn == null) return null;

            var settings = OperationHomecomingMod.Settings;
            int daysSinceCreation = (Find.TickManager.TicksGame - creationTick) / GenDate.TicksPerDay;
            int daysRemaining = settings.rescueWindowDays - daysSinceCreation;

            return $"Holding: {captivePawn.LabelShort}\nGuards: {guardRoster.Count}\nDays remaining: {daysRemaining}";
        }
    }
}
