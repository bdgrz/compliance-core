using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ApplyPersonMemberLinkHandler(IAggregateReader reader, ISourceFactResolver sources,
    IImmutableContentStore content, PersonOperations people) : IRequestHandler<ApplyPersonMemberLink>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyPersonMemberLink> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor) || request.Source is null || request.TenantId == Uuid.Empty ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(ApplyPersonMemberLink)))
            return Denied();
        var (proof, admission, _) = await SourceFactProofs.BuildAsync<MemberPersonLinkAdmitted>(sources, content,
            request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (admission.TenantId != request.TenantId || admission.PersonId != request.PersonId ||
            request.Source.Stream != Member.ForVerification(request.TenantId, admission.MemberId).Stream)
            return Denied();
        var member = await reader.HydrateAsync(new Member(request.TenantId, admission.UserId), ct).ConfigureAwait(false);
        if (member.PersonLinkAdmission?.Metadata.EventId != request.Source.EventId)
            return Result.Success;
        var verified = await sources.VerifyAsync(proof, ct).ConfigureAwait(false);
        var applied = await people.ApplyMemberLinkAsync(context, verified, ct).ConfigureAwait(false);
        return applied.IsSuccess ? Result.Success : Result.Failure(applied.Error);
    }

    static Result Denied() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "A person link can only apply its exact committed Member admission."));
}
