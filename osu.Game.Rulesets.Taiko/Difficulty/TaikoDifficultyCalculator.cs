// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing.Colour;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing.Rhythm;
using osu.Game.Rulesets.Taiko.Difficulty.Skills;
using osu.Game.Rulesets.Taiko.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Taiko.Difficulty
{
    public class TaikoDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.084375;
        private const double rhythm_skill_multiplier = 0.770 * difficulty_multiplier;
        private const double reading_skill_multiplier = 0.100 * difficulty_multiplier;
        private const double colour_skill_multiplier = 0.375 * difficulty_multiplier;
        private const double stamina_skill_multiplier = 0.445 * difficulty_multiplier;

        private double patternMultiplier;

        private bool isRelax;

        public override int Version => 20260706;

        public TaikoDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            isRelax = mods.Any(h => h is TaikoModRelax);

            return new Skill[]
            {
                new Rhythm(mods),
                new Reading(mods),
                new Colour(mods),
                new Stamina(mods),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new TaikoModDoubleTime(),
            new TaikoModHalfTime(),
            new TaikoModEasy(),
            new TaikoModHardRock(),
        };

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            var difficultyHitObjects = new List<DifficultyHitObject>();
            var centreObjects = new List<TaikoDifficultyHitObject>();
            var rimObjects = new List<TaikoDifficultyHitObject>();
            var noteObjects = new List<TaikoDifficultyHitObject>();

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            // Generate TaikoDifficultyHitObjects from the beatmap's hit objects.
            for (int i = 2; i < beatmap.HitObjects.Count; i++)
            {
                difficultyHitObjects.Add(new TaikoDifficultyHitObject(
                    beatmap.HitObjects[i],
                    beatmap.HitObjects[i - 1],
                    clockRate,
                    difficultyHitObjects,
                    centreObjects,
                    rimObjects,
                    noteObjects,
                    difficultyHitObjects.Count,
                    beatmap.ControlPointInfo,
                    beatmap.Difficulty.SliderMultiplier
                ));
            }

            TaikoColourDifficultyPreprocessor.ProcessAndAssign(difficultyHitObjects);
            TaikoRhythmDifficultyPreprocessor.ProcessAndAssign(noteObjects);

            return difficultyHitObjects;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new TaikoDifficultyAttributes { Mods = mods };

            var rhythm = skills.OfType<Rhythm>().Single();
            var reading = skills.OfType<Reading>().Single();
            var colour = skills.OfType<Colour>().Single();
            var stamina = skills.OfType<Stamina>().Single();

            double rhythmSkill = rhythm.DifficultyValue() * rhythm_skill_multiplier;
            double readingSkill = reading.DifficultyValue() * reading_skill_multiplier;
            double colourSkill = colour.DifficultyValue() * colour_skill_multiplier;
            double staminaSkill = stamina.DifficultyValue() * stamina_skill_multiplier;

            // As we don't have pattern integration in osu!taiko, we apply the other two skills relative to rhythm.
            patternMultiplier = DiffUtils.Pow(staminaSkill * colourSkill, 0.10);

            double combinedRating = combinedDifficultyValue(rhythm, reading, colour, stamina, out double consistencyFactor);
            double starRating = rescale(combinedRating * 1.4);

            // Calculate proportional contribution of each skill to the combinedRating.
            double skillRating = starRating / (rhythmSkill + readingSkill + colourSkill + staminaSkill);

            double rhythmDifficulty = rhythmSkill * skillRating;
            double readingDifficulty = readingSkill * skillRating;
            double colourDifficulty = colourSkill * skillRating;
            double staminaDifficulty = staminaSkill * skillRating;

            TaikoDifficultyAttributes attributes = new TaikoDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                RhythmDifficulty = rhythmDifficulty,
                ReadingDifficulty = readingDifficulty,
                ColourDifficulty = colourDifficulty,
                StaminaDifficulty = staminaDifficulty,
                ConsistencyFactor = consistencyFactor,
                MaxCombo = beatmap.GetMaxCombo(),
            };

            return attributes;
        }

        /// <summary>
        /// Returns the combined star rating of the beatmap, calculated using peak strains from all sections of the map.
        /// </summary>
        /// <remarks>
        /// For each section, the peak strains of all separate skills are combined into a single peak strain for the section.
        /// The resulting partial rating of the beatmap is a weighted sum of the combined peaks (higher peaks are weighted more).
        /// </remarks>
        private double combinedDifficultyValue(Rhythm rhythm, Reading reading, Colour colour, Stamina stamina, out double consistencyFactor)
        {
            List<double> peaks = combinePeaks(
                rhythm.GetCurrentStrainPeaks().ToList(),
                reading.GetCurrentStrainPeaks().ToList(),
                colour.GetCurrentStrainPeaks().ToList(),
                stamina.GetCurrentStrainPeaks().ToList()
            );

            if (peaks.Count == 0)
            {
                consistencyFactor = 0;
                return 0;
            }

            double difficulty = 0;
            double weight = 1;

            foreach (double strain in peaks.OrderDescending())
            {
                difficulty += strain * weight;
                weight *= 0.9;
            }

            List<double> hitObjectStrainPeaks = combinePeaks(
                rhythm.GetObjectDifficulties(),
                reading.GetObjectDifficulties(),
                colour.GetObjectDifficulties(),
                stamina.GetObjectDifficulties()
            );

            if (hitObjectStrainPeaks.Count == 0)
            {
                consistencyFactor = 0;
                return 0;
            }

            // The average of the top 5% of strain peaks from hit objects.
            double topAverageHitObjectStrain = hitObjectStrainPeaks.OrderDescending().Take(1 + hitObjectStrainPeaks.Count / 20).Average();

            // Calculates a consistency factor as the sum of difficulty from hit objects compared to if every object were as hard as the hardest.
            // The top average strain is used instead of the very hardest to prevent exceptionally hard objects lowering the factor.
            consistencyFactor = hitObjectStrainPeaks.Sum() / (topAverageHitObjectStrain * hitObjectStrainPeaks.Count);

            return difficulty;
        }

        /// <summary>
        /// Combines lists of peak strains from multiple skills into a list of single peak strains for each section.
        /// </summary>
        private List<double> combinePeaks(IReadOnlyList<double> rhythmPeaks, IReadOnlyList<double> readingPeaks, IReadOnlyList<double> colourPeaks, IReadOnlyList<double> staminaPeaks)
        {
            var combinedPeaks = new List<double>();

            for (int i = 0; i < colourPeaks.Count; i++)
            {
                double rhythmPeak = rhythmPeaks[i] * rhythm_skill_multiplier * patternMultiplier;
                double readingPeak = readingPeaks[i] * reading_skill_multiplier;
                double colourPeak = isRelax ? 0 : colourPeaks[i] * colour_skill_multiplier; // There is no colour difficulty in relax.
                double staminaPeak = staminaPeaks[i] * stamina_skill_multiplier;
                // Below code nerfs obscenely fast maps the more repetitive their patterns are, with the effect being stronger the faster the map gets over a threshold.
                // This is intended as a nerf to maps where TL/raketapping are most viable. Usually converts, but we want to avoid applying a nerf to converts specifically.
                double staminaPeakThreshold = 0.95;
                if (staminaPeak > staminaPeakThreshold)
                    staminaPeak = staminaPeakThreshold + ((staminaPeak - staminaPeakThreshold) * Math.Pow(colourPeak / staminaPeak, 1));

                double peak = DifficultyCalculationUtils.Norm(2, DifficultyCalculationUtils.Norm(1.5, staminaPeak), rhythmPeak, readingPeak);

                // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
                // These sections will not contribute to the difficulty.
                if (peak > 0)
                    combinedPeaks.Add(peak);
            }

            return combinedPeaks;
        }

        /// <summary>
        /// Applies a final re-scaling of the star rating.
        /// </summary>
        /// <param name="sr">The raw star rating value before re-scaling.</param>
        private static double rescale(double sr)
        {
            if (sr < 0)
                return sr;

            return 10.43 * Math.Log(sr / 8 + 1);
        }
    }
}