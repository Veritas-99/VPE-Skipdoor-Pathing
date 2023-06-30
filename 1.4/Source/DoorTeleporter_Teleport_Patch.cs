using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using VFECore;

namespace VPESkipdoorPathing
{
    [HarmonyPatch(typeof(DoorTeleporter), nameof(DoorTeleporter.Teleport))]
    public static class DoorTeleporter_Teleport_Patch
    {
        public static bool Prefix(DoorTeleporter __instance, Thing thing, Map mapTarget, IntVec3 cellTarget)
        {
            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                if (pawn.Map != mapTarget)
                {
                    Thing resultingThing = pawn.carryTracker.CarriedThing;
                    if (resultingThing != null)
                    {
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out resultingThing);
                        resultingThing.DeSpawn();
                        GenSpawn.Spawn(resultingThing, cellTarget, mapTarget);
                    }
                }

                bool drafted = pawn.drafter != null && pawn.Drafted;
                bool flag = Find.Selector.IsSelected(pawn);
                pawn.teleporting = true;
                if (pawn.Map == mapTarget)
                {
                    pawn.Position = cellTarget;
                    pawn.teleporting = false;
                }
                else
                {
                    pawn.ClearAllReservations(releaseDestinationsOnlyIfObsolete: false);
                    pawn.ExitMap(allowedToJoinOrCreateCaravan: false, Rot4.Invalid);
                    pawn.teleporting = false;
                    GenSpawn.Spawn(pawn, cellTarget, mapTarget);
                }
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = drafted;
                }

                if (flag)
                {
                    Find.Selector.Select(pawn);
                }
            }
            else
            {
                thing.DeSpawn();
                GenSpawn.Spawn(thing, cellTarget, mapTarget);
            }

            __instance.teleportEffecters.Remove(thing);

            return false;
        }

        public static void Postfix(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                var comp = pawn.GetComp<CompSkipdoorPathing>();
                if (comp.JobQueue != null)
                {
                    pawn.jobs.RestoreCapturedJobs(comp.JobQueue);
                    comp.JobQueue = null;
                }
            }
        }
    }
}
