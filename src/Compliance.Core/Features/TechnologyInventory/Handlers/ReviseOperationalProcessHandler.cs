using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Admits a revision of an operational process at its expected revision as the first native fact on the
///     process's own owner. <c>CommandId</c> is the logical identity, so an exact retry replays the original 202 after
///     any later change.
/// </summary>
public sealed class ReviseOperationalProcessHandler(NamedRecordAdmission admission,
    TechnologyInventoryReferences references)
    : IRequestHandler<ReviseOperationalProcess, OperationAcceptedResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<ReviseOperationalProcess> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.OperationalProcessId == Uuid.Empty ||
            request.TenantId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, operational process identity and tenant are required."));
        if (request.ExpectedRevision < 1)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected operational process revision must be positive."));
        return await admission.AdmitAsync(OperationalProcessNames.Descriptor, context,
            new NamedRecordAdmissionRequest(request.TenantId, request.OperationalProcessId, request.CommandId,
                false, request.ExpectedRevision, TechnologyInventoryActor.From(context),
                AuthoredIntentDigest.Compute(request), token => OperationalProcessContents.ResolveAsync(references,
                    request.TenantId, new OperationalProcessContent(request.Name, request.Purpose,
                        request.OwnerPersonId, request.Inputs, request.Outputs, request.Lifecycle), token)),
            (OperationalProcess record, AcceptAttempt change) => record.AcceptChange(change.Operation, change.Create,
                change.ExpectedRevision, change.Intent, change.FirstResponse), ct)
            .ConfigureAwait(false);
    }
}
