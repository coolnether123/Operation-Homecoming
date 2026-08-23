// ────────────────────────────────────────────────────────────────────────────────
// OperationHomecoming.cs  –  RimWorld 1.5+
// Core mod initialization and main entry point for Operation Homecoming
// ────────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace OperationHomecoming.Core
{
    [StaticConstructorOnStartup]
    public static class OperationHomecomingMod
    {
        public static ModSettings_OperationHomecoming Settings
        {
            get
            {
                var mod = LoadedModManager.GetMod<OperationHomecomingMod_Instance>();
                return mod?.GetSettings<ModSettings_OperationHomecoming>();
            }
        }

        static OperationHomecomingMod()
        {
            try
            {
                var harmony = new Harmony("coolnether123.OperationHomecoming");
                harmony.PatchAll();
                Log.Message("[OpHomecoming] Harmony patches applied successfully.");
            }
            catch (System.Exception e)
            {
                Log.Error($"[OpHomecoming] Failed to apply Harmony patches: {e}");
            }
        }
    }

    public class OperationHomecomingMod_Instance : Mod
    {
        public OperationHomecomingMod_Instance(ModContentPack content) : base(content)
        {
            GetSettings<ModSettings_OperationHomecoming>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            GetSettings<ModSettings_OperationHomecoming>().DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Operation Homecoming";
        }
    }

    // Incident worker classes for Operation Homecoming. Developer fixtures are
    // intentionally kept out of this shipping assembly.

    /// <summary>Handles successful contact attempts from kidnapped pawns.</summary>
    public class IncidentWorker_ContactSuccess : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // This will be triggered by signals, implementation in signal handlers
            return true;
        }
    }

    /// <summary>Handles failed contact attempts from kidnapped pawns.</summary>
    public class IncidentWorker_ContactFailed : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // This will be triggered by signals, implementation in signal handlers
            return true;
        }
    }

    /// <summary>Handles rumor letters about kidnapped pawns.</summary>
    public class IncidentWorker_RumorLetter : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // Implementation for rumor system
            return true;
        }
    }

    /// <summary>Stub for the rescue-site quest incident.</summary>
    public class IncidentWorker_KidnapContact : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Log.Warning("[OpHomecoming] Debug incident fired – creating test quest.");

            // For testing purposes, create a simple rescue quest
            var colonists = Find.ColonistBar.GetColonistsInOrder();
            if (colonists.Count > 0)
            {
                var captivePawn = colonists.First();
                OHQuestUtility.CreateRescueQuest(captivePawn);
                return true;
            }

            return false;
        }
    }
}
