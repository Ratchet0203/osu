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
        private double skillMultiplier => 1.1;
        private double strainDecayBase => 0.4;

        private double currentStrain;

        private List<double> fingerStrains = new List<double> {0.0, 0.0, 0.0, 0.0};

        /// <summary>
        /// Creates a <see cref="Stamina"/> skill.
        /// </summary>
        public Stamina(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            if (current.BaseObject is not Hit)
                return 0.0;

            double staminaDifficulty = StaminaEvaluator.EvaluateDifficultyOf(current);

            // The location in the strains list of this object's finger
            int fingerIndex = (current as TaikoDifficultyHitObject).MonoIndex % 2 + (
                (current.BaseObject as Hit).Type == HitType.Centre 
                    ? 0 
                    : 2
                );

            double existingStrainOnFinger = fingerStrains[fingerIndex];

            // The existing strain value decayed based on the time since the finger's last input
            double decayedStrain = existingStrainOnFinger * strainDecay(
                StaminaEvaluator.GetTimeSincePreviousSameFingerObject(current)
                );

            // Formula generating a new strain value from the existing strain and new difficulty, experiment with this
            currentStrain = (decayedStrain + staminaDifficulty) / 2;

            fingerStrains[fingerIndex] = currentStrain;
            return currentStrain;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);
    }
}
