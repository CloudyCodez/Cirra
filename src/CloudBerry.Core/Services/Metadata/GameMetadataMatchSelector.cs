using System.Text;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

public sealed class GameMetadataMatchSelector
{
    private const double MinimumAutoMatchScore = 42;
    private static readonly HashSet<string> EditionPenaltyTerms = new(StringComparer.Ordinal)
    {
        "demo",
        "dlc",
        "pack",
        "bundle",
        "soundtrack",
        "upgrade",
        "season",
        "pass",
        "artbook",
        "cosmetic",
        "skin",
        "remaster",
        "remastered",
        "definitive",
        "complete",
        "collection",
        "trilogy",
        "anthology",
        "anniversary",
        "ultimate",
        "enhanced",
        "director",
        "cut",
        "edition",
        "goty"
    };

    public GameMetadataRecord? SelectBestMatch(
        string title,
        string platform,
        IEnumerable<GameMetadataRecord> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = ScoreCandidate(title, platform, candidate)
            })
            .Where(entry => entry.Score >= MinimumAutoMatchScore)
            .OrderByDescending(entry => entry.Score)
            .ThenByDescending(entry => entry.Candidate.CommunityRating ?? 0)
            .ThenBy(entry => Math.Abs(entry.Candidate.Title.Length - title.Length))
            .Select(entry => entry.Candidate)
            .FirstOrDefault();
    }

    private static double ScoreCandidate(string title, string platform, GameMetadataRecord candidate)
    {
        var normalizedTitle = Normalize(title);
        var normalizedCandidateTitle = Normalize(candidate.Title);

        if (string.IsNullOrWhiteSpace(normalizedCandidateTitle))
        {
            return double.MinValue;
        }

        if (ShouldBlockEditionAutoMatch(normalizedTitle, normalizedCandidateTitle))
        {
            // Avoid base-title auto-matches to remasters or editions when the edition was not requested.
            return double.MinValue;
        }

        if (IsHighRiskEditionMismatch(normalizedTitle, normalizedCandidateTitle))
        {
            return double.MinValue;
        }

        var score = 0d;

        if (normalizedTitle.Equals(normalizedCandidateTitle, StringComparison.Ordinal))
        {
            score += 120;
        }
        else
        {
            if (normalizedCandidateTitle.StartsWith(normalizedTitle, StringComparison.Ordinal) ||
                normalizedTitle.StartsWith(normalizedCandidateTitle, StringComparison.Ordinal))
            {
                score += 45;
            }

            if (normalizedCandidateTitle.Contains(normalizedTitle, StringComparison.Ordinal) ||
                normalizedTitle.Contains(normalizedCandidateTitle, StringComparison.Ordinal))
            {
                score += 18;
            }

            score += CalculateTokenOverlapScore(normalizedTitle, normalizedCandidateTitle);
        }

        if (!string.IsNullOrWhiteSpace(platform) &&
            candidate.Platforms.Any(candidatePlatform => PlatformMatches(platform, candidatePlatform)))
        {
            score += 14;
        }

        if (candidate.CommunityRating is double rating)
        {
            score += Math.Clamp(rating, 0, 5);
        }

        score -= CalculateEditionPenalty(normalizedTitle, normalizedCandidateTitle);
        score -= Math.Abs(normalizedCandidateTitle.Length - normalizedTitle.Length) * 0.12;
        return score;
    }

    private static bool ShouldBlockEditionAutoMatch(string expectedTitle, string candidateTitle)
    {
        if (expectedTitle.Equals(candidateTitle, StringComparison.Ordinal))
        {
            return false;
        }

        var expectedTokens = Tokenize(expectedTitle);
        var candidateTokens = Tokenize(candidateTitle);
        var expectedHasEditionTerm = EditionPenaltyTerms.Any(expectedTokens.Contains);
        var candidateHasEditionTerm = EditionPenaltyTerms.Any(candidateTokens.Contains);
        return candidateHasEditionTerm && !expectedHasEditionTerm;
    }

    private static double CalculateEditionPenalty(string expectedTitle, string candidateTitle)
    {
        if (expectedTitle.Equals(candidateTitle, StringComparison.Ordinal))
        {
            return 0;
        }

        var candidateTokens = Tokenize(candidateTitle);
        var expectedTokens = Tokenize(expectedTitle);

        var penalty = EditionPenaltyTerms
            .Where(term => candidateTokens.Contains(term) && !expectedTokens.Contains(term))
            .Sum(_ => 55d);

        if (candidateTitle.Contains(" deluxe ", StringComparison.Ordinal) &&
            !expectedTitle.Contains(" deluxe ", StringComparison.Ordinal))
        {
            penalty += 30;
        }

        if (candidateTitle.Contains(" x ", StringComparison.Ordinal) &&
            !expectedTitle.Contains(" x ", StringComparison.Ordinal))
        {
            penalty += 70;
        }

        penalty += CalculateSeriesMismatchPenalty(expectedTokens, candidateTokens);

        return penalty;
    }
    private static bool IsHighRiskEditionMismatch(string expectedTitle, string candidateTitle)
    {
        if (expectedTitle.Equals(candidateTitle, StringComparison.Ordinal))
        {
            return false;
        }

        var expectedTokens = Tokenize(expectedTitle);
        var candidateTokens = Tokenize(candidateTitle);

        if (!candidateTokens.IsSupersetOf(expectedTokens))
        {
            return false;
        }

        return EditionPenaltyTerms.Any(term =>
            candidateTokens.Contains(term) &&
            !expectedTokens.Contains(term));
    }
    private static double CalculateSeriesMismatchPenalty(IReadOnlySet<string> expectedTokens, IReadOnlySet<string> candidateTokens)
    {
        var expectedSeriesMarker = ExtractSeriesMarker(expectedTokens);
        var candidateSeriesMarker = ExtractSeriesMarker(candidateTokens);

        if (string.IsNullOrWhiteSpace(expectedSeriesMarker) && string.IsNullOrWhiteSpace(candidateSeriesMarker))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(expectedSeriesMarker) || string.IsNullOrWhiteSpace(candidateSeriesMarker))
        {
            // Avoid sequel or spin-off matches when only one side contains a series marker.
            return 80;
        }

        return string.Equals(expectedSeriesMarker, candidateSeriesMarker, StringComparison.Ordinal) ? 0 : 45;
    }

    private static string ExtractSeriesMarker(IReadOnlySet<string> tokens)
    {
        foreach (var token in tokens)
        {
            if (IsSeriesMarker(token))
            {
                return token;
            }
        }

        return string.Empty;
    }

    private static bool IsSeriesMarker(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (int.TryParse(token, out var numericValue))
        {
            return numericValue is >= 2 and <= 10;
        }

        return token is "ii" or "iii" or "iv" or "v" or "vi" or "vii" or "viii" or "ix" or "x";
    }

    private static double CalculateTokenOverlapScore(string left, string right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);

        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();
        return union == 0 ? 0 : (intersection / (double)union) * 36;
    }

    private static bool PlatformMatches(string expectedPlatform, string candidatePlatform)
    {
        var normalizedExpected = Normalize(expectedPlatform);
        var normalizedCandidate = Normalize(candidatePlatform);

        if (string.IsNullOrWhiteSpace(normalizedExpected) || string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return false;
        }

        return normalizedExpected.Equals(normalizedCandidate, StringComparison.Ordinal) ||
               normalizedExpected.Contains(normalizedCandidate, StringComparison.Ordinal) ||
               normalizedCandidate.Contains(normalizedExpected, StringComparison.Ordinal) ||
               Tokenize(normalizedExpected).Intersect(Tokenize(normalizedCandidate), StringComparer.Ordinal).Any();
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
