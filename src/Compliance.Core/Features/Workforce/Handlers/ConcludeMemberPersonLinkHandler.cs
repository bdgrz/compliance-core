using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ConcludeMemberPersonLinkHandler(ISourceFactResolver sources, IImmutableContentStore content,
    PersonMemberLinks links) : IRequestHandler<ConcludeMemberPersonLink>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ConcludeMemberPersonLink> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor) || request.Source is null || request.TenantId == Uuid.Empty ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(ConcludeMemberPersonLink)))
            return Denied();
        if (request.Released)
        {
            var (_, fact, _) = await SourceFactProofs.BuildAsync<PersonUnlinkedFromMember>(sources, content,
                request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
            if (fact.Header.TenantId != request.TenantId || request.Source.Stream != new Person(request.TenantId, fact.Header.PersonId).Stream)
                return Denied();
            return await links.ConcludeAsync(context, request.TenantId, fact.Link.UserId, fact, true, ct).ConfigureAwait(false);
        }
        else
        {
            var (_, fact, _) = await SourceFactProofs.BuildAsync<PersonLinkedToMember>(sources, content,
                request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
            if (fact.Header.TenantId != request.TenantId || request.Source.Stream != new Person(request.TenantId, fact.Header.PersonId).Stream)
                return Denied();
            return await links.ConcludeAsync(context, request.TenantId, fact.Link.UserId, fact, false, ct).ConfigureAwait(false);
        }
    }

    static Result Denied() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "A member association can only conclude from its exact committed Person fact."));
}
