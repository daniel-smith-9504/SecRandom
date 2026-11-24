using SecRandom.Core.Enums.Configs;
using SecRandom.Core.Services.Profiles;
using SecRandom.Shared.Models.Profile;

namespace SecRandom.Core.Services.Draw;

/// <summary>
///     Shared candidate filtering for every draw channel. Students: IsCandidate + group/gender scope +
///     temporary-record repeat threshold. Prizes: IsCandidate + Count-mode inventory or Pan-mode repeat
///     threshold. Repeat limits are always evaluated against temporary records, never persistent history.
/// </summary>
public static class DrawCandidateFilter
{
    public static bool MatchesScope(Student student, string groupScope, string genderScope, string tagScope = "")
    {
        return student.IsCandidate
               && (string.IsNullOrEmpty(groupScope) || student.Group == groupScope)
               && (string.IsNullOrEmpty(genderScope) || student.Gender == genderScope)
               && (string.IsNullOrWhiteSpace(tagScope) || HasTag(student, tagScope));
    }

    /// <summary>成员标签按导入时的同一套分隔符切分，命中任一标签即视为属于该标签范围。</summary>
    public static bool HasTag(Student student, string tag)
    {
        return !string.IsNullOrWhiteSpace(tag)
               && RosterImportParser.SplitTags(student.Tags).Contains(tag, StringComparer.Ordinal);
    }

    public static List<Student> FilterEligibleStudents(
        IEnumerable<Student> students,
        string groupScope,
        string genderScope,
        IReadOnlyDictionary<string, int> temporaryCounts,
        int repeatThreshold,
        Func<Student, bool>? additionalFilter = null,
        string tagScope = "")
    {
        return students
            .Where(student => MatchesScope(student, groupScope, genderScope, tagScope))
            .Where(student => additionalFilter is null || additionalFilter(student))
            .Where(student => !DrawRepeatPolicy.HasReachedLimit(
                temporaryCounts.GetValueOrDefault(ProfileRecordIdentity.EnsureRecordId(student)),
                repeatThreshold))
            .ToList();
    }

    public static List<Prize> FilterEligiblePrizes(
        IEnumerable<Prize> prizes,
        IReadOnlyDictionary<string, int> temporaryCounts,
        LotteryDrawType drawType,
        int repeatThreshold,
        Func<Prize, bool>? additionalFilter = null)
    {
        var candidates = prizes.Where(prize => prize.IsCandidate);
        if (additionalFilter is not null)
            candidates = candidates.Where(additionalFilter);

        if (drawType == LotteryDrawType.Count)
        {
            return candidates
                .Where(prize => prize.Count - temporaryCounts.GetValueOrDefault(ProfileRecordIdentity.EnsureRecordId(prize)) > 0)
                .ToList();
        }

        return candidates
            .Where(prize => !DrawRepeatPolicy.HasReachedLimit(
                temporaryCounts.GetValueOrDefault(ProfileRecordIdentity.EnsureRecordId(prize)),
                repeatThreshold))
            .ToList();
    }
}
