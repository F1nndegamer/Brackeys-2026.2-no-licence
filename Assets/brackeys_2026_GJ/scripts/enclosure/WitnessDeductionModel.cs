using System;
using System.Collections.Generic;

public enum MonkeyPointDirection
{
    None,
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft
}

public readonly struct WitnessDeductionCue
{
    public int SpeakerIndex { get; }
    public int AccusedIndex { get; }
    public bool IsTruthful { get; }
    public bool IsShrug => AccusedIndex < 0;

    public WitnessDeductionCue(int speakerIndex, int accusedIndex, bool isTruthful)
    {
        SpeakerIndex = speakerIndex;
        AccusedIndex = accusedIndex;
        IsTruthful = isTruthful;
    }
}

public static class WitnessDeductionModel
{
    public const double UniqueLikelihoodRatio = 1.000001d;
    public const double PreferredLikelihoodRatio = 1.75d;

    private const int RandomAttemptCount = 64;
    private const double ScoreTieTolerance = 0.000001d;

    public static MonkeyPointDirection ResolvePointDirection(double x, double y)
    {
        if (x * x + y * y < 0.001d)
            return MonkeyPointDirection.None;

        double absoluteX = Math.Abs(x);
        double absoluteY = Math.Abs(y);
        const double straightDirectionRatio = 0.25d;

        if (absoluteY <= absoluteX * straightDirectionRatio)
            return x > 0d ? MonkeyPointDirection.Right : MonkeyPointDirection.Left;

        if (absoluteX <= absoluteY * straightDirectionRatio)
            return y > 0d ? MonkeyPointDirection.Up : MonkeyPointDirection.Down;

        if (x > 0d)
            return y > 0d ? MonkeyPointDirection.UpRight : MonkeyPointDirection.DownRight;

        return y > 0d ? MonkeyPointDirection.UpLeft : MonkeyPointDirection.DownLeft;
    }

    public static IReadOnlyList<WitnessDeductionCue> GenerateFairCues(
        Random random,
        IReadOnlyList<float> trustPercentages,
        int culpritIndex,
        bool allowShrugs,
        out double likelihoodRatio
    )
    {
        if (TryGenerateFairCues(
            random,
            trustPercentages,
            culpritIndex,
            allowShrugs,
            out IReadOnlyList<WitnessDeductionCue> cues,
            out likelihoodRatio
        ))
            return cues;

        throw new InvalidOperationException("No fair witness pattern could be generated.");
    }

    public static bool TryGenerateFairCues(
        Random random,
        IReadOnlyList<float> trustPercentages,
        int culpritIndex,
        bool allowShrugs,
        out IReadOnlyList<WitnessDeductionCue> cues,
        out double likelihoodRatio
    )
    {
        ValidateInputs(random, trustPercentages, culpritIndex);

        for (int attempt = 0; attempt < RandomAttemptCount; attempt++)
        {
            WitnessDeductionCue[] generatedCues = GenerateRandomCues(
                random,
                trustPercentages,
                culpritIndex,
                allowShrugs
            );

            if (IsUniquelySupported(
                trustPercentages,
                generatedCues,
                culpritIndex,
                PreferredLikelihoodRatio,
                out likelihoodRatio
            ))
            {
                cues = generatedCues;
                return true;
            }
        }

        WitnessDeductionCue[] fallback = FindStrongestValidCueSet(
            trustPercentages,
            culpritIndex,
            out likelihoodRatio
        );

        cues = fallback ?? Array.Empty<WitnessDeductionCue>();
        return fallback != null && IsUniquelySupported(
            trustPercentages,
            fallback,
            culpritIndex,
            UniqueLikelihoodRatio,
            out likelihoodRatio
        );
    }

