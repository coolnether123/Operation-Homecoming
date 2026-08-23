// ────────────────────────────────────────────────────────────────────────────────
// OHQuestUtility.cs  –  RimWorld 1.5+
// Static utility class for creating quests and sites for Operation Homecoming
// ────────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace OperationHomecoming.Core
{
    public static class OHQuestUtility
    {
        public static Quest CreateRescueQuest(Pawn captivePawn, string contactFlavor = null)
        {
            try
            {
                var slate = new Slate();
                slate.Set("captivePawn", captivePawn);
                slate.Set("contactFlavor", contactFlavor ?? "default");

                // Set asker as player faction
                if (Faction.OfPlayer != null)
                    slate.Set("asker", Faction.OfPlayer);

                if (captivePawn.Map != null)
                    slate.Set("map", captivePawn.Map);

                var questDef = DefDatabase<QuestScriptDef>.GetNamed("OH_RescueCaptive");
                if (questDef == null)
                {
                    Log.Error("[OpHomecoming] Could not find OH_RescueCaptive quest script def");
                    return null;
                }

                var quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);

                if (quest != null)
                {
                    // Contact directly initiates this rescue rather than showing
                    // an expiring acceptance offer. Keep it active through save
                    // and reload until the camp outcome resolves it.
                    quest.SetInitiallyAccepted();
                    Site site = CreateKidnapCamp(captivePawn);
                    if (site == null)
                    {
                        quest.End(QuestEndOutcome.Fail);
                        return null;
                    }

                    quest.AddPart(new QuestPart_OperationHomecomingCamp
                    {
                        site = site,
                        captivePawn = captivePawn
                    });
                    site.GetComponent<KidnapCampSiteComp>()?.AttachQuest(quest);
                    Find.World.GetComponent<WorldComponent_OperationHomecoming>()
                        ?.RegisterRescueQuest(captivePawn, quest, site);
                    Log.Message($"[OpHomecoming] Created rescue quest and camp for {captivePawn.LabelShort}");
                }

                return quest;
            }
            catch (System.Exception e)
            {
                Log.Error($"[OpHomecoming] Error creating rescue quest: {e}");
                return null;
            }
        }

        public static Site CreateKidnapCamp(Pawn captivePawn, int tile = -1)
        {
            if (tile == -1)
            {
                tile = FindSuitableCampTile(captivePawn);
            }

            var siteDef = DefDatabase<SitePartDef>.GetNamed("OH_KidnapCamp");
            if (siteDef == null)
            {
                Log.Error("[OpHomecoming] Could not find OH_KidnapCamp site part def");
                return null;
            }

            if (tile < 0 || tile >= Find.WorldGrid.TilesCount || !IsValidCampTile(tile))
            {
                Log.Error("[OpHomecoming] No valid world tile exists for the kidnap camp");
                return null;
            }

            var worldObjectDef = DefDatabase<WorldObjectDef>.GetNamed("OH_KidnapCampWorldObject", false);
            if (worldObjectDef == null)
            {
                Log.Error("[OpHomecoming] Could not find OH_KidnapCampWorldObject world object def");
                return null;
            }

            var site = (Site)WorldObjectMaker.MakeWorldObject(worldObjectDef);
            site.Tile = tile;
            site.AddPart(new SitePart(site, siteDef, new SitePartParams()));
            if (site != null)
            {
                var comp = site.GetComponent<KidnapCampSiteComp>();
                if (comp == null)
                {
                    Log.Error("[OpHomecoming] Kidnap camp world object is missing its state component");
                    return null;
                }

                comp.Initialize(captivePawn);

                Find.WorldObjects.Add(site);
                Log.Message($"[OpHomecoming] Created kidnap camp at tile {tile} for {captivePawn.LabelShort}");
            }

            return site;
        }

        public static Site CreateInvestigationSite(Pawn captivePawn, int tile = -1)
        {
            if (tile == -1)
            {
                tile = FindSuitableInvestigationTile(captivePawn);
            }

            var siteDef = DefDatabase<SitePartDef>.GetNamed("OH_Investigation");
            if (siteDef == null)
            {
                Log.Error("[OpHomecoming] Could not find OH_Investigation site part def");
                return null;
            }

            if (tile < 0 || tile >= Find.WorldGrid.TilesCount || !IsValidInvestigationTile(tile))
            {
                Log.Error("[OpHomecoming] No valid world tile exists for the investigation site");
                return null;
            }

            var site = SiteMaker.MakeSite(siteDef, tile, null);
            if (site != null)
            {
                Find.WorldObjects.Add(site);
                Log.Message($"[OpHomecoming] Created investigation site at tile {tile}");
            }

            return site;
        }

        public static Quest CreatePrisonBreakQuest(Pawn captivePawn)
        {
            var slate = new Slate();
            slate.Set("captivePawn", captivePawn);
            slate.Set("map", captivePawn.Map);
            
            var questDef = DefDatabase<QuestScriptDef>.GetNamed("OH_PrisonBreak");
            if (questDef == null)
            {
                Log.Error("[OpHomecoming] Could not find OH_PrisonBreak quest script def");
                return null;
            }

            var quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            
            if (quest != null)
            {
                Log.Message($"[OpHomecoming] Created prison break quest for {captivePawn.LabelShort}");
            }
            
            return quest;
        }

        public static int FindSuitableCampTile(Pawn captivePawn)
        {
            var playerTile = captivePawn.Map?.Tile ?? Find.AnyPlayerHomeMap?.Tile ?? -1;
            if (playerTile == -1)
            {
                Log.Warning("[OpHomecoming] Could not find player tile for camp placement");
                return Rand.Range(0, Find.WorldGrid.TilesCount);
            }

            // Find a tile 2-8 tiles away from player
            var candidates = new List<int>();
            Find.WorldFloodFiller.FloodFill(playerTile, (int tile) => true, delegate(int tile, int dist)
            {
                if (dist >= 2 && dist <= 8 && IsValidCampTile(tile))
                {
                    candidates.Add(tile);
                }
                return dist <= 8;
            });

            if (candidates.Count > 0)
            {
                return candidates.RandomElement();
            }

            Log.Message("[OpHomecoming] No nearby camp tile was available; using the first valid world tile");
            for (int tile = 0; tile < Find.WorldGrid.TilesCount; tile++)
            {
                if (IsValidCampTile(tile))
                {
                    return tile;
                }
            }

            return -1;
        }

        public static int FindSuitableInvestigationTile(Pawn captivePawn)
        {
            var playerTile = captivePawn.Map?.Tile ?? Find.AnyPlayerHomeMap?.Tile ?? -1;
            if (playerTile == -1)
            {
                return Rand.Range(0, Find.WorldGrid.TilesCount);
            }

            // Find a tile 1-4 tiles away from player
            var candidates = new List<int>();
            Find.WorldFloodFiller.FloodFill(playerTile, (int tile) => true, delegate(int tile, int dist)
            {
                if (dist >= 1 && dist <= 4 && IsValidInvestigationTile(tile))
                {
                    candidates.Add(tile);
                }
                return dist <= 4;
            });

            if (candidates.Count > 0)
            {
                return candidates.RandomElement();
            }

            for (int tile = 0; tile < Find.WorldGrid.TilesCount; tile++)
            {
                if (IsValidInvestigationTile(tile))
                {
                    return tile;
                }
            }

            return -1;
        }

        private static bool IsValidCampTile(int tile)
        {
            var tileInfo = Find.WorldGrid[tile];
            
            // Avoid water, ice, and extreme temperatures
            if (tileInfo.WaterCovered || tileInfo.biome.defName.Contains("Ice"))
                return false;
                
            // Avoid tiles with existing world objects
            if (Find.WorldObjects.AnyWorldObjectAt(tile))
                return false;
                
            return true;
        }

        private static bool IsValidInvestigationTile(int tile)
        {
            var tileInfo = Find.WorldGrid[tile];
            
            // More lenient than camp tiles
            if (tileInfo.WaterCovered)
                return false;
                
            if (Find.WorldObjects.AnyWorldObjectAt(tile))
                return false;
                
            return true;
        }

        public static void HandleQuestSuccess(Quest quest, Pawn captivePawn)
        {
            WorldComponent_OperationHomecoming world = Find.World.GetComponent<WorldComponent_OperationHomecoming>();
            if (world != null && !world.TryResolve(captivePawn, HomecomingLifecycleStage.Rescued))
            {
                return;
            }

            Log.Message($"[OpHomecoming] Rescue quest succeeded for {captivePawn.LabelShort}");
            
            // Apply captivity consequences
            ApplyCaptivityConsequences(captivePawn);
            
            // Send success letter
            var letterDef = DefDatabase<LetterDef>.GetNamed("OH_RescueSuccess");
            if (letterDef != null)
            {
                Find.LetterStack.ReceiveLetter(
                    "OH_RescueSuccessLabel".Translate(captivePawn.LabelShort),
                    "OH_RescueSuccessText".Translate(captivePawn.LabelShort),
                    letterDef,
                    captivePawn
                );
            }
        }

        public static void HandleQuestFailure(Quest quest, Pawn captivePawn)
        {
            WorldComponent_OperationHomecoming world = Find.World.GetComponent<WorldComponent_OperationHomecoming>();

            Log.Message($"[OpHomecoming] Rescue quest failed for {captivePawn.LabelShort}");

            if (world?.IsAtStage(captivePawn, HomecomingLifecycleStage.Expired) == true)
            {
                return;
            }
            
            var settings = OperationHomecomingMod.Settings;
            
            if (Rand.Chance(settings.postFailureRaidChance))
            {
                world?.TryResolve(captivePawn, HomecomingLifecycleStage.FailedReturnRaid);
                // Spawn return raid
                SpawnReturnRaid(captivePawn);
            }
            else
            {
                world?.TryResolve(captivePawn, HomecomingLifecycleStage.FailedWoundedReturn);
                // Wounded return
                ScheduleWoundedReturn(captivePawn);
            }
        }

        private static void ApplyCaptivityConsequences(Pawn pawn)
        {
            if (pawn?.health != null)
            {
                HediffDef bruiseDef = HediffDef.Named("Bruise");
                float multiplier = OperationHomecomingMod.Settings?.illnessSeverityMultiplier ?? 1f;
                if (bruiseDef != null && multiplier > 0f)
                {
                    Hediff bruise = HediffMaker.MakeHediff(
                        bruiseDef, pawn, pawn.RaceProps?.body?.corePart);
                    bruise.Severity = 0.1f * multiplier;
                    pawn.health.AddHediff(bruise);
                }
            }

            ReturnPawnToPlayer(pawn);
        }

        private static void SpawnReturnRaid(Pawn captivePawn)
        {
            Log.Message($"[OpHomecoming] Spawning return raid with {captivePawn.LabelShort}");
            Find.World.GetComponent<WorldComponent_OperationHomecoming>()
                ?.QueueReturnRaid(captivePawn, GenDate.TicksPerDay);
        }

        private static void ScheduleWoundedReturn(Pawn captivePawn)
        {
            Log.Message($"[OpHomecoming] Scheduling wounded return for {captivePawn.LabelShort}");
            Find.World.GetComponent<WorldComponent_OperationHomecoming>()
                ?.QueueWoundedReturn(captivePawn, GenDate.TicksPerDay);
        }

        private static void ReturnPawnToPlayer(Pawn pawn)
        {
            if (pawn == null || Faction.OfPlayer == null)
            {
                return;
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn();
            }

            pawn.SetFaction(Faction.OfPlayer);
            pawn.guest?.SetGuestStatus(null);

            Map map = Find.AnyPlayerHomeMap;
            bool spawned = false;
            if (map != null && CellFinder.TryFindRandomSpawnCellForPawnNear(
                map.Center, map, out IntVec3 cell))
            {
                GenSpawn.Spawn(pawn, cell, map);
                spawned = pawn.Spawned;
            }

            if (!spawned && map != null && CellFinder.TryFindRandomCell(
                map, c => c.Standable(map), out IntVec3 fallbackCell))
            {
                GenSpawn.Spawn(pawn, fallbackCell, map);
                spawned = pawn.Spawned;
            }

            // A player pawn must remain save-reachable even if a heavily
            // blocked map has no valid spawn cell. World-pawn storage is the
            // engine's durable fallback and prevents dangling Scribe refs.
            if (!spawned && !pawn.Spawned)
            {
                Find.WorldPawns.PassToWorld(pawn);
            }
        }
    }
}
