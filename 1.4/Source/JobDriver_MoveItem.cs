using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using VFECore;

namespace VPESkipdoorPathing
{
    public class JobDriver_MoveItem : JobDriver_UseDoorTeleporter
    {
        private bool forbiddenInitially;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref forbiddenInitially, "forbiddenInitially", defaultValue: false);
        }
        public override void Notify_Starting()
        {
            base.Notify_Starting();
            if (TargetThingB != null)
            {
                forbiddenInitially = TargetThingB.IsForbidden(pawn);
            }
            else
            {
                forbiddenInitially = false;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, -1, null, errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnBurningImmobile(TargetIndex.B);
            if (!forbiddenInitially)
            {
                this.FailOnForbidden(TargetIndex.B);
            }
            Toils_General.DoAtomic(delegate
            {
                startTick = Find.TickManager.TicksGame;
            });
            Toil reserveTargetA = Toils_Reserve.Reserve(TargetIndex.B);
            yield return reserveTargetA;
            Toil postCarry = Toils_General.Label();
            Thing carriedThing;
            yield return Toils_Jump.JumpIf(postCarry, () => (carriedThing = pawn.carryTracker.CarriedThing) != null && carriedThing == pawn.jobs.curJob.GetTarget(TargetIndex.B).Thing);
            //Toil toilGoto = null;
            //toilGoto = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            //yield return toilGoto;
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true);
            yield return postCarry;
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return carryToCell;

            this.AddEndCondition(() => this.Dest is null || !this.Dest.Spawned || this.Dest.Destroyed ? JobCondition.Incompletable : JobCondition.Ongoing);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil wait = Toils_General.Wait(16, TargetIndex.A).WithProgressBarToilDelay(TargetIndex.A).WithEffect(EffecterDefOf.Skip_Entry, TargetIndex.A);
            wait.AddPreTickAction(() =>
            {
                Origin.DoTeleportEffects(this.pawn, this.ticksLeftThisToil, this.job.globalTarget.Map, ref targetCell, Dest);
            });
            yield return wait;
            yield return Toils_General.DoAtomic(() =>
            {
                Origin.Teleport(pawn, this.job.globalTarget.Map, this.targetCell);
            });
        }
    }
}
