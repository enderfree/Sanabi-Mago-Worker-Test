using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace SMW
{
    // This is all from VFE Pirates. I couldn't find any way to do better. 
    public class PawnFlyer_Pulled: PawnFlyer
    {
        public GrapplingHook Hook;

        protected override void RespawnPawn()
        {
            base.RespawnPawn();
            Hook.Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref Hook, "hook");
        }
    }
}
