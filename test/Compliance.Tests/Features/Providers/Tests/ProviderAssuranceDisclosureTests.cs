using System.Security.Claims;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.Tests.AssuranceSamples;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Readers;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class ProviderAssuranceDisclosureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldWithholdRestrictedContentGivenReaderWithoutProviderAuthoringAuthority(bool manager)
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var userId = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(userId);
        var handler = fixture.ReportsHandler(manager);
        var reviews = fixture.ReviewsHandler(manager);

        // Act
        var page = await handler.HandleAsync(new RequestContext<ListProviderAssuranceReports>(
            new ListProviderAssuranceReports(fixture.TenantId, fixture.ProviderId), actor), CancellationToken.None);
        var reviewPage = await reviews.HandleAsync(new RequestContext<ListProviderReviews>(
            new ListProviderReviews(fixture.TenantId, fixture.ProviderId), actor), CancellationToken.None);
        var anonymous = await handler.HandleAsync(new RequestContext<ListProviderAssuranceReports>(
            new ListProviderAssuranceReports(fixture.TenantId, fixture.ProviderId), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(page.IsSuccess);
        var byIssuer = page.Value.Items.ToDictionary(static item => item.Content.Issuer);
        Assert.Equal(3, byIssuer.Count);
        Assert.Equal(!manager, byIssuer["Restricted LLP"].Redacted);
        Assert.Equal(!manager, byIssuer["Unclassified LLP"].Redacted);
        Assert.False(byIssuer["Internal LLP"].Redacted);
        Assert.Equal(manager ? 1 : 0, byIssuer["Restricted LLP"].Content.Exceptions!.Count);
        Assert.Equal(manager, byIssuer["Restricted LLP"].Content.Citation is not null);
        Assert.Equal(1, byIssuer["Restricted LLP"].ExceptionCount);
        Assert.Equal("unqualified", byIssuer["Restricted LLP"].Content.Opinion);
        Assert.NotNull(byIssuer["Internal LLP"].Content.Citation);
        Assert.Single(byIssuer["Internal LLP"].Content.Exceptions!);
        var byRationale = reviewPage.Value.Items.ToDictionary(static item => item.Content.Rationale);
        Assert.Equal("acceptable", byRationale["restricted evidence"].Content.Conclusion);
        Assert.Equal(manager, byRationale["restricted evidence"].Content.Evidence is not null);
        Assert.Equal(!manager, byRationale["restricted evidence"].Redacted);
        Assert.NotNull(byRationale["internal evidence"].Content.Evidence);
        Assert.False(byRationale["internal evidence"].Redacted);
        Assert.Equal(2, anonymous.Value.Items.Count(static item => item.Redacted));
    }

    sealed class Fixture
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProviderId { get; } = Uuid.CreateVersion4();
        FitzAssuranceDirectory Directory { get; } = new(new InMemoryKvClient());
        ServiceProvider Source { get; set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var collection = new ServiceCollection();
            var events = new InMemoryEventStore();
            collection.AddSingleton<IEventStore>(events);
            collection.AddSingleton<IDomainEventReader>(events);
            collection.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
                new PersistenceKvClient(new InMemoryKvClient())));
            collection.AddPortia();
            fixture.Source = collection.BuildServiceProvider();
            await NamedRecordFacts.SeedProviderAsync(fixture.Source, fixture.TenantId, fixture.ProviderId, MaterialProvider());
            var exception = new[] { new AssuranceExceptionNote("CC6.1", "Exception text") };
            await using var batch = await fixture.Directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
                FitzAssuranceDirectory.ProjectorName, EventStreamPattern.ForPattern(fixture.TenantId.ToString(), ProviderAssuranceStreams.Area)),
                ProjectionCheckpoint.Start));
            var tenant = fixture.TenantId;
            var provider = fixture.ProviderId;
            await fixture.Directory.ApplyReportAsync(ReportView(tenant, provider, Uuid.CreateVersion4(), 1,
                Report("restricted") with { Issuer = "Restricted LLP", Exceptions = exception }));
            await fixture.Directory.ApplyReportAsync(ReportView(tenant, provider, Uuid.CreateVersion4(), 1,
                Report("internal") with { Issuer = "Internal LLP", Exceptions = exception }));
            await fixture.Directory.ApplyReportAsync(ReportView(tenant, provider, Uuid.CreateVersion4(), 1,
                Report() with { Issuer = "Unclassified LLP", Citation = null }));
            await fixture.Directory.ApplyReviewAsync(ReviewView(tenant, provider, Uuid.CreateVersion4(),
                Review() with { Rationale = "restricted evidence", Evidence = Citation("restricted") }));
            await fixture.Directory.ApplyReviewAsync(ReviewView(tenant, provider, Uuid.CreateVersion4(),
                Review() with { Rationale = "internal evidence", Evidence = Citation("internal") }));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
            return fixture;
        }

        public ListProviderAssuranceReportsHandler ReportsHandler(bool manager) => new(Directory, References(), Consistency(), Disclosure(manager));

        public ListProviderReviewsHandler ReviewsHandler(bool manager) => new(Directory, References(), Consistency(), Disclosure(manager));

        AssuranceReferences References()
        {
            var reader = Source.GetRequiredService<IAggregateReader>();
            return new(reader, new ProviderSource(reader, Source.GetRequiredService<IImmutableContentStore>()));
        }

        AssuranceReadConsistency Consistency() => new(Directory, new InMemoryEventStore());

        static AssuranceDisclosure Disclosure(bool manager) => new(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(manager)));
    }
}
