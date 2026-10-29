using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>
///     One immutable historical preview of a client, identified by its evaluation. It holds the single materialized
///     <see cref="ClientIndependenceEvaluated" /> fact, which carries the frozen intent it was computed from. The
///     preview never grants production acceptance and is never rewritten.
/// </summary>
public sealed class ClientIndependenceDecision : Aggregate
{
    /// <summary>Stream area of every preview decision.</summary>
    public const string Area = "client-independence-decisions";

    readonly Uuid _tenantId;
    ClientIndependenceEvaluated? _evaluated;

    /// <summary>Opens the preview of one evaluation, selecting the logical operation to resolve.</summary>
    public ClientIndependenceDecision(Uuid tenantId, Uuid evaluationId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(ClientIndependenceDecision), tenantId, evaluationId),
            new EventStreamAddress(tenantId.ToString(), Area, evaluationId.ToString()))
    {
        _tenantId = tenantId;
        EvaluationId = evaluationId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ClientIndependenceEvaluated>(evaluated =>
        {
            Acceptance.Observe(evaluated);
            if (_evaluated is not null || evaluated.TenantId != _tenantId || evaluated.OperationId != EvaluationId ||
                evaluated.Operation.Owner != Stream || evaluated.Revision != 1 || evaluated.OperationResult is null)
                throw new InvalidOperationException(
                    "A preview is materialized exactly once, for its own evaluation of its own client.");
            _evaluated = evaluated;
        });
    }

    /// <summary>The evaluation this decision materializes.</summary>
    public Uuid EvaluationId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The materialized preview fact, once recorded.</summary>
    public ClientIndependenceEvaluated? Evaluated => _evaluated;

    /// <summary>Records the preview computed from the frozen intent; a second preview is never recorded.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef evaluationContent,
        ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(evaluationContent);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (_tenantId == Uuid.Empty || EvaluationId == Uuid.Empty || operation.LogicalId == Uuid.Empty ||
            operation.Owner != Stream || operation.RootOperationId != EvaluationId ||
            operation.CausationEventId is null ||
            operation.CommandKind != nameof(MaterializeClientIndependencePreview) ||
            operation.FirstOwner != new ClientServiceIndependence(_tenantId).Stream ||
            operation.AffectedOwner != Stream || operation.ExpectedRevisionOwner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A preview requires its own evaluation and the admitted client operation provenance."));
        if (_evaluated is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This evaluation already has its immutable preview."));
        RaiseEvent(new ClientIndependenceEvaluated(_tenantId, EvaluationId, evaluationContent, operation, 1, intent,
            null, operationResult));
        return Result.Success;
    }
}
