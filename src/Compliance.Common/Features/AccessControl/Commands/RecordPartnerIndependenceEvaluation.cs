using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Admits the designated partner's personal independence evaluation against the exact engagement content
///     revision, the exact service epoch and the exact ratified rule version, and acknowledges it with a fixed 202.
///     <paramref name="EvaluationId" /> is the logical operation identity. The evaluation is materialized later, only
///     after the engagement owner authorizes the personal decision; it never approves production acceptance.
/// </summary>
[Discriminator("bdgrz.service-engagement.partner-evaluation.record", 1)]
public sealed record RecordPartnerIndependenceEvaluation(Uuid TenantId, Uuid EngagementId, Uuid EvaluationId,
    long ExpectedEngagementRevision, long RuleVersion, string ExpectedRuleContentDigest,
    string ExpectedServiceHistoryDigest, string Rationale, long ExpectedServiceEpoch)
    : IRequest<OperationAcceptedResponse>, IPersonalEngagementPartnerRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => EvaluationId;
}
