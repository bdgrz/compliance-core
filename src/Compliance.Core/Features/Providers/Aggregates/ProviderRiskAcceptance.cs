using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Aggregates;

/// <summary>
///     One immutable accepted-risk link, materialized from its admission on the gap. Its identity is the admitting
///     command id. The owner never changes the gap and never decides closure; it keeps the admission revision and
///     instant even if the gap closes or the acceptance expires later.
/// </summary>
public sealed class ProviderRiskAcceptance : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _linkId;
    ProviderCoverageGapRiskAcceptanceLinked? _linked;

    /// <summary>Opens the link, selecting the logical operation to resolve.</summary>
    public ProviderRiskAcceptance(Uuid tenantId, Uuid linkId, Uuid selectedLogicalId = default)
        : base(ProviderAssuranceStreams.RiskAcceptanceIdentity(tenantId, linkId),
            ProviderAssuranceStreams.RiskAcceptance(tenantId, linkId))
    {
        _tenantId = tenantId;
        _linkId = linkId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ProviderCoverageGapRiskAcceptanceLinked>(linked =>
        {
            Acceptance.Observe(linked);
            if (_linked is not null || linked.TenantId != _tenantId || linked.RequestId != _linkId ||
                linked.Operation.Owner != Stream || linked.Operation.RootOperationId != _linkId ||
                linked.Revision < 2)
                throw new InvalidOperationException("A link is materialized once, by its own admitting command.");
            _linked = linked;
        });
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The link this owner holds.</summary>
    public Uuid LinkId => _linkId;

    /// <summary>The materialized link, once recorded.</summary>
    public ProviderCoverageGapRiskAcceptanceLinked? Linked => _linked;

    /// <summary>Materializes the immutable link under the receiver command that serves the admission.</summary>
    public Result Materialize(OperationProvenance operation, Uuid gapId, Uuid providerId, long gapAdmissionRevision,
        ImmutableContentRef acceptanceContent, ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.RootOperationId != _linkId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A link is materialized on its own stream for the command that admitted it."));
        if (_linked is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk acceptance link is already materialized."));
        RaiseEvent(new ProviderCoverageGapRiskAcceptanceLinked(_tenantId, gapId, providerId, _linkId,
            gapAdmissionRevision, acceptanceContent, operation, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>The provenance of a receiver operation: it copies the root header of the frozen intent.</summary>
    public OperationProvenance Provenance(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, DateTimeOffset acceptedAt) =>
        new(commandId, header.RootOperationId, causationEventId, commandKind, header.OriginalActor, Stream,
            header.Episode, header.OriginalExpectedRevision, header.AuthoredIntentSha256, acceptedAt, Stream, Stream);
}
