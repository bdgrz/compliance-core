using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>A captured read-side finding inventory; this is never retained as aggregate command state.</summary>
public sealed class FindingReadSnapshot(
    IReadOnlyList<FindingView> findings,
    IReadOnlyDictionary<Uuid, IReadOnlyList<FindingClosureView>> closures,
    bool usesFindingStreams, IReadOnlyList<DomainEventRecord>? sourceRecords = null)
{
    public bool UsesFindingStreams { get; } = usesFindingStreams;
    public IReadOnlyList<FindingView> Findings { get; } = findings;
    public IReadOnlyList<DomainEventRecord> SourceRecords { get; } = sourceRecords ?? [];
    public bool Contains(Uuid findingId) => Findings.Any(finding => finding.FindingId == findingId);
    public FindingView? Read(Uuid findingId) => Findings.FirstOrDefault(finding => finding.FindingId == findingId);
    public IReadOnlyList<FindingClosureView> ReadClosures(Uuid findingId) => closures.GetValueOrDefault(findingId) ?? [];
}
