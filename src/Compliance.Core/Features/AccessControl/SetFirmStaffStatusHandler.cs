using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Changes the status of one registered staff record at its own revision. The complete resulting view is frozen as
///     the first response, so an exact retry returns the original view even after later changes.
/// </summary>
public sealed class SetFirmStaffStatusHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<SetFirmStaffStatus, FirmStaffMemberView>
{
    const string CommandKind = "SetFirmStaffStatus";
    const string InitialEpisode = "initial";

    public async ValueTask<Result<FirmStaffMemberView>> HandleAsync(IRequestContext<SetFirmStaffStatus> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.StaffMemberId == Uuid.Empty ||
            !FirmStaffContent.Bounded(request.Reason, 2000))
            return Failure(RequestErrorKind.Validation,
                "A staff status change requires a command id, staff member and bounded reason.");
        if (request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation, "The expected revision cannot be negative.");

        var now = clock.GetUtcNow();
        var actor = FirmStaffOperator.From(context);
        var stream = new FirmStaffRecord(request.StaffMemberId).Stream;
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind, actor,
            stream, InitialEpisode, (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now,
            stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        // The view is derived from the immutable registration intent and the expected revision, so it is built
        // before the append; the owner guard re-checks the revision on every attempt.
        var current = await reader.HydrateAsync(new FirmStaffRecord(request.StaffMemberId, request.CommandId), ct)
            .ConfigureAwait(false);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? staffContent = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None &&
            current.PrepareStatus((ulong)request.ExpectedRevision).IsSuccess)
        {
            var intent = await FirmStaffContent.ReadIntentAsync(content, current.RegistrationIntent!, ct)
                .ConfigureAwait(false);
            var view = new FirmStaffMemberView(request.StaffMemberId, intent.CanonicalUserId, intent.Practice,
                intent.SourceReference, request.IsActive, request.ExpectedRevision + 1, actor, now);
            staffContent = await FirmStaffContent.WriteStaffAsync(content, Uuid.CreateVersion4(), view, ct)
                .ConfigureAwait(false);
            result = await FirmStaffContent.WriteResultAsync(content, Uuid.CreateVersion4(), staffContent, ct)
                .ConfigureAwait(false);
            response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
                FirmStaffContent.StaffSchema, new StandardResponseBody(staffContent));
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FirmStaffRecord>(binding,
            () => new FirmStaffRecord(request.StaffMemberId, request.CommandId), staff => staff.Acceptance,
            staff => Prepare(staff, request, response),
            (FirmStaffRecord staff, ImmutableContentRef frozen) =>
                staff.SetStatus(provenance, request.Reason, staffContent!, frozen, result!)),
            context, ct).ConfigureAwait(false);
        if (!outcome.IsSuccess)
            return Result<FirmStaffMemberView>.Failure(outcome.Error!);
        var descriptor = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<FirmStaffMemberView>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("A staff status response is standard content.");
        return Result<FirmStaffMemberView>.Success(await FirmStaffContent.ReadStaffAsync(content, body.Content, ct)
            .ConfigureAwait(false));
    }

    static Result<FirstResponseDescriptor> Prepare(FirmStaffRecord staff, SetFirmStaffStatus request,
        FirstResponseDescriptor? response)
    {
        var prepared = staff.PrepareStatus((ulong)request.ExpectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The staff record changed while the status change was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<FirmStaffMemberView> Failure(RequestErrorKind kind, string message) =>
        Result<FirmStaffMemberView>.Failure(new RequestError(kind, message));
}
