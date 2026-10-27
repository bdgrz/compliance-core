using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
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
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Handlers;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

public sealed class TrainingRequirementTests
{
    static readonly Uuid Tenant = Uuid.Parse("11111111-1111-4111-8111-111111111111", CultureInfo.InvariantCulture);
    static readonly Uuid Program = Uuid.Parse("22222222-2222-4222-8222-222222222222", CultureInfo.InvariantCulture);

    static readonly TrainingRequirementContent Content = new("Security Awareness", "Annual course",
        PolicyAudience.CoreSecurity, null, "manual");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnPendingOnlyForMissingContentGivenTrainingRequirementQuery(bool unavailable)
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = await TrainingSeed.DefineAsync(host.Provider, host.Tenant, host.Program, "TR-SEC", Content,
            host.ManagerActor, host.Clock.GetUtcNow());
        var owner = await ProgramManagementServices.HydrateAsync(host.Provider, new TrainingRequirement(host.Tenant, id));
        var store = new FaultedContent(host.Content, owner.Latest!.ContentContent.ContentId, unavailable);
        var handler = new GetTrainingRequirementHandler(host.Scope.ServiceProvider.GetRequiredService<IAggregateReader>(), host.Events, store);
        var context = new InventoryRequestContext<GetTrainingRequirement>(new GetTrainingRequirement(host.Tenant,
            host.Program, id), new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldKeepRequirementAuthorityBoundedGivenManyNativeVersions()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = TrainingRequirementIdentity.IdFor(host.Tenant, host.Program, "TR-SEC");
        var defined = await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program, "TR-SEC", Content),
            host.Manager);
        Assert.True(defined.IsSuccess, defined.Error?.Message);
        for (var version = 1; version < 32; version++)
        {
            var revised = await host.SendAsync(new ReviseTrainingRequirement(Uuid.CreateVersion4(), host.Tenant,
                host.Program, id, version, Content with { CourseName = $"Course version {version + 1}" }), host.Manager);
            Assert.True(revised.IsSuccess, revised.Error?.Message);
        }

        // Act
        var requirement = await ProgramManagementServices.HydrateAsync(host.Provider,
            new TrainingRequirement(host.Tenant, id));
        var pinned = await host.SendAsync(new GetTrainingRequirement(host.Tenant, host.Program, id, 1), host.Manager);

        // Assert
        Assert.Equal(32, requirement.CurrentVersion);
        Assert.True(pinned.IsSuccess, pinned.Error?.Message);
        Assert.Equal("Security Awareness", pinned.Value.Content.CourseName);
        Assert.Equal(32, pinned.Value.LatestVersion);
        Assert.All(typeof(TrainingRequirement).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(requirement) is ICollection { Count: > 2 },
                $"The requirement command owner retains lifetime version history in {field.Name}."));
    }

    [Theory]
    [InlineData("TR-SEC", "7d484654-8238-5ea8-a5bf-69a4931b7349")]
    [InlineData("  tr-sec ", "7d484654-8238-5ea8-a5bf-69a4931b7349")]
    [InlineData("SAT_2026.1", "f44215ec-6f52-5bb3-a3cc-3933706d7cbd")]
    [InlineData("a", "fc063986-e089-5051-be41-6e0c7134d35c")]
    public void ShouldKeepPublishedIdentityVectorsGivenNormalizedIdentifier(string identifier, string expected)
    {
        // Arrange

        // Act
        var id = TrainingRequirementIdentity.IdFor(Tenant, Program, identifier);

        // Assert
        Assert.Equal(Uuid.Parse(expected, CultureInfo.InvariantCulture), id);
    }

    [Fact]
    public void ShouldKeepIdentityIndependentGivenDifferentIdentifiersProgramsOrTenants()
    {
        // Arrange
        var other = Uuid.CreateVersion4();

        // Act
        var ids = new[]
        {
            TrainingRequirementIdentity.IdFor(Tenant, Program, "TR-A"),
            TrainingRequirementIdentity.IdFor(Tenant, Program, "TR-B"),
            TrainingRequirementIdentity.IdFor(Tenant, other, "TR-A"),
            TrainingRequirementIdentity.IdFor(other, Program, "TR-A"),
        };

        // Assert
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Theory]
    [InlineData("TR-SEC", true)]
    [InlineData("TR_SEC.2026", true)]
    [InlineData("", false)]
    [InlineData("TR SEC", false)]
    [InlineData("TR/SEC", false)]
    public void ShouldAcceptOnlyAsciiLettersDigitsAndSeparatorsGivenIdentifier(string identifier, bool valid)
    {
        // Arrange

        // Act
        var result = TrainingRequirementIdentity.IsValidIdentifier(
            TrainingRequirementIdentity.NormalizeIdentifier(identifier));

        // Assert
        Assert.Equal(valid, result);
    }

    [Fact]
    public async Task ShouldRegisterFirstVersionAndReplayOriginalRegistrationGivenExactRetryAfterRevision()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = TrainingRequirementIdentity.IdFor(host.Tenant, host.Program, "TR-SEC");
        var define = new DefineTrainingRequirement(id, host.Tenant, host.Program, "tr-sec", Content);
        var defined = await host.SendAsync(define, host.Manager);
        var revise = new ReviseTrainingRequirement(Uuid.CreateVersion4(), host.Tenant, host.Program, id, 1,
            Content with { CourseName = "Security Awareness v2" });
        var revised = await host.SendAsync(revise, host.Manager);

        // Act
        var retry = await host.SendAsync(define, host.Manager);
        var revisedAgain = await host.SendAsync(revise, host.Manager);
        var changed = await host.SendAsync(define with { Content = Content with { Description = "Changed" } },
            host.Manager);

        // Assert
        Assert.True(defined.IsSuccess, defined.Error?.Message);
        Assert.Equal(new TrainingRequirementRegistration(id, "TR-SEC", 1), defined.Value);
        Assert.Equal(new TrainingRequirementRegistration(id, "TR-SEC", 2), revised.Value);
        Assert.Equal(defined.Value, retry.Value);
        Assert.Equal(revised.Value, revisedAgain.Value);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        await using var scope = host.Provider.CreateAsyncScope();
        var requirement = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TrainingRequirement(host.Tenant, id));
        Assert.Equal(2, requirement.CurrentVersion);
        Assert.Equal(2, (await TrainingRequirementHistory.FactsAsync(
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), requirement)).Count);
        Assert.Equal("TR-SEC", requirement.Identifier);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenMismatchedIdentityOrInvalidContent()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = TrainingRequirementIdentity.IdFor(host.Tenant, host.Program, "TR-SEC");

        // Act
        var wrongId = await host.SendAsync(new DefineTrainingRequirement(Uuid.CreateVersion4(), host.Tenant,
            host.Program, "TR-SEC", Content), host.Manager);
        var badIdentifier = await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program,
            "TR SEC", Content), host.Manager);
        var badSource = await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program, "TR-SEC",
            Content with { DeliverySource = "connector" }), host.Manager);
        var noCourse = await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program, "TR-SEC",
            Content with { CourseName = " " }), host.Manager);
        var badAudience = await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program, "TR-SEC",
            Content with { AudienceKind = "everyone_ever" }), host.Manager);

        // Assert
        foreach (var result in new[] { wrongId, badIdentifier, badSource, noCourse, badAudience })
            Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        await using var scope = host.Provider.CreateAsyncScope();
        var requirement = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TrainingRequirement(host.Tenant, id));
        Assert.False(requirement.IsDefined);
    }

    [Fact]
    public async Task ShouldRejectStaleVersionAndKeepSoleVersionCounterGivenRevisions()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var id = TrainingRequirementIdentity.IdFor(host.Tenant, host.Program, "TR-SEC");
        await host.SendAsync(new DefineTrainingRequirement(id, host.Tenant, host.Program, "TR-SEC", Content),
            host.Manager);

        // Act
        var stale = await host.SendAsync(new ReviseTrainingRequirement(Uuid.CreateVersion4(), host.Tenant,
            host.Program, id, 0, Content with { CourseName = "Stale" }), host.Manager);
        var missing = await host.SendAsync(new ReviseTrainingRequirement(Uuid.CreateVersion4(), host.Tenant,
            host.Program, Uuid.CreateVersion4(), 1, Content), host.Manager);
        var other = await host.SendAsync(new ReviseTrainingRequirement(Uuid.CreateVersion4(), host.Tenant,
            Uuid.CreateVersion4(), id, 1, Content), host.Manager);
        var invalid = await host.SendAsync(new ReviseTrainingRequirement(Uuid.CreateVersion4(), host.Tenant,
            host.Program, id, 1, Content with { DeliverySource = "connector" }), host.Manager);
        var next = await host.SendAsync(new ReviseTrainingRequirement(Uuid.CreateVersion4(), host.Tenant, host.Program,
            id, 1, Content with { CourseName = "v2" }), host.Manager);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, missing.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, other.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
        Assert.Equal(2, next.Value.Version);
    }

    [Fact]
    public async Task ShouldNotConflictGivenConcurrentDefinitionsOfDifferentRequirements()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var identifiers = Enumerable.Range(0, 8).Select(index => $"TR-{index}").ToArray();

        // Act
        var results = await Task.WhenAll(identifiers.Select(identifier => host.SendAsync(
            new DefineTrainingRequirement(TrainingRequirementIdentity.IdFor(host.Tenant, host.Program, identifier),
                host.Tenant, host.Program, identifier, Content), host.Manager)));

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(identifiers.Length, results.Select(result => result.Value.RequirementId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldKeepLaunchedMaterialGivenLaterRequirementRevision()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var id = await TrainingSeed.DefineAsync(harness.Provider, harness.TenantId, harness.ProgramId, "TR-SEC",
            Content, harness.Manager, CampaignOwnerHarness.Now);
        var first = TrainingRequirementRules.Clean(Content);
        var subject = new CampaignSubject("training", id, "TR-SEC", first.CourseName, 1,
            TrainingRequirementRules.Hash(first));
        var person = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        await CampaignSeed.LaunchAsync(harness.Provider, harness.TenantId, harness.ProgramId, campaignId, subject,
            PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(), new string('b', 64), new RosterAudience(
                new Dictionary<Uuid, RosterMatch> { [person] = new(person, "Ada", "employee", null, new DateOnly(2025, 1, 1)) },
                new HashSet<Uuid> { person }), new DateOnly(2026, 12, 1), null, harness.Manager, harness.ManagerId,
            CampaignOwnerHarness.Now);

        // Act
        await TrainingSeed.ReviseAsync(harness.Provider, harness.TenantId, id, "TR-SEC", 1,
            Content with { CourseName = "Changed" }, harness.Manager, CampaignOwnerHarness.Now.AddDays(1));
        var state = await harness.StateAsync(campaignId);
        await using var scope = harness.Provider.CreateAsyncScope();
        var requirement = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TrainingRequirement(harness.TenantId, id));
        var pinned = await TrainingRequirementRules.ViewAsync(
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), requirement, 1, CancellationToken.None);

        // Assert
        Assert.Equal(subject.ContentSha256, state.Campaign.Subject!.ContentSha256);
        Assert.Equal(1, state.Campaign.Subject.Version);
        Assert.Equal(subject.ContentSha256, pinned!.ContentSha256);
        Assert.Equal("Security Awareness", pinned.Content.CourseName);
        Assert.Equal(2, pinned.LatestVersion);
    }

    [Fact]
    public async Task ShouldProjectLatestVersionsOrderedByIdentifierGivenCatalogProjection()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var ids = new List<Uuid>();
        foreach (var identifier in new[] { "TR-B", "TR-A", "TR-C" })
            ids.Add(await TrainingSeed.DefineAsync(harness.Provider, harness.TenantId, harness.ProgramId, identifier,
                Content, harness.Manager, CampaignOwnerHarness.Now));
        await TrainingSeed.ReviseAsync(harness.Provider, harness.TenantId, ids[0], "TR-B", 1,
            Content with { CourseName = "B v2" }, harness.Manager, CampaignOwnerHarness.Now.AddDays(1));
        var directory = new FitzTrainingRequirementDirectory(new InMemoryKvClient(),
            harness.Provider.GetRequiredService<IImmutableContentStore>());
        var pattern = EventStreamPattern.ForPattern(harness.TenantId.ToString(), TrainingRequirement.Area);
        var identity = new CheckpointIdentity(FitzTrainingRequirementDirectory.ProjectorName, pattern);

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            var cursor = EventCursor.Start;
            await foreach (var record in harness.Events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }

            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        var page = await directory.ListProgramAsync(harness.TenantId, harness.ProgramId, 50, null);
        var otherProgram = await directory.ListProgramAsync(harness.TenantId, Uuid.CreateVersion4(), 50, null);

        // Assert
        Assert.Equal(["TR-A", "TR-B", "TR-C"], page.Items.Select(static item => item.Identifier));
        var changed = page.Items.Single(static item => item.Identifier == "TR-B");
        Assert.Equal("B v2", changed.Content.CourseName);
        Assert.Equal(2, changed.Version);
        Assert.Equal(2, changed.LatestVersion);
        Assert.Empty(otherProgram.Items);
    }

    sealed class FaultedContent(IImmutableContentStore inner, Uuid selected, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.ContentId != selected
            ? inner.ReadVerifiedAsync(realm, reference, ct)
            : throw (unavailable
                ? new ImmutableContentUnavailableException("Selected source has not become visible.")
                : new ImmutableContentIntegrityException("Selected source is corrupt."));
    }
}
