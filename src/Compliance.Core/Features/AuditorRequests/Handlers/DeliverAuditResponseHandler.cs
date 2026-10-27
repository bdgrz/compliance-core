using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Records the intentional delivery of the exact response version. When the request required an independent review
///     (frozen in the admitted draft), the version must be approved; a delivery receipt is immutable.
/// </summary>
sealed class DeliverAuditResponseHandler(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock) : IRequestHandler<DeliverAuditResponse>
{
    const string CommandKind = "DeliverAuditResponse";

    public async ValueTask<Result> HandleAsync(IRequestContext<DeliverAuditResponse> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Delivering an audit response requires a personal HTTP actor.");
        if (!AuditorResponseVersionNumber.TryGet(request.Version, out var versionNumber))
            return AuditorCommands.Validation("The audit response version is invalid.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ResponseId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return AuditorCommands.Validation(
                "A delivery requires a command id, a response and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = new AuditorResponse(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId, request.CommandId);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? delivery = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        var requiresReview = true;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(AuditorResponse owner, CancellationToken token)
        {
            var policy = await AuditorCommands.RequiresReviewAsync(content, request.TenantId, owner, token)
                .ConfigureAwait(false);
            if (!policy.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(policy.Error!);
            requiresReview = policy.Value;
            var prepared = owner.PrepareDeliver(request.ExpectedRevision, request.Version, requiresReview);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (AuditorRequestRules.Delivery(request.Recipient, request.DeliveryReference) is not { } receipt)
                return AuditorCommands.Validation<FirstResponseDescriptor>("Intentional response delivery requires a recipient and delivery reference.");
            delivery = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.DeliverySchema,
                new AuditorResponseDeliveryView(versionNumber, receipt.Recipient, receipt.Reference, actorMemberId,
                    actorDisplay, now), token).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorResponse>(binding,
            () => new AuditorResponse(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId,
                request.CommandId),
            auditorResponse => auditorResponse.Acceptance,
            PrepareAsync,
            (AuditorResponse auditorResponse, ImmutableContentRef frozen) => auditorResponse.Deliver(provenance,
                request.Version, requiresReview, delivery!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
