// ────────────────────────────────────────────────────────────────────────────────
// SignalHandlers.cs  –  RimWorld 1.5+
// Handles signals for contact success/failure and quest events
// ────────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace OperationHomecoming.Core
{
    public static class SignalHandlers
    {
        private static SignalAction_ContactSuccess contactSuccessHandler;
        private static SignalAction_ContactFailed contactFailedHandler;
        private static bool initialized = false;

        public static bool IsInitialized => initialized;

        public static void Initialize()
        {
            if (initialized) return;

            // Check if the game is in a state where we can safely register signals
            if (Current.Game == null || Find.SignalManager == null)
            {
                Log.Warning("[OpHomecoming] Cannot initialize signal handlers - game not ready");
                return;
            }

            try
            {
                contactSuccessHandler = new SignalAction_ContactSuccess();
                contactFailedHandler = new SignalAction_ContactFailed();

                Find.SignalManager.RegisterReceiver(contactSuccessHandler);
                Find.SignalManager.RegisterReceiver(contactFailedHandler);

                initialized = true;
                Log.Message("[OpHomecoming] Signal handlers registered successfully.");
            }
            catch (System.Exception e)
            {
                Log.Error($"[OpHomecoming] Failed to register signal handlers: {e}");
            }
        }
    }

    public class SignalAction_ContactSuccess : ISignalReceiver
    {
        public bool Accepts(Signal signal)
        {
            return signal.tag == "Signal_ContactSuccess";
        }

        public void Notify_SignalReceived(Signal signal)
        {
            if (signal.args.TryGetArg("PAWN", out Pawn captivePawn))
            {
                HandleContactSuccess(captivePawn);
            }
        }

        private void HandleContactSuccess(Pawn captivePawn)
        {
            Log.Message($"[OpHomecoming] Handling contact success for {captivePawn.LabelShort}");

            var settings = OperationHomecomingMod.Settings;

            // Determine contact flavor based on highest skill
            string contactFlavor = DetermineContactFlavor(captivePawn);

            // For now, always create rescue quest since we don't have prison break implemented yet
            OHQuestUtility.CreateRescueQuest(captivePawn, contactFlavor);

            // Send contact success letter with flavor
            SendContactLetter(captivePawn, contactFlavor);
        }

        private string DetermineContactFlavor(Pawn pawn)
        {
            if (pawn?.skills?.skills == null) return "default";

            var highestSkill = pawn.skills.skills.MaxBy(s => s.Level);
            if (highestSkill == null || highestSkill.Level < 8) return "default";

            // Map skills to contact flavors
            switch (highestSkill.def.defName)
            {
                case "Animals": return "bonded_animal_courier";
                case "Social": return "caravan_smuggled_note";
                case "Intellectual": return "hacked_comms_burst";
                case "Artistic": return "coded_art_trade";
                case "Crafting": return "knotted_item_on_road";
                case "Construction": return "structural_flaw_note";
                case "Mining": return "ore_chunk_message";
                case "Cooking": return "mass_food_poisoning";
                case "Medicine": return "indebted_prisoner";
                case "Shooting":
                case "Melee": return "hijacked_guard_radio";
                default: return "default";
            }
        }

        private void SendContactLetter(Pawn captivePawn, string contactFlavor)
        {
            string letterKey = $"OH_Contact_{contactFlavor}";
            string letterText = letterKey.Translate(captivePawn.LabelShort);

            // Fallback to default if translation missing
            if (letterText == letterKey)
            {
                letterText = "OH_Contact_default".Translate(captivePawn.LabelShort);
            }

            Find.LetterStack.ReceiveLetter(
                "OH_ContactSuccessLabel".Translate(captivePawn.LabelShort),
                letterText,
                LetterDefOf.PositiveEvent,
                captivePawn
            );
        }
    }

    public class SignalAction_ContactFailed : ISignalReceiver
    {
        public bool Accepts(Signal signal)
        {
            return signal.tag == "Signal_ContactFailed";
        }

        public void Notify_SignalReceived(Signal signal)
        {
            if (signal.args.TryGetArg("PAWN", out Pawn captivePawn) &&
                signal.args.TryGetArg("ATTEMPT", out int attemptIndex))
            {
                HandleContactFailed(captivePawn, attemptIndex);
            }
        }

        private void HandleContactFailed(Pawn captivePawn, int attemptIndex)
        {
            Log.Message($"[OpHomecoming] Handling contact failure for {captivePawn.LabelShort}, attempt #{attemptIndex}");

            var settings = OperationHomecomingMod.Settings;

            // Every third failure, spawn investigation site
            if (attemptIndex % 3 == 0)
            {
                OHQuestUtility.CreateInvestigationSite(captivePawn);
            }

            // Schedule rumor letter
            ScheduleRumorLetter(captivePawn, attemptIndex);
        }

        private void ScheduleRumorLetter(Pawn captivePawn, int attemptIndex)
        {
            var settings = OperationHomecomingMod.Settings;
            int delayTicks = settings.rumorFrequencyDays * GenDate.TicksPerDay;

            // Add some randomness
            delayTicks += Rand.Range(-GenDate.TicksPerDay, GenDate.TicksPerDay);

            var worldComp = Find.World.GetComponent<WorldComponent_OperationHomecoming>();
            worldComp?.ScheduleRumorLetter(captivePawn, delayTicks);
        }
    }
}

