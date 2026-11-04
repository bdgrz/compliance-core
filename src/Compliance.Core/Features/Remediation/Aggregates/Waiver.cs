using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Aggregates;

/// <summary>One independently reviewed, time-bounded exception to one product rule for exact records.</summary>
public sealed class Waiver : Aggregate
{
    public const string Area = "waivers";

    public Waiver(Uuid tenantId, Uuid waiverId)
        : base(waiverId, new EventStreamAddress(tenantId.ToString(), Area, waiverId.ToString()))
    {
    }

    public long Revision => 0;

    public Result<WaiverRegistration> Request(RequestWaiver request, ActorReference requester, DateTimeOffset now) =>
        Result<WaiverRegistration>.Failure(new RequestError(RequestErrorKind.Validation, "The product waiver is not implemented."));

    public Result Review(ReviewWaiver request, ActorReference reviewer, DateTimeOffset now) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, "The product waiver is not implemented."));

    public Result Revoke(RevokeWaiver request, ActorReference revokedBy, DateTimeOffset now) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, "The product waiver is not implemented."));

    public bool Covers(string rule, IReadOnlyList<WaiverScopeRecord> scope, DateTimeOffset at) => false;

    public WaiverView? ViewOrNull(DateTimeOffset now) => null;
}
