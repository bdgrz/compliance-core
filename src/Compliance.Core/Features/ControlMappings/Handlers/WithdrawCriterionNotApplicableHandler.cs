using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.ControlMappings.Handlers;

/// <summary>
///     Restores an accepted not-applicable criterion to coverage while keeping its history. The withdrawal names the
///     exact accepted decision, so a withdrawal prepared against an older decision cannot remove a newer accepted
///     one, and it is checked against the revision of that criterion line only.
/// </summary>
public sealed class WithdrawCriterionNotApplicableHandler(NativeOperationExecutor operations,
    ApplicabilityReader applicability, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<WithdrawCriterionNotApplicable>
{
    const string CommandKind = "WithdrawCriterionNotApplicable";

    public ValueTask<Result> HandleAsync(IRequestContext<WithdrawCriterionNotApplicable> context,
        CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result> HandleCoreAsync(IRequestContext<WithdrawCriterionNotApplicable> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.DecisionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A withdrawal requires a command id, a decision and a non-negative expected revision."));
        if (await applicability.LocateAsync(request.TenantId, request.ProgramId, request.DecisionId, ct)
                .ConfigureAwait(false) is not { } criterion)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criterion applicability decision was not found."));

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var line = await applicability.ReadLineAsync(request.TenantId, request.ProgramId, criterion.EditionId,
            criterion.CriterionIdentifier, request.CommandId, ct).ConfigureAwait(false);
        var provenance = RootOperation.Provenance(request.CommandId, CommandKind, line.Stream, member,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        MappingLinePrepared? prepared = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<CriterionApplicability>(binding,
            () => new CriterionApplicability(request.TenantId, request.ProgramId, criterion.EditionId, criterion.CriterionIdentifier, request.CommandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var rules = await applicability.ComposeAsync(owner, token).ConfigureAwait(false);
                FirstResponseDescriptor? response = null;
                ImmutableContentRef? result = null;
                var guard = rules.PrepareWithdrawal(request.DecisionId, request.ExpectedRevision, request.Rationale);
                if (!guard.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                (response, result) = await NativeContent.FreezeUnitAsync(content, request.TenantId, token)
                    .ConfigureAwait(false);

                var applied = rules.Withdraw(provenance, request.DecisionId, request.Rationale, member, now);
                if (!applied.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(applied.Error!);
                prepared = await MappingLineContent.PrepareAsync(content, owner, request, rules.Changes[^1],
                    MappingLineContent.Current(rules), MappingLineContent.Header(rules), result!, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response!);
            }, (owner, frozen) => owner.Apply(prepared ?? throw new InvalidOperationException("The native line was not prepared."), frozen)),
            context, ct).ConfigureAwait(false);
        return NativeContent.UnitOutcome(outcome);
    }
}
