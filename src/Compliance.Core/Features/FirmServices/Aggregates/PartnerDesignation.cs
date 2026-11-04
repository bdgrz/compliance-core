using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Events;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Aggregates;

/// <summary>One UserId-owned partner duty, retaining every reasoned operator decision.</summary>
public sealed class PartnerDesignation : Aggregate
{
    public const string Area = "firm-partner-designation";
    readonly Uuid _userId;
    readonly string _duty;
    readonly Uuid _selectedCommandId;
    readonly HashSet<Uuid> _commands = [];
    FirmServiceReceipt? _selectedReceipt;
    PartnerDesignationView? _selectedView;
    public PartnerDesignationView? View { get; private set; }
    public long Revision => View?.Revision ?? 0;
    public bool IsDesignated => View?.IsDesignated == true;

    public PartnerDesignation(Uuid userId, string duty, Uuid selectedCommandId = default)
        : base(AggregateIdentity.Create(nameof(PartnerDesignation), userId, duty),
            new EventStreamAddress("platform", Area, AggregateIdentity.Create(nameof(PartnerDesignation), userId, duty).ToString()))
    {
        _userId = userId;
        _duty = duty;
        _selectedCommandId = selectedCommandId;
        On<PartnerDesignated>(fact => Apply(fact.UserId, fact.Duty, fact.Revision, true, fact.Reason, fact.Receipt));
        On<PartnerDesignationRevoked>(fact => Apply(fact.UserId, fact.Duty, fact.Revision, false, fact.Reason, fact.Receipt));
    }

    public Result<PartnerDesignationView> Designate(DesignatePartner request, Uuid actorUserId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Change(request.UserId, request.Duty, request.CommandId, request.ExpectedRevision, request.Reason,
            AuthoredIntentDigest.Compute(request), actorUserId, at, true);
    }

    public Result<PartnerDesignationView> Revoke(RevokePartnerDesignation request, Uuid actorUserId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Change(request.UserId, request.Duty, request.CommandId, request.ExpectedRevision, request.Reason,
            AuthoredIntentDigest.Compute(request), actorUserId, at, false);
    }

    Result<PartnerDesignationView> Change(Uuid userId, string duty, Uuid commandId, long expectedRevision,
        string reason, string digest, Uuid actorUserId, DateTimeOffset at, bool designated)
    {
        if (_selectedReceipt is { } receipt && receipt.CommandId == commandId)
            return receipt.AuthoredIntentSha256 == digest && receipt.ActorUserId == actorUserId
                ? Result<PartnerDesignationView>.Success(_selectedView!)
                : Error(RequestErrorKind.Conflict, "The command already records another actor or intent.");
        if (userId != _userId || duty != _duty || !ValidDuty(duty) || userId == Uuid.Empty ||
            actorUserId == Uuid.Empty || commandId == Uuid.Empty || at == default ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000 ||
            View is not null && at < View.RecordedAt)
            return Error(RequestErrorKind.Validation, "A partner decision needs a known duty, identities, time and reason.");
        if (expectedRevision != Revision || _commands.Contains(commandId))
            return Error(RequestErrorKind.Conflict, "Reload the partner designation before changing it.");
        if (designated == IsDesignated)
            return View is not null
                ? Result<PartnerDesignationView>.Success(View)
                : Error(RequestErrorKind.NotFound, "The partner designation was not found.");
        var accepted = new FirmServiceReceipt(commandId, expectedRevision, digest, actorUserId, at);
        if (designated)
            RaiseEvent(new PartnerDesignated(userId, duty, Revision + 1, reason.Trim(), accepted));
        else
            RaiseEvent(new PartnerDesignationRevoked(userId, duty, Revision + 1, reason.Trim(), accepted));
        return Result<PartnerDesignationView>.Success(View!);
    }

    void Apply(Uuid userId, string duty, long revision, bool designated, string reason, FirmServiceReceipt receipt)
    {
        if (userId != _userId || userId == Uuid.Empty || duty != _duty || !ValidDuty(duty) ||
            revision != Revision + 1 || receipt is null || receipt.ExpectedRevision != Revision ||
            receipt.CommandId == Uuid.Empty || receipt.ActorUserId == Uuid.Empty || receipt.OccurredAt == default ||
            View is not null && receipt.OccurredAt < View.RecordedAt ||
            receipt.AuthoredIntentSha256 is not { Length: 64 } hash ||
            hash.Any(static c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
            designated == IsDesignated || string.IsNullOrWhiteSpace(reason) || reason.Trim() != reason ||
            reason.Length > 2000 || !_commands.Add(receipt.CommandId))
            throw new InvalidOperationException("The partner designation history is invalid.");
        View = new(userId, duty, revision, designated, reason, receipt.ActorUserId, receipt.OccurredAt);
        if (_selectedCommandId == Uuid.Empty || _selectedCommandId == receipt.CommandId)
        {
            _selectedReceipt = receipt;
            _selectedView = View;
        }
    }

    public static bool ValidDuty(string duty) => duty is "engagement_partner" or "quality_management_partner";
    static Result<PartnerDesignationView> Error(RequestErrorKind kind, string message) =>
        Result<PartnerDesignationView>.Failure(new RequestError(kind, message));
}
