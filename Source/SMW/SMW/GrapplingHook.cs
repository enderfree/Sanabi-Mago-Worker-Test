using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.Noise;
using Verse.Sound;

namespace SMW
{
    [StaticConstructorOnStartup]
    public class GrapplingHook: Bullet
    {
        private static readonly Material RopeLineMat = MaterialPool.MatFrom("UI/Overlays/Rope", ShaderDatabase.Transparent, GenColor.FromBytes(99, 70, 41));
        private int ticksTillPull = -1;

        public Vector3 Origin => launcher.Spawned
            ? launcher.DrawPos
            : ThingOwnerUtility.SpawnedParentOrMe(launcher.ParentHolder)?.DrawPos ?? origin;

        public override Vector3 ExactPosition => ticksTillPull == -1 ? base.ExactPosition : destination;

        public void UpdateDest()
        {
            destination = usedTarget.Cell.ToVector3Shifted();
        }

        public override void Tick()
        {
            base.Tick();
            if (ticksTillPull <= 0) return;
            ticksTillPull--;
            if (ticksTillPull <= 0) Pull();
        }

        public void Pull()
        {
            ticksTillPull = -2;

            IntVec3 destCell = new IntVec3();
            //Thing target = usedTarget.Thing;

            try
            {
                destCell = usedTarget.Thing.OccupiedRect().AdjacentCells.MinBy(cell => cell.DistanceTo(launcher.Position));
            }
            catch 
            {
                destCell = usedTarget.Cell;
            }

            var selected = Find.Selector.IsSelected(launcher);
            var flyer = (PawnFlyer_Pulled)PawnFlyer.MakeFlyer(InternalDefOf.SMW_GrapplingPawn, launcher as Pawn, destCell, null, null);
            flyer.Hook = this;
            GenSpawn.Spawn(flyer, destCell, Map);
            if (selected)
                Find.Selector.Select(launcher);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            GenDraw.DrawLineBetween(Origin, DrawPos, AltitudeLayer.PawnRope.AltitudeFor(), RopeLineMat, 0.05f);
            base.DrawAt(drawLoc, flip);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // Hit
            Map map = base.Map;
            IntVec3 position = base.Position;
            GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);
            if (!blockedByShield && this.def.projectile.landedEffecter != null)
            {
                this.def.projectile.landedEffecter.Spawn(base.Position, base.Map, 1f).Cleanup();
            }
            BattleLogEntry_RangedImpact battleLogEntry_RangedImpact = new BattleLogEntry_RangedImpact(this.launcher, hitThing, this.intendedTarget.Thing, this.equipmentDef, this.def, this.targetCoverDef);
            Find.BattleLog.Add(battleLogEntry_RangedImpact);
            this.NotifyImpact(hitThing, map, position);
            if (hitThing != null)
            {
                Pawn pawn;
                bool instigatorGuilty = (pawn = (this.launcher as Pawn)) == null || !pawn.Drafted;
                DamageInfo dinfo = new DamageInfo(this.def.projectile.damageDef, (float)this.DamageAmount, this.ArmorPenetration, this.ExactRotation.eulerAngles.y, this.launcher, null, this.equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, this.intendedTarget.Thing, instigatorGuilty, true, QualityCategory.Normal, true);
                dinfo.SetWeaponQuality(this.equipmentQuality);
                hitThing.TakeDamage(dinfo).AssociateWithLog(battleLogEntry_RangedImpact);
                Pawn pawn2 = hitThing as Pawn;
                if (pawn2 != null)
                {
                    Pawn_StanceTracker stances = pawn2.stances;
                    if (stances != null)
                    {
                        stances.stagger.Notify_BulletImpact(this);
                    }
                }
                if (this.def.projectile.extraDamages != null)
                {
                    foreach (ExtraDamage extraDamage in this.def.projectile.extraDamages)
                    {
                        if (Rand.Chance(extraDamage.chance))
                        {
                            DamageInfo dinfo2 = new DamageInfo(extraDamage.def, extraDamage.amount, extraDamage.AdjustedArmorPenetration(), this.ExactRotation.eulerAngles.y, this.launcher, null, this.equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, this.intendedTarget.Thing, instigatorGuilty, true, QualityCategory.Normal, true);
                            hitThing.TakeDamage(dinfo2).AssociateWithLog(battleLogEntry_RangedImpact);
                        }
                    }
                }
                if (Rand.Chance(this.def.projectile.bulletChanceToStartFire) && (pawn2 == null || Rand.Chance(FireUtility.ChanceToAttachFireFromEvent(pawn2))))
                {
                    hitThing.TryAttachFire(this.def.projectile.bulletFireSizeRange.RandomInRange, this.launcher);
                    return;
                }
            }
            else
            {
                if (!blockedByShield)
                {
                    SoundDefOf.BulletImpact_Ground.PlayOneShot(new TargetInfo(base.Position, map, false));
                    if (base.Position.GetTerrain(map).takeSplashes)
                    {
                        FleckMaker.WaterSplash(this.ExactPosition, map, Mathf.Sqrt((float)this.DamageAmount) * 1f, 4f);
                    }
                    else
                    {
                        FleckMaker.Static(this.ExactPosition, map, FleckDefOf.ShotHit_Dirt, 1f);
                    }
                }
                if (Rand.Chance(this.def.projectile.bulletChanceToStartFire))
                {
                    FireUtility.TryStartFireIn(base.Position, map, this.def.projectile.bulletFireSizeRange.RandomInRange, this.launcher, null);
                }
            }

            // Grapple
            if (!blockedByShield)
            {
                GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);
                ticksTillPull = 10;
                landed = true;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksTillPull, "ticksTillPull");
        }

        private void NotifyImpact(Thing hitThing, Map map, IntVec3 position)
        {
            BulletImpactData impactData = new BulletImpactData
            {
                bullet = this,
                hitThing = hitThing,
                impactPosition = position
            };
            if (hitThing != null)
            {
                hitThing.Notify_BulletImpactNearby(impactData);
            }
            int num = 9;
            for (int i = 0; i < num; i++)
            {
                IntVec3 c = position + GenRadial.RadialPattern[i];
                if (c.InBounds(map))
                {
                    List<Thing> thingList = c.GetThingList(map);
                    for (int j = 0; j < thingList.Count; j++)
                    {
                        if (thingList[j] != hitThing)
                        {
                            thingList[j].Notify_BulletImpactNearby(impactData);
                        }
                    }
                }
            }
        }

        public override bool AnimalsFleeImpact
        {
            get
            {
                return true;
            }
        }
    }
}
