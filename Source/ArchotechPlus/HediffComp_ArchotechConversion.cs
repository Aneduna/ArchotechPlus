using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
namespace ArchotechPlus
{
    public class HediffCompProperties_ArchotechConversion : HediffCompProperties
    {
        public HediffCompProperties_ArchotechConversion()
        {
            compClass = typeof(HediffComp_ArchotechConversion);
        }
    }
    public class HediffComp_ArchotechConversion : HediffComp
    {
        private void EnsureMaxLevel(Hediff part)
        {
            if (part is Hediff_Level withLevel)
            {
                withLevel.SetLevelTo((int)withLevel.def.maxSeverity);
                withLevel.Severity = withLevel.def.maxSeverity;
            }
            if (part is Hediff_ImplantWithLevel withImplLevel)
            {
                withImplLevel.SetLevelTo((int)withImplLevel.def.maxSeverity);
                withImplLevel.Severity = withImplLevel.def.maxSeverity;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Dictionary<BodyPartRecord, List<HediffDef>> hediffsToAdd = new Dictionary<BodyPartRecord, List<HediffDef>>();
            foreach (var implant in DefDatabase<RecipeDef>.AllDefs.Where(x => x.addsHediff != null && x.addsHediff.defName.ToLower().Contains("archotech")))
            {
                if (implant.addsHediff != this.Def)
                {
                    if (implant.appliedOnFixedBodyParts != null)
                    {
                        foreach (var partDef in implant.appliedOnFixedBodyParts)
                        {
                            var parts = Pawn.RaceProps.body.GetPartsWithDef(partDef);
                            if (parts != null)
                            {
                                foreach (var part in parts)
                                {
                                    if (part != this.parent.Part)
                                    {
                                        if (Pawn.health.hediffSet.PartIsMissing(part))
                                        {
                                            Pawn.health.RestorePart(part);
                                        }
                                        var newHediff = HediffMaker.MakeHediff(implant.addsHediff, Pawn);
                                        Pawn.health.AddHediff(newHediff, part);
                                        EnsureMaxLevel(newHediff);
                                    }
                                    else
                                    {
                                        if (hediffsToAdd.ContainsKey(part))
                                        {
                                            hediffsToAdd[part].Add(implant.addsHediff);
                                        }
                                        else
                                        {
                                            hediffsToAdd[part] = new List<HediffDef> { implant.addsHediff };
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var newHediff = HediffMaker.MakeHediff(implant.addsHediff, Pawn);
                        Pawn.health.AddHediff(newHediff);
                        EnsureMaxLevel(newHediff);
                    }
                }
            }

            foreach (var data in hediffsToAdd)
            {
                if (Pawn.health.hediffSet.PartIsMissing(data.Key))
                {
                    Pawn.health.RestorePart(data.Key);
                }
                foreach (var hediffDef in data.Value)
                {
                    var newHediff = HediffMaker.MakeHediff(hediffDef, Pawn);
                    Pawn.health.AddHediff(newHediff, data.Key);
                    if (newHediff is Hediff_Level withLevel)
                    {
                        withLevel.SetLevelTo((int)withLevel.def.maxSeverity);
                    }
                    if (newHediff is Hediff_ImplantWithLevel withImplLevel)
                    {
                        withImplLevel.SetLevelTo((int)withImplLevel.def.maxSeverity);
                    }
                }
            }
        }
    }
}
