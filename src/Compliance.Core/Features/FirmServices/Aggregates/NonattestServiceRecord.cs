using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Events;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Aggregates;

/// <summary>One independently corrected advisory service; voiding preserves all recorded facts.</summary>
public sealed class NonattestServiceRecord : Aggregate
{
    public const string Area = "firm-nonattest-service";
    readonly Uuid _tenantId;
    readonly Uuid _selectedCommandId;
    readonly long? _selectedRevision;
    readonly HashSet<Uuid> _commands = [];
    FirmServiceReceipt? _selectedReceipt;
    NonattestServiceRecordView? _selectedView;
    public NonattestServiceRecordView? View { get; private set; }
    public long Revision => View?.Revision ?? 0;
    public NonattestServiceRecordView? SelectedRevisionView { get; private set; }

    public NonattestServiceRecord(Uuid tenantId, Uuid recordId, Uuid selectedCommandId = default, long? selectedRevision = null)
        : base(recordId, new EventStreamAddress(tenantId.ToString(), Area, recordId.ToString()))
    {
        _tenantId = tenantId;
        _selectedCommandId = selectedCommandId;
        _selectedRevision = selectedRevision;
        On<NonattestServiceRecorded>(Apply);
        On<NonattestServiceRevised>(Apply);
        On<NonattestServiceVoided>(Apply);
    }

    public Result<NonattestServiceRecordView> Record(RecordNonattestService request, Uuid actorUserId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = AuthoredIntentDigest.Compute(request);
        if (Replay(request.CommandId, digest, actorUserId) is { } retry)
            return retry;
        var valid = Validate(request.TenantId, request.RecordId, request.CommandId, request.ExpectedRevision,
            actorUserId, occurredAt);
        if (valid is not null)
            return Failure(valid);
        if (View is not null)
            return Error(RequestErrorKind.Conflict, "The nonattest service identity is already recorded.");
        if (!ValidTerms(request.Terms))
            return Error(RequestErrorKind.Validation, "A known advisory service kind and ordered dates are required.");
        RaiseEvent(new NonattestServiceRecorded(_tenantId, Id, 1, request.Terms,
            new(request.CommandId, request.ExpectedRevision, digest, actorUserId, occurredAt)));
        return Result<NonattestServiceRecordView>.Success(View!);
    }

    public Result<NonattestServiceRecordView> Revise(ReviseNonattestService request, Uuid actorUserId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = AuthoredIntentDigest.Compute(request);
        if (Replay(request.CommandId, digest, actorUserId) is { } retry)
            return retry;
        var valid = Validate(request.TenantId, request.RecordId, request.CommandId, request.ExpectedRevision,
            actorUserId, occurredAt);
        if (valid is not null)
            return Failure(valid);
        if (View is null)
            return Error(RequestErrorKind.NotFound, "The nonattest service record was not found.");
        if (View.IsVoided)
            return Error(RequestErrorKind.Conflict, "A voided service record cannot be revised.");
        if (!ValidTerms(request.Terms) || !ValidReason(request.Reason) ||
            request.Terms.EngagementId != View.Terms.EngagementId)
            return Error(RequestErrorKind.Validation, "A correction needs valid service terms, its original engagement and a reason.");
        if (request.Terms == View.Terms)
            return Result<NonattestServiceRecordView>.Success(View);
        RaiseEvent(new NonattestServiceRevised(_tenantId, Id, Revision + 1, request.Terms, request.Reason.Trim(),
            new(request.CommandId, request.ExpectedRevision, digest, actorUserId, occurredAt)));
        return Result<NonattestServiceRecordView>.Success(View!);
    }

    public Result<NonattestServiceRecordView> Void(VoidNonattestService request, Uuid actorUserId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = AuthoredIntentDigest.Compute(request);
        if (Replay(request.CommandId, digest, actorUserId) is { } retry)
            return retry;
        var valid = Validate(request.TenantId, request.RecordId, request.CommandId, request.ExpectedRevision,
            actorUserId, occurredAt);
        if (valid is not null)
            return Failure(valid);
        if (View is null)
            return Error(RequestErrorKind.NotFound, "The nonattest service record was not found.");
        if (View.IsVoided)
            return Error(RequestErrorKind.Conflict, "The nonattest service record is already voided.");
        if (!ValidReason(request.Reason))
            return Error(RequestErrorKind.Validation, "Voiding a service record requires a reason.");
        RaiseEvent(new NonattestServiceVoided(_tenantId, Id, Revision + 1, request.Reason.Trim(),
            new(request.CommandId, request.ExpectedRevision, digest, actorUserId, occurredAt)));
        return Result<NonattestServiceRecordView>.Success(View!);
    }

