using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
namespace Bdgrz.Compliance.Features.Remediation.Reactors;

/// <summary>Settles every source use of the exact review fenced by a committed proposal withdrawal.</summary>
sealed partial class FindingClosureWithdrawalReactor(IProjectionCheckpointStore checkpoints,
    IImmutableContentStore content, ISourceFactResolver sources, FindingClosureVerifier verifier, FindingClosureFences fences)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(FindingAggregate.Area), WorkloadName),
        IReactorHandler<FindingClosureWithdrawn>
{
    public const string WorkloadName = "FindingClosureWithdrawalV1";

    public async ValueTask<Result> HandleAsync(IReactorContext<FindingClosureWithdrawn> context, CancellationToken ct)
    {
        var withdrawn = context.Trigger;
        var owner = new FindingAggregate(withdrawn.TenantId, withdrawn.ProgramId, withdrawn.FindingId);
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context, owner.Stream, ct).ConfigureAwait(false);
        if (withdrawn.CanceledReview is not { } review)
            return Result.Success;
        var source = NativeFactReference.Of(context.Source.Stream, withdrawn);
        var closure = await verifier.VerifyTerminalAsync(source, review.Intent, review.RootOperationId,
            Uuid.CreateVersion5(withdrawn.Metadata.EventId, nameof(FindingClosureWithdrawalReactor)), ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return Result.Failure(closure.Error);
        var settled = await fences.SettleCanceledAsync(closure, source, review.Admission, review.Intent, context, ct).ConfigureAwait(false);
        return settled.IsSuccess ? Result.Success : Result.Failure(settled.Error!);
    }
}
