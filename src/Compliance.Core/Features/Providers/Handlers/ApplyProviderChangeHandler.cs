using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>Applies the accepted provider change once its needed claims are held; a canceled change is a terminal no-op.</summary>
public sealed class ApplyProviderChangeHandler(NamedRecordReceivers receivers)
    : IRequestHandler<ApplyProviderChange, ReceiverOutcome>
{
    /// <inheritdoc />
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyProviderChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await receivers.VerifyAsync(request.Source, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        return await receivers.ApplyAsync(ProviderNames.Descriptor, new NameChangeStep(request.CommandId,
            request.Target, request.OperationId, request.Generation, request.ExpectedRevision, request.Intent,
            request.RecordId), verified.Value!,
            (Provider record, ApplyAttempt attempt) => record.ApplyChange(attempt.Operation, attempt.Intent,
                attempt.Content, attempt.Actor, attempt.ChangedAt, attempt.Result), context, ct)
            .ConfigureAwait(false);
    }
}
