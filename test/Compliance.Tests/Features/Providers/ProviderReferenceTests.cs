using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderReferenceTests
{
    [Fact]
    public async Task ShouldRetainInspectedRevisionsGivenExactRetryAfterCanonicalChanges()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = new RecordProvider(Uuid.CreateVersion4(), fixture.TenantId, fixture.Content());
        var scenario = RequestScenario.For(fixture.Services).GivenActor(ProgramManagementServices.Actor(fixture.UserId));
        var first = (await scenario.When(request).ExpectSuccess()).Value;
        await ProgramManagementServices.SeedAsync(fixture.Services, new Person(fixture.TenantId, fixture.PersonId), person =>
        {
            Assert.Null(person.Revise(1, "Second name", null, fixture.Author, fixture.Now.AddMinutes(1)));
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(fixture.Services, new ClientService(fixture.TenantId, fixture.ServiceId), service =>
        {
            Assert.Null(service.Revise(1, "Second service", "Purpose", "Owner", fixture.UserId, "Recorder", fixture.Now.AddMinutes(1)));
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(fixture.Services, new DeclaredSystemInstance(fixture.TenantId, fixture.InstanceId), instance =>
        {
            Assert.Null(instance.Retire(1, fixture.Now, "Changed", fixture.UserId, "Recorder", fixture.Now.AddMinutes(1)));
            return Result.Success;
        });
        var revisedId = Uuid.CreateVersion4();
        await NamedRecordFacts.SeedProviderAsync(fixture.Services, fixture.TenantId, revisedId,
            (await fixture.References.ResolveAsync(fixture.TenantId, fixture.Content() with { Name = "Other provider" },
                CancellationToken.None)).Value!, fixture.Author, fixture.Now);
        var revision = new ReviseProvider(Uuid.CreateVersion4(), fixture.TenantId, revisedId, 1,
            request.Content with { Name = "Revised provider" });
        var second = (await scenario.When(revision).ExpectSuccess()).Value;

        // Act
        var createRetry = await scenario.When(request).ExpectSuccess();
        await ProgramManagementServices.SeedAsync(fixture.Services, new Person(fixture.TenantId, fixture.PersonId), person =>
        {
            Assert.Null(person.Revise(2, "Third name", null, fixture.Author, fixture.Now.AddMinutes(2)));
            return Result.Success;
        });
        var revisionRetry = await scenario.When(revision).ExpectSuccess();
        var pending = await scenario.When(revision with { CommandId = Uuid.CreateVersion4() })
            .ExpectFailure(RequestErrorKind.Conflict);
        var created = await FrozenContentAsync(fixture, request.ProviderId);
        var revised = await FrozenContentAsync(fixture, revisedId);
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Services,
            new Provider(fixture.TenantId, request.ProviderId));

        // Assert
        Assert.Equal(first, createRetry.Value);
        Assert.Equal(second, revisionRetry.Value);
        Assert.True(pending.Error!.IsTransient);
        Assert.Equal(1UL, owner.Version);
        Assert.Equal(1, created.OwnerPersonRevision);
        Assert.Equal(2, revised.OwnerPersonRevision);
        Assert.Equal("Revised provider", revised.Name);
        Assert.Equal([2L, 2L], revised.Dependencies!.Select(static dependency => dependency.SourceRevision!.Value));
        Assert.Equal(fixture.PersonId, created.OwnerPersonId);
    }

    static async Task<ProviderContent> FrozenContentAsync(Fixture fixture, Uuid providerId)
    {
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Services,
            new Provider(fixture.TenantId, providerId));
        var content = fixture.Services.GetRequiredService<IImmutableContentStore>();
        var realm = fixture.TenantId.ToString();
        var intent = await content.ReadJsonAsync<RecordNameChangeIntent>(realm, owner.Pending!.Intent);
        var bytes = await content.ReadVerifiedAsync(realm, intent.AuthoredContent);
        return System.Text.Json.JsonSerializer.Deserialize(bytes.Span,
            ComplianceCoreJsonContext.Default.ProviderContent)!;
    }

    [Fact]
    public async Task ShouldCaptureCanonicalRevisionsGivenNonSigningPersonAndOwnedDependencies()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var content = fixture.Content();

        // Act
        var resolved = await fixture.References.ResolveAsync(fixture.TenantId, content, CancellationToken.None);
        await ProgramManagementServices.SeedAsync(fixture.Services, new Person(fixture.TenantId, fixture.PersonId), person =>
        {
            Assert.Null(person.Revise(1, "Revised name", null, fixture.Author, fixture.Now.AddMinutes(1)));
            return Result.Success;
        });
        var later = await fixture.References.ResolveAsync(fixture.TenantId, content, CancellationToken.None);

        // Assert
        Assert.True(resolved.IsSuccess);
        Assert.Equal(1, resolved.Value.OwnerPersonRevision);
        Assert.Equal([1L, 1L], resolved.Value.Dependencies!.Select(static dependency => dependency.SourceRevision!.Value));
        Assert.Equal(1, resolved.Value.Dependencies![0].ProgramRevision);
        Assert.Equal(1, resolved.Value.Dependencies![1].ApplicationRevision);
        Assert.Equal(2, later.Value.OwnerPersonRevision);
        Assert.Equal(fixture.PersonId, resolved.Value.OwnerPersonId);
        Assert.Equal(fixture.Now, resolved.Value.Dependencies![0].EffectiveFrom);
        Assert.Equal(fixture.Now.AddDays(1), resolved.Value.Dependencies![0].EffectiveUntilExclusive);
        Assert.Null((await ProgramManagementServices.HydrateAsync(fixture.Services,
            new Person(fixture.TenantId, fixture.PersonId))).CorrelatedUserId);
    }

    [Fact]
    public async Task ShouldRetainExternalCitationAndValidateArtifactGivenOrdinaryMetadata()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var artifactId = Uuid.CreateVersion4();
        var citation = new ProviderSourceCitation("policy", "Supplier facts", "2026-10",
            "https://example.invalid/source#section-2", "internal", artifactId);
        var content = new ProviderContent("Provider", "Supplier", SourceCitation: citation);
        await ProgramManagementServices.SeedAsync(fixture.Services, new EvidenceArtifact(fixture.TenantId, artifactId), artifact =>
            artifact.Register(new EvidenceArtifactContent("Source", null, "policy", "manual", fixture.Now,
                new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), "confidential"), new string('a', 64), 1, fixture.Author, fixture.Now));

        // Act
        var found = await fixture.References.ResolveAsync(fixture.TenantId, content, CancellationToken.None);
        var missing = await fixture.References.ResolveAsync(fixture.TenantId,
            content with { SourceCitation = citation with { ArtifactId = Uuid.CreateVersion4() } }, CancellationToken.None);
        var foreign = await fixture.References.ResolveAsync(Uuid.CreateVersion4(), content, CancellationToken.None);

        // Assert
        Assert.True(found.IsSuccess);
        Assert.Equal(citation, found.Value.SourceCitation);
        Assert.False(missing.IsSuccess);
        Assert.False(foreign.IsSuccess);
        Assert.Equal(missing.Error!.Message, foreign.Error!.Message);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("service")]
    [InlineData("service_program")]
    [InlineData("system")]
    [InlineData("system_application")]
    public async Task ShouldHideForeignAndMissingReferenceGivenTenantOrContextMismatch(string subject)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var valid = fixture.Content();
        var wrong = subject switch
        {
            "owner" => valid with { OwnerPersonId = Uuid.CreateVersion4() },
            "service" => valid with { Dependencies = [valid.Dependencies![0] with { SubjectId = Uuid.CreateVersion4() }] },
            "service_program" => valid with { Dependencies = [valid.Dependencies![0] with { ProgramId = Uuid.CreateVersion4() }] },
            "system" => valid with { Dependencies = [valid.Dependencies![1] with { SubjectId = Uuid.CreateVersion4() }] },
            _ => valid with { Dependencies = [valid.Dependencies![1] with { ApplicationId = Uuid.CreateVersion4() }] },
        };

        // Act
        var missing = await fixture.References.ResolveAsync(fixture.TenantId, wrong, CancellationToken.None);
        var foreign = await fixture.References.ResolveAsync(Uuid.CreateVersion4(), valid, CancellationToken.None);

        // Assert
        Assert.False(missing.IsSuccess);
        Assert.False(foreign.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, missing.Error!.Kind);
        Assert.Equal(RequestErrorKind.NotFound, foreign.Error!.Kind);
        Assert.Equal(missing.Error.Message, foreign.Error.Message);
        Assert.DoesNotContain(fixture.PersonId.ToString(), foreign.Error.Message!, StringComparison.Ordinal);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid PersonId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ServiceId { get; } = Uuid.CreateVersion4();
        public Uuid ApplicationId { get; } = Uuid.CreateVersion4();
        public Uuid InstanceId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public ActorReference Author { get; } = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        public ServiceProvider Services { get; private set; } = null!;
        public AsyncServiceScope Scope { get; private set; }
        public ProviderReferences References => Scope.ServiceProvider.GetRequiredService<ProviderReferences>();

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture
            {
                Services = ProgramManagementServices.Build(new RecordingPermissionAuthorizer(true), portia => portia.AddRequestAuthorizer<ProviderAuthorizer>()
                    .AddRequestHandler<RecordProviderHandler>().AddRequestHandler<ReviseProviderHandler>(), services =>
                {
                    var store = new InMemoryEventStore();
                    services.AddSingleton<IEventStore>(store);
                    services.AddSingleton<IDomainEventReader>(store);
                    services.AddScoped<ProviderReferences>();
                    services.AddScoped<NativeOperationExecutor>();
                    services.AddScoped<NamedRecordAdmission>();
                }),
            };
            await ProgramManagementServices.SeedAsync(fixture.Services, new Person(fixture.TenantId, fixture.PersonId),
                person => person.Record("Non-signing owner", null, fixture.Author, fixture.Now));
            await ProgramManagementServices.SeedAsync(fixture.Services, new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
            {
                Assert.Null(program.Create("SOC2", new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(), "Recorder", fixture.Now));
                return Result.Success;
            });
            await ProgramManagementServices.SeedAsync(fixture.Services, new ClientService(fixture.TenantId, fixture.ServiceId),
                service => service.Create(fixture.ProgramId, "Service", "Purpose", "Owner", Uuid.CreateVersion4(), "Recorder", fixture.Now));
            await ProgramManagementServices.SeedAsync(fixture.Services, new DeclaredApplication(fixture.TenantId, fixture.ApplicationId),
                application => application.Declare("Application", "Purpose", "Owner", Uuid.CreateVersion4(), "Recorder", fixture.Now));
            await ProgramManagementServices.SeedAsync(fixture.Services, new DeclaredSystemInstance(fixture.TenantId, fixture.InstanceId),
                instance => instance.Declare(fixture.ApplicationId, "Production", "cloud_account", null, null, Uuid.CreateVersion4(), "Recorder", fixture.Now));
            fixture.Scope = fixture.Services.CreateAsyncScope();
            return fixture;
        }

        public ProviderContent Content() => new("Provider", "Supplier", OwnerPersonId: PersonId, Dependencies:
        [
            new ProviderDependency("client_service", ServiceId, ProgramId, null, "Delivers service", Now, Now.AddDays(1)),
            new ProviderDependency("system_instance", InstanceId, null, ApplicationId, "Hosts system", Now),
        ]);

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await Services.DisposeAsync();
        }
    }
}
