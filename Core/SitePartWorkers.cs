// ────────────────────────────────────────────────────────────────────────────────
// SitePartWorkers.cs  –  RimWorld 1.5+
// Site part workers for kidnap camps and investigation sites
// ────────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Grammar;

namespace OperationHomecoming.Core
{
    public class SitePartWorker_KidnapCamp : SitePartWorker
    {
        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            base.Notify_GeneratedByQuestGen(part, slate, outExtraDescriptionRules, outExtraDescriptionConstants);

            if (slate.TryGet("captivePawn", out Pawn captivePawn))
            {
                var comp = part.site.GetComponent<KidnapCampSiteComp>();
                if (comp != null)
                {
                    string contactFlavor = slate.Get("contactFlavor", "default");
                    comp.Initialize(captivePawn, contactFlavor);
                }
            }
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);

            var site = map.Parent as Site;
            var comp = site?.GetComponent<KidnapCampSiteComp>();

            if (comp?.CaptivePawn != null)
            {
                GenerateKidnapCamp(map, comp);
            }
        }

        private void GenerateKidnapCamp(Map map, KidnapCampSiteComp comp)
        {
            // Find a good location for the camp
            var campCenter = FindCampCenter(map);

            // Generate camp structures
            GenerateCampStructures(map, campCenter, comp);

            // Spawn guards
            SpawnGuards(map, campCenter, comp);

            // Spawn the captive
            SpawnCaptive(map, campCenter, comp);

            Log.Message($"[OpHomecoming] Generated kidnap camp for {comp.CaptivePawn.LabelShort}");
        }

        private IntVec3 FindCampCenter(Map map)
        {
            // Find a suitable location away from map edges
            var candidates = new List<IntVec3>();

            for (int x = 20; x < map.Size.x - 20; x++)
            {
                for (int z = 20; z < map.Size.z - 20; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (cell.Standable(map) && !cell.Roofed(map))
                    {
                        candidates.Add(cell);
                    }
                }
            }

            return candidates.Count > 0 ? candidates.RandomElement() : map.Center;
        }

        private void GenerateCampStructures(Map map, IntVec3 center, KidnapCampSiteComp comp)
        {
            // Generate a simple fortified camp
            var campRadius = 8;

            // Build perimeter walls
            for (int x = center.x - campRadius; x <= center.x + campRadius; x++)
            {
                for (int z = center.z - campRadius; z <= center.z + campRadius; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)) continue;

                    var distanceFromCenter = center.DistanceTo(cell);

                    // Perimeter wall
                    if (Mathf.Abs(distanceFromCenter - campRadius) < 1f)
                    {
                        if (Rand.Chance(0.8f)) // Some gaps for realism
                        {
                            GenSpawn.Spawn(ThingDefOf.Wall, cell, map);
                        }
                    }
                    // Clear interior
                    else if (distanceFromCenter < campRadius - 1)
                    {
                        var terrain = map.terrainGrid.TerrainAt(cell);
                        if (terrain.passability == Traversability.Impassable)
                        {
                            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                        }
                    }
                }
            }

            // Add entrance
            var entranceCell = center + IntVec3.South * campRadius;
            if (entranceCell.InBounds(map))
            {
                for (int i = -1; i <= 1; i++)
                {
                    var gateCell = entranceCell + IntVec3.East * i;
                    if (gateCell.InBounds(map))
                    {
                        var wall = gateCell.GetFirstBuilding(map);
                        wall?.Destroy();
                    }
                }
            }

            // Add some camp furniture
            AddCampFurniture(map, center, comp);
        }

        private void AddCampFurniture(Map map, IntVec3 center, KidnapCampSiteComp comp)
        {
            // Add sleeping spots
            for (int i = 0; i < 3; i++)
            {
                var sleepSpot = center + new IntVec3(Rand.Range(-3, 4), 0, Rand.Range(-3, 4));
                if (sleepSpot.InBounds(map) && sleepSpot.Standable(map))
                {
                    GenSpawn.Spawn(ThingDefOf.SleepingSpot, sleepSpot, map);
                }
            }

            // Add campfire
            var fireSpot = center + IntVec3.North * 2;
            if (fireSpot.InBounds(map) && fireSpot.Standable(map))
            {
                GenSpawn.Spawn(ThingDefOf.Campfire, fireSpot, map);
            }

            // Add prison cell for captive
            var prisonCell = center + IntVec3.East * 4;
            if (prisonCell.InBounds(map))
            {
                // Build small prison cell
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        var cell = prisonCell + new IntVec3(x, 0, z);
                        if (!cell.InBounds(map)) continue;

                        if (x == 0 && z == 0)
                        {
                            // Center - sleeping spot for prisoner
                            GenSpawn.Spawn(ThingDefOf.SleepingSpot, cell, map);
                        }
                        else if (Mathf.Abs(x) == 1 || Mathf.Abs(z) == 1)
                        {
                            // Walls, but leave one opening
                            if (!(x == 0 && z == -1)) // Leave south side open
                            {
                                GenSpawn.Spawn(ThingDefOf.Wall, cell, map);
                            }
                        }
                    }
                }
            }
        }

        private void SpawnGuards(Map map, IntVec3 center, KidnapCampSiteComp comp)
        {
            foreach (var guard in comp.GuardRoster)
            {
                var spawnCell = FindGuardSpawnCell(map, center);
                if (spawnCell.IsValid)
                {
                    GenSpawn.Spawn(guard, spawnCell, map);

                    // Set up guard behavior
                    guard.mindState.duty = new PawnDuty(DutyDefOf.Defend, center, 15f);
                }
            }
        }

        private void SpawnCaptive(Map map, IntVec3 center, KidnapCampSiteComp comp)
        {
            var prisonCell = center + IntVec3.East * 4;
            if (prisonCell.InBounds(map) && prisonCell.Standable(map))
            {
                GenSpawn.Spawn(comp.CaptivePawn, prisonCell, map);

                // Make them a prisoner
                comp.CaptivePawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            }
        }

        private IntVec3 FindGuardSpawnCell(Map map, IntVec3 center)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                var cell = center + new IntVec3(Rand.Range(-6, 7), 0, Rand.Range(-6, 7));
                if (cell.InBounds(map) && cell.Standable(map) && !cell.GetThingList(map).Any(t => t is Pawn))
                {
                    return cell;
                }
            }
            return IntVec3.Invalid;
        }
    }

    public class SitePartWorker_Investigation : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            GenerateInvestigationSite(map);
        }

        private void GenerateInvestigationSite(Map map)
        {
            // Simple investigation site - just some clues scattered around
            var center = map.Center;

            // Spawn some items that provide clues
            for (int i = 0; i < 3; i++)
            {
                var clueSpot = center + new IntVec3(Rand.Range(-5, 6), 0, Rand.Range(-5, 6));
                if (clueSpot.InBounds(map) && clueSpot.Standable(map))
                {
                    // Spawn random items as "clues"
                    var clueItem = ThingMaker.MakeThing(ThingDefOf.Silver);
                    clueItem.stackCount = Rand.Range(10, 50);
                    GenSpawn.Spawn(clueItem, clueSpot, map);
                }
            }

            Log.Message("[OpHomecoming] Generated investigation site");
        }
    }
}
