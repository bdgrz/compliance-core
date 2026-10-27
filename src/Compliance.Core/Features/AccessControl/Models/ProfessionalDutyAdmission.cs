using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     Admits a signed professional-duty change on the designation owner of the assertion's subject, duty and scope.
///     The verified assertion, the authored command, the predecessor and (for recording) the exact staff content to
///     hold are frozen into the intent before the 202 acknowledgment is frozen on the root fact, so an exact retry
///     replays the original acknowledgment even after the owner or the staff record changed. A revocation never reads
///     or requires the staff record.
/// </summary>
sealed class ProfessionalDutyAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    FirmStaffViews staffViews, IPlatformUserDirectoryReader users, IFirmProfessionalDutyAssertionVerifier assertions,
    IImmutableContentStore content, TimeProvider clock, ProfessionalDutyReader dutyViews)
{
    const string InitialEpisode = "initial";

    public async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync<TRequest>(IRequestContext<TRequest> context,
        ProfessionalDutyChangeKind kind, Uuid designationId, Uuid assertionId, string signedAssertion,
        long expectedRevision, CancellationToken ct)
        where TRequest : IRequest<OperationAcceptedResponse>, IProfessionalDutyAdministrationRequest
    {
        var recording = kind == ProfessionalDutyChangeKind.Record;
        var commandKind = recording ? "RecordFirmProfessionalDutyDesignation" : "RevokeFirmProfessionalDutyDesignation";
        if (designationId == Uuid.Empty || assertionId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A duty change requires a designation and a signed assertion identity.");
        if (expectedRevision < 0)
            return Failure(RequestErrorKind.Validation, "The expected revision cannot be negative.");
        var now = clock.GetUtcNow();
        if (!assertions.TryVerifyBinding(signedAssertion, out var evidence) ||
            evidence.Status != (recording ? "active" : "revoked") || evidence.DesignationId != designationId ||
            evidence.AssertionId != assertionId)
            return Failure(RequestErrorKind.Forbidden, recording
                ? "A valid, current signed active-duty assertion from the configured issuer is required."
                : "A valid, current signed revocation assertion from the configured issuer is required.");
        if (!ProfessionalDutyRules.ValidAssertion(evidence, signedAssertion))
            return Failure(RequestErrorKind.Validation,
                "A duty change requires a valid signed assertion with a supported duty scope.");

        var actor = FirmStaffOperator.From(context);
        var stream = new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty, evidence.TenantId).Stream;
        var provenance = new OperationProvenance(assertionId, assertionId, null, commandKind, actor, stream,
            InitialEpisode, (ulong)expectedRevision, AuthoredIntentDigest.Compute(context.Request), now, stream, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        (ulong Version, ulong Position, string Digest)? preparedCut = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FirmProfessionalDutyDesignation owner,
            CancellationToken token)
        {
            var ready = owner.PrepareChange(expectedRevision, assertionId);
            if (!ready.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(ready.Error!);
            if (!ProfessionalDutyRules.ValidAt(evidence, now))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "A first professional-duty admission requires signed validity at its frozen decision instant."));
            var frozen = await FreezeAsync(context.Request, kind, evidence, signedAssertion, designationId, assertionId,
                owner, provenance, now, token).ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
            intent = frozen.Value.Intent;
            preparedCut = (owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
            return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FirmProfessionalDutyDesignation>(binding,
            () => new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty, evidence.TenantId, assertionId),
            owner => owner.Acceptance,
            PrepareAsync,
            (FirmProfessionalDutyDesignation owner, ImmutableContentRef acknowledgment) =>
                preparedCut == (owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest)
                    ? owner.AcceptChange(provenance, intent!, acknowledgment)
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The prepared designation prefix changed before admission.", true))), context, ct).ConfigureAwait(false);
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
            throw new InvalidOperationException("A professional-duty acknowledgment is standard content.");
        return Result<OperationAcceptedResponse>.Success(await content
            .ReadJsonAsync<OperationAcceptedResponse>(ProfessionalDutyContent.Realm, body.Content, ct)
            .ConfigureAwait(false));
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync<TRequest>(
        TRequest request, ProfessionalDutyChangeKind kind, VerifiedFirmProfessionalDutyAssertion evidence,
        string signedAssertion, Uuid designationId, Uuid assertionId, FirmProfessionalDutyDesignation snapshot,
        OperationProvenance provenance, DateTimeOffset now, CancellationToken ct) where TRequest : notnull
    {
        FirmProfessionalDutyDesignationView? current;
        try
        {
            current = await dutyViews.ReadCurrentAsync(snapshot, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail(RequestErrorKind.Conflict,
                $"The current designation cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        var chain = ProfessionalDutyRules.CheckChain(kind, evidence, current);
        if (!chain.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(chain.Error!);
        var claim = await reader.HydrateAsync(new ProfessionalAssertionIdentityClaim(assertionId), ct)
            .ConfigureAwait(false);
        if (claim.IsClaimed)
            return Fail(RequestErrorKind.Conflict,
                "The signed assertion identity is already permanently claimed by another change.");

        SourceUseRequirement? requirement = null;
        if (kind == ProfessionalDutyChangeKind.Record)
        {
            var staff = await staffViews.FindByUserAsync(evidence.UserId, ct).ConfigureAwait(false);
            if (staff is not { IsActive: true })
                return Fail(RequestErrorKind.Conflict,
                    "The firm-staff identity is missing or inactive; reload before designating a professional duty.",
                    true);
            if (!await users.ExistsAsync(staff.UserId, ct).ConfigureAwait(false))
                return Fail(RequestErrorKind.Conflict,
                    "The canonical platform user is unavailable; wait for directory recovery.", true);
            var record = await reader.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId), ct).ConfigureAwait(false);
            if (record.Latest is not { } latestStaff || (long)latestStaff.Revision != staff.Revision)
                return Fail(RequestErrorKind.Conflict,
                    "The staff record changed while the change was prepared; reload and retry.", true);
            requirement = new SourceUseRequirement(record.Stream, NativeFactReference.Of(record.Stream, latestStaff),
                latestStaff.Revision, SourceUsePurpose.ProfessionalDutyRecording, latestStaff.StaffContent);
        }

        var header = new DurableIntentHeader(assertionId, snapshot.Stream, snapshot.Stream, snapshot.Stream,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now);
        var authored = await content.WriteJsonAsync(ProfessionalDutyContent.Realm, Uuid.CreateVersion4(),
            kind == ProfessionalDutyChangeKind.Record
                ? ProfessionalDutyContent.RecordCommandSchema
                : ProfessionalDutyContent.RevokeCommandSchema, request, ct).ConfigureAwait(false);
        var scope = await content.WriteJsonAsync(ProfessionalDutyContent.Realm, Uuid.CreateVersion4(),
            ProfessionalDutyContent.VerifiedScopeSchema, evidence, ct).ConfigureAwait(false);
        var intent = await content.WriteJsonAsync(ProfessionalDutyContent.Realm, Uuid.CreateVersion4(),
            ProfessionalDutyChangeIntent.Schema, new ProfessionalDutyChangeIntent(header, designationId, kind,
                authored, signedAssertion, assertionId, (ulong)evidence.AuthorityRevision, scope,
                snapshot.Latest is DomainEvent previous ? NativeFactReference.Of(snapshot.Stream, previous) : null,
                requirement), ct).ConfigureAwait(false);
        var acknowledgment = await content.WriteJsonAsync(ProfessionalDutyContent.Realm, Uuid.CreateVersion4(),
            "operation-accepted-response.v1", new OperationAcceptedResponse(assertionId,
                RegisterFirmStaffHandler.StatusUrlTemplate.Replace("{operation_id}", assertionId.ToString(),
                    StringComparison.Ordinal), now), ct).ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent,
            new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
                "operation-accepted-response.v1", new StandardResponseBody(acknowledgment))));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message,
        bool transient = false) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message, transient));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
