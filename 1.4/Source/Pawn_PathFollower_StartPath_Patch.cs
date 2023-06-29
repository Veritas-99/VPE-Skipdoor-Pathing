using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace VPESkipdoorPathing
{
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
                                        comp.JobQueue = pawn.jobs.CaptureAndClearJobQueue();
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
                                comp.JobQueue = pawn.jobs.CaptureAndClearJobQueue();
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
}
