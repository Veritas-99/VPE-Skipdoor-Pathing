using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI;
using Verse;

namespace VPESkipdoorPathing
{
    public class CompSkipdoorPathing : ThingComp
    {
        public JobQueue JobQueue;

        public bool Enabled;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (this.parent is Pawn pawn && pawn.IsColonistPlayerControlled)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "DraftedSkipdoorPathing".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/DraftedSkipdoorPathing"),
                    isActive = () => Enabled,
                    toggleAction = () => Enabled = !Enabled
                };
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref Enabled, "enabled");
            Scribe_Deep.Look(ref JobQueue, "jobQueue");
        }
    }
}
