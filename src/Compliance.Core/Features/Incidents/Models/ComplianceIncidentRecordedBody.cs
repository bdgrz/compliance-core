using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>Transient local rule delta, never a native aggregate fact.</summary>
public sealed record ComplianceIncidentRecordedBody(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId, ComplianceIncidentView State, DateTimeOffset OccurredAt);
