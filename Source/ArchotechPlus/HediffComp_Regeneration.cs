﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
namespace ArchotechPlus
{
    // ReSharper disable once InconsistentNaming
    // Disabled for this project to maintain consistency with RimWorld naming conventions.
    public class HediffComp_Regeneration : HediffComp
    {
        private const int HourTickInterval = 2500;
        private const int AgeMultiplier = 10;
        private static long TargetAgeInTicks => (ArchotechPlusSettings.TargetAge * 3600000L) + 1800000L;

        private int _ticks;
        private int _ticksFullCharge;

        private int _healingCharges;

        private static int _resurrectionCharges;
        
        private BodyPartRecord _bodyPartRegenerationTarget;
        private Hediff _woundRegenerationTarget;
        private Hediff _illnessHealingTarget;

        public Dictionary<BodyPartRecord, HediffDef> previousImplants;
        private static readonly HediffDef RegenProgress = DefDatabase<HediffDef>.GetNamed("RegenerationProgress");
        private float PercentageCharged => (float)_ticks / _ticksFullCharge;
        
        public void RememberImplant(Hediff_Implant hediff_Implant, BodyPartRecord part)
        {
            if (previousImplants is null)
            {
                previousImplants = new Dictionary<BodyPartRecord, HediffDef>();
            }
            previousImplants[part] = hediff_Implant.def;
        }
        public override void CompPostMake()
        {
            ResetChargingTicks();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            _ticks++;
            if (_ticks > _ticksFullCharge)
            {
                ChargeRegenerator();
                ResetChargingTicks();
            }
            if (_ticks % HourTickInterval == 0)
            {
                LongTick();
            }
        }

        private void LongTick()
        {
            if (IsPawnInjured() && UsableHealingCharge())
            {
                if (TryRestoreMissingPart() || TryHealRandomPermanentWound() || TryHealRandomDisease())
                {
                    _ticks = 0;
                    IsPawnInjured();
                    return;
                }
            }
            if(ArchotechPlusSettings.RegeneratorDeAge)
            {
                ReduceAge();
            }
        }
        private void ChargeRegenerator()
        {
            if (ResurrectorCanCharge())
            {
                _resurrectionCharges++;
            }
            else if (HealerCanCharge())
            {
                _healingCharges++;
            }
        }
        private bool ResurrectorCanCharge()
        {
            return parent.Severity > 2 && ArchotechPlusSettings.RegeneratorResurrects && _resurrectionCharges < ArchotechPlusSettings.MaxResurrectionCharges;
        }
        private bool HealerCanCharge()
        {
            return parent.Severity > 1 && _healingCharges < ArchotechPlusSettings.MaxHealingCharges;
        }
        private bool UsableHealingCharge()
        {
            if (_healingCharges <= 0)
            {
                return false;
            }

            _healingCharges--;
            return true;
        }
        private void ResetChargingTicks()
        {
            _ticks = 0;
            _ticksFullCharge = ArchotechPlusSettings.HealingRange.RandomInRange * HourTickInterval;
        }
        private bool IsPawnInjured()
        {
            _bodyPartRegenerationTarget = FindBiggestMissingBodyPart();
            _woundRegenerationTarget = FindRandomPermanentWound();
            _illnessHealingTarget = FindRandomDisease();
            return _bodyPartRegenerationTarget != null || _woundRegenerationTarget != null || _illnessHealingTarget != null;
        }
        private Hediff FindRandomDisease()
        {
            return Pawn.health.hediffSet.hediffs.Where(hd => hd.def.tendable).TryRandomElement(out var result) ? result : null;
        }

        private bool SpecialEyesRule(Hediff_MissingPart part) 
        {
            //Log.Message("Special Eyes Rule");
            if(ArchotechPlusSettings.RegeneratorAlwaysHealEyes)
            {
                //Log.Message("Setting is on to always heal");
                return true;
            } else if(part?.Part?.def?.defName != "Eye")
            {
                //Log.Message("Part is not Eyes");
                return true;
            } else if(Pawn?.Ideo?.IdeoApprovesOfBlindness() ?? false)
            {
                //Log.Message("Special Case applies, skipping this");
                return false;
            } else
            {
                //Log.Message("Ideology doesn't want Blindness");
                return true;
            }
        }


        private BodyPartRecord FindBiggestMissingBodyPart(float minCoverage = 0.0f)
        {
            BodyPartRecord bodyPartRecord = null;
            foreach (var partsCommonAncestor in Pawn.health.hediffSet.GetMissingPartsCommonAncestors().Where(
                partsCommonAncestor =>
                    (double) partsCommonAncestor.Part.coverageAbsWithChildren >= (double) minCoverage
                    && !Pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(partsCommonAncestor.Part)
                    && (bodyPartRecord == null || (double) partsCommonAncestor.Part.coverageAbsWithChildren >
                        (double) bodyPartRecord.coverageAbsWithChildren)
                    && SpecialEyesRule(partsCommonAncestor)
                    ))
            {
                bodyPartRecord = partsCommonAncestor.Part;
            }
            return bodyPartRecord;
        }
        private Hediff FindRandomPermanentWound()
        {
            return !Pawn.health.hediffSet.hediffs.Where(hd => hd.def == HediffDefOf.ResurrectionPsychosis || hd.IsPermanent() || hd.def.chronic)
                .TryRandomElement(out var result) ? null : result;
        }

