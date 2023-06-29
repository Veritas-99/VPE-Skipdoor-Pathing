using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace VPESkipdoorPathing
{
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
