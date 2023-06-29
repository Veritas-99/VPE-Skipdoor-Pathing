using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using VFECore;

namespace VPESkipdoorPathing
{
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
}
