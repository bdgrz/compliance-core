using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Changes the status of one registered staff record at its own revision. The complete resulting view is frozen as
///     the first response, so an exact retry returns the original view even after later changes.
/// </summary>
public sealed class SetFirmStaffStatusHandler(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
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
            stream, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? staffContent = null;
        ImmutableContentRef? result = null;
        (ulong Version, ulong Position, string Digest)? preparedCut = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FirmStaffRecord staff, CancellationToken token)
        {
            var ready = staff.PrepareStatus((ulong)request.ExpectedRevision);
            if (!ready.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(ready.Error!);
            var intent = await FirmStaffContent.ReadIntentAsync(content, staff.RegistrationIntent!, token)
                .ConfigureAwait(false);
            FirmStaffContent.RequireIntent(staff, intent);
            var view = new FirmStaffMemberView(request.StaffMemberId, intent.CanonicalUserId, intent.Practice,
                intent.SourceReference, request.IsActive, request.ExpectedRevision + 1, actor, now);
            staffContent = await FirmStaffContent.WriteStaffAsync(content, Uuid.CreateVersion4(), view, token)
                .ConfigureAwait(false);
            result = await FirmStaffContent.WriteResultAsync(content, Uuid.CreateVersion4(), staffContent, token)
                .ConfigureAwait(false);
            preparedCut = (staff.Version, staff.CommittedStreamPosition, staff.ReplayCut.Digest);
            return Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(200,
                ImmutableContentStoreExtensions.JsonMediaType, FirmStaffContent.StaffSchema,
                new StandardResponseBody(staffContent)));
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FirmStaffRecord>(binding,
            () => new FirmStaffRecord(request.StaffMemberId, request.CommandId), staff => staff.Acceptance,
            PrepareAsync,
            (FirmStaffRecord staff, ImmutableContentRef frozen) =>
                preparedCut == (staff.Version, staff.CommittedStreamPosition, staff.ReplayCut.Digest)
                    ? staff.SetStatus(provenance, request.Reason, staffContent!, frozen, result!)
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The prepared staff prefix changed before the status append.", true))),
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

    static Result<FirmStaffMemberView> Failure(RequestErrorKind kind, string message) =>
        Result<FirmStaffMemberView>.Failure(new RequestError(kind, message));
}
