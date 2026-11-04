using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>The Member admits uniqueness before the Person records its link; exact committed facts conclude that admission.</summary>
public sealed class PersonMemberLinks(PersonMemberLinkAccess access, IAggregateReader reader, IDomainEventReader events,
    PersonReader people, PersonOperations persons, NativeOperationExecutor operations, IImmutableContentStore content,
    ISourceFactResolver sources, TimeProvider clock)
{
    public async ValueTask<Result<UnitResult>> LinkAsync(IRequestContext<LinkPersonToMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var allowed = await access.RequireAsync(context, request.TenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<UnitResult>.Failure(allowed.Error);
        if (request.TenantId == Uuid.Empty || request.CommandId == Uuid.Empty || request.PersonId == Uuid.Empty || request.UserId == Uuid.Empty)
            return Invalid();
        var person = await reader.HydrateAsync(new Person(request.TenantId, request.PersonId, request.CommandId), ct).ConfigureAwait(false);
        if (!person.IsCreated)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.NotFound, "The person was not found."));
        var candidate = RootOperation.Provenance(request.CommandId, nameof(LinkPersonToMember), person.Stream,
            WorkforceActor.From(context), null, AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
        if (person.Acceptance.Decide(AcceptedOperationBinding.Of(candidate)) == NativeAcceptanceDecision.Conflict)
            return Result<UnitResult>.Failure(AcceptedAppendResult.ConflictError);
        var member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId, selectedCommandId: request.CommandId), ct).ConfigureAwait(false);
        MemberPersonLinkAdmitted admission;
        if (person.Acceptance.SelectedFact is PersonLinkedToMember prior)
        {
            var original = await ProofAsync<MemberPersonLinkAdmitted>(member.Stream, prior.Link.AdmissionEventId, ct).ConfigureAwait(false);
            admission = original.Facts;
        }
        else
        {
            var operation = RootOperation.Provenance(request.CommandId, nameof(LinkPersonToMember), member.Stream,
                WorkforceActor.From(context), null, AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
            long generation = 0;
            var accepted = await operations.ExecuteAsync(new NativeOperation<Member>(AcceptedOperationBinding.Of(operation),
                () => new Member(request.TenantId, request.UserId, selectedCommandId: request.CommandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    if (!owner.IsRegistered || owner.Affiliation == "firm_staff")
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                            "The tenant workforce member was not found."));
                    generation = owner.PersonLinkGeneration;
                    return Result<FirstResponseDescriptor>.Success((await NativeContent.FreezeUnitAsync(content,
                        request.TenantId, token).ConfigureAwait(false)).Response);
                }, (owner, frozen) => owner.RequestPersonLink(operation, frozen, request.PersonId, generation)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<UnitResult>.Failure(accepted.Error);
            if (accepted.Value is AcceptedAppendResult.Conflict)
                return Result<UnitResult>.Failure(AcceptedAppendResult.ConflictError);
            member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId, selectedCommandId: request.CommandId), ct).ConfigureAwait(false);
            if (member.Acceptance.SelectedFact is not MemberPersonLinkAdmitted admitted)
                return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The member is already linked or has another pending person admission."));
            admission = admitted;
            if (member.PersonLinkAdmission?.Metadata.EventId != admission.Metadata.EventId ||
                (member.PendingPersonId ?? member.LinkedPersonId) != request.PersonId)
                return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "This member admission has already been released."));
        }
        if (admission.PersonId != request.PersonId || admission.UserId != request.UserId || admission.LinkId != request.CommandId)
            return Result<UnitResult>.Failure(AcceptedAppendResult.ConflictError);
        var proof = await ProofAsync<MemberPersonLinkAdmitted>(member.Stream, admission.Metadata.EventId, ct).ConfigureAwait(false);
        var changed = await persons.LinkMemberAsync(context, proof, ct).ConfigureAwait(false);
        if (!changed.IsSuccess)
            return changed;
        person = await reader.HydrateAsync(new Person(request.TenantId, request.PersonId, request.CommandId), ct).ConfigureAwait(false);
        if (person.Acceptance.SelectedFact is not PersonLinkedToMember linked)
            throw new InvalidOperationException("The accepted link has no exact Person source fact.");
        var conclusion = await ConcludeAsync(context, request.TenantId, request.UserId, linked, false, ct).ConfigureAwait(false);
        return conclusion.IsSuccess ? changed : Result<UnitResult>.Failure(conclusion.Error);
    }

    public async ValueTask<Result<UnitResult>> UnlinkAsync(IRequestContext<UnlinkPersonFromMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var allowed = await access.RequireAsync(context, request.TenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<UnitResult>.Failure(allowed.Error);
        if (request.TenantId == Uuid.Empty || request.CommandId == Uuid.Empty || request.PersonId == Uuid.Empty || request.UserId == Uuid.Empty)
            return Invalid();
        var person = await reader.HydrateAsync(new Person(request.TenantId, request.PersonId, request.CommandId), ct).ConfigureAwait(false);
        PersonMemberLink? link;
        if (person.Acceptance.SelectedFact is PersonUnlinkedFromMember prior)
            link = prior.Link;
        else
        {
            var snapshot = await people.ReadAsync(person, ct).ConfigureAwait(false);
            link = snapshot.View?.MemberLinks?.SingleOrDefault(value => value.UserId == request.UserId);
        }
        if (link is null || link.UserId != request.UserId)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.NotFound, "The person member link was not found."));
        var changed = await persons.UnlinkMemberAsync(context, link, ct).ConfigureAwait(false);
        if (!changed.IsSuccess)
            return changed;
        person = await reader.HydrateAsync(new Person(request.TenantId, request.PersonId, request.CommandId), ct).ConfigureAwait(false);
        if (person.Acceptance.SelectedFact is not PersonUnlinkedFromMember unlinked)
            throw new InvalidOperationException("The accepted unlink has no exact Person source fact.");
        var conclusion = await ConcludeAsync(context, request.TenantId, request.UserId, unlinked, true, ct).ConfigureAwait(false);
        return conclusion.IsSuccess ? changed : Result<UnitResult>.Failure(conclusion.Error);
    }

    public async ValueTask<Result> ConcludeAsync<T>(IRequestContext<T> context, Uuid tenant, Uuid user,
        DomainEvent fact, bool release, CancellationToken ct) where T : IRequestBase
    {
        var link = fact is PersonLinkedToMember linked ? linked.Link : ((PersonUnlinkedFromMember)fact).Link;
        var personId = fact is PersonLinkedToMember added ? added.Header.PersonId : ((PersonUnlinkedFromMember)fact).Header.PersonId;
        var member = await reader.HydrateAsync(new Member(tenant, user), ct).ConfigureAwait(false);
        // A later exact release/reassignment must never be undone by recovery of an older accepted command.
        if (member.PersonLinkAdmission?.Metadata.EventId != link.AdmissionEventId)
            return Result.Success;
        var command = Uuid.CreateVersion5(fact.Metadata.EventId, release ? "ReleaseMemberPersonLink" : "ConfirmMemberPersonLink");
        var operation = RootOperation.Provenance(command, release ? "ReleaseMemberPersonLink" : "ConfirmMemberPersonLink",
            member.Stream, ((Bdgrz.Compliance.Features.Workforce.Contracts.IPersonNativeFact)fact).Operation.Actor, null,
            ContentDigest.OfJson(link), ((Bdgrz.Compliance.Features.Workforce.Contracts.IPersonNativeFact)fact).Operation.AcceptedAt) with
        { RootOperationId = link.LinkId, CausationEventId = fact.Metadata.EventId };
        var stream = new Person(tenant, personId).Stream;
        var confirmation = release ? null : await ProofAsync<PersonLinkedToMember>(stream, fact.Metadata.EventId, ct).ConfigureAwait(false);
        var withdrawal = release ? await ProofAsync<PersonUnlinkedFromMember>(stream, fact.Metadata.EventId, ct).ConfigureAwait(false) : null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<Member>(AcceptedOperationBinding.Of(operation),
            () => new Member(tenant, user, selectedCommandId: command), owner => owner.Acceptance,
            async (_, token) => Result<FirstResponseDescriptor>.Success((await NativeContent.FreezeUnitAsync(content,
                tenant, token).ConfigureAwait(false)).Response),
            (owner, frozen) => release ? owner.ReleasePersonLink(operation, frozen, withdrawal!) :
                owner.ConfirmPersonLink(operation, frozen, confirmation!)), context, ct).ConfigureAwait(false);
        return !accepted.IsSuccess ? Result.Failure(accepted.Error) : accepted.Value is AcceptedAppendResult.Conflict
            ? Result.Failure(AcceptedAppendResult.ConflictError) : Result.Success;
    }

    async Task<VerifiedSourceProof<T>> ProofAsync<T>(EventStreamAddress stream, Uuid eventId, CancellationToken ct) where T : DomainEvent
    {
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != eventId)
                continue;
            var (proof, _, _) = await SourceFactProofs.BuildAsync<T>(sources, content, stream.Realm,
                NativeFactReference.Of(stream, record.Event), record.ResourceOffset, ct).ConfigureAwait(false);
            return await sources.VerifyAsync(proof, ct).ConfigureAwait(false);
        }
        throw new SourceFactVerificationException("The exact committed member-link source is not visible yet.");
    }

    static Result<UnitResult> Invalid() => Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation,
        "A member link requires tenant, person, user and command identities."));
}
