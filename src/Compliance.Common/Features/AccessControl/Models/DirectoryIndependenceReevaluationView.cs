using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>A denial-only receipt; it supplies no partner evidence or new professional authority.</summary>
public sealed record DirectoryIndependenceReevaluationView(Uuid ReevaluationId, Uuid TenantId,
    DirectoryStatusSourceView Source, string SourceDescriptorSha256, Uuid OriginalAcceptanceRequestId, string OriginalSourceSha256,
    ServiceEngagementAcceptanceView OriginalAcceptance, string OriginalAcceptanceSha256,
    ServiceEngagementAcceptanceView ObservedAcceptance, string ObservedAcceptanceSha256,
    string State, bool ProductionAcceptanceBlocked, ActorReference Actor, DateTimeOffset RecordedAt);
