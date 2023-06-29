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
}
