using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Admits a revision of a provider at its expected revision as the first native fact on the provider's own owner.
///     <c>CommandId</c> is the logical identity, so an exact retry replays the original 202 after any later change.
/// </summary>
public sealed class ReviseProviderHandler(NamedRecordAdmission admission, ProviderReferences references)
    : IRequestHandler<ReviseProvider, OperationAcceptedResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<ReviseProvider> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ProviderId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, provider identity and tenant are required."));
        if (request.ExpectedRevision < 1)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected provider revision must be positive."));
        if (ProviderRules.InputError(request.Content) is { } inputError)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation, inputError));
        return await admission.AdmitAsync(ProviderNames.Descriptor, context, new NamedRecordAdmissionRequest(
            request.TenantId, request.ProviderId, request.CommandId, false, request.ExpectedRevision,
            ProviderActor.From(context), AuthoredIntentDigest.Compute(request), token =>
                ProviderContents.ResolveAsync(references, request.TenantId, request.Content, token)),
            (Provider record, AcceptAttempt change) => record.AcceptChange(change.Operation, change.Create,
                change.ExpectedRevision, change.Intent, change.FirstResponse), ct)
            .ConfigureAwait(false);
    }
}
