using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Admits a signed professional-duty change on the designation owner of the assertion's subject, duty and scope.
///     The verified assertion, the authored command, the predecessor and (for recording) the exact staff content to
///     hold are frozen into the intent before the 202 acknowledgment is frozen on the root fact, so an exact retry
///     replays the original acknowledgment even after the owner or the staff record changed. A revocation never reads
///     or requires the staff record.
/// </summary>
sealed class ProfessionalDutyAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    FirmStaffViews staffViews, IPlatformUserDirectoryReader users, IFirmProfessionalDutyAssertionVerifier assertions,
    IImmutableContentStore content, TimeProvider clock)
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
        if (!assertions.TryVerify(signedAssertion, now, out var evidence) ||
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
            InitialEpisode, (ulong)expectedRevision, AuthoredIntentDigest.Compute(context.Request), now, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        // A logical id that was already accepted resolves from the owner's facts before any guard is evaluated.
        var snapshot = await reader.HydrateAsync(
            new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty, evidence.TenantId, assertionId), ct)
            .ConfigureAwait(false);
        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var frozen = await FreezeAsync(context.Request, kind, evidence, signedAssertion, designationId, assertionId,
                snapshot, provenance, now, ct).ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(frozen.Error!);
            (intent, response) = frozen.Value;
        }

        var snapshotLatest = EventIdOf(snapshot.Latest);
        var outcome = await operations.ExecuteAsync(new NativeOperation<FirmProfessionalDutyDesignation>(binding,
            () => new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty, evidence.TenantId, assertionId),
            owner => owner.Acceptance,
            owner => Prepare(owner, expectedRevision, assertionId, snapshotLatest, response),
            (FirmProfessionalDutyDesignation owner, ImmutableContentRef acknowledgment) =>
                owner.AcceptChange(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
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
            current = snapshot.LatestContent is { } latest
                ? await ProfessionalDutyContent.ReadDesignationAsync(content, latest, ct).ConfigureAwait(false)
                : null;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail(RequestErrorKind.Conflict,
                $"The current designation cannot be verified: {exception.Message}", true);
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

    static Result<FirstResponseDescriptor> Prepare(FirmProfessionalDutyDesignation owner, long expectedRevision,
        Uuid assertionId, Uuid? snapshotLatest, FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareChange(expectedRevision, assertionId);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null || EventIdOf(owner.Latest) != snapshotLatest
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The designation changed while the change was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Uuid? EventIdOf(INativeOperationFact? fact) => (fact as DomainEvent)?.Metadata?.EventId;

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message,
        bool transient = false) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message, transient));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
