using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Queries;

/// <summary>
///     A personal acknowledgement of the exact version, hash, and text shown. A member
///     acknowledges for themselves; a program manager may record it for a non-member with
///     separate attribution. HTTP-only: it is deliberately not an MCP tool. The campaign admits the complete
///     result first (<paramref name="ExpectedCampaignRevision" /> is the campaign's logical revision); the
///     participant applies it afterwards, so the response is a fixed 202 acknowledgment.
/// </summary>
[Discriminator("bdgrz.policy.acknowledge", 2)]
public sealed record AcknowledgePolicy(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid PersonId, long PolicyVersion, string ContentSha256, string AcknowledgementText,
    long ExpectedCampaignRevision)
    : IRequest<OperationAcceptedResponse>, IProgramReadRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
