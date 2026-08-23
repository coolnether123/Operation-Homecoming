// ────────────────────────────────────────────────────────────────────────────────
// HarmonyPatches.cs  –  RimWorld 1.5+
// Harmony patches for Operation Homecoming functionality
// ────────────────────────────────────────────────────────────────────────────────

using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace OperationHomecoming.Core
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // When a pawn's guest status changes, add our comp if they're now a secure prisoner of a non-player faction.
        [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
        [HarmonyPostfix]
        public static void SetGuestStatus_Postfix(Pawn_GuestTracker __instance, Faction newHost, GuestStatus guestStatus)
        {
            // Only proceed if we have a proper game state
            if (Current.Game == null || Find.FactionManager == null)
                return;

            var pawnField = typeof(Pawn_GuestTracker)
                .GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
            var pawn = pawnField?.GetValue(__instance) as Pawn;
            if (pawn == null) return;

            // Only proceed if player faction exists
            if (Faction.OfPlayer == null)
                return;

            if (guestStatus == GuestStatus.Prisoner
                && __instance.PrisonerIsSecure
                && newHost != Faction.OfPlayer)
            {
                EnsureCompExists(pawn);
                Log.Message($"[OpHomecoming] Added contact tracker to kidnapped pawn: {pawn.LabelShort}");
            }
        }

        // Patch to handle pawn generation for ensuring our comp is available - but only when safe
        [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
        [HarmonyPostfix]
        public static void GeneratePawn_Postfix(Pawn __result, PawnGenerationRequest request)
        {
            // Safety checks - only proceed if game is properly initialized
            if (__result == null || Current.Game == null)
                return;

            // Don't run during world generation or before game is fully initialized
            if (Current.ProgramState != ProgramState.Playing)
                return;

            // Don't run if no faction manager exists yet
            if (Find.FactionManager == null)
                return;

            // Additional safety check - ensure player faction actually exists
            try
            {
                var playerFaction = Faction.OfPlayer;
                if (playerFaction == null)
                    return;
            }
            catch
            {
                // If accessing Faction.OfPlayer throws an exception, we're not ready yet
                return;
            }

            // Only add to humanlike pawns that could be kidnapped
            if (__result.RaceProps?.Humanlike == true)
            {
                // Check if this pawn belongs to player or friendly faction
                bool shouldHaveComp = false;

                if (__result.Faction == Faction.OfPlayer)
                {
                    shouldHaveComp = true;
                }
                else if (__result.Faction != null)
                {
                    try
                    {
                        if (__result.Faction.PlayerGoodwill > 0)
                            shouldHaveComp = true;
                    }
                    catch
                    {
                        // Ignore goodwill access errors during generation
                    }
                }

                if (shouldHaveComp)
                {
                    EnsureCompExists(__result);
                }
            }
        }

        // Helper method to safely add the component
        private static void EnsureCompExists(Pawn pawn)
        {
            if (pawn?.AllComps == null) return;

            var comp = pawn.GetComp<CompKidnapContactTracker>();
            if (comp == null)
            {
                comp = new CompKidnapContactTracker
                {
                    parent = pawn,
                    props = new CompProperties_KidnapContactTracker()
                };
                pawn.AllComps.Add(comp);

                // Only call PostSpawnSetup if the pawn is spawned
                if (pawn.Spawned)
                    comp.PostSpawnSetup(false);
            }
        }

        // Patch to ensure world component is available
        [HarmonyPatch(typeof(World), nameof(World.ExposeData))]
        [HarmonyPostfix]
        public static void World_ExposeData_Postfix(World __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Ensure our world component exists
                var component = __instance.GetComponent<WorldComponent_OperationHomecoming>();
                if (component == null)
                {
                    component = new WorldComponent_OperationHomecoming(__instance);
                    __instance.components.Add(component);
                }
            }
        }

        // Patch to handle quest completion
        [HarmonyPatch(typeof(Quest), nameof(Quest.End))]
        [HarmonyPrefix]
        public static void Quest_End_Prefix(Quest __instance, QuestEndOutcome outcome)
        {
            // Handle Operation Homecoming quest endings
            if (__instance?.root?.defName == "OH_RescueCaptive")
            {
                // Try to find the captive pawn from the quest
                Pawn captivePawn = null;

                // Check quest parts for our specific site or pawn references
                foreach (var part in __instance.PartsListForReading)
                {
                    // Look for our site component or pawn references
                    if (part is QuestPart_OperationHomecomingCamp homecomingPart)
                    {
                        if (homecomingPart.captivePawn != null)
                        {
                            captivePawn = homecomingPart.captivePawn;
                            break;
                        }

                        Site site = homecomingPart.site;
                        if (site != null)
                        {
                            var comp = site.GetComponent<KidnapCampSiteComp>();
                            if (comp?.CaptivePawn != null)
                            {
                                captivePawn = comp.CaptivePawn;
                                break;
                            }
                        }
                    }
                }

                if (captivePawn != null)
                {
                    if (outcome == QuestEndOutcome.Success)
                    {
                        OHQuestUtility.HandleQuestSuccess(__instance, captivePawn);
                    }
                    else if (outcome == QuestEndOutcome.Fail)
                    {
                        OHQuestUtility.HandleQuestFailure(__instance, captivePawn);
                    }
                }
            }
        }

        // Patch to handle site destruction
        [HarmonyPatch(typeof(Site), nameof(Site.Destroy))]
        [HarmonyPrefix]
        public static void Site_Destroy_Prefix(Site __instance)
        {
            var comp = __instance?.GetComponent<KidnapCampSiteComp>();
            if (comp?.CaptivePawn != null)
            {
                Log.Message($"[OpHomecoming] Kidnap camp destroyed, handling failure for {comp.CaptivePawn.LabelShort}");

                // This will trigger quest failure through the quest system
                if (Find.SignalManager != null)
                {
                    Find.SignalManager.SendSignal(new Signal("site.Destroyed", __instance.Named("SITE")));
                }
            }
        }

        // Patch to handle map generation for sites
        [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.GetComponent), new[] { typeof(System.Type) })]
        [HarmonyPostfix]
        public static void GetComponent_Postfix(WorldObject __instance, System.Type type, ref WorldObjectComp __result)
        {
            // only run if original returned null and this is a Kidnap Camp site
            if (__result != null) return;
            if (!(__instance is Site site)) return;
            if (type != typeof(KidnapCampSiteComp)) return;

            // ensure our comp gets added once
            if (site.AllComps?.OfType<KidnapCampSiteComp>().Any() == true) return;

            var comp = new KidnapCampSiteComp { parent = site };
            site.AllComps.Add(comp);
        }

        // Add missing components to existing colonists when game starts
        [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
        [HarmonyPostfix]
        public static void Game_FinalizeInit_Postfix()
        {
            try
            {
                // Add components to existing colonists using proper pawn access
                if (Faction.OfPlayer != null)
                {
                    // Get all player pawns from all sources
                    var playerPawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive
                        .Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike);

                    foreach (var pawn in playerPawns)
                    {
                        EnsureCompExists(pawn);
                    }
                }

                // Initialize signal handlers now that the game is ready
                SignalHandlers.Initialize();

                Log.Message("[OpHomecoming] Game finalization complete, components added to existing pawns");
            }
            catch (System.Exception e)
            {
                Log.Error($"[OpHomecoming] Error during game finalization: {e}");
            }
        }

        // Patch the getter of PrisonerIsSecure to catch any edge cases
        [HarmonyPatch(typeof(Pawn_GuestTracker), "PrisonerIsSecure", MethodType.Getter)]
        [HarmonyPostfix]
        public static void PrisonerIsSecure_Postfix(bool __result, Pawn_GuestTracker __instance)
        {
            if (!__result || Current.Game == null || Faction.OfPlayer == null) return;

            var pawnField = typeof(Pawn_GuestTracker)
                .GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
            var pawn = pawnField?.GetValue(__instance) as Pawn;
            if (pawn == null || pawn.Faction == Faction.OfPlayer) return;

            EnsureCompExists(pawn);
            Log.Message($"[OpHomecoming] Added contact tracker via PrisonerIsSecure: {pawn.LabelShort}");
        }
    }
}
