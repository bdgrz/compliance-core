using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>
///     Admits owned corrective work on a finding. The complete action intent and the finding generation are frozen on
///     the finding before the 202 acknowledgment is frozen on the admission, so an exact retry replays the original
///     acknowledgment even after the finding changed; <c>IndependentChildMaterialization</c> then materializes the
///     action owner and the action enters its owner's work queue. A finding that is closing or closed admits no action.
/// </summary>
public sealed class AddCorrectiveActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AddCorrectiveAction, OperationAcceptedResponse>
{
    const string CommandKind = "AddCorrectiveAction";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<AddCorrectiveAction> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.ActionId == Uuid.Empty || request.ExpectedFindingRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A corrective action requires its own identity and a non-negative expected finding revision.");
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.ActionId);
        var provenance = FindingContent.Provenance(request.ActionId, CommandKind, snapshot.Stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), (ulong)request.ExpectedFindingRevision,
            FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var failure = FindingRules.ValidateAction(request.ActionId, request.Description, request.OwnerMemberId,
                request.DueOn);
            if (failure is not null)
                return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
            var prepared = owner.PrepareWrite(request.ExpectedFindingRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (owner.HasAdmitted(request.ActionId))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A corrective action identity cannot be reused."));
            if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.OwnerMemberId, token)
                    .ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(FindingCommands.InactiveOwner());
            var authored = await FindingContent.WriteAsync(content, request.TenantId,
                FindingContent.ActionAuthoredSchema, request with { Description = request.Description.Trim() }, token)
                .ConfigureAwait(false);
            var header = FindingContent.Header(provenance, now);
            intent = await FindingContent.WriteAsync(content, request.TenantId,
                ChildAdmissionIntent.Schema, new ChildAdmissionIntent(header, request.FindingId,
                    (ulong)owner.Generation, AdmittedChildKind.CorrectiveAction, request.ActionId, authored,
                    await RoutedFinding.EmptyProofsAsync(content, request.TenantId, request.ActionId, token)
                        .ConfigureAwait(false), (ulong)request.ExpectedFindingRevision + 1), token).ConfigureAwait(false);
            response = await FindingContent.AcceptedResponseAsync(content, request.TenantId, request.ActionId,
                RegisterFirmStaffHandler.StatusUrlTemplate, now, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.ActionId),
            finding => finding.Acceptance,
            PrepareAsync,
            (FindingAggregate finding, ImmutableContentRef acknowledgment) =>
                finding.AdmitAction(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
