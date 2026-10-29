using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;

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
    TrainingRequirementDefined? _latest;

    /// <summary>Opens the requirement with a known id, selecting the logical operation to resolve.</summary>
    public TrainingRequirement(Uuid tenantId, Uuid requirementId, Uuid selectedLogicalId = default)
        : base(requirementId, new EventStreamAddress(tenantId.ToString(), Area, requirementId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TrainingRequirementDefined>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>True once the first version was defined.</summary>
    public bool IsDefined => _latest is not null;

    /// <summary>The program of the requirement, once defined.</summary>
    public Uuid ProgramId => _latest?.ProgramId ?? Uuid.Empty;

    /// <summary>The immutable normalized identifier, once defined.</summary>
    public string Identifier => _latest?.Identifier ?? string.Empty;

    /// <summary>The logical version: the number of defined versions.</summary>
    public long CurrentVersion => _latest?.Version ?? 0;

    /// <summary>The latest defined version fact, once defined.</summary>
    public TrainingRequirementDefined? Latest => _latest;

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
        if (!TrainingRequirementIdentity.IsValidIdentifier(normalizedIdentifier) ||
            TrainingRequirementIdentity.NormalizeIdentifier(normalizedIdentifier) != normalizedIdentifier ||
            TrainingRequirementIdentity.IdFor(_tenantId, programId, normalizedIdentifier) != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The requirement id is derived from its tenant, program and normalized identifier."));
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
        RaiseEvent(new TrainingRequirementDefined(_tenantId, ProgramId, Id, Identifier, CurrentVersion + 1,
            content, contentSha256, operation.LogicalId, actor, changedAt, operation, firstResponse,
            operationResult));
        return Result.Success;
    }

    void Apply(TrainingRequirementDefined ev)
    {
        Acceptance.Observe(ev);
        var first = _latest is null;
        if (ev.TenantId != _tenantId || ev.RequirementId != Id || ev.Version != CurrentVersion + 1 ||
            ev.FirstResponse is null || ev.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation,
                first ? nameof(DefineTrainingRequirement) : nameof(ReviseTrainingRequirement),
                first ? null : (ulong)CurrentVersion) || ev.DefineRequestId != ev.Operation.LogicalId ||
            ev.Actor != ev.Operation.Actor || ev.ChangedAt != ev.Operation.AcceptedAt ||
            first && (ev.Operation.LogicalId != Id ||
                TrainingRequirementIdentity.IdFor(ev.TenantId, ev.ProgramId, ev.Identifier) != Id) ||
            !first && (ev.Identifier != Identifier || ev.ProgramId != ProgramId))
            throw new InvalidOperationException("A requirement defines sequential native versions of its own normalized identifier.");
        _latest = ev;
    }
}
