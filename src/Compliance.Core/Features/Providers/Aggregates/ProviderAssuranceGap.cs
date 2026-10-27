using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

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
        if (operation.LogicalId != _gapId || operation.Owner != Stream || providerRevision < 1)
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
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
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
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream ||
            operation.Episode != PairEpisode(riskId, acceptanceId))
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
