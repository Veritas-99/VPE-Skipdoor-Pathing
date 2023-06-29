using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

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
}
