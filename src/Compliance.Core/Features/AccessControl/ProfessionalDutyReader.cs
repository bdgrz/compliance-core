using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Composes exact signed designation history, original bodies and continuity outside current authority.</summary>
sealed class ProfessionalDutyReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content,
    IFirmProfessionalDutyAssertionVerifier assertions, FirmStaffViews staffViews)
{
    public Task<FirmProfessionalDutyDesignationView?> ReadCurrentAsync(FirmProfessionalDutyDesignation owner,
        CancellationToken ct) => ReadAtAsync(owner, owner.Latest as DomainEvent, ct);

    public async Task<FirmProfessionalDutyDesignationView> ReadAsync(DomainEvent fact, CancellationToken ct)
    {
        var body = await ProfessionalDutyContent.ReadDesignationAsync(content, Body(fact), ct).ConfigureAwait(false);
        var owner = await reader.HydrateAsync(new FirmProfessionalDutyDesignation(body.UserId, body.Duty, body.TenantId), ct)
            .ConfigureAwait(false);
        return await ReadAtAsync(owner, fact, ct).ConfigureAwait(false) ??
            throw new ImmutableContentIntegrityException("The designation source has no verified original body.");
    }

    async Task<FirmProfessionalDutyDesignationView?> ReadAtAsync(FirmProfessionalDutyDesignation owner,
        DomainEvent? selected, CancellationToken ct)
    {
        if (selected is null)
            return null;
        var history = await ProfessionalDutyHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        if (!history.Matches(selected))
            throw Invalid("The selected designation source is not in the exact native prefix.");
        FirmProfessionalDutyDesignationView? previous = null;
        NativeFactReference? previousSource = null;
        foreach (var fact in history.Facts)
        {
            if (fact is not (FirmProfessionalDutyDesignationRecorded or FirmProfessionalDutyDesignationRevoked))
                continue;
            var native = (INativeOperationFact)fact;
            var admission = history.AcceptedFor(native.Operation.RootOperationId)!;
            var intent = await ProfessionalDutyContent.ReadIntentAsync(content, admission.Intent, ct).ConfigureAwait(false);
            var evidence = await ProfessionalDutyContent.ReadVerifiedScopeAsync(content, intent.VerifiedScope, ct).ConfigureAwait(false);
            ProfessionalDutyContent.RequireIntent(owner, admission, intent, evidence);
            await ProfessionalDutyContent.RequireAuthoredAsync(content, intent, ct).ConfigureAwait(false);
            if (!assertions.TryVerify(intent.SignedAssertion, intent.Header.AcceptedAt, out var signed) || signed != evidence ||
                Signed(fact) != intent.SignedAssertion || intent.PreviousAssertion != previousSource)
                throw Invalid("The designation does not carry its exact signed scope, validity or native predecessor.");
            var chain = ProfessionalDutyRules.CheckChain(intent.ChangeKind, evidence, previous);
            if (!chain.IsSuccess)
                throw Invalid(chain.Error!.Message);
            if (Body(fact).Schema != ProfessionalDutyContent.DesignationSchema(intent.ChangeKind))
                throw Invalid("The designation body has another selected schema.");
            var view = await ProfessionalDutyContent.ReadDesignationAsync(content, Body(fact), ct).ConfigureAwait(false);
            FirmProfessionalDutyDesignationView expected;
            if (intent.ChangeKind == ProfessionalDutyChangeKind.Record)
            {
                var pin = intent.StaffEligibility!;
                if (pin.Purpose != SourceUsePurpose.ProfessionalDutyRecording ||
                    pin.SourceOwner != pin.ContentEvent.Stream || pin.SourceOwner.Realm != FirmStaffContent.Realm ||
                    pin.SourceOwner.Area != FirmStaffRecord.Area ||
                    !Uuid.TryParse(pin.SourceOwner.Resource, null, out var staffId))
                    throw Invalid("The signed designation pins another exact staff source.");
                var staffOwner = await reader.HydrateAsync(new FirmStaffRecord(staffId), ct).ConfigureAwait(false);
                var staffHistory = await FirmStaffHistory.ReadAsync(events, staffOwner, ct).ConfigureAwait(false);
                var source = staffHistory.Changes.SingleOrDefault(change => change.Metadata.EventId == pin.ContentEvent.EventId);
                if (source is null || NativeFactReference.Of(staffOwner.Stream, source) != pin.ContentEvent ||
                    source.Revision != pin.ResourceRevision || source.StaffContent != pin.RequiredFacts)
                    throw Invalid("The designation staff pin differs from its exact historical decision.");
                var staff = await staffViews.ReadAsync(source, ct).ConfigureAwait(false);
                if (!staff.IsActive || staff.UserId != evidence.UserId)
                    throw Invalid("The signed designation requires its exact historically active canonical staff member.");
                expected = ProfessionalDutyRules.Recorded(previous, evidence, staffId, (long)pin.ResourceRevision,
                    (long)Revision(fact), intent.Header.OriginalActor, intent.Header.AcceptedAt);
            }
            else
                expected = ProfessionalDutyRules.Revoked(previous!, evidence, (long)Revision(fact),
                    intent.Header.OriginalActor, intent.Header.AcceptedAt);
            if (view != expected || view.UserId != owner.UserId || view.Duty != owner.Duty || view.TenantId != owner.TenantId)
                throw Invalid("The complete designation body differs from its signed authority and continuity chain.");
            var result = await content.ReadJsonAsync<MutationResultContent>(ProfessionalDutyContent.Realm, Result(fact), ct)
                .ConfigureAwait(false);
            if (result.Schema != ProfessionalDutyContent.DesignationSchema(intent.ChangeKind) ||
                result.Value is not StandardResponseBody response || response.Content != Body(fact))
                throw Invalid("The designation result differs from its exact original typed body.");
            previous = view;
            previousSource = NativeFactReference.Of(owner.Stream, fact);
            if (fact.Metadata.EventId == selected.Metadata.EventId)
                return view;
        }
        throw Invalid("The designation source is not an applied decision.");
    }

    static ImmutableContentRef Body(DomainEvent fact) => fact switch
    {
        FirmProfessionalDutyDesignationRecorded value => value.DesignationContent,
        FirmProfessionalDutyDesignationRevoked value => value.DesignationContent,
        _ => throw Invalid("The source is not an applied designation."),
    };
    static ImmutableContentRef Result(DomainEvent fact) => fact switch
    {
        FirmProfessionalDutyDesignationRecorded { OperationResult: { } value } => value,
        FirmProfessionalDutyDesignationRevoked { OperationResult: { } value } => value,
        _ => throw Invalid("The source has no original typed result."),
    };
    static string Signed(DomainEvent fact) => fact switch
    {
        FirmProfessionalDutyDesignationRecorded value => value.SignedAssertion,
        FirmProfessionalDutyDesignationRevoked value => value.SignedAssertion,
        _ => throw Invalid("The source is not an applied designation."),
    };
    static ulong Revision(DomainEvent fact) => fact switch
    {
        FirmProfessionalDutyDesignationRecorded value => value.Revision,
        FirmProfessionalDutyDesignationRevoked value => value.Revision,
        _ => throw Invalid("The source is not an applied designation."),
    };
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
