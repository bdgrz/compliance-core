using System.Text.Json;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests;

/// <summary>
///     Seeds well-formed accepted-and-applied changes of named records for tests that only need a recorded provider,
///     location or operational process. Seeding writes the real content and intent, but acquires no name claim.
/// </summary>
static class NamedRecordFacts
{
    public static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public static readonly ActorReference Author = ActorReference.ForMember(
        Uuid.Parse("00000000-0000-4000-8000-0000000000a1", System.Globalization.CultureInfo.InvariantCulture), "Author");

    /// <summary>Records the provider, or applies its next revision when it already exists.</summary>
    public static Task<long> SeedProviderAsync(IServiceProvider services, Uuid tenantId, Uuid providerId,
        ProviderContent content, ActorReference? actor = null, DateTimeOffset? at = null) =>
        SeedAsync(services, ProviderNames.Descriptor, tenantId, providerId, JsonSerializer.SerializeToUtf8Bytes(
            ProviderRules.Normalize(content), ComplianceCoreJsonContext.Default.ProviderContent), actor, at);

    /// <summary>Records the location, or applies its next revision when it already exists.</summary>
    public static Task<long> SeedLocationAsync(IServiceProvider services, Uuid tenantId, Uuid locationId,
        LocationContent content, ActorReference? actor = null, DateTimeOffset? at = null) =>
        SeedAsync(services, LocationNames.Descriptor, tenantId, locationId, JsonSerializer.SerializeToUtf8Bytes(
            InventoryRegisterRules.Normalize(content), ComplianceCoreJsonContext.Default.LocationContent), actor, at);

    /// <summary>Records the operational process, or applies its next revision when it already exists.</summary>
    public static Task<long> SeedProcessAsync(IServiceProvider services, Uuid tenantId, Uuid processId,
        OperationalProcessContent content, ActorReference? actor = null, DateTimeOffset? at = null) =>
        SeedAsync(services, OperationalProcessNames.Descriptor, tenantId, processId, JsonSerializer.SerializeToUtf8Bytes(
            InventoryRegisterRules.Normalize(content), ComplianceCoreJsonContext.Default.OperationalProcessContent),
            actor, at);

    /// <summary>A content reference of the right schema; nothing reads it back.</summary>
    public static ImmutableContentRef Ref(string schema) => new(Uuid.CreateVersion4(), new string('d', 64), 2,
        ImmutableContentStoreExtensions.JsonMediaType, schema);

