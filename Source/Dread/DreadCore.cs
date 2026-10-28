using HarmonyLib;
using RimWorld;
using Verse;

namespace Dread
{
    [DefOf]
    public static class DR_DefOf
    {
        public static ThoughtDef DR_Dread;
        public static ThoughtDef DR_TooQuiet;
        public static ThoughtDef DR_Steady;
        public static ThoughtDef DR_Sharpening;
        public static ThoughtDef DR_StandsBetween;

        static DR_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DR_DefOf));
        }
    }

    /// <summary>All the tuning in one place. The ones a player can change come from the settings menu.</summary>
    public static class Tuning
    {
        private static DreadSettings S => DreadMod.Settings;

        /// <summary>Multiplier on the whole feeling.</summary>
        public static float Power => S.power;

        /// <summary>
        /// How many threat points one unit of readiness is reckoned to answer.
        ///
        /// This is the dial that decides whether a colony feels prepared or overmatched, and it is
        /// the one most worth moving. Lower it and colonists are harder to reassure.
        /// </summary>
        public static float ReadinessWorth => S.readinessWorth;

        /// <summary>Days of quiet before it starts to be the quiet that bothers them.</summary>
        public static float QuietDays => S.quietDays;

        public static bool Readiness => S.readiness;
        public static bool Quiet => S.quiet;
        public static bool Steadying => S.steadying;
        public static bool Relief => S.relief;

        /// <summary>How often the colony is measured. Colony-wide and not cheap, so not every tick.</summary>
        public const int CheckInterval = 2500;

        /// <summary>Stages of DR_Dread, on the ratio of what is coming to what we have.</summary>
        public static readonly float[] Stages = { 0.95f, 1.6f, 2.6f };

        /// <summary>Stages of DR_TooQuiet, in days past the settings figure.</summary>
        public static readonly float[] QuietStages = { 0f, 10f };

        /// <summary>Below this ratio a colonist is not worried at all, and may feel the opposite.</summary>
        public const float SteadyBelow = 0.7f;

        /// <summary>Stages of DR_StandsBetween, on how much of the colony's readiness one person is.</summary>
        public static readonly float[] StandsBetweenStages = { 0.22f, 0.38f };

        /// <summary>A ceiling, so a wealthy undefended colony is grim rather than unplayable.</summary>
        public const float MaxRatio = 4f;
    }

    [StaticConstructorOnStartup]
    public static class DR
    {
        /// <summary>Not in TraitDefOf, so it is looked up once here rather than every time it is wanted.</summary>
        public static readonly TraitDef Nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");

        static DR()
        {
            // There are no Harmony patches in this mod at all -- it reads the game and never
            // changes anything about how it runs. PatchAll is left in place so the shape matches
            // the rest of the set and so a patch added later is picked up without anybody
            // remembering to wire it in.
            new Harmony("dread.anticipation").PatchAll();
            Bridges.Connect();
            Log.Message("[Dread] Loaded. Watching the treeline." + Bridges.Report());
        }

        public static bool Counts(Pawn p) =>
            p != null && !p.Dead && p.needs?.mood != null &&
            p.IsColonist && !p.IsPrisoner && p.RaceProps.Humanlike &&
            !p.DevelopmentalStage.Juvenile();

        /// <summary>
        /// Can this colonist actually do anything about what is coming?
        ///
        /// Used for measuring the colony's readiness rather than for deciding who feels afraid.
        /// Somebody in bed with a gut infection still dreads the raid; they just are not going to
        /// be standing in the line when it arrives.
        /// </summary>
        public static bool AbleToStand(Pawn p) =>
            Counts(p) && p.Spawned && !p.Downed && !p.InMentalState &&
            !p.WorkTagIsDisabled(WorkTags.Violent);

        /// <summary>
        /// Is the storyteller even going to send anything?
        ///
        /// On a peaceful run big threats are switched off outright, and colonists should not be
        /// afraid of raids that are never coming. This is the difference between a mod that reads
        /// the game and one that just assumes RimWorld.
        /// </summary>
        public static bool ThreatsExpected()
        {
            // The runtime Difficulty, not the DifficultyDef. They carry the same two fields and
            // the def is only the preset: a player who turned big threats off in the custom
            // settings still has whatever def they started from, so reading the def would tell us
            // raids are coming when the player has said they are not.
            Difficulty d = Find.Storyteller?.difficulty;
            if (d == null)
            {
                return true;
            }
            return d.allowBigThreats && d.threatScale > 0.01f;
        }

        /// <summary>Which stage of a staged threshold table a value falls in, or -1 for none.</summary>
        public static int StageFor(float value, float[] thresholds)
        {
            int stage = -1;
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (value >= thresholds[i])
                {
                    stage = i;
                }
            }
            return stage;
        }
    }
}
