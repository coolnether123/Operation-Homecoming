// ────────────────────────────────────────────────────────────────────────────────
// OperationHomecoming.cs  –  RimWorld 1.5+
// Core mod initialization and main entry point for Operation Homecoming
// ────────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace OperationHomecoming.Core
{
    [StaticConstructorOnStartup]
    public static class OperationHomecomingMod
    {
        public static ModSettings_OperationHomecoming Settings
        {
            get
            {
                var mod = LoadedModManager.GetMod<OperationHomecomingMod_Instance>();
                return mod?.GetSettings<ModSettings_OperationHomecoming>();
            }
        }

        static OperationHomecomingMod()
        {
            try
            {
                var harmony = new Harmony("coolnether123.OperationHomecoming");
                harmony.PatchAll();
                Log.Message("[OpHomecoming] Harmony patches applied successfully.");
            }
            catch (System.Exception e)
            {
                Log.Error($"[OpHomecoming] Failed to apply Harmony patches: {e}");
            }
        }
    }

    public class OperationHomecomingMod_Instance : Mod
    {
        public OperationHomecomingMod_Instance(ModContentPack content) : base(content)
        {
            GetSettings<ModSettings_OperationHomecoming>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            GetSettings<ModSettings_OperationHomecoming>().DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Operation Homecoming";
        }
    }

    // Incident worker classes for Operation Homecoming

    /// <summary>Dev-mode debug tools for Operation Homecoming.</summary>
    public static class DebugTools_OpHome
    {
        [DebugAction("OpHomecoming", "Force Contact (selected pawn)")]
        private static void ForceContact()
        {
            var p = Find.Selector.SingleSelectedThing as Pawn;
            if (p == null)
            {
                Messages.Message("Select a pawn first.", MessageTypeDefOf.RejectInput);
                return;
            }

            var comp = p.GetComp<CompKidnapContactTracker>();
            if (comp == null)
            {
                Messages.Message("CompKidnapContactTracker not attached.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Trigger a contact attempt manually
            Find.SignalManager.SendSignal(new Signal("Signal_ContactSuccess", p.Named("PAWN")));
        }

        [DebugAction("OpHomecoming", "Make Pawn Kidnapped (selected)")]
        private static void MakePawnKidnapped()
        {
            var p = Find.Selector.SingleSelectedThing as Pawn;
            if (p == null)
            {
                Messages.Message("Select a pawn first.", MessageTypeDefOf.RejectInput);
                return;
            }

            if (p.Faction != Faction.OfPlayer)
            {
                Messages.Message("Can only kidnap player pawns.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Find a hostile faction to assign the pawn to
            var hostileFaction = Find.FactionManager.AllFactions
                .Where(f => f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction)
                .RandomElementWithFallback();

            if (hostileFaction == null)
            {
                Messages.Message("No hostile factions available.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Make them a prisoner of the hostile faction
            if (p.Spawned)
            {
                p.DeSpawn();
            }

            p.SetFaction(hostileFaction);
            p.guest.SetGuestStatus(hostileFaction, GuestStatus.Prisoner);

            // Ensure they have the tracking component
            var comp = p.GetComp<CompKidnapContactTracker>();
            if (comp == null)
            {
                comp = new CompKidnapContactTracker
                {
                    parent = p,
                    props = new CompProperties_KidnapContactTracker()
                };
                p.AllComps.Add(comp);
            }

            Messages.Message($"{p.LabelShort} has been kidnapped by {hostileFaction.Name}!", MessageTypeDefOf.NeutralEvent);
        }

        [DebugAction("OpHomecoming", "List All Quest Script Defs")]
        private static void ListQuestScriptDefs()
        {
            var questDefs = DefDatabase<QuestScriptDef>.AllDefsListForReading;

            var options = new List<DebugMenuOption>();

            foreach (var def in questDefs.OrderBy(d => d.defName))
            {
                options.Add(new DebugMenuOption(
                    $"{def.defName} - {def.label ?? "No Label"}",
                    DebugMenuOptionMode.Action,
                    () => {
                        var slate = new Slate();
                        if (Faction.OfPlayer != null)
                            slate.Set("asker", Faction.OfPlayer);

                        try
                        {
                            var quest = QuestUtility.GenerateQuestAndMakeAvailable(def, slate);
                            if (quest != null)
                                Messages.Message($"Generated quest: {quest.name}", MessageTypeDefOf.PositiveEvent);
                            else
                                Messages.Message($"Failed to generate quest: {def.defName}", MessageTypeDefOf.RejectInput);
                        }
                        catch (System.Exception e)
                        {
                            Messages.Message($"Error generating {def.defName}: {e.Message}", MessageTypeDefOf.RejectInput);
                            Log.Error($"Quest generation error: {e}");
                        }
                    }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction("OpHomecoming", "Kidnapped Pawns Status")]
        private static void ShowKidnappedPawnsStatus()
        {
            var kidnappedPawns = new List<string>();

            // Check all pawns across all maps and caravans
            var allPawns = PawnsFinder.All_AliveOrDead;

            foreach (var pawn in allPawns)
            {
                if (pawn.IsKidnapped())
                {
                    var comp = pawn.GetComp<CompKidnapContactTracker>();
                    if (comp != null)
                    {
                        var daysCaptured = comp.DaysCaptured;
                        var attemptsCount = comp.AttemptCount;
                        var nextAttemptDays = comp.DaysUntilNextAttempt;

                        kidnappedPawns.Add($"{pawn.LabelShort} - {daysCaptured} days captive, {attemptsCount} attempts, next in {nextAttemptDays} days");
                    }
                    else
                    {
                        kidnappedPawns.Add($"{pawn.LabelShort} - Kidnapped but no tracking component!");
                    }
                }
            }

            if (kidnappedPawns.Count == 0)
            {
                Messages.Message("No kidnapped pawns found.", MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                var message = $"Kidnapped Pawns ({kidnappedPawns.Count}):\n" + string.Join("\n", kidnappedPawns);
                Find.WindowStack.Add(new Dialog_MessageBox(message));
            }
        }

        [DebugAction("OpHomecoming", "Spawn Test Kidnap Camp")]
        private static void SpawnTestKidnapCamp()
        {
            var playerPawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive
                .Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike)
                .ToList();

            if (playerPawns.Count == 0)
            {
                Messages.Message("No player pawns available for test camp.", MessageTypeDefOf.RejectInput);
                return;
            }

            var testPawn = playerPawns.RandomElement();
            var camp = OHQuestUtility.CreateKidnapCamp(testPawn);

            if (camp != null)
            {
                Messages.Message($"Test kidnap camp created for {testPawn.LabelShort} at tile {camp.Tile}", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("Failed to create test camp.", MessageTypeDefOf.RejectInput);
            }
        }

        [DebugAction("OpHomecoming", "Signal Handlers Status")]
        private static void ShowSignalHandlersStatus()
        {
            var initialized = SignalHandlers.IsInitialized;
            var signalManager = Find.SignalManager != null;
            var gameState = Current.ProgramState.ToString();
            var playerFaction = Faction.OfPlayer != null;

            var status = $"Signal Handlers Status:\n" +
                        $"Initialized: {initialized}\n" +
                        $"Signal Manager: {signalManager}\n" +
                        $"Game State: {gameState}\n" +
                        $"Player Faction: {playerFaction}";

            Find.WindowStack.Add(new Dialog_MessageBox(status));
        }

        [DebugAction("OpHomecoming", "Force Contact Failure (selected)")]
        private static void ForceContactFailure()
        {
            var p = Find.Selector.SingleSelectedThing as Pawn;
            if (p == null)
            {
                Messages.Message("Select a pawn first.", MessageTypeDefOf.RejectInput);
                return;
            }

            var comp = p.GetComp<CompKidnapContactTracker>();
            if (comp == null)
            {
                Messages.Message("CompKidnapContactTracker not attached.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Trigger a contact failure manually
            Find.SignalManager.SendSignal(new Signal("Signal_ContactFailed", p.Named("PAWN"), 1.Named("ATTEMPT")));
        }

        [DebugAction("OpHomecoming", "Test All Contact Flavors")]
        private static void TestAllContactFlavors()
        {
            var flavors = new[]
            {
            "default", "bonded_animal_courier", "caravan_smuggled_note",
            "hacked_comms_burst", "coded_art_trade", "knotted_item_on_road",
            "structural_flaw_note", "ore_chunk_message", "mass_food_poisoning",
            "indebted_prisoner", "hijacked_guard_radio"
        };

            var options = new List<DebugMenuOption>();

            foreach (var flavor in flavors)
            {
                options.Add(new DebugMenuOption(
                    $"Test {flavor} contact",
                    DebugMenuOptionMode.Action,
                    () => {
                        var playerPawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive
                            .Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike)
                            .ToList();

                        if (playerPawns.Count > 0)
                        {
                            var testPawn = playerPawns.RandomElement();
                            var quest = OHQuestUtility.CreateRescueQuest(testPawn, flavor);
                            if (quest != null)
                            {
                                Messages.Message($"Created {flavor} rescue quest for {testPawn.LabelShort}", MessageTypeDefOf.PositiveEvent);
                            }
                            else
                            {
                                Messages.Message($"Failed to create {flavor} quest", MessageTypeDefOf.RejectInput);
                            }
                        }
                    }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }
    }

    /// <summary>Handles successful contact attempts from kidnapped pawns.</summary>
    public class IncidentWorker_ContactSuccess : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // This will be triggered by signals, implementation in signal handlers
            return true;
        }
    }

    /// <summary>Handles failed contact attempts from kidnapped pawns.</summary>
    public class IncidentWorker_ContactFailed : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // This will be triggered by signals, implementation in signal handlers
            return true;
        }
    }

    /// <summary>Handles rumor letters about kidnapped pawns.</summary>
    public class IncidentWorker_RumorLetter : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // Implementation for rumor system
            return true;
        }
    }

    /// <summary>Stub for the rescue-site quest incident.</summary>
    public class IncidentWorker_KidnapContact : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Log.Warning("[OpHomecoming] Debug incident fired – creating test quest.");

            // For testing purposes, create a simple rescue quest
            var colonists = Find.ColonistBar.GetColonistsInOrder();
            if (colonists.Count > 0)
            {
                var captivePawn = colonists.First();
                OHQuestUtility.CreateRescueQuest(captivePawn);
                return true;
            }

            return false;
        }
    }
}
