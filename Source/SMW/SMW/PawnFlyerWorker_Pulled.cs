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
    internal class PawnFlyerWorker_Pulled: PawnFlyerWorker
    {
        public PawnFlyerWorker_Pulled(PawnFlyerProperties properties) : base(properties)
        {

        }

        public override float GetHeight(float t) => 0f;
    }
}
