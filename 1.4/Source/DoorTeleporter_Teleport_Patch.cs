using HarmonyLib;
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
}
