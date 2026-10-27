using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

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
        var now = clock.GetUtcNow();
        var record = new FirmStaffRecord(request.StaffMemberId, request.StaffMemberId);
        var provenance = new OperationProvenance(request.StaffMemberId, request.StaffMemberId, null, CommandKind,
            FirmStaffOperator.From(context), record.Stream, InitialEpisode, (ulong)request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now, record.Stream, record.Stream, record.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        var header = new DurableIntentHeader(provenance.RootOperationId, record.Stream, record.Stream, record.Stream,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now);
        ImmutableContentRef? intent = null;
        (ulong Version, ulong Position, string Digest)? preparedCut = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FirmStaffRecord staff, CancellationToken token)
        {
            var prepared = staff.PrepareRegistration(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (!await users.ExistsAsync(request.UserId, token).ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The canonical platform user was not found; wait for directory recovery before registering staff."));
            intent = await content.WriteJsonAsync(FirmStaffContent.Realm, Uuid.CreateVersion4(),
                FirmStaffRegistrationIntent.Schema, new FirmStaffRegistrationIntent(header, request.StaffMemberId,
                    request.UserId, request.Practice, request.SourceReference, FirmStaffRegistrationIntent.ActiveStatus),
                token).ConfigureAwait(false);
            var accepted = await content.WriteJsonAsync(FirmStaffContent.Realm, Uuid.CreateVersion4(),
                "operation-accepted-response.v1", new OperationAcceptedResponse(request.StaffMemberId,
                    StatusUrlTemplate.Replace("{operation_id}", request.StaffMemberId.ToString(), StringComparison.Ordinal),
                    now), token).ConfigureAwait(false);
            var response = new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
                "operation-accepted-response.v1", new StandardResponseBody(accepted));
            preparedCut = (staff.Version, staff.CommittedStreamPosition, staff.ReplayCut.Digest);
            return Result<FirstResponseDescriptor>.Success(response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FirmStaffRecord>(binding,
            () => new FirmStaffRecord(request.StaffMemberId, request.StaffMemberId), staff => staff.Acceptance,
            PrepareAsync,
            (FirmStaffRecord staff, ImmutableContentRef frozen) =>
                preparedCut == (staff.Version, staff.CommittedStreamPosition, staff.ReplayCut.Digest)
                    ? staff.AcceptRegistration(provenance, intent!, frozen)
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The prepared staff prefix changed before registration admission.", true))), context, ct)
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
