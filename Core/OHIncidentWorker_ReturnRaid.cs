using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;                // for Site
using OperationHomecoming.Core;       // for KidnapCampSiteComp

namespace OperationHomecoming.Core
{
    public class OHIncidentWorker_ReturnRaid : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // 1) Ensure the target is a Map
            if (!(parms.target is Map map))
                return false;

            // 2) Get the world‐object (Site) that spawned this map
            var site = map.Parent as Site;
            if (site == null)
                return false;

            // 3) Fetch your camp component from the Site
            var campComp = site.GetComponent<KidnapCampSiteComp>();
            if (campComp == null || campComp.CaptivePawn == null)
                return false;

            // 4) We now have the captive pawn
            Pawn captive = campComp.CaptivePawn;

            // 5) Rough ’em up: add a bruise
            var bruiseDef = HediffDef.Named("Bruise");
            captive.health.AddHediff(bruiseDef);

            // 6) Equip basic clothes if missing
            var apparelDefs = new[]
            {
                ThingDef.Named("Pants"),
                ThingDef.Named("Shirt"),
                ThingDef.Named("Jacket")
            };
            foreach (var def in apparelDefs)
            {
                if (!captive.apparel.WornApparel.Any(a => a.def == def))
                {
                    var piece = (Apparel)ThingMaker.MakeThing(def);
                    GenSpawn.Spawn(piece, captive.Position, map);
                    captive.apparel.Wear(piece);
                }
            }

            // 7) Build new raid parms and launch
            var raidParms = new IncidentParms
            {
                target = map,
                faction = parms.faction,
                points = parms.points * 0.75f,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn
            };
            return new IncidentWorker_RaidEnemy().TryExecute(raidParms);
        }
    }
}