    public static bool IsUniquelySupported(
        IReadOnlyList<float> trustPercentages,
        IReadOnlyList<WitnessDeductionCue> cues,
        int expectedCulpritIndex,
        double minimumLikelihoodRatio,
        out double likelihoodRatio
    )
    {
        int mostLikely = GetMostLikelyCandidate(
            trustPercentages,
            cues,
            out double bestScore,
            out double secondBestScore
        );

        double margin = bestScore - secondBestScore;
        likelihoodRatio = double.IsPositiveInfinity(margin)
            ? double.PositiveInfinity
            : Math.Exp(Math.Min(50d, margin));

        return mostLikely == expectedCulpritIndex &&
            likelihoodRatio + ScoreTieTolerance >= Math.Max(1d, minimumLikelihoodRatio);
    }

    public static int GetMostLikelyCandidate(
        IReadOnlyList<float> trustPercentages,
        IReadOnlyList<WitnessDeductionCue> cues,
        out double bestScore,
        out double secondBestScore
    )
    {
        if (trustPercentages == null || trustPercentages.Count < 3)
            throw new ArgumentException("At least three candidates are required.", nameof(trustPercentages));
        if (cues == null)
            throw new ArgumentNullException(nameof(cues));

        int bestCandidate = -1;
        bestScore = double.NegativeInfinity;
        secondBestScore = double.NegativeInfinity;

        for (int candidateIndex = 0; candidateIndex < trustPercentages.Count; candidateIndex++)
        {
            double score = ScoreCandidate(trustPercentages, cues, candidateIndex);

            if (score > bestScore + ScoreTieTolerance)
            {
                secondBestScore = bestScore;
                bestScore = score;
                bestCandidate = candidateIndex;
            }
            else if (Math.Abs(score - bestScore) <= ScoreTieTolerance)
            {
                secondBestScore = bestScore;
                bestCandidate = -1;
            }
            else if (score > secondBestScore)
            {
                secondBestScore = score;
            }
        }

        return bestCandidate;
    }

    public static double ScoreCandidate(
        IReadOnlyList<float> trustPercentages,
        IReadOnlyList<WitnessDeductionCue> cues,
        int candidateIndex
    )
    {
        if (candidateIndex < 0 || candidateIndex >= trustPercentages.Count)
            throw new ArgumentOutOfRangeException(nameof(candidateIndex));

        double score = 0d;

        for (int index = 0; index < cues.Count; index++)
        {
            WitnessDeductionCue cue = cues[index];
            if (cue.IsShrug)
                continue;

            double probability = CueProbability(
                trustPercentages,
                candidateIndex,
                cue.SpeakerIndex,
                cue.AccusedIndex
            );

            if (probability <= 0d)
                return double.NegativeInfinity;

            score += Math.Log(probability);
        }

        return score;
    }

    private static WitnessDeductionCue[] GenerateRandomCues(
        Random random,
        IReadOnlyList<float> trustPercentages,
        int culpritIndex,
        bool allowShrugs
    )
    {
        int candidateCount = trustPercentages.Count;
        WitnessDeductionCue[] cues = new WitnessDeductionCue[candidateCount];

        for (int speakerIndex = 0; speakerIndex < candidateCount; speakerIndex++)
        {
            if (allowShrugs && random.NextDouble() < 0.1d)
            {
                cues[speakerIndex] = new WitnessDeductionCue(speakerIndex, -1, false);
                continue;
            }

            double trust = ClampTrust(trustPercentages[speakerIndex]);
            bool truthful = speakerIndex != culpritIndex && random.NextDouble() < trust;
            int accusedIndex = truthful
                ? culpritIndex
                : SelectRandomLieTarget(random, candidateCount, speakerIndex, culpritIndex);
            cues[speakerIndex] = new WitnessDeductionCue(speakerIndex, accusedIndex, truthful);
        }

        return cues;
    }

    private static WitnessDeductionCue[] FindStrongestValidCueSet(
        IReadOnlyList<float> trustPercentages,
        int culpritIndex,
        out double likelihoodRatio
    )
    {
        int candidateCount = trustPercentages.Count;
        WitnessDeductionCue[] working = new WitnessDeductionCue[candidateCount];
        WitnessDeductionCue[] best = null;
        double bestMargin = double.NegativeInfinity;

        SearchCueSets(
            trustPercentages,
            culpritIndex,
            0,
            working,
            ref best,
            ref bestMargin
        );

        likelihoodRatio = best == null
            ? 0d
            : double.IsPositiveInfinity(bestMargin)
                ? double.PositiveInfinity
                : Math.Exp(Math.Min(50d, bestMargin));
        return best;
    }

