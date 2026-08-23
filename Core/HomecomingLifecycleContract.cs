using System;

namespace OperationHomecoming.Core
{
    public enum HomecomingLifecycleStage
    {
        Kidnapped,
        Contacted,
        RescueQuestActive,
        Rescued,
        FailedReturnRaid,
        FailedWoundedReturn,
        Expired
    }

    public static class HomecomingLifecycleContract
    {
        public static bool TryAdvance(
            HomecomingLifecycleStage current,
            HomecomingLifecycleStage next)
        {
            if (current == next)
            {
                return false;
            }

            if (current == HomecomingLifecycleStage.Kidnapped)
            {
                return next == HomecomingLifecycleStage.Contacted;
            }

            if (current == HomecomingLifecycleStage.Contacted)
            {
                return next == HomecomingLifecycleStage.RescueQuestActive;
            }

            if (current == HomecomingLifecycleStage.RescueQuestActive)
            {
                return next == HomecomingLifecycleStage.Rescued ||
                       next == HomecomingLifecycleStage.FailedReturnRaid ||
                       next == HomecomingLifecycleStage.FailedWoundedReturn ||
                       next == HomecomingLifecycleStage.Expired;
            }

            return false;
        }

        public static bool IsTerminal(HomecomingLifecycleStage stage)
        {
            return stage == HomecomingLifecycleStage.Rescued ||
                   stage == HomecomingLifecycleStage.FailedReturnRaid ||
                   stage == HomecomingLifecycleStage.FailedWoundedReturn ||
                   stage == HomecomingLifecycleStage.Expired;
        }
    }
}
