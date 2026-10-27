using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>
///     One immutable personal management acknowledgement, identified by its acknowledgement. It holds the single
///     materialized <see cref="EngagementManagementAcknowledged" /> fact, which carries the frozen intent it was
///     computed from. The acknowledgement is never rewritten and never approves production acceptance by itself.
/// </summary>
public sealed class EngagementManagementAcknowledgement : Aggregate
{
    /// <summary>Stream area of every management acknowledgement.</summary>
    public const string Area = "engagement-management-acknowledgements";

    EngagementManagementAcknowledged? _acknowledged;

    /// <summary>Opens the acknowledgement of one client, selecting the logical operation to resolve.</summary>
    public EngagementManagementAcknowledgement(Uuid tenantId, Uuid acknowledgementId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(EngagementManagementAcknowledgement), tenantId, acknowledgementId),
            new EventStreamAddress(tenantId.ToString(), Area, acknowledgementId.ToString()))
    {
        TenantId = tenantId;
        AcknowledgementId = acknowledgementId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<EngagementManagementAcknowledged>(acknowledged =>
        {
            Acceptance.Observe(acknowledged);
            if (_acknowledged is not null || acknowledged.TenantId != TenantId ||
                acknowledged.OperationId != AcknowledgementId || acknowledged.Operation.Owner != Stream ||
                acknowledged.Revision != 1 || acknowledged.OperationResult is null)
                throw new InvalidOperationException(
                    "An acknowledgement is materialized exactly once, for its own acknowledgement on its own stream.");
            _acknowledged = acknowledged;
        });
    }

    /// <summary>The client tenant of the acknowledgement.</summary>
    public Uuid TenantId { get; }

    /// <summary>The acknowledgement this owner materializes.</summary>
    public Uuid AcknowledgementId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The materialized acknowledgement fact, once recorded.</summary>
    public EngagementManagementAcknowledged? Acknowledged => _acknowledged;

    /// <summary>Records the acknowledgement computed from the frozen intent; a second one is never recorded.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef acknowledgementContent,
        ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(acknowledgementContent);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An acknowledgement is recorded on the decision owner of its acknowledgement."));
        if (_acknowledged is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This acknowledgement is already immutable."));
        RaiseEvent(new EngagementManagementAcknowledged(TenantId, AcknowledgementId, acknowledgementContent, operation,
            1, intent, null, operationResult));
        return Result.Success;
    }
}
