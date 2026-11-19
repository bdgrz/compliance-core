using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;

/// <summary>
///     Owns one manually inventoried information asset and its attributed revisions. Every revision is the native
///     fact of its own logical operation, so exact retries are decided by the original operation and never by the
///     asset's current content.
/// </summary>
public sealed class InformationAsset : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    bool _foreign;
    long _revision;
    InformationAssetContent? _content;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public bool IsCreated => _created && !_foreign;
    public long Revision => _revision;
    public InformationAssetContent? Content => IsCreated ? _content : null;

    public InformationAsset(Uuid tenantId, Uuid id, Uuid selectedLogicalId = default)
        : base(id, TechnologyInventoryStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<InformationAssetRevisionRecorded>(ev =>
        {
            Acceptance.Observe(ev);
            _created = true;
            _revision = ev.Revision;
            _content = ev.Content;
        });
        On<InformationAssetRecorded>(ev => ApplyNamed(ev));
        On<InformationAssetDetailsChanged>(ev => ApplyNamed(ev));
        On<InformationAssetClassified>(ev => ApplyNamed(ev));
        On<InformationAssetOwnerChanged>(ev => ApplyNamed(ev));
        On<InformationAssetRetired>(ev => ApplyNamed(ev));
        On<TechnologyComponentRevisionRecorded>(_ => _foreign = true);
        On<DataFlowRevisionRecorded>(_ => _foreign = true);
    }

    /// <summary>Checks a creation against hydrated state without raising anything; returns the normalized content.</summary>
    public Result<InformationAssetContent> PrepareRecord(InformationAssetContent requested)
    {
        var content = TechnologyInventoryRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = TechnologyInventoryRules.Validate(content);
        if (error is not null)
            return Result<InformationAssetContent>.Failure(new RequestError(RequestErrorKind.Validation, error));
        if (_foreign)
            return Result<InformationAssetContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity belongs to another inventory record."));
        return _created
            ? Result<InformationAssetContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The information asset already exists."))
            : Result<InformationAssetContent>.Success(content);
    }

    /// <summary>Accepts the creation as the native fact of <paramref name="operation" />.</summary>
    public Result Record(OperationProvenance operation, ImmutableContentRef firstResponse,
        InformationAssetContent requested, ActorReference actor, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An information asset creation requires its own logical operation."));
        var prepared = PrepareRecord(requested);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        RaiseEvent(new InformationAssetRecorded(_tenantId, Id, 1, prepared.Value!, actor, changedAt,
            operation, firstResponse));
        return Result.Success;
    }

    /// <summary>Checks a revision against hydrated state without raising anything; returns the normalized content.</summary>
    public Result<InformationAssetContent> PrepareRevise(long expectedRevision,
        Func<InformationAssetContent, InformationAssetContent> change)
    {
        if (!IsCreated)
            return Failure(CommandFailure.MissingRecord("The information asset was not found."));
        if (expectedRevision != _revision)
            return Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("information asset",
                _revision)));
        if (_content!.Lifecycle == TechnologyInventoryRules.Retired)
            return Result<InformationAssetContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "A retired information asset cannot change; record a new identity."));
        var content = TechnologyInventoryRules.Normalize(change(_content!));
        return TechnologyInventoryRules.Validate(content) is { } error
            ? Failure(CommandFailure.InvalidContent(error))
            : Result<InformationAssetContent>.Success(content);
    }

    /// <summary>Accepts the revision as the native fact of <paramref name="operation" />.</summary>
    public Result Revise(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        Func<InformationAssetContent, InformationAssetContent> change, ActorReference actor, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An information asset revision requires its own logical operation."));
        var prepared = PrepareRevise(expectedRevision, change);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        RaiseEvent(new InformationAssetRevisionRecorded(_tenantId, Id, _revision + 1, prepared.Value!, actor,
            changedAt, operation, firstResponse));
        return Result.Success;
    }

    public Result Classify(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        string classification, ActorReference actor, DateTimeOffset at) =>
        ChangeNamed(operation, firstResponse, expectedRevision,
            value => value with { Classification = classification },
            value => new InformationAssetClassified(_tenantId, Id, _revision + 1, value,
                _content!.Classification, value.Classification, actor, at, operation, firstResponse));

    public Result ChangeDetails(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        string name, string retentionReference, string? description, ActorReference actor, DateTimeOffset at) =>
        ChangeNamed(operation, firstResponse, expectedRevision,
            value => value with { Name = name, RetentionReference = retentionReference, Description = description },
            value => new InformationAssetDetailsChanged(_tenantId, Id, _revision + 1, value, actor, at, operation, firstResponse));

    public Result ChangeOwner(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        Uuid ownerPersonId, ActorReference actor, DateTimeOffset at) =>
        ChangeNamed(operation, firstResponse, expectedRevision,
            value => value with { OwnerPersonId = ownerPersonId },
            value => new InformationAssetOwnerChanged(_tenantId, Id, _revision + 1, value,
                _content!.OwnerPersonId, ownerPersonId, actor, at, operation, firstResponse));

    public Result Retire(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        string reason, DateTimeOffset effectiveAt, ActorReference actor, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Retiring an information asset requires a reason of at most 2000 characters."));
        return ChangeNamed(operation, firstResponse, expectedRevision, value => value with { Lifecycle = "retired" },
            value => new InformationAssetRetired(_tenantId, Id, _revision + 1, value, effectiveAt, reason.Trim(),
                actor, at, operation, firstResponse));
    }

    public Result<InformationAssetContent> PrepareNamedChange(long expectedRevision,
        Func<InformationAssetContent, InformationAssetContent> change)
    {
        var prepared = PrepareRevise(expectedRevision, change);
        return prepared.IsSuccess && prepared.Value == _content
            ? Result<InformationAssetContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The information asset change must differ from its current facts."))
            : prepared;
    }

    Result ChangeNamed(OperationProvenance operation, ImmutableContentRef firstResponse, long expectedRevision,
        Func<InformationAssetContent, InformationAssetContent> change, Func<InformationAssetContent, DomainEvent> fact)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An information asset change requires its own logical operation."));
        var prepared = PrepareNamedChange(expectedRevision, change);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error!);
        RaiseEvent(fact(prepared.Value!));
        return Result.Success;
    }

    void ApplyNamed<T>(T fact) where T : DomainEvent, IInformationAssetFact
    {
        if (fact.TenantId != _tenantId || fact.InformationAssetId != Id || fact.Operation.Owner != Stream ||
            fact.Revision != _revision + 1 || (fact is InformationAssetRecorded) == _created ||
            TechnologyInventoryRules.Validate(fact.Content) is not null)
            throw new InvalidOperationException("An information asset fact belongs to its owner at the next revision.");
        var validChange = fact switch
        {
            InformationAssetRecorded => fact.Content.Lifecycle == TechnologyInventoryRules.Active,
            InformationAssetClassified classified => _content!.Lifecycle == TechnologyInventoryRules.Active &&
                classified.PreviousClassification == _content.Classification &&
                classified.Classification != classified.PreviousClassification &&
                fact.Content == _content with { Classification = classified.Classification },
            InformationAssetOwnerChanged changed => _content!.Lifecycle == TechnologyInventoryRules.Active &&
                changed.PreviousOwnerPersonId == _content.OwnerPersonId && changed.OwnerPersonId != changed.PreviousOwnerPersonId &&
                fact.Content == _content with { OwnerPersonId = changed.OwnerPersonId },
            InformationAssetDetailsChanged => _content!.Lifecycle == TechnologyInventoryRules.Active &&
                fact.Content != _content && fact.Content.Classification == _content.Classification &&
                fact.Content.OwnerPersonId == _content.OwnerPersonId && fact.Content.Lifecycle == _content.Lifecycle,
            InformationAssetRetired retired => _content!.Lifecycle == TechnologyInventoryRules.Active &&
                !string.IsNullOrWhiteSpace(retired.Reason) && retired.Reason.Length <= 2000 &&
                fact.Content == _content with { Lifecycle = TechnologyInventoryRules.Retired },
            _ => false
        };
        if (!validChange)
            throw new InvalidOperationException("A named information asset fact changes only the named business fields.");
        Acceptance.Observe(fact);
        _created = true;
        _revision = fact.Revision;
        _content = fact.Content;
    }

    static Result<InformationAssetContent> Failure(CommandFailure failure) =>
        Result<InformationAssetContent>.Failure(failure.ToRequestError());
}
