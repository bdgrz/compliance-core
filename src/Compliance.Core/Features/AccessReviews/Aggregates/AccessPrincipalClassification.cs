using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Aggregates;

/// <summary>
///     The classification decision history of one accepted principal, keyed by the population and the exact ordinal
///     provider subject id. Its logical revision is the contiguous number of decisions of this principal only, so two
///     principals never contend. The complete decision of each fact is verified immutable content; the rules over
///     it are decided by the caller and re-checked here against the revision. Classification never changes the
///     accepted raw population.
/// </summary>
public sealed class AccessPrincipalClassification : Aggregate
{
    /// <summary>Stream area of every principal classification (the realm is the tenant).</summary>
    public const string Area = "access-principal-classifications";

    /// <summary>The identity owner type of a principal classification.</summary>
    public const string OwnerType = "AccessPrincipalClassification";

    readonly Uuid _tenantId;
    readonly Uuid _populationId;
    readonly string _providerSubjectId;
    AccessPrincipalClassified? _latest;
    long _revision;

    /// <summary>Opens the classification of one principal, selecting the logical operation to resolve.</summary>
    public AccessPrincipalClassification(Uuid tenantId, Uuid populationId, string providerSubjectId,
        Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, populationId, providerSubjectId),
            new EventStreamAddress(tenantId.ToString(), Area,
                IdentityOf(tenantId, populationId, providerSubjectId).ToString()))
    {
        (_tenantId, _populationId, _providerSubjectId) = (tenantId, populationId, providerSubjectId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AccessPrincipalClassified>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The population this principal belongs to.</summary>
    public Uuid PopulationId => _populationId;

    /// <summary>The exact provider subject id.</summary>
    public string ProviderSubjectId => _providerSubjectId;

    /// <summary>The logical revision: the number of decisions recorded for this principal.</summary>
    public long Revision => _revision;

    /// <summary>The latest immutable decision fact; full history belongs to read composition.</summary>
    public AccessPrincipalClassified? Latest => _latest;

    /// <summary>The identity of one principal's classification, exact and ordinal in the provider subject id.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid populationId, string providerSubjectId)
    {
        ArgumentNullException.ThrowIfNull(providerSubjectId);
        return AggregateIdentity.Create(OwnerType, tenantId, populationId, providerSubjectId);
    }

    /// <summary>Checks the classification guard without raising anything.</summary>
    public Result PrepareClassify(long expectedRevision) => expectedRevision == Revision
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            $"The principal has {Revision} classification decisions; expected {expectedRevision}."));

    /// <summary>Records a decision as the root native fact of its operation.</summary>
    public Result Classify(OperationProvenance operation, ImmutableContentRef decisionContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(decisionContent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "A classification is a root operation of its own principal with an expected revision.");
        var prepared = PrepareClassify((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessPrincipalClassified(_tenantId, _populationId, _providerSubjectId, decisionContent,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(AccessPrincipalClassified ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.PopulationId != _populationId ||
            !string.Equals(ev.ProviderSubjectId, _providerSubjectId, StringComparison.Ordinal) ||
            ev.FirstResponse is null || ev.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(ClassifyAccessPrincipal), (ulong)_revision))
            throw new InvalidOperationException(
                "A classification follows the previous decision of its own principal.");
        _revision++;
        _latest = ev;
    }
}