    private static void SearchCueSets(
        IReadOnlyList<float> trustPercentages,
        int culpritIndex,
        int speakerIndex,
        WitnessDeductionCue[] working,
        ref WitnessDeductionCue[] best,
        ref double bestMargin
    )
    {
        int candidateCount = trustPercentages.Count;
        if (speakerIndex >= candidateCount)
        {
            int mostLikely = GetMostLikelyCandidate(
                trustPercentages,
                working,
                out double bestScore,
                out double secondBestScore
            );
            double margin = bestScore - secondBestScore;

            if (mostLikely == culpritIndex && margin > bestMargin + ScoreTieTolerance)
            {
                bestMargin = margin;
                best = (WitnessDeductionCue[])working.Clone();
            }

            return;
        }

        for (int accusedIndex = 0; accusedIndex < candidateCount; accusedIndex++)
        {
            if (accusedIndex == speakerIndex)
                continue;
            if (CueProbability(
                trustPercentages,
                culpritIndex,
                speakerIndex,
                accusedIndex
            ) <= 0d)
                continue;

            bool truthful = speakerIndex != culpritIndex && accusedIndex == culpritIndex;
            working[speakerIndex] = new WitnessDeductionCue(
                speakerIndex,
                accusedIndex,
                truthful
            );
            SearchCueSets(
                trustPercentages,
                culpritIndex,
                speakerIndex + 1,
                working,
                ref best,
                ref bestMargin
            );
        }
    }

    private static double CueProbability(
        IReadOnlyList<float> trustPercentages,
        int candidateCulpritIndex,
        int speakerIndex,
        int accusedIndex
    )
    {
        int candidateCount = trustPercentages.Count;
        if (speakerIndex < 0 || speakerIndex >= candidateCount ||
            accusedIndex < 0 || accusedIndex >= candidateCount ||
            accusedIndex == speakerIndex)
            return 0d;

        if (speakerIndex == candidateCulpritIndex)
            return accusedIndex == candidateCulpritIndex ? 0d : 1d / (candidateCount - 1d);

        double trust = ClampTrust(trustPercentages[speakerIndex]);
        if (accusedIndex == candidateCulpritIndex)
            return trust;

        return (1d - trust) / (candidateCount - 2d);
    }

    private static int SelectRandomLieTarget(
        Random random,
        int candidateCount,
        int speakerIndex,
        int culpritIndex
    )
    {
        int optionCount = speakerIndex == culpritIndex
            ? candidateCount - 1
            : candidateCount - 2;
        int selectedOption = random.Next(optionCount);

        for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            if (candidateIndex == speakerIndex || candidateIndex == culpritIndex)
                continue;
            if (selectedOption-- == 0)
                return candidateIndex;
        }

        for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            if (candidateIndex != speakerIndex)
                return candidateIndex;
        }

        throw new InvalidOperationException("No valid lie target exists.");
    }

    private static double ClampTrust(float percentage)
    {
        return Math.Max(0d, Math.Min(1d, percentage / 100d));
    }

    private static void ValidateInputs(
        Random random,
        IReadOnlyList<float> trustPercentages,
        int culpritIndex
    )
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        if (trustPercentages == null || trustPercentages.Count < 3)
            throw new ArgumentException("At least three candidates are required.", nameof(trustPercentages));
        if (trustPercentages.Count > 6)
            throw new ArgumentException("The lineup supports at most six candidates.", nameof(trustPercentages));
        if (culpritIndex < 0 || culpritIndex >= trustPercentages.Count)
            throw new ArgumentOutOfRangeException(nameof(culpritIndex));
    }
}
