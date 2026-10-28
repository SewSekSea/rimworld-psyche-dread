using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dread
{
    /// <summary>What one colonist is bringing to it.</summary>
    public class Worth
    {
        public float readiness;   // what they are worth in a fight, to the colony
        public float felt;        // what they personally feel about what is coming
    }

    /// <summary>
    /// The colony measured against what is coming for it.
    ///
    /// Everything expensive happens here, on a slow tick, and the answer is swapped in whole. The
    /// thought workers do nothing but look their answer up. That is not tidiness: a worker is
    /// asked its question from inside the game's opinion and mood machinery, and the two figures
    /// this needs -- the storyteller's own threat points and the colony's wealth -- walk every
    /// pawn and every item on the map to get themselves. Neither belongs anywhere near a thought.
    ///
    /// Nothing here is saved. It is all derived from what the game already tracks, so it rebuilds
    /// itself within one pass of a save being loaded and there is nothing to migrate.
    /// </summary>
    public class GameComponent_Dread : GameComponent
    {
        private static GameComponent_Dread instance;

        /// <summary>The last COMPLETED measurement. Replaced whole, never edited in place.</summary>
        private Dictionary<Pawn, Worth> table = new Dictionary<Pawn, Worth>();

        private float pressure;      // threat points the storyteller would spend on us now
        private float readiness;     // what this colony can answer with, in the same currency
        private float ratio;         // pressure / readiness, capped
        private float quietDays;     // how long since the last big threat
        private bool expected;       // is the storyteller sending anything at all

        public GameComponent_Dread(Game game)
        {
            instance = this;
        }

        public static GameComponent_Dread Instance =>
            instance ?? Current.Game?.GetComponent<GameComponent_Dread>();

        public override void FinalizeInit()
        {
            instance = this;
            Measure();
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % Tuning.CheckInterval != 0)
            {
                return;
            }
            Measure();
        }

        // ---------- reading ----------

        public float Pressure => pressure;
        public float Readiness => readiness;
        public float Ratio => ratio;
        public float QuietDays => quietDays;
        public bool Expected => expected;

        /// <summary>What this colonist feels about it. Zero for anyone not measured yet.</summary>
        public float FeltBy(Pawn p)
        {
            if (p == null)
            {
                return 0f;
            }
            return table.TryGetValue(p, out Worth w) ? w.felt : 0f;
        }

        /// <summary>How much of the colony's ability to answer is this one person.</summary>
        public float ShareOf(Pawn p)
        {
            if (p == null || readiness <= 0f)
            {
                return 0f;
            }
            return table.TryGetValue(p, out Worth w) ? w.readiness / readiness : 0f;
        }

        // ---------- the measurement ----------

        /// <summary>Exposed so the tests can force a pass without waiting on the tick.</summary>
        public void Measure()
        {
            expected = DR.ThreatsExpected();

            List<Map> maps = Find.Maps;
            if (maps == null || maps.Count == 0)
            {
                table = new Dictionary<Pawn, Worth>();
                pressure = readiness = ratio = quietDays = 0f;
                return;
            }

            // Built into a fresh table and swapped in at the end. Nothing reads a half-written one,
            // which is the rule three of the four bugs in the September review came down to.
            Dictionary<Pawn, Worth> fresh = new Dictionary<Pawn, Worth>();

            float points = 0f;
            float able = 0f;
            float defences = 0f;
            float longestQuiet = 0f;

            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome)
                {
                    continue;
                }

                // The storyteller's own number for how hard the next one would be. There is no
                // better source for "what is coming" than the thing that decides it.
                points += StorytellerUtility.DefaultThreatPointsNow(map);

                float since = (Find.TickManager.TicksGame - map.StoryState.LastThreatBigTick)
                              / (float)GenDate.TicksPerDay;
                if (since > longestQuiet)
                {
                    longestQuiet = since;
                }

                defences += DefencesOf(map);

                // A copy. FreeColonistsSpawned would have been the obvious property and it is the
                // wrong one: it refills a single shared list on every read, so touching it can
                // break a loop some other mod is in the middle of. AllPawnsSpawned is the real
                // backing list and DR.Counts does the filtering.
                List<Pawn> here = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                for (int i = 0; i < here.Count; i++)
                {
                    Pawn p = here[i];
                    defences += MechReadinessOf(p);
                    if (!DR.Counts(p))
                    {
                        continue;
                    }
                    Worth w = new Worth();
                    w.readiness = DR.AbleToStand(p) ? ReadinessOf(p) : 0f;
                    able += w.readiness;
                    fresh[p] = w;
                }
            }

            pressure = points;
            readiness = able + defences;
            quietDays = longestQuiet;

            float answerable = readiness * Tuning.ReadinessWorth;
            if (!expected || points <= 0f)
            {
                ratio = 0f;
            }
            else if (answerable <= 0f)
            {
                // Nobody left standing and nothing built. There is no ratio to take, only the top.
                ratio = Tuning.MaxRatio;
            }
            else
            {
                ratio = Mathf.Min(points / answerable, Tuning.MaxRatio);
            }

            // Second pass: what each of them personally makes of it. Deliberately after the colony
            // figures are settled, so every colonist is judged against the same finished picture
            // rather than against however much of it had been added up when their turn came.
            float average = fresh.Count > 0 ? able / fresh.Count : 0f;
            foreach (KeyValuePair<Pawn, Worth> pair in fresh)
            {
                pair.Value.felt = ratio * PersonalFactor(pair.Key, pair.Value, average) * Tuning.Power;
            }

            table = fresh;
        }

        /// <summary>
        /// What this colonist is worth if it comes to it.
        ///
        /// Not a combat simulation and not trying to be. It is four things a colonist could see
        /// about themselves: am I armed, am I armoured, am I any good, am I standing up.
        /// </summary>
        internal static float MechReadinessOf(Pawn p)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed || p.InMentalState ||
                p.Faction != Faction.OfPlayer || !p.RaceProps.IsMechanoid ||
                p.kindDef == null || !p.kindDef.isFighter || p.kindDef.combatPower <= 0f ||
                p.IsSelfShutdown() || p.IsDeactivated() ||
                (p.canBeDormant != null && !p.canBeDormant.Awake) ||
                (p.needs?.energy != null && p.needs.energy.CurLevel <= 0f))
            {
                return 0f;
            }
            return Mathf.Clamp(p.kindDef.combatPower / 100f, 0.5f, 8f)
                * Mathf.Clamp01(p.health.summaryHealth.SummaryHealthPercent);
        }

        private static float ReadinessOf(Pawn p)
        {
            float w = 1f;

            ThingWithComps weapon = p.equipment?.Primary;
            if (weapon != null)
            {
                w += weapon.def.IsRangedWeapon ? 0.9f : 0.4f;
                w += Mathf.Min(weapon.MarketValue / 1500f, 0.6f);
            }

            w += Mathf.Min(p.GetStatValue(StatDefOf.ArmorRating_Sharp) * 1.2f, 0.9f);

            if (p.skills != null)
            {
                int shoot = p.skills.GetSkill(SkillDefOf.Shooting).Level;
                int melee = p.skills.GetSkill(SkillDefOf.Melee).Level;
                w += Mathf.Max(shoot, melee) / 20f;
            }

            // Somebody who has actually done this before is worth more than their kit says.
            w *= Bridges.SteadinessFactor(p);
            return w;
        }

        /// <summary>
        /// Walls are not counted and that is deliberate. Every colony is inside a building and
        /// counting them would just be a second wealth figure. What counts is the things that
        /// exist only to shoot at somebody.
        /// </summary>
        private static float DefencesOf(Map map)
        {
            float d = 0f;
            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            if (things == null)
            {
                return 0f;
            }
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].Faction != Faction.OfPlayer)
                {
                    continue;
                }
                if (things[i] is Building_TurretGun turret)
                {
                    // A turret nobody is manning and that cannot fire is scenery.
                    d += turret.Active ? 2.2f : 0.3f;
                }
                else if (things[i] is Building_Trap)
                {
                    d += 0.12f;
                }
            }
            return Mathf.Min(d, 24f);
        }

        /// <summary>
        /// Why two colonists in the same colony on the same day do not feel the same about it.
        ///
        /// Nerve, temperament, whether they are the one who always gets sent, and whether they
        /// have been through it before. None of it reads an opinion.
        /// </summary>
        private static float PersonalFactor(Pawn p, Worth mine, float colonyAverage)
        {
            float f = 1f;

            if (p.story?.traits != null)
            {
                if (DR.Nerves != null)
                {
                    Trait n = p.story.traits.GetTrait(DR.Nerves);
                    if (n != null)
                    {
                        // iron-willed 2, steadfast 1, nervous -1, volatile -2
                        f *= 1f - (n.Degree * 0.18f);
                    }
                }
                if (p.story.traits.HasTrait(TraitDefOf.Wimp))
                {
                    f *= 1.35f;
                }
            }

            // Being the one who is always sent out. Resentment already works out who that is and
            // there is no reason to count it twice.
            f *= Bridges.CarriesTheRiskFactor(p);

            // What they are personally holding, against what everybody else is holding. Being the
            // only unarmed person in an armed colony is its own particular feeling, and it is not
            // the same feeling as everybody being unarmed together.
            //
            // The average is handed in from the pass that is running, not read back off the
            // component. The component still holds the PREVIOUS pass at this point, on purpose,
            // and mixing this pass's colony total with last pass's head count would quietly
            // produce an average belonging to neither.
            if (colonyAverage > 0f && mine != null)
            {
                f *= Mathf.Clamp(1.35f - (mine.readiness / colonyAverage) * 0.35f, 0.7f, 1.4f);
            }

            return Mathf.Clamp(f, 0.3f, 2.2f);
        }
    }
}
