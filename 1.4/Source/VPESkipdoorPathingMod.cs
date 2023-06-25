using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using VFECore;

namespace VPESkipdoorPathing
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            new Harmony("VPESkipdoorPathingMod").PatchAll();
            foreach (var race in DefDatabase<ThingDef>.AllDefs.Where(x => x.race?.Humanlike ?? false))
            {
                race.comps.Add(new CompProperties
                {
                    compClass = typeof(CompSkipdoorPathing)
                });
            }
        }
    }

    public class CompSkipdoorPathing : ThingComp
    {
        public JobQueue jobQueue;
        public bool enabled;
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (this.parent is Pawn pawn && pawn.IsColonistPlayerControlled)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "DraftedSkipdoorPathing".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/DraftedSkipdoorPathing"),
                    isActive = () => enabled,
                    toggleAction = () => enabled = !enabled
                };
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref enabled, "enabled");
            Scribe_Deep.Look(ref jobQueue, "jobQueue");
        }
    }

    [DefOf]
    public static class VPE_DefOf
    {
        public static JobDef VEF_UseDoorTeleporter;
        public static JobDef VPESP_MoveItem;
    }

    [HarmonyPatch(typeof(DoorTeleporter), nameof(DoorTeleporter.Teleport))]
    public static class DoorTeleporter_Teleport_Patch
    {
        public static void Postfix(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                var comp = pawn.GetComp<CompSkipdoorPathing>();
                if (comp.jobQueue != null)
                {
                    pawn.jobs.RestoreCapturedJobs(comp.jobQueue);
                    comp.jobQueue = null;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "StartPath")]
    public static class Pawn_PathFollower_StartPath_Patch
    {
        public static void Postfix(Pawn_PathFollower __instance, Pawn ___pawn, LocalTargetInfo dest, PathEndMode peMode)
        {
            if (___pawn.CurJob?.jobGiver is JobGiver_Work || ___pawn.CurJob?.jobGiver is ThinkNode_QueuedJob queuedJob)
            {
                Pawn pawn = __instance.pawn;
                if (pawn.IsColonist || pawn.IsSlaveOfColony)
                {
                    var job = pawn.CurJob;
                    if (Pawn_JobTracker_DetermineNextJob_Patch.ShouldNotUseTeleport(job, pawn))
                    {
                        return;
                    }
                    float pawnTargetDistance = pawn.Position.DistanceTo(dest.Cell);
                    if (pawnTargetDistance > 5)
                    {
                        if (Pawn_JobTracker_DetermineNextJob_Patch.GetBestSkipdoor(pawn, out var bestGate, out var bestTarget,
                            dest.Cell))
                        {
                            var comp = pawn.GetComp<CompSkipdoorPathing>();
                            if (pawn.carryTracker.CarriedThing is not null)
                            {
                                if (VPESkipdoorPathingSettings.skipdoorPathingWhenHauling)
                                {
                                    var stackCount = pawn.carryTracker.CarriedThing.stackCount;
                                    if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var thing))
                                    {
                                        pawn.CurJob.count = stackCount;
                                        pawn.jobs.jobQueue.EnqueueFirst(pawn.CurJob);
                                        comp.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
                                        var newJob = JobMaker.MakeJob(VPE_DefOf.VPESP_MoveItem, bestGate, thing, bestTarget);
                                        newJob.count = stackCount;
                                        newJob.globalTarget = bestTarget;
                                        pawn.jobs.StartJob(newJob, JobCondition.InterruptForced);
                                    }
                                }
                            }
                            else
                            {
                                pawn.jobs.jobQueue.EnqueueFirst(pawn.CurJob);
                                comp.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
                                var newJob = JobMaker.MakeJob(VPE_DefOf.VEF_UseDoorTeleporter, bestGate);
                                newJob.globalTarget = bestTarget;
                                pawn.jobs.StartJob(newJob, JobCondition.InterruptForced);
                            }
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.DetermineNextJob))]
    public static class Pawn_JobTracker_DetermineNextJob_Patch
    {
        public static void Postfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = __instance.pawn;
            if (pawn.IsColonist || pawn.IsSlaveOfColony)
            {
                if (ShouldNotUseTeleport(__result.Job, pawn))
                {
                    return;
                }
                var jobDef = __result.Job.def;
                if (!__result.Job.DetermineTargets(out IntVec3 firstTarget, out IntVec3 secondTarget)) return;
                float pawnTargetDistance = pawn.Position.DistanceTo(firstTarget);
                float firstToSecondTargetDistance;
                if (secondTarget.IsValid && (jobDef == JobDefOf.HaulToCell || jobDef == JobDefOf.HaulToContainer))
                    firstToSecondTargetDistance = firstTarget.DistanceTo(secondTarget);
                else firstToSecondTargetDistance = 0;

                if (pawnTargetDistance + firstToSecondTargetDistance > 5)
                {
                    if (GetBestSkipdoor(pawn, out var bestGate, out var bestTarget, firstTarget))
                    {
                        pawn.jobs.jobQueue.EnqueueFirst(__result.Job);
                        var comp = pawn.GetComp<CompSkipdoorPathing>();
                        comp.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
                        var job = JobMaker.MakeJob(VPE_DefOf.VEF_UseDoorTeleporter, bestGate);
                        job.globalTarget = bestTarget;
                        __result = new ThinkResult(job, __result.SourceNode, __result.Tag, false);
                    }
                }
            }
        }

        public static bool ShouldNotUseTeleport(Job job, Pawn pawn)
        {
            return pawn.IsBorrowedByAnyFaction() || !pawn.IsColonistPlayerControlled 
                || pawn.def.race.intelligence != Intelligence.Humanlike || job == null 
                || typeof(JobDriver_UseDoorTeleporter).IsAssignableFrom(job.def.driverClass)
                || job.def == JobDefOf.Wait_Wander || job.def == JobDefOf.GotoWander
                || pawn.GetComp<CompSkipdoorPathing>().enabled is false && pawn.Drafted || pawn.InMentalState || (pawn.mindState.duty != null 
                    && (pawn.mindState.duty.def == DutyDefOf.TravelOrWait 
                    || pawn.mindState.duty.def == DutyDefOf.TravelOrLeave));
        }

        public static bool GetBestSkipdoor(Pawn pawn, out Thing bestGate, out Thing bestTarget, IntVec3 firstTarget)
        {
            bestGate = bestTarget = null;
            Map map = pawn.Map;
            var list = WorldComponent_DoorTeleporterManager.Instance.DoorTeleporters.Where(x => x.Map == map).ToList();
            if (list.Count > 1)
            {
                bestGate = list.Where(x => pawn.CanReserveAndReach(x, PathEndMode.OnCell, Danger.Deadly))
                    .OrderBy(x => GetPathCost(map.pathFinder.FindPath(pawn.Position, x, pawn))).FirstOrDefault();
                if (bestGate != null)
                {
                    bestTarget = list.OrderBy(x => GetPathCost(map.pathFinder.FindPath(firstTarget, x, pawn))).FirstOrDefault();
                    map.pawnPathPool.paths.Clear();
                    if (bestGate != null && bestTarget != null && bestGate != bestTarget)
                    {
                        var distanceWithTeleporters = pawn.Position.DistanceTo(bestGate.Position) + bestTarget.Position.DistanceTo(firstTarget);
                        var pawnDistance = pawn.Position.DistanceTo(firstTarget);
                        if (pawnDistance > distanceWithTeleporters + 3)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static float GetPathCost(PawnPath path)
        {
            if (path == PawnPath.NotFound)
            {
                return int.MaxValue;
            }
            return path.TotalCost;
        }
        public static bool DetermineTargets(this Job job, out IntVec3 firstTarget, out IntVec3 secondTarget)
        {
            var thinkResultJob = job;
            var thinkResultJobDef = thinkResultJob.def;
            if (thinkResultJobDef == JobDefOf.TendPatient || thinkResultJobDef == JobDefOf.Refuel
                || thinkResultJobDef == JobDefOf.FixBrokenDownBuilding)
            {
                firstTarget = thinkResultJob.GetFirstTarget(TargetIndex.B);
                secondTarget = thinkResultJob.GetFirstTarget(TargetIndex.A);
            }
            else if (thinkResultJobDef == JobDefOf.DoBill && !thinkResultJob.targetQueueB.NullOrEmpty())
            {
                firstTarget = thinkResultJob.targetQueueB[0].Cell;
                secondTarget = thinkResultJob.GetFirstTarget(TargetIndex.A);
            }
            else
            {
                firstTarget = thinkResultJob.GetFirstTarget(TargetIndex.A);
                secondTarget = thinkResultJob.GetFirstTarget(TargetIndex.B);
            }
            if (!firstTarget.IsValid) return false;
            return true;
        }
        public static IntVec3 GetFirstTarget(this Job job, TargetIndex index)
        {
            var queue = job.GetTargetQueue(index);
            if (queue.Count != 0)
            {
                return queue[0].Cell;
            }
            return job.GetTarget(index).Cell;
        }
    }

    public class VPESkipdoorPathingMod : Mod
    {
        public static VPESkipdoorPathingSettings settings;
        public VPESkipdoorPathingMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<VPESkipdoorPathingSettings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }

    public class VPESkipdoorPathingSettings : ModSettings
    {
        public static bool skipdoorPathingWhenHauling = true;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref skipdoorPathingWhenHauling, "skipdoorPathingWhenHauling", true);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            var ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("SkipdoorPathingWhenHauling".Translate(), ref skipdoorPathingWhenHauling);
            ls.End();
        }
    }
}