        private bool TryRestoreMissingPart()
        {
            if (_bodyPartRegenerationTarget == null)
            {
                return false;
            }

            Pawn.health.RestorePart(_bodyPartRegenerationTarget);
            Pawn.health.AddHediff(RegenProgress, _bodyPartRegenerationTarget);
            if (!PawnUtility.ShouldSendNotificationAbout(Pawn))
            {
                return true;
            }

            Messages.Message(
                "ArchotechPlusPartRegenerated".Translate((NamedArgument) parent.LabelCap,
                    (NamedArgument) Pawn.LabelShort, (NamedArgument) _bodyPartRegenerationTarget.Label, Pawn.Named("PAWN")), Pawn,
                MessageTypeDefOf.PositiveEvent);
            return true;
        }
        private bool TryHealRandomPermanentWound()
        {
            if (_woundRegenerationTarget == null)
            {
                return false;
            }

            _woundRegenerationTarget.Severity = 0.0f;
            if (!PawnUtility.ShouldSendNotificationAbout(Pawn))
            {
                return true;
            }

            Messages.Message(
                "ArchotechPlusMessagePermanentWoundHealed".Translate((NamedArgument) parent.LabelCap, 
                    (NamedArgument) Pawn.LabelShort, (NamedArgument) _woundRegenerationTarget.Label,
                    Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private bool TryHealRandomDisease()
        {
            if (_illnessHealingTarget == null)
            {
                return false;
            }
            Pawn.health.hediffSet.hediffs.Remove(_illnessHealingTarget);
            if (!PawnUtility.ShouldSendNotificationAbout(Pawn))
            {
                return true;
            }

            Messages.Message("ArchotechPlusMessageDiseaseHealed".Translate(parent.LabelCap, Pawn.LabelShort, _illnessHealingTarget.Label, Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.PositiveEvent);
            return true;
        }
        private void ReduceAge()
        {
            if (Pawn.ageTracker.AgeBiologicalTicks < TargetAgeInTicks)
            {
                return;
            }

            Pawn.ageTracker.AgeBiologicalTicks -= HourTickInterval * AgeMultiplier;
        }

        public override void Notify_PawnDied()
        {
            if (_resurrectionCharges > 0)
            {
                SpendResurrectorCharge();
                CreateResurrector();
            }
            else
            {
                if (PawnUtility.ShouldSendNotificationAbout(Pawn))
                {
                    Messages.Message(
                        "ArchotechPlusNoResurrectorCharges".Translate(
                            (NamedArgument) parent.LabelCap,
                            (NamedArgument) Pawn.LabelShort,
                            Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.NegativeEvent);
                }
            }
            base.Notify_PawnDied();
        }

        private void SpendResurrectorCharge()
        {
            --_resurrectionCharges;
            _ticks = 0;
            if (!PawnUtility.ShouldSendNotificationAbout(Pawn))
            {
                return;
            }

            Messages.Message(
                "ArchotechPlusResurrectionChargeSpent".Translate(
                    (NamedArgument) parent.LabelCap,
                    (NamedArgument) Pawn.LabelShort,
                    Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.PositiveEvent);
        }
        private void CreateResurrector()
        {
            var resurrectionTracker = (ThingWithComps) GenSpawn.Spawn(ThingDef.Named("ResurrectorTracker"),
                Pawn.Corpse.Position, Pawn.Corpse.Map);
            resurrectionTracker.GetComp<CompResurrector>().Corpse = Pawn.Corpse;
            resurrectionTracker.GetComp<CompFollowsTarget>().Target = Pawn.Corpse;
        }
        
        public override void CompExposeData()
        {
            Scribe_Values.Look(ref _ticks, "ticksToHeal");
            Scribe_Values.Look(ref _ticksFullCharge, "ticksFullCharge");
            Scribe_Values.Look(ref _healingCharges, "healingCharges");
            Scribe_Values.Look(ref _resurrectionCharges, "resurrectionCharges");
            Scribe_Collections.Look(ref previousImplants, "previousImplants", LookMode.BodyPart, LookMode.Def, ref bodyPartKeys, ref defsValues);
        }

        private List<BodyPartRecord> bodyPartKeys;
        private List<HediffDef> defsValues;
        public override string CompDebugString()
        {
            return "Ticks: " + _ticks
                                   + "\nTicksToFullCharge" + _ticksFullCharge
                                   + "\nTargetAge: " + ArchotechPlusSettings.TargetAge;
        }

        private string CompTipStringBuilder()
        {
            var tipBuilder = new StringBuilder();
            if (parent.Severity > 2 && ArchotechPlusSettings.RegeneratorResurrects)
            {
                tipBuilder.AppendLine("Resurrector " + (_resurrectionCharges > 0 ? "charged" + "(" + _resurrectionCharges + "x)" : "not charged"));
            }

            tipBuilder.AppendLine("Healer " + (_healingCharges > 0 ? "charged" + "(" + _healingCharges + "x)" : "not charged"));
            if (_bodyPartRegenerationTarget != null)
            {
                tipBuilder.AppendLine("Injury targeted: " + _bodyPartRegenerationTarget.LabelCap);
            }
            else if (_woundRegenerationTarget != null)
            {
                tipBuilder.AppendLine("Injury targeted: " + _woundRegenerationTarget.LabelCap + "(" +
                                      _woundRegenerationTarget.Part.LabelCap + ")");
            }
            if (ResurrectorCanCharge() || HealerCanCharge())
            {
                tipBuilder.Append(PercentageCharged.ToStringPercent() + " charged");
            }

            return tipBuilder.ToString();
        }
        
        public override string CompTipStringExtra => parent.Severity > 1 ? CompTipStringBuilder() : null;
        
    }
}
