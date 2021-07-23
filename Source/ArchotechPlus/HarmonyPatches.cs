using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace ArchotechPlus
{
	[StaticConstructorOnStartup]
	public static class HarmonyInit
	{
		public static Harmony harmonyInstance;
		static HarmonyInit()
		{
			harmonyInstance = new Harmony("ArchotechPlus.Mod");
			harmonyInstance.PatchAll();
		}
	}
	[HarmonyPatch(typeof(HediffSet), "AddDirect")]
	public static class HediffSet_Patch
	{
		public static void Prefix(HediffSet __instance, Hediff hediff, DamageInfo? dinfo = null, DamageWorker.DamageResult damageResult = null)
		{
			if (hediff is Hediff_MissingPart)
			{
				var hediffRegeneration = __instance.pawn.health.hediffSet.hediffs.FirstOrDefault(x => x.TryGetComp<HediffComp_Regeneration>() != null);
				if (hediffRegeneration != null)
                {
					var hediffComp = hediffRegeneration.TryGetComp<HediffComp_Regeneration>();
					var previousHediff = __instance.pawn.health.hediffSet.hediffs.FirstOrDefault(x => x.Part == hediff.Part);
					if (previousHediff is Hediff_Implant implant)
                    {
						hediffComp.RememberImplant(implant, hediff.Part);
                    }
                }
			}
		}
	}

	[HarmonyPatch(typeof(Need_Joy), "FallPerInterval", MethodType.Getter)]
	public static class FallPerInterval_Patch
	{
		public static void Postfix(Need_Joy __instance, Pawn ___pawn, ref float __result)
		{
			var hediff = ___pawn.health?.hediffSet.GetFirstHediffOfDef(HediffDef.Named("ArchotechCortex"));
			if (hediff != null)
            {
				__result *= 0f;
            }
		}
	}
	[HarmonyPatch(typeof(CompUseEffect_InstallImplant), "CanBeUsedBy")]
	public static class CompUseEffect_InstallImplant_UsedBy_Patch
    {
		public static bool Prefix(ref bool __result, CompUseEffect_InstallImplant __instance, Pawn p, out string failReason)
        {
			failReason = null;
			if ((!p.IsFreeColonist || p.HasExtraHomeFaction()) && !__instance.Props.allowNonColonists)
			{
				return true;
			}
			if (p.RaceProps.body.GetPartsWithDef(__instance.Props.bodyPart).FirstOrFallback() == null)
			{
				return true;
			}
			Hediff existingImplant = __instance.GetExistingImplant(p);
			if (existingImplant != null)
			{
				if (!__instance.Props.canUpgrade)
				{
					return true;
				}
				if(existingImplant is Hediff_ImplantWithLevel)
                {
					Hediff_ImplantWithLevel hediff_Level = (Hediff_ImplantWithLevel)existingImplant;
					if ((float)hediff_Level.level >= hediff_Level.def.maxSeverity)
					{
						failReason = "InstallImplantAlreadyMaxLevel".Translate();
						__result = false;
						return false;
					}
					__result = true;
					return false;
				}
			}
			return true;
        }
    }

	[HarmonyPatch(typeof(CompUseEffect_InstallImplant), "DoEffect")]
	public static class CompUseEffect_InstallImplant_doEffect_Patch
	{
		public static bool Prefix(CompUseEffect_InstallImplant __instance, Pawn user)
		{
			BodyPartRecord bodyPartRecord = user.RaceProps.body.GetPartsWithDef(__instance.Props.bodyPart).FirstOrFallback();
			if (bodyPartRecord != null)
			{
				Hediff firstHediffOfDef = user.health.hediffSet.GetFirstHediffOfDef(__instance.Props.hediffDef);
				if (firstHediffOfDef == null || firstHediffOfDef is Hediff_Level)
				{
					return true;
				}
				else if (firstHediffOfDef is Hediff_ImplantWithLevel)
				{
					((Hediff_ImplantWithLevel)firstHediffOfDef).ChangeLevel(1);
					return false;
				}
			}
			return true;
		}
	}

}