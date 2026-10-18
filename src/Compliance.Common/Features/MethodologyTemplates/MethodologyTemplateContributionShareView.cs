using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Reviewed content safe to transfer into platform methodology; omits tenant and source-record identities.</summary>
public sealed record MethodologyTemplateContributionShareView(
    Uuid AcceptanceId,
    MethodologyTemplateRecordContent Content,
    string ContentSha256,
    MethodologyTemplateReviewView Review,
    Uuid AcceptedByMemberId,
    string AcceptedByDisplay,
    string AcceptanceRationale,
    DateTimeOffset AcceptedAt);
