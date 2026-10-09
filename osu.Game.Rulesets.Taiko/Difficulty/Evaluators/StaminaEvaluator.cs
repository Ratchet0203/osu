// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.Taiko.Difficulty.Evaluators
{
    public static class StaminaEvaluator
    {
        /// <summary>
        /// Evaluates how difficult this note is from a mostly mechanical perspective.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            // Don't reward anything for drumrolls (sliders) or swells (spinners).
            if (current.BaseObject is not Hit)
            {
                return 0.0;
            }

            TaikoDifficultyHitObject taikoCurrent = (TaikoDifficultyHitObject)current;
            TaikoDifficultyHitObject taikoPrevious = taikoCurrent.PreviousNote(0);

            // Strain is based on time since last object.
            // Placeholder values. Formula will be more fleshed out later.
            double objectStrain = 0;

            if (taikoPrevious != null)
                objectStrain = 1 / current.DeltaTime;

            return objectStrain;
        }
    }
}