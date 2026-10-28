using RimWorld;
using Verse;

namespace Dread
{
    /*
        Every worker below does nothing but look its answer up, for the same reason Shame's do:
        a thought worker is asked its question from inside the game's own mood and opinion
        machinery, and the two figures this mod is built on -- the storyteller's threat points and
        the colony's wealth -- each walk every pawn and every item on the map to produce
        themselves. Doing that from a worker would be doing it many times a second.

        The measuring happens on a slow tick in GameComponent_Dread and is swapped in whole.
    */

    /// <summary>
    /// What is coming, against what we have to meet it with.
    ///
    /// Bloodlust is excluded here and picked up by DR_Sharpening instead. Psychopath is not
    /// excluded anywhere in this mod: fear for your own skin is not an empathy, and this is the
    /// one thing in the set a psychopath feels exactly like everybody else.
    /// </summary>
    public class ThoughtWorker_Dread : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!Tuning.Readiness || !DR.Counts(p))
            {
                return ThoughtState.Inactive;
            }
            if (p.story?.traits != null && p.story.traits.HasTrait(TraitDefOf.Bloodlust))
            {
                return ThoughtState.Inactive;
            }
            GameComponent_Dread watch = GameComponent_Dread.Instance;
            if (watch == null || !watch.Expected)
            {
                return ThoughtState.Inactive;
            }
            int stage = DR.StageFor(watch.FeltBy(p), Tuning.Stages);
            return stage < 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(stage);
        }
    }

    /// <summary>
    /// The quiet.
    ///
    /// Kept apart from the readiness thought because it is a different feeling and because it does
    /// something no mood in this game does: it gets worse while nothing is happening. It is also
    /// true, which is the part that makes it worth having. The storyteller really is winding up.
    /// </summary>
    public class ThoughtWorker_TooQuiet : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!Tuning.Quiet || !DR.Counts(p))
            {
                return ThoughtState.Inactive;
            }
            if (p.story?.traits != null && p.story.traits.HasTrait(TraitDefOf.Bloodlust))
            {
                return ThoughtState.Inactive;
            }
            GameComponent_Dread watch = GameComponent_Dread.Instance;
            if (watch == null || !watch.Expected)
            {
                return ThoughtState.Inactive;
            }
            float past = watch.QuietDays - Tuning.QuietDays;
            if (past < 0f)
            {
                return ThoughtState.Inactive;
            }
            int stage = DR.StageFor(past, Tuning.QuietStages);
            return stage < 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(stage);
        }
    }

    /// <summary>
    /// The other direction, and the reason this is not only a punishment.
    ///
    /// Only when there is genuinely something to be ready for. A colony on a peaceful run is not
    /// standing proud behind its walls, it is just a colony, and the feeling would be hollow.
    /// </summary>
    public class ThoughtWorker_Steady : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!Tuning.Steadying || !DR.Counts(p))
            {
                return ThoughtState.Inactive;
            }
            GameComponent_Dread watch = GameComponent_Dread.Instance;
            if (watch == null || !watch.Expected || watch.Pressure <= 0f)
            {
                return ThoughtState.Inactive;
            }
            return watch.FeltBy(p) < Tuning.SteadyBelow
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }

    /// <summary>Somebody who cannot wait. The trait itself is required in the def.</summary>
    public class ThoughtWorker_Sharpening : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!Tuning.Readiness || !DR.Counts(p))
            {
                return ThoughtState.Inactive;
            }
            GameComponent_Dread watch = GameComponent_Dread.Instance;
            if (watch == null || !watch.Expected)
            {
                return ThoughtState.Inactive;
            }
            // The same figure everybody else is frightened by. They just read it differently.
            return watch.FeltBy(p) >= Tuning.Stages[0]
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }

    /// <summary>
    /// The ones you are glad are here.
    ///
    /// The first thing in this set that raises an opinion rather than lowering one. It needs both
    /// halves to be true: there has to be something to be afraid of, and this particular person
    /// has to be a real part of the answer to it. Nobody is grateful for a good shot in peacetime.
    /// </summary>
    public class ThoughtWorker_StandsBetween : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (!Tuning.Relief || !DR.Counts(p) || other == null || p == other || !DR.Counts(other))
            {
                return ThoughtState.Inactive;
            }
            GameComponent_Dread watch = GameComponent_Dread.Instance;
            if (watch == null || !watch.Expected)
            {
                return ThoughtState.Inactive;
            }
            // Only somebody who is actually worried is grateful for the person holding the line.
            if (watch.FeltBy(p) < Tuning.Stages[0] * 0.5f)
            {
                return ThoughtState.Inactive;
            }
            int stage = DR.StageFor(watch.ShareOf(other), Tuning.StandsBetweenStages);
            return stage < 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(stage);
        }
    }

    /// <summary>Scaled by the settings slider, like every other opinion in the set.</summary>
    public class Thought_Relieved : Thought_SituationalSocial
    {
        public override float OpinionOffset() => base.OpinionOffset() * Tuning.Power;
    }
}
