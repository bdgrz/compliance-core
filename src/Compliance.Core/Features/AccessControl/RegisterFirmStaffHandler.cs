using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Admits a staff registration on its own staff record. The 202 acknowledgment is frozen on the root fact, so an
///     exact retry replays it and appends nothing; claim and completion run in <c>FirmStaffRegistrationProcess</c>.
/// </summary>
public sealed class RegisterFirmStaffHandler(NativeOperationExecutor operations, IPlatformUserDirectoryReader users,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RegisterFirmStaff, OperationAcceptedResponse>
{
    const string CommandKind = "RegisterFirmStaff";
    const string InitialEpisode = "initial";

    /// <summary>Where a caller reads the status of an accepted operation.</summary>
    public const string StatusUrlTemplate = "/api/v1/platform/operations/{operation_id}";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RegisterFirmStaff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.StaffMemberId == Uuid.Empty || request.UserId == Uuid.Empty ||
            request.Practice is not ("advisory" or "attest") ||
            !FirmStaffContent.Bounded(request.SourceReference, 2000))
            return Failure(RequestErrorKind.Validation,
                "Registration requires a staff member, canonical user, practice and attributed general staff source.");
        if (request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation, "The expected revision cannot be negative.");
        if (!await users.ExistsAsync(request.UserId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound,
                "The canonical platform user was not found; wait for directory recovery before registering staff.");

        var now = clock.GetUtcNow();
        var record = new FirmStaffRecord(request.StaffMemberId, request.StaffMemberId);
        var provenance = new OperationProvenance(request.StaffMemberId, request.StaffMemberId, null, CommandKind,
            FirmStaffOperator.From(context), record.Stream, InitialEpisode, (ulong)request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now, record.Stream, record.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        var header = new DurableIntentHeader(provenance.RootOperationId, record.Stream, record.Stream, record.Stream,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now);
        var intent = await content.WriteJsonAsync(FirmStaffContent.Realm, Uuid.CreateVersion4(),
            FirmStaffRegistrationIntent.Schema, new FirmStaffRegistrationIntent(header, request.StaffMemberId,
                request.UserId, request.Practice, request.SourceReference, FirmStaffRegistrationIntent.ActiveStatus),
            ct).ConfigureAwait(false);
        var accepted = await content.WriteJsonAsync(FirmStaffContent.Realm, Uuid.CreateVersion4(),
            "operation-accepted-response.v1", new OperationAcceptedResponse(request.StaffMemberId,
                StatusUrlTemplate.Replace("{operation_id}", request.StaffMemberId.ToString(), StringComparison.Ordinal),
                now), ct).ConfigureAwait(false);
        var response = new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
            "operation-accepted-response.v1", new StandardResponseBody(accepted));

        var outcome = await operations.ExecuteAsync(new NativeOperation<FirmStaffRecord>(binding,
            () => new FirmStaffRecord(request.StaffMemberId, request.StaffMemberId), staff => staff.Acceptance,
            staff => staff.PrepareRegistration(request.ExpectedRevision) is { IsSuccess: false } refused
                ? Result<FirstResponseDescriptor>.Failure(refused.Error!)
                : Result<FirstResponseDescriptor>.Success(response),
            (FirmStaffRecord staff, ImmutableContentRef frozen) => staff.AcceptRegistration(provenance, intent, frozen)), context, ct)
            .ConfigureAwait(false);
        if (!outcome.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(outcome.Error!);
        var descriptor = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<OperationAcceptedResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("A staff registration acknowledgment is standard content.");
        return Result<OperationAcceptedResponse>.Success(await content
            .ReadJsonAsync<OperationAcceptedResponse>(FirmStaffContent.Realm, body.Content, ct).ConfigureAwait(false));
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
