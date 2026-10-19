namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

static class AdvisorHandoffSelectionComparison
{
    public static AdvisorHandoffSourceChange Classify(AdvisorRecordVersion selected,
        IReadOnlyList<AdvisorRecordVersion> previous)
    {
        var match = previous.FirstOrDefault(item => SameLogicalSource(item, selected));
        return match is null ? AdvisorHandoffSourceChange.New :
            match == selected ? AdvisorHandoffSourceChange.Unchanged : AdvisorHandoffSourceChange.Changed;
    }

    public static bool SameLogicalSource(AdvisorRecordVersion left, AdvisorRecordVersion right) =>
        left.SourceFamily == right.SourceFamily && left.RecordId == right.RecordId &&
        left.EntryId == right.EntryId;

    public static IReadOnlyList<AdvisorRecordVersion> Removed(
        IReadOnlyList<AdvisorRecordVersion> previous, IReadOnlyList<AdvisorRecordVersion> current) =>
        previous.Where(candidate => !current.Any(selected => SameLogicalSource(candidate, selected)))
            .ToArray();
}
