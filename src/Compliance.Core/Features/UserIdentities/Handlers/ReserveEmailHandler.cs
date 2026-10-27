using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

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