    RequestError? Validate(Uuid tenantId, Uuid recordId, Uuid commandId, long expectedRevision,
        Uuid actorUserId, DateTimeOffset occurredAt)
    {
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || tenantId != _tenantId || recordId != Id ||
            commandId == Uuid.Empty || actorUserId == Uuid.Empty || occurredAt == default ||
            View is not null && occurredAt < View.RecordedAt)
            return new(RequestErrorKind.Validation, "The service decision requires its exact identity and attributed time.");
        return expectedRevision != Revision || _commands.Contains(commandId)
            ? new(RequestErrorKind.Conflict, "Reload the current service revision before changing it.")
            : null;
    }

    Result<NonattestServiceRecordView>? Replay(Uuid commandId, string digest, Uuid actorUserId) =>
        _selectedReceipt is not { } receipt || receipt.CommandId != commandId ? null :
        receipt.AuthoredIntentSha256 == digest && receipt.ActorUserId == actorUserId
            ? Result<NonattestServiceRecordView>.Success(_selectedView!)
            : Error(RequestErrorKind.Conflict, "The command identity already records another actor or intent.");

    void Apply(NonattestServiceRecorded fact)
    {
        RequireFact(fact.TenantId, fact.RecordId, fact.Revision, fact.Receipt);
        if (View is not null || !ValidTerms(fact.Terms))
            throw new InvalidOperationException("The nonattest service creation history is invalid.");
        View = new(_tenantId, Id, fact.Revision, fact.Terms, false, null,
            fact.Receipt.ActorUserId, fact.Receipt.OccurredAt);
        Capture(fact.Receipt);
    }

    void Apply(NonattestServiceRevised fact)
    {
        RequireFact(fact.TenantId, fact.RecordId, fact.Revision, fact.Receipt);
        if (View is null || View.IsVoided || !ValidTerms(fact.Terms) || !ValidReason(fact.Reason) ||
            fact.Reason.Trim() != fact.Reason || fact.Terms.EngagementId != View.Terms.EngagementId ||
            fact.Terms == View.Terms)
            throw new InvalidOperationException("The nonattest service correction history is invalid.");
        View = new(_tenantId, Id, fact.Revision, fact.Terms, false, fact.Reason,
            fact.Receipt.ActorUserId, fact.Receipt.OccurredAt);
        Capture(fact.Receipt);
    }

    void Apply(NonattestServiceVoided fact)
    {
        RequireFact(fact.TenantId, fact.RecordId, fact.Revision, fact.Receipt);
        if (View is null || View.IsVoided || !ValidReason(fact.Reason) || fact.Reason.Trim() != fact.Reason)
            throw new InvalidOperationException("The nonattest service void history is invalid.");
        View = View with { Revision = fact.Revision, IsVoided = true, Reason = fact.Reason,
            ActorUserId = fact.Receipt.ActorUserId, RecordedAt = fact.Receipt.OccurredAt };
        Capture(fact.Receipt);
    }

    void RequireFact(Uuid tenantId, Uuid recordId, long revision, FirmServiceReceipt receipt)
    {
        if (receipt is null || revision != Revision + 1 || receipt.ExpectedRevision != Revision ||
            Validate(tenantId, recordId, receipt.CommandId, receipt.ExpectedRevision,
                receipt.ActorUserId, receipt.OccurredAt) is not null ||
            receipt.AuthoredIntentSha256 is not { Length: 64 } hash ||
            hash.Any(static c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
            !_commands.Add(receipt.CommandId))
            throw new InvalidOperationException("The nonattest service fact identity or sequence is invalid.");
    }

    void Capture(FirmServiceReceipt receipt)
    {
        if (_selectedRevision == Revision)
            SelectedRevisionView = View;
        if (_selectedCommandId != Uuid.Empty && receipt.CommandId != _selectedCommandId)
            return;
        _selectedReceipt = receipt;
        _selectedView = View;
    }

    public static bool ValidTerms(NonattestServiceTerms? terms) => terms is not null &&
        terms.ServiceKind is "readiness_assessment" or "control_design" or "control_implementation" or
            "operating_controls" or "vciso" && terms.StartedOn != default &&
        (terms.EndedOn is null || terms.EndedOn >= terms.StartedOn) && terms.EngagementId != Uuid.Empty;

    static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 2000;
    static Result<NonattestServiceRecordView> Failure(RequestError error) => Result<NonattestServiceRecordView>.Failure(error);
    static Result<NonattestServiceRecordView> Error(RequestErrorKind kind, string message) => Failure(new(kind, message));
}
