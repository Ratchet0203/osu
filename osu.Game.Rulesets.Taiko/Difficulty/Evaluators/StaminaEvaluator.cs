// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.Taiko.Difficulty.Evaluators
{
    public class StaminaEvaluator
    {
        /// <summary>
        /// Evaluates the minimum mechanical stamina required to play the current object.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            // The time in milliseconds between the current object and the last object played on this finger
            double timeSincePreviousSameFingerObject = GetTimeSincePreviousSameFingerObject(current);

            // Formula generating stamina difficulty based on this time, experiment with this
            return 0.25 + (2000 / timeSincePreviousSameFingerObject);
        }

        public static double GetTimeSincePreviousSameFingerObject(DifficultyHitObject current)
        {
            TaikoDifficultyHitObject taikoCurrent = (TaikoDifficultyHitObject)current;
            TaikoDifficultyHitObject? taikoPreviousOnSameFinger = taikoCurrent.PreviousMono(1);

            if (taikoPreviousOnSameFinger == null)
                return double.PositiveInfinity;

            // The time in milliseconds between the current object and the last object played on this finger, prevented from going below 1ms
            return Math.Max(taikoCurrent.StartTime -  taikoCurrent.PreviousMono(1).StartTime, 1.0);
        }
    }
}
