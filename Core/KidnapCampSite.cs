using RimWorld.Planet;

namespace OperationHomecoming.Core
{
    public class KidnapCampSite : Site
    {
        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            KidnapCampSiteComp comp = GetComponent<KidnapCampSiteComp>();
            if (comp?.CaptivePawn != null)
            {
                alsoRemoveWorldObject = false;
                return false;
            }

            return base.ShouldRemoveMapNow(out alsoRemoveWorldObject);
        }
    }
}
