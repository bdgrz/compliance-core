using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>Applies the accepted operational process change once its needed claims are held; a canceled change is a terminal no-op.</summary>
public sealed class ApplyOperationalProcessChangeHandler(NamedRecordReceivers receivers)
    : IRequestHandler<ApplyOperationalProcessChange, ReceiverOutcome>
{
    /// <inheritdoc />
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyOperationalProcessChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = request.Source switch
        {
            ApplyOperationalProcessChangeSourceProof.ClaimedProof claimed =>
                await receivers.VerifyAsync(claimed.Fact, ct).ConfigureAwait(false),
            ApplyOperationalProcessChangeSourceProof.AcceptedProof accepted =>
                await receivers.VerifyAsync(accepted.Fact, ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unknown apply source proof."),
        };
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        return await receivers.ApplyAsync(OperationalProcessNames.Descriptor, new NameChangeStep(request.CommandId,
            request.Target, request.OperationId, request.Generation, request.ExpectedRevision, request.Intent,
            request.RecordId), verified.Value!,
            (OperationalProcess record, ApplyAttempt attempt) => record.ApplyChange(attempt.Operation, attempt.Intent,
                attempt.Content, attempt.Actor, attempt.ChangedAt, attempt.Result), context, ct)
            .ConfigureAwait(false);
    }
}
