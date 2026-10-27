using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>Acquires or re-pins the location name claim a pending change needs; another holder is a definite refusal.</summary>
public sealed class ClaimLocationNameHandler(NamedRecordReceivers receivers)
    : IRequestHandler<ClaimLocationName, ReceiverOutcome>
{
    /// <inheritdoc />
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ClaimLocationName> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await receivers.VerifyAsync(request.Source, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        return await receivers.ClaimAsync(LocationNames.Descriptor, new NameChangeStep(request.CommandId,
            request.Target, request.OperationId, request.Generation, request.ExpectedRevision, request.Intent,
            request.RecordId), request.NameKind, request.NormalizedName, verified.Value!,
            (LocationNameClaim claim, ClaimAttempt attempt) => claim.Claim(attempt.Operation, attempt.Holder,
                attempt.ExpectedRevision, attempt.Generation, attempt.Intent), context, ct).ConfigureAwait(false);
    }
}
