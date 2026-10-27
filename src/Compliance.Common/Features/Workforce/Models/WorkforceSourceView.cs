using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>
///     An immutable source observation and decision. Accepted provenance binds Decision.TargetRevision;
///     only a single-record read checks whether it is still the current canonical revision.
/// </summary>
public sealed record WorkforceSourceView(Uuid TenantId, Uuid ObservationId, long Revision,
    WorkforceSourceIdentity Source, string TargetKind, Uuid TargetId, long ObservedTargetRevision,
    WorkforceSourceFacts Facts, DateTimeOffset ObservedAt, ActorReference RecordedBy,
    DateTimeOffset RecordedAt, WorkforceSourceDecision? Decision = null,
    bool RestrictedFieldsRedacted = false, long? CurrentTargetRevision = null,
    bool? AcceptedForCurrentRevision = null);
