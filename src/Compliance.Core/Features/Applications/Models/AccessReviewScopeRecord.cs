using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>The projected, ordered scope history of one system instance.</summary>
public sealed record AccessReviewScopeRecord(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, IReadOnlyList<AccessReviewScopeDecisionView> Decisions);
