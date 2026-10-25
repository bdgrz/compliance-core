using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Explicit source-verified read composition, never aggregate command state.</summary>
public sealed record SystemDescriptionReadSnapshot(SystemDescription Owner, SystemDescriptionRules Rules,
    IReadOnlyList<DomainEvent> Facts)
{
    public SystemDescriptionCurrentAuthority? CurrentAuthority { get; init; }
    public ImmutableContentRef? PreparedSourceSnapshotContent { get; set; }
    public SystemDescriptionView View => Rules.Read();
    public Uuid Id => Owner.Id;
    public Uuid TenantId => Owner.TenantId;
    public Uuid ProgramId => Owner.ProgramId;
    public long Revision => Rules.Revision;
    public EventStreamAddress Stream => Owner.Stream;
    public SystemDescriptionVersionView? CurrentVersion => Rules.CurrentVersion;
    public IReadOnlyList<SystemDescriptionReviewView> Reviews => Rules.Reviews;
    public IReadOnlyList<SystemDescriptionNoChangeDecisionView> NoChangeDecisions => Rules.NoChangeDecisions;
    public SystemDescriptionView Read() => View;
    public SystemDescriptionVersionView? FindVersion(long version) => Rules.FindVersion(version);
    public SystemDescriptionVersionView? FindVersion(Uuid versionId) => Rules.FindVersion(versionId);
    public SystemDescriptionApprovalView? FindApproval(Uuid versionId) => Rules.FindApproval(versionId);
}
