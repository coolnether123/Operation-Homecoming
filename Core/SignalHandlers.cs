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
        private static object registeredSignalManager;

        public static bool IsInitialized => initialized;

        public static void Initialize()
        {
            // Check if the game is in a state where we can safely register signals
            if (Current.Game == null || Find.SignalManager == null)
            {
                Log.Warning("[OpHomecoming] Cannot initialize signal handlers - game not ready");
                return;
            }

            // A save/reload replaces the SignalManager. Static state survives the
            // reload, so registration must be repeated for the new manager.
            if (initialized && object.ReferenceEquals(registeredSignalManager, Find.SignalManager)) return;

            try
            {
                contactSuccessHandler = new SignalAction_ContactSuccess();
                contactFailedHandler = new SignalAction_ContactFailed();

                Find.SignalManager.RegisterReceiver(contactSuccessHandler);
                Find.SignalManager.RegisterReceiver(contactFailedHandler);

                registeredSignalManager = Find.SignalManager;
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

            // Determine contact flavor based on highest skill
            string contactFlavor = DetermineContactFlavor(captivePawn);

            Find.World.GetComponent<WorldComponent_OperationHomecoming>()?.RegisterContact(captivePawn);
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