    /// <summary>An applied creating provider fact whose content nothing reads back.</summary>
    public static ProviderRecorded ProviderRecordedFact(Uuid tenantId, Uuid providerId, ActorReference? actor = null,
        DateTimeOffset? at = null)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), Provider.Area, providerId.ToString());
        var root = Uuid.CreateVersion4();
        return new ProviderRecorded(tenantId, providerId, root, Ref(ProviderRecorded.ContentSchema), actor ?? Author,
            at ?? At, Provenance(stream, Uuid.CreateVersion4(), root, "ApplyProviderChange", actor ?? Author,
                at ?? At, null), Ref(RecordNameChangeIntent.Schema), null, null);
    }

    /// <summary>An applied provider revision fact whose content nothing reads back.</summary>
    public static ProviderRevised ProviderRevisedFact(Uuid tenantId, Uuid providerId, long revision,
        ActorReference? actor = null, DateTimeOffset? at = null)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), Provider.Area, providerId.ToString());
        var root = Uuid.CreateVersion4();
        return new ProviderRevised(tenantId, providerId, root, revision, Ref(ProviderRevised.ContentSchema),
            actor ?? Author, at ?? At, Provenance(stream, Uuid.CreateVersion4(), root, "ApplyProviderChange",
                actor ?? Author, at ?? At, null), Ref(RecordNameChangeIntent.Schema), null, null);
    }

    /// <summary>A hydrated-looking provider owner at revision one; its content is not in any store.</summary>
    public static Provider RecordedProvider(Uuid tenantId, Uuid providerId, ActorReference? actor = null,
        DateTimeOffset? at = null)
    {
        var owner = new Provider(tenantId, providerId);
        var root = Uuid.CreateVersion4();
        var intent = Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Provenance(owner.Stream, root, root, "RecordProvider", actor ?? Author,
            at ?? At, null), true, 0, intent, Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.ApplyChange(Provenance(owner.Stream, Uuid.CreateVersion4(), root, "ApplyProviderChange",
            actor ?? Author, at ?? At, null), intent, Ref(ProviderRecorded.ContentSchema), actor ?? Author,
            at ?? At, Ref("mutation-result-content.v1")).IsSuccess);
        return owner;
    }

    /// <summary>Applies the next revision of an in-memory provider owner; its content is not in any store.</summary>
    public static Provider Revise(this Provider owner, ActorReference? actor = null, DateTimeOffset? at = null)
    {
        var root = Uuid.CreateVersion4();
        var intent = Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Provenance(owner.Stream, root, root, "ReviseProvider", actor ?? Author,
            at ?? At, (ulong)owner.Revision), false, owner.Revision, intent, Ref("first-response-descriptor.v1"))
            .IsSuccess);
        Assert.True(owner.ApplyChange(Provenance(owner.Stream, Uuid.CreateVersion4(), root, "ApplyProviderChange",
            actor ?? Author, at ?? At, null), intent, Ref(ProviderRevised.ContentSchema), actor ?? Author,
            at ?? At, Ref("mutation-result-content.v1")).IsSuccess);
        return owner;
    }

    /// <summary>An applied location fact whose content nothing reads back.</summary>
    public static LocationRevisionRecorded LocationFact(Uuid tenantId, Uuid locationId, long revision,
        ActorReference? actor = null, DateTimeOffset? at = null)
    {
        var stream = new Location(tenantId, locationId).Stream;
        var root = Uuid.CreateVersion4();
        return new LocationRevisionRecorded(tenantId, locationId, revision, Ref(LocationRevisionRecorded.ContentSchema),
            actor ?? Author, at ?? At, Provenance(stream, Uuid.CreateVersion4(), root, "ApplyLocationChange",
                actor ?? Author, at ?? At, null), Ref(RecordNameChangeIntent.Schema), null, null);
    }

    /// <summary>An applied operational process fact whose content nothing reads back.</summary>
    public static OperationalProcessRevisionRecorded ProcessFact(Uuid tenantId, Uuid processId, long revision,
        ActorReference? actor = null, DateTimeOffset? at = null)
    {
        var stream = new OperationalProcess(tenantId, processId).Stream;
        var root = Uuid.CreateVersion4();
        return new OperationalProcessRevisionRecorded(tenantId, processId, revision,
            Ref(OperationalProcessRevisionRecorded.ContentSchema), actor ?? Author, at ?? At,
            Provenance(stream, Uuid.CreateVersion4(), root, "ApplyOperationalProcessChange", actor ?? Author,
                at ?? At, null), Ref(RecordNameChangeIntent.Schema), null, null);
    }

    /// <summary>Applies the next revision of an in-memory owner; its content is not in any store.</summary>
    public static T Applied<T>(this T owner, string applyKind, string contentSchema, ActorReference? actor = null,
        DateTimeOffset? at = null) where T : NamedRecord
    {
        var root = Uuid.CreateVersion4();
        var intent = Ref(RecordNameChangeIntent.Schema);
        var create = owner.Generation == 0;
        Assert.True(owner.AcceptChange(Provenance(owner.Stream, root, root, create ? "Record" : "Revise",
            actor ?? Author, at ?? At, create ? null : (ulong)owner.Revision), create, owner.Revision, intent,
            Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.ApplyChange(Provenance(owner.Stream, Uuid.CreateVersion4(), root, applyKind,
            actor ?? Author, at ?? At, null), intent, Ref(contentSchema), actor ?? Author, at ?? At,
            Ref("mutation-result-content.v1")).IsSuccess);
        return owner;
    }

    /// <summary>A location owner at the given number of applied revisions; the content is not in any store.</summary>
    public static Location RecordedLocation(Uuid tenantId, Uuid locationId, int revisions = 1)
    {
        var owner = new Location(tenantId, locationId);
        for (var i = 0; i < revisions; i++)
            owner.Applied("ApplyLocationChange", LocationRevisionRecorded.ContentSchema);
        return owner;
    }

    /// <summary>An operational process owner at the given number of applied revisions; the content is not in any store.</summary>
    public static OperationalProcess RecordedProcess(Uuid tenantId, Uuid processId, int revisions = 1)
    {
        var owner = new OperationalProcess(tenantId, processId);
        for (var i = 0; i < revisions; i++)
            owner.Applied("ApplyOperationalProcessChange", OperationalProcessRevisionRecorded.ContentSchema);
        return owner;
    }

    static async Task<long> SeedAsync(IServiceProvider services, NamedRecordKindDescriptor kind, Uuid tenantId,
        Uuid recordId, byte[] bytes, ActorReference? actor, DateTimeOffset? at)
    {
        await using var scope = services.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var current = await reader.HydrateAsync(kind.OpenRecord(tenantId, recordId, Uuid.Empty));
        var create = current.Generation == 0;
        var revision = current.Revision + 1;
        var realm = tenantId.ToString();
        var root = Uuid.CreateVersion4();
        var applyId = Uuid.CreateVersion4();
        var changedAt = at ?? At;
        var who = actor ?? Author;
        var stream = current.Stream;
        var authored = await content.WriteVerifiedAsync(realm, Uuid.CreateVersion4(), kind.AuthoredContentSchema,
            ImmutableContentStoreExtensions.JsonMediaType, bytes);
        var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), RecordNameChangeIntent.Schema,
            new RecordNameChangeIntent(new DurableIntentHeader(root, stream, stream, stream, who, "current",
                create ? null : (ulong)current.Revision, new string('a', 64), changedAt), kind.Kind, recordId,
                (ulong)current.Revision, null, null, null, authored, RecordNameChangeIntent.NormalizationVersion));
        var created = await content.WriteVerifiedAsync(realm, Uuid.CreateVersion4(),
            create ? kind.CreatedContentSchema : kind.RevisedContentSchema,
            ImmutableContentStoreExtensions.JsonMediaType, bytes);
        var response = await FirstResponses.FreezeAsync(content, realm, new FirstResponseDescriptor(202,
            ImmutableContentStoreExtensions.JsonMediaType, "operation-accepted-response.v1",
            new StandardResponseBody(created)));
        var accepted = Provenance(stream, root, root, create ? kind.CreateCommandKind : kind.ReviseCommandKind, who,
            changedAt, create ? null : (ulong)current.Revision);
        var applied = Provenance(stream, applyId, root, kind.ApplyCommandKind, who, changedAt, null);
        var result = await executor.ExecuteAsync(kind.OpenRecord(tenantId, recordId, Uuid.Empty), owner =>
        {
            var first = owner.AcceptChange(accepted, create, current.Revision, intent, response);
            return AggregateOutcome.Commit(first.IsSuccess
                ? owner.ApplyChange(applied, intent, created, who, changedAt, response)
                : first);
        }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return revision;
    }

    static OperationProvenance Provenance(EventStreamAddress owner, Uuid logicalId, Uuid rootId, string commandKind,
        ActorReference actor, DateTimeOffset at, ulong? expected) =>
        new(logicalId, rootId, null, commandKind, actor, owner, "current", expected, new string('a', 64), at, owner,
            owner);
}
