// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Taiko.Difficulty.Evaluators;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.Taiko.Difficulty.Skills
{
    /// <summary>
    /// Calculates the stamina coefficient of taiko difficulty.
    /// </summary>
    public class Stamina : StrainSkill
    {
        private List<double> fingerStrains = new List<double> { 0.0, 0.0, 0.0, 0.0 };
        private double currentStrain;

        /// <summary>
        /// Creates a <see cref="Stamina"/> skill.
        /// </summary>
        /// <param name="mods">Mods for use in skill calculations.</param>
        public Stamina(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double decayBase, double ms) => Math.Pow(decayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            if (current.BaseObject is not Hit)
            {
                return currentStrain;
            }

            // The location in the strains list of this object's finger.
            // Each color has two fingers and they always alternate.
            // Their layout does not matter.
            // Two fingers per color applies to all playstyles, so none are assumed here.
            int fingerIndex = (current as TaikoDifficultyHitObject).MonoIndex % 2 + (
            (current.BaseObject as Hit).Type == HitType.Centre
                ? 0
                : 2
            );

            // Strain values decay over time.
            // WIP: 0.4 will be a dynamic value in the future!
            currentStrain *= strainDecay(0.4, current.DeltaTime);
            for (int i = 0; i < 4; i++)
                fingerStrains[i] *= strainDecay(0.1, current.DeltaTime);

            double staminaDifficulty = StaminaEvaluator.EvaluateDifficultyOf(current);

            fingerStrains[fingerIndex] += staminaDifficulty;

            // WIP: The amount added to currentStrain will likely be weighted in some way!
            currentStrain += fingerStrains[fingerIndex];

            return currentStrain;
        }

        // Note for reviewers: This function is unneeded, but line 18 gives me a CS0534 error when I try to remove it. Help.
        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) =>
        currentStrain * strainDecay(0.4, time - current.Previous(0).StartTime);
    }
}