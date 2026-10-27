using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

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
