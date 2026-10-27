using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>
///     The receivers of the name-change processes run as the trusted process principal only; the tenant they act for is
///     the realm of their verified source and target, checked by each receiver against its proof.
/// </summary>
public sealed class NamedRecordReceiverAuthorizer : IRequestAuthorizer<INamedRecordReceiver>
{
    /// <inheritdoc />
    public ValueTask<Result> AuthorizeAsync(IRequestContext<INamedRecordReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Name-change receivers run only as the system process.")));
}
