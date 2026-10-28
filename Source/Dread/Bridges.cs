using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Dread
{
    /// <summary>
    /// What else is installed.
    ///
    /// None of it is required. The base game supplies every figure this mod actually needs -- the
    /// storyteller's own threat points, how long it has been quiet, and what everybody is holding.
    /// What the rest of the set adds is the question of who is steadier than their kit suggests,
    /// which is a thing about people rather than about equipment and is therefore not something
    /// the base game has ever recorded.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Bridges
    {
        private static TraitDef weathered;
        private static TraitDef experience;   // Squad's recruit/veteran spectrum
        private static HediffDef warHardened; // Squad PTSD

        private static MethodInfo resentmentInstance;
        private static MethodInfo resentmentFor;
        private static PropertyInfo resentmentAvgRisk;
        private static FieldInfo standingRisk;

        private static int found;

        public static void Connect()
        {
            found = 0;

            weathered = DefDatabase<TraitDef>.GetNamedSilentFail("MT_Weathered");
            if (weathered != null) { found++; }

            experience = DefDatabase<TraitDef>.GetNamedSilentFail("SQ_Experience");
            if (experience != null) { found++; }

            warHardened = DefDatabase<HediffDef>.GetNamedSilentFail("SQP_WarHardened");
            if (warHardened != null) { found++; }

            try
            {
                Type comp = AccessTools.TypeByName("Resentment.GameComponent_Resentment");
                Type standing = AccessTools.TypeByName("Resentment.Standing");
                if (comp != null && standing != null)
                {
                    resentmentInstance = comp.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                    resentmentFor = comp.GetMethod("For", new[] { typeof(Pawn) });
                    resentmentAvgRisk = comp.GetProperty("AvgRisk", BindingFlags.Public | BindingFlags.Instance);
                    standingRisk = standing.GetField("risk", BindingFlags.Public | BindingFlags.Instance);
                    if (resentmentInstance != null && resentmentFor != null &&
                        resentmentAvgRisk != null && standingRisk != null)
                    {
                        found++;
                    }
                }
            }
            catch
            {
                resentmentFor = null;
            }
        }

        public static string Report() =>
            found == 0
                ? " Running alone: the storyteller's own figures are enough for it."
                : $" {found} of the set found: it will know who has done this before.";

        /// <summary>
        /// How much steadier than their equipment somebody is.
        ///
        /// Multiplies what they are worth to the colony, which is the honest place for it: a
        /// veteran with the same rifle as a recruit really is worth more, and every colonist who
        /// is counting what the colony has can see that.
        /// </summary>
        public static float SteadinessFactor(Pawn p)
        {
            float f = 1f;
            if (p?.story?.traits == null)
            {
                return f;
            }
            if (weathered != null && p.story.traits.HasTrait(weathered))
            {
                f *= 1.35f;
            }
            if (experience != null)
            {
                Trait t = p.story.traits.GetTrait(experience);
                if (t != null)
                {
                    // Squad runs this from green recruit up through veteran.
                    f *= 1f + (t.Degree * 0.15f);
                }
            }
            if (warHardened != null && p.health?.hediffSet != null &&
                p.health.hediffSet.HasHediff(warHardened))
            {
                f *= 1.2f;
            }
            return f;
        }

        /// <summary>
        /// Somebody who is always the one sent out dreads the next one more, and somebody who is
        /// never sent dreads it less. Resentment has already worked out which is which and there
        /// is no sense counting it a second time from scratch.
        ///
        /// Reading it costs nothing dangerous: Resentment's standing comes off the base game's
        /// records and touches no opinion. It is still only ever called from this mod's slow pass.
        /// </summary>
        public static float CarriesTheRiskFactor(Pawn p)
        {
            if (resentmentFor == null || p == null)
            {
                return 1f;
            }
            try
            {
                object comp = resentmentInstance.Invoke(null, null);
                if (comp == null)
                {
                    return 1f;
                }
                object standing = resentmentFor.Invoke(comp, new object[] { p });
                if (standing == null)
                {
                    return 1f;
                }
                float mine = (float)standingRisk.GetValue(standing);
                float average = (float)resentmentAvgRisk.GetValue(comp);
                if (average <= 0f)
                {
                    return 1f;
                }
                // Half again at twice the colony's share of it, a little less at none of it.
                return UnityEngine.Mathf.Clamp(0.85f + (mine / average) * 0.3f, 0.8f, 1.45f);
            }
            catch
            {
                return 1f;
            }
        }
    }
}
