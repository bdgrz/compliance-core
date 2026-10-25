using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Reserves an address for a user. The command identity is the caller's: an exact retry replays the original
///     acceptance even after later revisions, and a changed user or revision on the same identity conflicts. An
///     address already reserved for the same user is a success that appends nothing.
/// </summary>
public sealed class ReserveEmailHandler(
    NativeOperationExecutor operations,
    IAggregateReader reader,
    IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<ReserveEmail>
{
    const string CommandKind = nameof(ReserveEmail);

    public async ValueTask<Result> HandleAsync(IRequestContext<ReserveEmail> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));
        if (request.CommandId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A command id is required."));
        if (EmailAddressOperations.ActorOf(context.Actor) is not { } actor)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The caller identity is not recognized."));

        var current = await reader.HydrateAsync(Hydrate(normalized, request), ct).ConfigureAwait(false);
        var provenance = EmailAddressOperations.Provenance(request.CommandId, CommandKind, actor, current.Stream,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request with { EmailAddress = normalized }),
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None && current.IsReservedFor(request.UserId))
        {
            // Already reserved for this user by an earlier operation: nothing to accept, the revision still fences it.
            return current.PrepareReserve(request.UserId, request.ExpectedRevision);
        }

        var preparation = new IdentityOwnerPreparation(current);
        return EmailAddressOperations.Of(await operations.ExecuteAsync(new NativeOperation<EmailAddress>(binding,
            () => Hydrate(normalized, request), address => address.Acceptance,
            (address, cancel) => preparation.PrepareAsync(address,
                address.PrepareReserve(request.UserId, request.ExpectedRevision),
                bodyCancel => EmailAddressOperations.UnitResponseAsync(content, bodyCancel), cancel),
            (EmailAddress address, ImmutableContentRef frozen) => preparation.Apply(address,
                () => address.Reserve(provenance, frozen, request.UserId, request.ExpectedRevision))), context, ct).ConfigureAwait(false));
    }

    static EmailAddress Hydrate(string normalized, ReserveEmail request) =>
        new(normalized, request.CommandId, CommandKind);
}
