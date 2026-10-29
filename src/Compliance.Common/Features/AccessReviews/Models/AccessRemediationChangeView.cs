using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>A provider-side change reported for an item; it is not verification.</summary>
public sealed record AccessRemediationChangeView(Uuid ChangeId, string Reference,
    string Description, DateTimeOffset ChangedAt, ActorReference RecordedBy,
    DateTimeOffset RecordedAt);
