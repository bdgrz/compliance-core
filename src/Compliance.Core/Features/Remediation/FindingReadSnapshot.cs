using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>A captured read-side finding inventory; this is never retained as aggregate command state.</summary>
public sealed class FindingReadSnapshot(
    IReadOnlyList<FindingView> findings,
    IReadOnlyDictionary<Uuid, IReadOnlyList<FindingClosureView>> closures)
{
    /// <summary>Every finding of the program, oldest first.</summary>
    public IReadOnlyList<FindingView> Findings { get; } = findings;

    /// <summary>True when the program has this finding.</summary>
    public bool Contains(Uuid findingId) => Findings.Any(finding => finding.FindingId == findingId);

    /// <summary>The finding, or null.</summary>
    public FindingView? Read(Uuid findingId) => Findings.FirstOrDefault(finding => finding.FindingId == findingId);

    /// <summary>Every closure the finding recorded, across reopened generations.</summary>
    public IReadOnlyList<FindingClosureView> ReadClosures(Uuid findingId) => closures.GetValueOrDefault(findingId) ?? [];
}
