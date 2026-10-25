using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Pure rule fixtures publish actual identified native facts; no legacy aggregate writer is retained.</summary>
static class PersonSeeds
{
    public static async Task AppendAsync(IServiceProvider provider, PersonRules source, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await AppendAsync(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), source, ct);
    }
    public static async Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, PersonRules source, CancellationToken ct = default)
    {
        foreach (var change in source.PendingEvents)
        {
            var logical = change is PersonRecorded ? source.Id : Uuid.CreateVersion4();
            var owner = await reader.HydrateAsync(new Person(source.TenantId, source.Id, logical), ct);
            var snapshot = await new PersonReader(reader, events, content).ReadAsync(owner, ct);
            var rules = snapshot.Rules;
            ActorReference actor;
            DateTimeOffset at;
            string kind;
            Result result;
            switch (change)
            {
                case PersonRecorded value:
                    actor = value.Actor; at = value.ChangedAt; kind = nameof(RecordPerson);
                    var created = rules.Record(value.DisplayName, value.WorkEmail, value.PersonalContact, actor, at);
                    result = created.IsSuccess ? Result.Success : Result.Failure(created.Error!);
                    break;
                case PersonRevised value:
                    actor = value.Actor; at = value.ChangedAt; kind = nameof(RevisePerson);
                    var revised = rules.Revise(value.Revision - 1, value.DisplayName, value.WorkEmail, value.PersonalContact, actor, at);
                    result = revised is null ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, revised.Message!));
                    break;
                case PersonMembershipCorrelated value:
                    actor = value.Actor; at = value.ChangedAt; kind = nameof(CorrelatePersonMembership);
                    var correlated = rules.CorrelateMembership(value.Revision - 1, value.UserId, actor, at);
                    result = correlated is null ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, correlated.Message!));
                    break;
                default: throw new InvalidOperationException("Another pure person fixture mutation.");
            }
            Assert.True(result.IsSuccess, result.Error?.Message);
            var operation = RootOperation.Provenance(logical, kind, owner.Stream, actor,
                change is PersonRecorded ? null : owner.Revision, ContentDigest.Sha256(change, change.GetType()), at) with { FirstOwner = owner.Stream };
            var prepared = await PersonContent.FreezeAsync(content, snapshot, rules, operation, ct);
            var frozen = await FirstResponses.FreezeAsync(content, owner.Stream.Realm, prepared.Response, ct);
            var appended = owner.Append(prepared, operation, frozen);
            Assert.True(appended.IsSuccess, appended.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
    }
}
