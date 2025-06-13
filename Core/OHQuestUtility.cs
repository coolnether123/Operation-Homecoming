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

                var questDef = DefDatabase<QuestScriptDef>.GetNamed("OH_RescueCaptive");
                if (questDef == null)
                {
                    Log.Error("[OpHomecoming] Could not find OH_RescueCaptive quest script def");
                    return null;
                }

                var quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);

                if (quest != null)
                {
                    Log.Message($"[OpHomecoming] Created rescue quest for {captivePawn.LabelShort}");
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

            var site = SiteMaker.MakeSite(siteDef, tile, null);
            if (site != null)
            {
                // Add our custom site component
                var comp = site.GetComponent<KidnapCampSiteComp>();
                if (comp != null)
                {
                    comp.Initialize(captivePawn);
                }

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

        private static int FindSuitableCampTile(Pawn captivePawn)
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

            Log.Warning("[OpHomecoming] Could not find suitable camp tile, using random");
            return Rand.Range(0, Find.WorldGrid.TilesCount);
        }

        private static int FindSuitableInvestigationTile(Pawn captivePawn)
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

            return candidates.Count > 0 ? candidates.RandomElement() : Rand.Range(0, Find.WorldGrid.TilesCount);
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
            Log.Message($"[OpHomecoming] Rescue quest failed for {captivePawn.LabelShort}");
            
            var settings = OperationHomecomingMod.Settings;
            
            if (Rand.Chance(settings.postFailureRaidChance))
            {
                // Spawn return raid
                SpawnReturnRaid(captivePawn);
            }
            else
            {
                // Wounded return
                ScheduleWoundedReturn(captivePawn);
            }
        }

        private static void ApplyCaptivityConsequences(Pawn pawn)
        {
            // This will be expanded with hediff sets, clothing changes, and skill modifications
            Log.Message($"[OpHomecoming] Applying captivity consequences to {pawn.LabelShort}");
        }

        private static void SpawnReturnRaid(Pawn captivePawn)
        {
            Log.Message($"[OpHomecoming] Spawning return raid with {captivePawn.LabelShort}");
            // Implementation will be added later
        }

        private static void ScheduleWoundedReturn(Pawn captivePawn)
        {
            Log.Message($"[OpHomecoming] Scheduling wounded return for {captivePawn.LabelShort}");
            // Implementation will be added later
        }
    }
}