// World component to handle delayed events
namespace OperationHomecoming.Core
{
    public partial class WorldComponent_OperationHomecoming : WorldComponent
    {
        // We need this constructor so RimWorld can instantiate our component:
        public WorldComponent_OperationHomecoming(World world) : base(world)
        {
            // ensure our list is non-null even if loaded from an empty save
            if (scheduledRumors == null)
                scheduledRumors = new List<ScheduledRumor>();
        }

        // The list of all pending rumor‐letters, scheduled to go out at triggerTick
        private List<ScheduledRumor> scheduledRumors = new List<ScheduledRumor>();

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // Iterate backwards so we can remove from the list
            for (int i = scheduledRumors.Count - 1; i >= 0; i--)
            {
                var rumor = scheduledRumors[i];
                if (Find.TickManager.TicksGame >= rumor.triggerTick)
                {
                    SendRumorLetter(rumor.pawn);
                    scheduledRumors.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Schedule a rumor‐letter for <paramref name="pawn"/> after <paramref name="delayTicks"/>.
        /// </summary>
        public void ScheduleRumorLetter(Pawn pawn, int delayTicks)
        {
            if (pawn == null) return;
            scheduledRumors.Add(new ScheduledRumor
            {
                pawn = pawn,
                triggerTick = Find.TickManager.TicksGame + delayTicks
            });
        }

        private void SendRumorLetter(Pawn pawn)
        {
            // Pick one of your rumor defs at random
            var keys = new[]
            {
                "OH_Rumor_Sighting",
                "OH_Rumor_Trader",
                "OH_Rumor_Escape",
                "OH_Rumor_Condition"
            };
            string defKey = keys.RandomElement();
            string text = defKey.Translate(pawn.LabelShort);

            Find.LetterStack.ReceiveLetter(
                "OH_RumorLabel".Translate(),  // your label def
                text,
                LetterDefOf.NeutralEvent,
                pawn
            );
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Save/load our scheduled list
            Scribe_Collections.Look(ref scheduledRumors, "scheduledRumors", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Trim out any null pawns (e.g. deleted by other mods)
                scheduledRumors.RemoveAll(r => r.pawn == null);
            }
        }

        // Helper class so Scribe can deep‐save pawn + tick
        private class ScheduledRumor : IExposable
        {
            public Pawn pawn;
            public int triggerTick;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref triggerTick, "triggerTick");
            }
        }
    }
}
