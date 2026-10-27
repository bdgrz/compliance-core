using Microsoft.Extensions.DependencyInjection;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Tests;
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
                    actor = value.Actor;
                    at = value.ChangedAt;
                    kind = nameof(RecordPerson);
                    var created = rules.Record(value.DisplayName, value.WorkEmail, value.PersonalContact, actor, at);
                    result = created.IsSuccess ? Result.Success : Result.Failure(created.Error!);
                    break;
                case PersonRevised value:
                    actor = value.Actor;
                    at = value.ChangedAt;
                    kind = nameof(RevisePerson);
                    var revised = rules.Revise(value.Revision - 1, value.DisplayName, value.WorkEmail, value.PersonalContact, actor, at);
                    result = revised is null ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, revised.Message!));
                    break;
                case PersonMembershipCorrelated value:
                    actor = value.Actor;
                    at = value.ChangedAt;
                    kind = nameof(CorrelatePersonMembership);
                    var correlated = rules.CorrelateMembership(value.Revision - 1, value.UserId, actor, at);
                    result = correlated is null ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, correlated.Message!));
                    break;
                default:
                    throw new InvalidOperationException("Another pure person fixture mutation.");
            }
            Assert.True(result.IsSuccess, result.Error?.Message);
            var operation = RootOperation.Provenance(logical, kind, owner.Stream, actor,
                change is PersonRecorded ? null : owner.Revision, ContentDigest.Sha256(change, change.GetType()), at) with
            { FirstOwner = owner.Stream };
            var prepared = await PersonContent.FreezeAsync(content, snapshot, rules, operation, ct);
            var frozen = await FirstResponses.FreezeAsync(content, owner.Stream.Realm, prepared.Response, ct);
            var appended = owner.Append(prepared, operation, frozen);
            Assert.True(appended.IsSuccess, appended.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
    }
}
