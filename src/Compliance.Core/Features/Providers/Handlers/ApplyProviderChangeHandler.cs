using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
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
