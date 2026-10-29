using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Commands;
namespace Bdgrz.Compliance.Features.Providers.Aggregates;

/// <summary>
///     One provider coverage gap: its record, its single closure and every accepted-risk link admitted on it. The
///     logical revision counts the gap's own facts: the record, each admitted link and the closure. The gap
///     serializes the open check and the uniqueness of each (risk, acceptance) pair at admission; the immutable link
///     itself lives on its own <see cref="ProviderRiskAcceptance" /> owner and never changes the gap.
/// </summary>
public sealed class ProviderAssuranceGap : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _providerId;
    readonly Uuid _gapId;
    readonly ProviderGapReplayCut _cut = new();
    ProviderCoverageGapRecorded? _recorded;
    ProviderCoverageGapClosed? _closed;
    long _revision;

    /// <summary>Opens the gap of one provider, selecting the logical operation to resolve.</summary>
    public ProviderAssuranceGap(Uuid tenantId, Uuid providerId, Uuid gapId, Uuid selectedLogicalId = default,
        Uuid selectedAdmissionId = default, string? selectedPair = null)
        : base(ProviderAssuranceStreams.GapIdentity(tenantId, providerId, gapId),
            ProviderAssuranceStreams.Gap(tenantId, providerId, gapId))
    {
        _tenantId = tenantId;
        _providerId = providerId;
        _gapId = gapId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        SelectedAdmissionId = selectedAdmissionId;
        SelectedPair = selectedPair;
        On<ProviderCoverageGapRecorded>(recorded =>
        {
            if (recorded.Operation is null)
                throw new ImmutableContentIntegrityException("The native provider gap fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == recorded.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native provider gap operation occurs more than once.");
            Acceptance.Observe(recorded);
            if (_recorded is not null || recorded.TenantId != _tenantId || recorded.ProviderId != _providerId ||
                recorded.GapId != _gapId || recorded.ProviderRevision < 1 || recorded.Operation.Owner != Stream ||
                recorded.Operation.LogicalId != _gapId || recorded.RequestId != _gapId)
                throw new ImmutableContentIntegrityException("A gap is recorded once, by its own provider and logical identity.");
            _recorded = recorded;
            _revision = 1;
        });
        On<ProviderCoverageGapClosed>(closed =>
        {
            if (closed.Operation is null)
                throw new ImmutableContentIntegrityException("The native provider gap fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == closed.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native provider gap operation occurs more than once.");
            Acceptance.Observe(closed);
            if (_recorded is null || _closed is not null || closed.TenantId != _tenantId ||
                closed.ProviderId != _providerId || closed.GapId != _gapId || closed.Revision != _revision + 1 ||
                closed.Operation.Owner != Stream || closed.Operation.LogicalId != closed.RequestId)
                throw new ImmutableContentIntegrityException("A closure must follow the open gap and its previous revision.");
            _closed = closed;
            _revision = closed.Revision;
        });
        On<ProviderCoverageGapRiskAcceptanceAdmitted>(admitted =>
        {
            if (admitted.Operation is null)
                throw new ImmutableContentIntegrityException("The native provider gap fact lacks operation provenance.");
            if (Acceptance.SelectedFact is not null && Acceptance.SelectedLogicalId == admitted.Operation.LogicalId)
                throw new ImmutableContentIntegrityException("The selected native provider gap operation occurs more than once.");
            Acceptance.Observe(admitted);
            if (_recorded is null || _closed is not null || admitted.Owner != Stream ||
                admitted.Revision != _revision + 1 || admitted.Operation.Owner != Stream)
                throw new ImmutableContentIntegrityException(
                    "A risk acceptance link is admitted on the open gap for a new pair at the next revision.");
            _revision = admitted.Revision;
            if (admitted.Operation.LogicalId == SelectedAdmissionId)
                SelectedAdmission = admitted;
        });
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The provider this gap belongs to; it never changes.</summary>
    public Uuid ProviderId => _providerId;

    /// <summary>The gap this owner holds.</summary>
    public Uuid GapId => _gapId;

    /// <summary>True once the gap was recorded.</summary>
    public bool Exists => _recorded is not null;

    /// <summary>True while the gap is recorded and not closed.</summary>
    public bool IsOpen => _recorded is not null && _closed is null;

    /// <summary>The logical revision of the gap owner.</summary>
    public long Revision => _revision;

    /// <summary>The record, once accepted.</summary>
    public ProviderCoverageGapRecorded? Recorded => _recorded;

    /// <summary>The single closure, once it happened.</summary>
    public ProviderCoverageGapClosed? Closed => _closed;

    internal ProviderGapReplayCut ReplayCut => _cut;
    internal Uuid TenantId => _tenantId;
    internal Uuid SelectedAdmissionId { get; }
    internal ProviderCoverageGapRiskAcceptanceAdmitted? SelectedAdmission { get; set; }
    internal string? SelectedPair { get; }
    internal bool SelectedPairSeen { get; set; }

    /// <summary>The wire form of the pair a link admission is bound to; it is carried as the operation episode.</summary>
    public static string PairEpisode(Uuid riskId, Uuid acceptanceId) => $"{riskId}:{acceptanceId}";

    /// <summary>Checks the creation guard without raising anything.</summary>
    public Result PrepareRecord() => _recorded is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The provider coverage gap identity already exists."));

    /// <summary>Accepts the gap as the first native fact of its logical identity.</summary>
    public Result Record(OperationProvenance operation, long providerRevision, ImmutableContentRef content,
        ActorReference actor, DateTimeOffset recordedAt, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_tenantId == Uuid.Empty || _providerId == Uuid.Empty || _gapId == Uuid.Empty ||
            operation.LogicalId != _gapId || providerRevision < 1 ||
            !ValidRoot(operation, nameof(RecordProviderCoverageGap), "initial", null) ||
            actor != operation.Actor || recordedAt != operation.AcceptedAt ||
            !ProviderGapHistory.Reference(content, ProviderAssuranceContent.GapRecordedSchema) ||
            !ProviderGapHistory.Reference(firstResponse, FirstResponses.Schema) ||
            !ProviderGapHistory.Reference(operationResult, ProviderAssuranceContent.GapRegistrationSchema))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A provider coverage gap is the logical operation of its own gap owner and requires a provider revision."));
        var prepared = PrepareRecord();
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new ProviderCoverageGapRecorded(_tenantId, _gapId, _providerId, _gapId, providerRevision, content,
            actor, recordedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>
    ///     Checks the closure guards without raising anything. <paramref name="recorded" /> is the exact source the
    ///     gap was recorded against, read from the gap's immutable content.
    /// </summary>
    public Result PrepareClose(long expectedRevision, ProviderCoverageGapContent recorded,
        ProviderCoverageGapClosureContent closure)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(closure);
        if (_recorded is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The provider coverage gap was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("provider coverage gap", _revision)
                .ToRequestError());
        if (_closed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A provider coverage gap can be closed only once."));
        return recorded.SourceKind == closure.SourceKind && recorded.SourceId == closure.SourceId &&
               closure.SourceRevision <= recorded.SourceRevision
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A provider coverage gap requires a newer or different closure source."))
            : Result.Success;
    }

    /// <summary>Accepts the closure as the native fact of its logical command.</summary>
    public Result Close(OperationProvenance operation, long expectedRevision, ProviderCoverageGapContent recorded,
        ProviderCoverageGapClosureContent closure, ImmutableContentRef content, ActorReference actor,
        DateTimeOffset closedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (expectedRevision < 0 ||
            !ValidRoot(operation, nameof(CloseProviderCoverageGap), "current", (ulong)expectedRevision) ||
            actor != operation.Actor || closedAt != operation.AcceptedAt ||
            !ProviderGapHistory.Reference(content, ProviderAssuranceContent.GapClosedSchema) ||
            !ProviderGapHistory.Reference(firstResponse, FirstResponses.Schema) ||
            !ProviderGapHistory.Reference(operationResult, ProviderAssuranceContent.GapRegistrationSchema))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A gap closure requires its own logical operation."));
        var prepared = PrepareClose(expectedRevision, recorded, closure);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        RaiseEvent(new ProviderCoverageGapClosed(_tenantId, _gapId, _providerId, operation.LogicalId, _revision + 1,
            content, actor, closedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the link admission guards without raising anything.</summary>
    public Result PrepareAdmitLink(long expectedRevision, Uuid riskId, Uuid acceptanceId)
    {
        if (riskId == Uuid.Empty || acceptanceId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A link admission requires nonempty risk and acceptance identities."));
        if (_recorded is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The provider coverage gap was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("provider coverage gap", _revision)
                .ToRequestError());
        if (_closed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A closed provider coverage gap cannot receive a risk acceptance link."));
        if (SelectedPair != PairEpisode(riskId, acceptanceId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A link admission requires its exact selected pair at the finalized native gap cut.", true));
        return SelectedPairSeen
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk acceptance is already linked to this provider coverage gap."))
            : Result.Success;
    }

    /// <summary>
    ///     Admits the link as the root native fact of its logical command. The admission produces the next gap
    ///     revision, which <paramref name="operation" /> keeps even if the gap closes before the link materializes.
    /// </summary>
    public Result AdmitLink(OperationProvenance operation, long expectedRevision, Uuid riskId, Uuid acceptanceId,
        ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (expectedRevision < 0 || riskId == Uuid.Empty || acceptanceId == Uuid.Empty ||
            !ValidRoot(operation, nameof(LinkProviderCoverageGapRiskAcceptance), PairEpisode(riskId, acceptanceId),
                (ulong)expectedRevision) ||
            !ProviderGapHistory.Reference(intent, ProviderRiskLinkIntent.Schema) ||
            !ProviderGapHistory.Reference(firstResponse, FirstResponses.Schema))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A link admission is the logical operation of its own gap owner, bound to its pair."));
        var prepared = PrepareAdmitLink(expectedRevision, riskId, acceptanceId);
        if (!prepared.IsSuccess)
            return prepared;
        var append = _cut.Require(this, Acceptance, operation);
        if (!append.IsSuccess)
            return append;
        var revision = _revision + 1;
        RaiseEvent(new ProviderCoverageGapRiskAcceptanceAdmitted(Stream, revision, revision, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    bool ValidRoot(OperationProvenance operation, string kind, string episode, ulong? expectedRevision) =>
        operation.LogicalId != Uuid.Empty && operation.RootOperationId == operation.LogicalId &&
        operation.CausationEventId is null && operation.CommandKind == kind && operation.Owner == Stream &&
        operation.AffectedOwner == Stream && operation.ExpectedRevisionOwner == Stream &&
        (operation.FirstOwner ?? operation.Owner) == Stream && operation.Episode == episode &&
        operation.OriginalExpectedRevision == expectedRevision && operation.Actor is { } actor &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display) && operation.AcceptedAt != default &&
        operation.AuthoredIntentSha256 is { Length: 64 } sha && sha.All(Uri.IsHexDigit);

    protected override void Apply(DomainEvent ev)
    {
        _cut.Observe(ev);
        base.Apply(ev);
    }

    /// <summary>The provenance of an operation on this owner; the owner is its own first, affected and revision scope.</summary>
    public OperationProvenance Provenance(Uuid commandId, string commandKind, ActorReference actor, string episode,
        ulong? originalExpectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(commandId, commandId, null, commandKind, actor, Stream, episode, originalExpectedRevision,
            authoredIntentSha256, acceptedAt, Stream, Stream);
}
