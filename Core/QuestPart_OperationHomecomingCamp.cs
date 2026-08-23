using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;

namespace OperationHomecoming.Core
{
    public class QuestPart_OperationHomecomingCamp : QuestPart
    {
        public Site site;
        public Pawn captivePawn;

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                if (site != null)
                {
                    yield return new GlobalTargetInfo(site);
                }
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            if (site != null && !site.Destroyed)
            {
                site.Destroy();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "site");
            Scribe_References.Look(ref captivePawn, "captivePawn");
        }
    }
}
