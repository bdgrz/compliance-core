using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.Aggregates.SystemDescription;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SystemDescription.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Projections;

/// <summary>Explicit source-verified read composition, never aggregate command state.</summary>
public sealed record SystemDescriptionReadSnapshot(SystemDescriptionAggregate Owner, SystemDescriptionRules Rules,
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
