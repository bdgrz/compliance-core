using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
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
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Policies.Handlers;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.Policies.Tests;

public sealed class PolicyNativeDirectoryReadTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFailWholePageGivenUnreadableNativePolicyContent(bool unavailable)
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var first = await SeedAsync(source, tenant, program, "POL-A");
        var second = await SeedAsync(source, tenant, program, "POL-B");
        var native = Assert.IsType<PolicyDraftCreated>(Assert.Single(await source.FactsAsync(tenant, second)));
        var kv = new InMemoryKvClient();
        var directory = new FitzPolicyDirectory(kv, source.Policies);
        await ProjectAsync(directory, source, tenant);
        var bad = new UnreadableContent(source.Content, native.Header.Content.ContentId, unavailable);
        var reads = new PolicyReader(source.Reader, source.Events, bad);
        var queried = new FitzPolicyDirectory(kv, reads);
        var handler = new ListPoliciesHandler(queried, new PolicyDirectoryReadConsistency(queried, source.Events), source.Clock);
        var before = await source.Reader.HydrateAsync(new Policy(tenant, second));

        // Act
        var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<ListPolicies>(new ListPolicies(tenant, program, 50)),
            CancellationToken.None);
        var after = await source.Reader.HydrateAsync(new Policy(tenant, second));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(1, bad.RejectedReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.True((await source.Policies.ReadAsync(tenant, program, first)).IsVisible);
    }

    [Fact]
    public async Task ShouldRejectCrossProgramCursorAndHideForeignTenantGivenNativeDiscoveryIndex()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var otherProgram = Uuid.CreateVersion4();
        await SeedAsync(source, tenant, program, "POL-A");
        await SeedAsync(source, tenant, program, "POL-B");
        await SeedAsync(source, tenant, otherProgram, "POL-OTHER");
        var directory = new FitzPolicyDirectory(new InMemoryKvClient(), source.Policies);
        await ProjectAsync(directory, source, tenant);

        // Act
        var first = await directory.ListProgramAsync(tenant, program, 1, null);
        var second = await directory.ListProgramAsync(tenant, program, 1, first.NextCursor);
        var foreign = await directory.ListProgramAsync(Uuid.CreateVersion4(), program, 50, null);
        var invalid = await Assert.ThrowsAsync<KvDirectoryQueryException>(() =>
            directory.ListProgramAsync(tenant, otherProgram, 1, first.NextCursor).AsTask());

        // Assert
        Assert.Single(first.Items);
        Assert.Single(second.Items);
        Assert.Equal(["POL-A", "POL-B"], first.Items.Concat(second.Items).Select(item => item.Identifier));
        Assert.Empty(foreign.Items);
        Assert.NotNull(invalid);
    }

    static async Task<Uuid> SeedAsync(PolicySourceFixture source, Uuid tenant, Uuid program, string identifier)
    {
        var id = Policy.IdFor(tenant, program, identifier);
        var rules = new PolicyRules(tenant, id);
        var actor = Uuid.CreateVersion4();
        Assert.True(rules.Create(program, id, identifier, new PolicyContent(identifier, "Govern access",
            PolicyAudience.CoreSecurity, null, 12, "The policy body", null, "Owner", []),
            ActorReference.ForMember(actor, "Author"), actor, source.Clock.Now).IsSuccess);
        await source.SeedAsync(rules);
        return id;
    }

    static async Task ProjectAsync(FitzPolicyDirectory directory, PolicySourceFixture source, Uuid tenant)
    {
        var facts = new List<DomainEvent>();
        var cursor = EventCursor.Start;
        await foreach (var record in source.Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), "policies"),
            EventCursor.Start, CancellationToken.None))
        {
            facts.Add(record.Event);
            cursor = record.NextCursor;
        }
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            FitzPolicyDirectory.ProjectorName, EventStreamPattern.ForPattern(tenant.ToString(), "policies")), ProjectionCheckpoint.Start));
        foreach (var fact in facts)
            await directory.ApplyAsync(fact);
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    sealed class UnreadableContent(IImmutableContentStore inner, Uuid failedId, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.ContentId != failedId)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("The immutable policy is temporarily absent.")
                : new ImmutableContentIntegrityException("The immutable policy digest differs.");
        }
    }
}
