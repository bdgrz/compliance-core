using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     One training requirement, keyed by its stable requirement id: an immutable normalized identifier and a
///     sequential line of immutable content versions. The logical version counts the defined versions, so the
///     original creation stays recognizable after any number of revisions. The program catalog is a projection.
/// </summary>
public sealed class TrainingRequirement : Aggregate
{
    /// <summary>Stream area of every training requirement (the realm is the tenant).</summary>
    public const string Area = "training-requirements";

    /// <summary>The M0-D12 cadence every requirement carries.</summary>
    public const string Cadence = "hire_within_30_days_then_annual";

    readonly Uuid _tenantId;
    readonly List<TrainingRequirementDefined> _versions = [];

    /// <summary>Opens the requirement with a known id, selecting the logical operation to resolve.</summary>
    public TrainingRequirement(Uuid tenantId, Uuid requirementId, Uuid selectedLogicalId = default)
        : base(requirementId, new EventStreamAddress(tenantId.ToString(), Area, requirementId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TrainingRequirementDefined>(ev =>
        {
            Acceptance.Observe(ev);
            var first = _versions.Count == 0;
            if (ev.TenantId != _tenantId || ev.RequirementId != Id || ev.Operation.Owner != Stream ||
                ev.Version != _versions.Count + 1 || ev.FirstResponse is null ||
                first && TrainingRequirementIdentity.IdFor(ev.TenantId, ev.ProgramId, ev.Identifier) != Id ||
                !first && (ev.Identifier != _versions[0].Identifier || ev.ProgramId != _versions[0].ProgramId))
                throw new InvalidOperationException(
                    "A requirement defines sequential versions of its own normalized identifier.");
            _versions.Add(ev);
        });
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>True once the first version was defined.</summary>
    public bool IsDefined => _versions.Count > 0;

    /// <summary>The program of the requirement, once defined.</summary>
    public Uuid ProgramId => _versions.Count > 0 ? _versions[0].ProgramId : Uuid.Empty;

    /// <summary>The immutable normalized identifier, once defined.</summary>
    public string Identifier => _versions.Count > 0 ? _versions[0].Identifier : string.Empty;

    /// <summary>The logical version: the number of defined versions.</summary>
    public long CurrentVersion => _versions.Count;

    /// <summary>The latest defined version fact, once defined.</summary>
    public TrainingRequirementDefined? Latest => _versions.Count > 0 ? _versions[^1] : null;

    /// <summary>The fact that defined an exact version, or null.</summary>
    public TrainingRequirementDefined? FactOf(long version) =>
        version >= 1 && version <= _versions.Count ? _versions[(int)version - 1] : null;

    /// <summary>Every defined version in order.</summary>
    public IReadOnlyList<TrainingRequirementDefined> Versions => _versions;

    /// <summary>Checks the creation guards without raising anything.</summary>
    public Result PrepareDefine() => IsDefined
        ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The training requirement identifier already exists."))
        : Result.Success;

    /// <summary>Checks the revision guards without raising anything.</summary>
    public Result PrepareRevise(long expectedVersion)
    {
        if (!IsDefined)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The training requirement was not found."));
        return expectedVersion == CurrentVersion
            ? Result.Success
            : Result.Failure(VersionedRecordRules.StaleRevision("training requirement", CurrentVersion).ToRequestError());
    }

    /// <summary>Defines the first version as the root native fact of its operation.</summary>
    public Result Define(OperationProvenance operation, Uuid programId, string normalizedIdentifier,
        ImmutableContentRef content, string contentSha256, ActorReference actor, DateTimeOffset changedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.LogicalId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A definition is the root operation of its own requirement."));
        var prepared = PrepareDefine();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TrainingRequirementDefined(_tenantId, programId, Id, normalizedIdentifier, 1, content,
            contentSha256, operation.LogicalId, actor, changedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Defines the next version as the root native fact of its operation.</summary>
    public Result Revise(OperationProvenance operation, ImmutableContentRef content, string contentSha256,
        ActorReference actor, DateTimeOffset changedAt, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A revision is a root operation of its own requirement with an expected version."));
        var prepared = PrepareRevise((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        var first = _versions[0];
        RaiseEvent(new TrainingRequirementDefined(_tenantId, first.ProgramId, Id, first.Identifier, CurrentVersion + 1,
            content, contentSha256, operation.LogicalId, actor, changedAt, operation, firstResponse,
            operationResult));
        return Result.Success;
    }
}
