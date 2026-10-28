using UnityEngine;
using Verse;

namespace Dread
{
    public class DreadSettings : ModSettings
    {
        public float power = 1f;
        public float readinessWorth = 8f;
        public float quietDays = 14f;

        public bool readiness = true;
        public bool quiet = true;
        public bool steadying = true;
        public bool relief = true;

        public override void ExposeData()
        {
            DreadSettings d = new DreadSettings();
            Scribe_Values.Look(ref power, "power", d.power);
            Scribe_Values.Look(ref readinessWorth, "readinessWorth", d.readinessWorth);
            Scribe_Values.Look(ref quietDays, "quietDays", d.quietDays);
            Scribe_Values.Look(ref readiness, "readiness", true);
            Scribe_Values.Look(ref quiet, "quiet", true);
            Scribe_Values.Look(ref steadying, "steadying", true);
            Scribe_Values.Look(ref relief, "relief", true);
        }

        public void Reset()
        {
            DreadSettings d = new DreadSettings();
            power = d.power;
            readinessWorth = d.readinessWorth;
            quietDays = d.quietDays;
            readiness = quiet = steadying = relief = true;
        }
    }

    public class DreadMod : Mod
    {
        public static DreadSettings Settings = new DreadSettings();

        public DreadMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DreadSettings>();
        }

        public override string SettingsCategory() => "Dread";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            DreadSettings s = Settings;
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("How hard it lands");
            s.power = list.SliderLabeled($"What is coming weighs {s.power:P0}", s.power, 0.25f, 2f);
            list.Gap(4f);
            s.readinessWorth = Mathf.Round(list.SliderLabeled(
                $"One colonist's worth of readiness answers {s.readinessWorth:0} threat points",
                s.readinessWorth, 3f, 30f));
            list.Gap(4f);
            s.quietDays = Mathf.Round(list.SliderLabeled(
                $"The quiet starts to bother them after {s.quietDays:0} days",
                s.quietDays, 4f, 60f));

            list.Gap(12f);
            list.Label("What they feel");
            list.CheckboxLabeled("What is coming", ref s.readiness,
                "The weight of the next raid against what this colony has to meet it with. Armed, armoured and behind turrets is a different feeling from none of those.");
            list.CheckboxLabeled("The quiet", ref s.quiet,
                "It has been a long time since anything happened. Something is owed.");
            list.CheckboxLabeled("We can hold this", ref s.steadying,
                "The other direction. A colonist who is genuinely well prepared feels better for it, which is the one thing in this set that pays you back for doing the sensible thing.");
            list.CheckboxLabeled("Glad they are here", ref s.relief,
                "Colonists think better of the people who can actually fight, while there is something to fight. Turn this off and the mod only ever takes mood away.");

            list.Gap(12f);
            list.Label("The figure this reads for \"what is coming\" is the storyteller's own: the " +
                       "number it would spend on a raid against you right now. It rises with the " +
                       "wealth of the colony, which is the base game's rule and not this mod's.\n\n" +
                       "Nothing here reads a colonist's mood or opinion, so it costs nothing to run " +
                       "and cannot argue with anything else you have installed.\n\n" +
                       "A psychopath feels this exactly like everybody else. Fear for your own skin " +
                       "is not an empathy.");

            list.Gap(12f);
            if (list.ButtonText("Reset to defaults"))
            {
                s.Reset();
            }

            list.End();
        }
    }
}
