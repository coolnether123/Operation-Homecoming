using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace OperationHomecoming.Core
{
    public class OHIncidentWorker_ReturnRaid : IncidentWorker_RaidEnemy
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Pawn captive = Find.World.GetComponent<WorldComponent_OperationHomecoming>()
                ?.TakePendingReturnRaidPawn();
            if (captive == null || parms.target is not Map map)
            {
                return false;
            }

            if (captive.Spawned)
            {
                captive.DeSpawn();
            }

            if (captive.Faction != parms.faction)
            {
                captive.SetFaction(parms.faction);
            }
            captive.guest?.SetGuestStatus(null);

            bool raidStarted = base.TryExecuteWorker(parms);
            if (!raidStarted)
            {
                return false;
            }

            if (!CellFinder.TryFindRandomEdgeCellWith(
                c => c.Standable(map), map, CellFinder.EdgeRoadChance_Ignore, out IntVec3 edgeCell))
            {
                edgeCell = map.Center;
            }

            GenSpawn.Spawn(captive, edgeCell, map);
            LordMaker.MakeNewLord(
                parms.faction,
                new LordJob_AssaultColony(parms.faction),
                map,
                new List<Pawn> { captive });

            Find.LetterStack.ReceiveLetter(
                "OH_ReturnRaidLabel".Translate(captive.LabelShort, parms.faction?.Name ?? "raiders"),
                "OH_ReturnRaidText".Translate(captive.LabelShort, parms.faction?.Name ?? "raiders"),
                LetterDefOf.ThreatBig,
                captive);
            return true;
        }
    }
}
