using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
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
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Events;
namespace Bdgrz.Compliance.Features.Readiness.Aggregates;

/// <summary>
///     One immutable readiness assessment: the rule version, as-of time, exact inputs, findings and gaps computed from
///     the frozen inputs of one run. The stream holds exactly one fact at the assessment's own revision one; a
///     computation is a receiver fact of the run process and never a public command. Reducers never read content or
///     another owner.
/// </summary>
public sealed class ReadinessAssessment : Aggregate
{
    /// <summary>Stream area of every readiness assessment (the realm is the tenant).</summary>
    public const string Area = "readiness-assessments";

    /// <summary>The identity owner type of a readiness assessment.</summary>
    public const string OwnerType = "ReadinessAssessment";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _assessmentId;
    ReadinessAssessmentRecorded? _recorded;

    /// <summary>Opens one assessment, selecting the logical operation to resolve.</summary>
    public ReadinessAssessment(Uuid tenantId, Uuid programId, Uuid assessmentId, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, assessmentId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId, assessmentId).ToString()))
    {
        (_tenantId, _programId, _assessmentId) = (tenantId, programId, assessmentId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ReadinessAssessmentRecorded>(Apply);
    }

    /// <summary>The identity of the assessment of a program.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid assessmentId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, assessmentId);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The assessment identity.</summary>
    public Uuid AssessmentId => _assessmentId;

    /// <summary>The program the assessment belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The assessment's own logical revision: zero before it is computed, one afterwards.</summary>
    public long Revision => _recorded is null ? 0 : 1;

    /// <summary>True once the assessment was computed.</summary>
    public bool IsRecorded => _recorded is not null;

    /// <summary>The recorded fact, once computed.</summary>
    public ReadinessAssessmentRecorded? Recorded => _recorded;

    /// <summary>Checks that the assessment can still be computed.</summary>
    public Result PrepareRecord() => _recorded is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The readiness assessment was already computed by another operation."));

    /// <summary>Records the computed assessment as a receiver fact at the assessment's revision one.</summary>
    public Result Record(OperationProvenance operation, string ruleVersion, DateTimeOffset asOf, Uuid? editionId,
        string inputFingerprint, ImmutableContentRef inputs, ImmutableContentRef findings, ImmutableContentRef gaps,
        ImmutableContentRef accessReviewScopes, Uuid runnerMemberId, ActorReference runBy, DateTimeOffset runAt,
        ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareRecord();
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A computation is recorded on the assessment's own stream."));
        RaiseEvent(new ReadinessAssessmentRecorded(_tenantId, _programId, 1, _assessmentId, ruleVersion, asOf,
            editionId, inputFingerprint, inputs, findings, gaps, accessReviewScopes, runnerMemberId, runBy, runAt,
            operation, intent, null, null));
        return Result.Success;
    }

    void Apply(ReadinessAssessmentRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is not null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.AssessmentId != _assessmentId || ev.Revision != 1 || ev.Operation.Owner != Stream)
            throw new InvalidOperationException(
                "An assessment is computed once, at revision one, on its own stream.");
        _recorded = ev;
    }
}
