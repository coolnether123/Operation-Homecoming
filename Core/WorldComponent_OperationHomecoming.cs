using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace OperationHomecoming.Core
{
    public class WorldComponent_OperationHomecoming : WorldComponent
    {
        private List<ScheduledRumor> scheduledRumors = new List<ScheduledRumor>();
        private List<HomecomingRecord> records = new List<HomecomingRecord>();
        private Pawn pendingReturnRaidPawn;

        public IReadOnlyList<HomecomingRecord> Records => records;

        public WorldComponent_OperationHomecoming(World world) : base(world)
        {
        }

        public HomecomingRecord RegisterKidnapped(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            HomecomingRecord record = records.FirstOrDefault(r => r.pawn == pawn);
            if (record == null)
            {
                record = new HomecomingRecord
                {
                    pawn = pawn,
                    captorFaction = pawn.Faction,
                    stage = HomecomingLifecycleStage.Kidnapped
                };
                records.Add(record);
            }

            return record;
        }

        public void RegisterContact(Pawn pawn)
        {
            HomecomingRecord record = RegisterKidnapped(pawn);
            if (record != null && HomecomingLifecycleContract.TryAdvance(
                record.stage,
                HomecomingLifecycleStage.Contacted))
            {
                record.stage = HomecomingLifecycleStage.Contacted;
            }
        }

        public void RegisterRescueQuest(Pawn pawn, Quest quest, Site site)
        {
            HomecomingRecord record = RegisterKidnapped(pawn);
            if (record == null)
            {
                return;
            }

            if (record.stage == HomecomingLifecycleStage.Kidnapped)
            {
                record.stage = HomecomingLifecycleStage.Contacted;
            }

            if (record.stage == HomecomingLifecycleStage.Contacted &&
                HomecomingLifecycleContract.TryAdvance(
                    record.stage,
                    HomecomingLifecycleStage.RescueQuestActive))
            {
                record.stage = HomecomingLifecycleStage.RescueQuestActive;
            }

            record.quest = quest;
            record.site = site;
        }

        public bool TryResolve(Pawn pawn, HomecomingLifecycleStage outcome)
        {
            HomecomingRecord record = records.FirstOrDefault(r => r.pawn == pawn);
            if (record == null || HomecomingLifecycleContract.IsTerminal(record.stage))
            {
                return false;
            }

            if (!HomecomingLifecycleContract.TryAdvance(record.stage, outcome))
            {
                return false;
            }

            record.stage = outcome;
            return true;
        }

        public bool IsAtStage(Pawn pawn, HomecomingLifecycleStage stage)
        {
            return records.Any(r => r.pawn == pawn && r.stage == stage);
        }

        public void QueueReturnRaid(Pawn pawn, int delayTicks)
        {
            HomecomingRecord record = records.FirstOrDefault(r => r.pawn == pawn);
            if (record == null)
            {
                return;
            }

            record.returnAtTick = Find.TickManager.TicksGame + delayTicks;
            record.returnRaid = true;
        }

        public void QueueWoundedReturn(Pawn pawn, int delayTicks)
        {
            HomecomingRecord record = records.FirstOrDefault(r => r.pawn == pawn);
            if (record == null)
            {
                return;
            }

            record.returnAtTick = Find.TickManager.TicksGame + delayTicks;
            record.returnRaid = false;
        }

        public Pawn TakePendingReturnRaidPawn()
        {
            Pawn pawn = pendingReturnRaidPawn;
            pendingReturnRaidPawn = null;
            return pawn;
        }

        public void ScheduleRumorLetter(Pawn pawn, int delayTicks)
        {
            if (pawn == null)
            {
                return;
            }

            scheduledRumors.Add(new ScheduledRumor
            {
                pawn = pawn,
                triggerTick = Find.TickManager.TicksGame + delayTicks
            });
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            for (int i = scheduledRumors.Count - 1; i >= 0; i--)
            {
                ScheduledRumor rumor = scheduledRumors[i];
                if (rumor.pawn == null || Find.TickManager.TicksGame < rumor.triggerTick)
                {
                    continue;
                }

                SendRumorLetter(rumor.pawn);
                scheduledRumors.RemoveAt(i);
            }

            for (int i = 0; i < records.Count; i++)
            {
                HomecomingRecord record = records[i];
                if (record.pawn == null || record.returnAtTick < 0 ||
                    Find.TickManager.TicksGame < record.returnAtTick)
                {
                    continue;
                }

                if (record.returnRaid)
                {
                    TryStartReturnRaid(record);
                }
                else
                {
                    CompleteWoundedReturn(record);
                }
            }
        }

        private void TryStartReturnRaid(HomecomingRecord record)
        {
            Map map = Find.AnyPlayerHomeMap;
            Faction faction = record.captorFaction;
            if (map == null || faction == null || !faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamed("OH_ReturnRaid", false);
            if (incidentDef == null)
            {
                Log.Error("[OpHomecoming] OH_ReturnRaid is missing; retaining the return request.");
                return;
            }

            pendingReturnRaidPawn = record.pawn;
            IncidentParms parms = new IncidentParms
            {
                target = map,
                faction = faction,
                points = System.Math.Max(StorytellerUtility.DefaultThreatPointsNow(map) * 0.75f, 100f),
                forced = true
            };

            if (incidentDef.Worker.TryExecute(parms))
            {
                record.returnAtTick = -1;
                pendingReturnRaidPawn = null;
            }
        }

        private void CompleteWoundedReturn(HomecomingRecord record)
        {
            Map map = Find.AnyPlayerHomeMap;
            Pawn pawn = record.pawn;
            if (map == null || pawn == null)
            {
                return;
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn();
            }

            pawn.SetFaction(Faction.OfPlayer);
            pawn.guest?.SetGuestStatus(null);
            HediffDef bruiseDef = HediffDef.Named("Bruise");
            if (bruiseDef != null && pawn.health != null)
            {
                Hediff bruise = HediffMaker.MakeHediff(
                    bruiseDef, pawn, pawn.RaceProps?.body?.corePart);
                bruise.Severity = 0.2f * (OperationHomecomingMod.Settings?.illnessSeverityMultiplier ?? 1f);
                pawn.health.AddHediff(bruise);
            }

            if (CellFinder.TryFindRandomSpawnCellForPawnNear(map.Center, map, out IntVec3 cell))
            {
                GenSpawn.Spawn(pawn, cell, map);
            }

            Find.LetterStack.ReceiveLetter(
                "OH_RescueSuccessLabel".Translate(pawn.LabelShort),
                "OH_RescueSuccessText".Translate(pawn.LabelShort),
                LetterDefOf.PositiveEvent,
                pawn);
            record.returnAtTick = -1;
        }

        private void SendRumorLetter(Pawn pawn)
        {
            string[] keys =
            {
                "OH_Rumor_Sighting",
                "OH_Rumor_Trader",
                "OH_Rumor_Escape",
                "OH_Rumor_Condition"
            };
            Find.LetterStack.ReceiveLetter(
                "OH_RumorLabel".Translate(),
                keys.RandomElement().Translate(pawn.LabelShort),
                LetterDefOf.NeutralEvent,
                pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref scheduledRumors, "scheduledRumors", LookMode.Deep);
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                scheduledRumors ??= new List<ScheduledRumor>();
                records ??= new List<HomecomingRecord>();
                scheduledRumors.RemoveAll(r => r == null || r.pawn == null);
                records.RemoveAll(r => r == null || r.pawn == null);
            }
        }

        private class ScheduledRumor : IExposable
        {
            public Pawn pawn;
            public int triggerTick;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref triggerTick, "triggerTick");
            }
        }
    }

    public class HomecomingRecord : IExposable
    {
        public Pawn pawn;
        public Faction captorFaction;
        public Quest quest;
        public Site site;
        public HomecomingLifecycleStage stage;
        public int returnAtTick = -1;
        public bool returnRaid;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref captorFaction, "captorFaction");
            Scribe_References.Look(ref quest, "quest");
            Scribe_References.Look(ref site, "site");
            Scribe_Values.Look(ref stage, "stage", HomecomingLifecycleStage.Kidnapped);
            Scribe_Values.Look(ref returnAtTick, "returnAtTick", -1);
            Scribe_Values.Look(ref returnRaid, "returnRaid");
        }
    }
}
