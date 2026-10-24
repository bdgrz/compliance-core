using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationOwnerTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldPreserveOriginalDeclarationGivenNormalizedRetryAndChangedDisplay()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(true);
        var ownerId = await scenario.RecordPersonAsync("Owner");
        var userId = Uuid.CreateVersion4();
        var request = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, " Payroll ", " Run payroll ",
            " Attribution ", " internal ", ownerId, ownerId, true, " service ", " critical ");
        var firstContext = new FixedRequestContext<DeclareApplication>(request, Uuid.CreateVersion4(),
            BdgrzActor(userId, "Original manager"));
        var first = await scenario.DeclareHandler.HandleAsync(firstContext, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var original = Assert.Single(await scenario.ApplicationEventsAsync(first.Value!.ApplicationId));
        var originalEventCount = await scenario.TenantApplicationEventCountAsync();
        scenario.Clock.UtcNow = Now.AddHours(1);
        var normalized = request with
        {
            Name = "Payroll",
            Purpose = "Run payroll",
            OwnerReference = "Attribution",
            Classification = "internal",
            ApplicationKind = "service",
            Criticality = "critical",
        };
        var retryContext = new FixedRequestContext<DeclareApplication>(normalized, Uuid.CreateVersion4(),
            BdgrzActor(userId, "Renamed manager"));

        // Act
        var retry = await scenario.DeclareHandler.HandleAsync(retryContext, CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.NotEqual(firstContext.RequestId, retryContext.RequestId);
        Assert.Equal(request.ApplicationId, first.Value.ApplicationId);
        Assert.Equal(first.Value.ApplicationId, retry.Value!.ApplicationId);
        var retained = Assert.Single(await scenario.ApplicationEventsAsync(first.Value.ApplicationId));
        Assert.Equal(original, retained);
        Assert.Equal(originalEventCount, await scenario.TenantApplicationEventCountAsync());
        var declared = Assert.IsType<ApplicationDeclared>(retained.Event);
        Assert.Equal("Payroll", declared.Name);
        Assert.Equal("Run payroll", declared.Purpose);
        Assert.Equal("Attribution", declared.OwnerReference);
        Assert.Equal("internal", declared.Classification);
        Assert.Equal("service", declared.ApplicationKind);
        Assert.Equal("critical", declared.Criticality);
        Assert.Equal(ownerId, declared.SystemOwnerPersonId);
        Assert.Equal(ownerId, declared.AccessOwnerPersonId);
        Assert.True(declared.IsRestricted);
        Assert.Equal(RbacIds.Member(scenario.TenantId, userId), declared.ActorMemberId);
        Assert.Equal("Original manager", declared.ActorDisplay);
        Assert.Equal(Now, declared.ChangedAt);
        Assert.Equal(firstContext.RequestId, declared.Metadata.CorrelationId);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("purpose")]
    [InlineData("owner_reference")]
    [InlineData("classification")]
    [InlineData("system_owner")]
    [InlineData("access_owner")]
    [InlineData("kind")]
    [InlineData("criticality")]
    [InlineData("restriction")]
    public async Task ShouldConflictWithoutAppendGivenChangedContentAtApplicationId(string changedField)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(true);
        var ownerId = await scenario.RecordPersonAsync("Owner");
        var otherOwner = await scenario.RecordPersonAsync("Other owner");
        var actor = BdgrzActor();
        var request = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, "Payroll", "Run payroll",
            "Attribution", "internal", ownerId, ownerId, false, "service", "critical");
        var first = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request, Uuid.CreateVersion4(), actor),
            CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var original = Assert.Single(await scenario.ApplicationEventsAsync(first.Value!.ApplicationId));
        var originalEventCount = await scenario.TenantApplicationEventCountAsync();
        var changed = changedField switch
        {
            "name" => request with { Name = "Changed" },
            "purpose" => request with { Purpose = "Changed" },
            "owner_reference" => request with { OwnerReference = "Changed" },
            "classification" => request with { Classification = "confidential" },
            "system_owner" => request with { SystemOwnerPersonId = otherOwner },
            "access_owner" => request with { AccessOwnerPersonId = otherOwner },
            "kind" => request with { ApplicationKind = "tool" },
            "criticality" => request with { Criticality = "low" },
            "restriction" => request with { IsRestricted = true },
            _ => throw new InvalidOperationException("Unknown changed declaration field."),
        };

        // Act
        var retry = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(changed, Uuid.CreateVersion4(), actor),
            CancellationToken.None);

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => retry.Value);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error!.Kind);
        Assert.Equal("This operation identity already has different accepted intent.", retry.Error.Message);
        Assert.Equal(original, Assert.Single(await scenario.ApplicationEventsAsync(first.Value.ApplicationId)));
        Assert.Equal(originalEventCount, await scenario.TenantApplicationEventCountAsync());
    }

    [Fact]
    public async Task ShouldReturnOriginalRegistrationGivenExactRetryAfterLaterRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var applicationId = Uuid.CreateVersion4();
        var request = new DeclareApplication(applicationId, scenario.TenantId, "Payroll", "Run payroll");
        var actor = BdgrzActor();
        var first = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request, applicationId, actor), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var revised = await scenario.ReviseHandler.HandleAsync(new FixedRequestContext<ReviseApplication>(
            new ReviseApplication(Uuid.CreateVersion4(), scenario.TenantId, applicationId, 1, "Payroll v2",
                "Run payroll", null), Uuid.CreateVersion4(), actor), CancellationToken.None);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        var events = await scenario.TenantApplicationEventCountAsync();

        // Act
        var replay = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request, Uuid.CreateVersion4(), actor),
            CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(applicationId, replay.Value!.ApplicationId);
        Assert.Equal(events, await scenario.TenantApplicationEventCountAsync());
    }

    [Fact]
    public async Task ShouldRejectEmptyApplicationIdWithoutAppendGivenDeclaration()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(true);
        var request = new DeclareApplication(Uuid.Empty, scenario.TenantId, "Payroll", "Run payroll");
        var originalEventCount = await scenario.TenantApplicationEventCountAsync();

        // Act
        var result = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request, Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(await scenario.ApplicationEventsAsync(Uuid.Empty));
        Assert.Equal(originalEventCount, await scenario.TenantApplicationEventCountAsync());
    }

    [Fact]
    public async Task ShouldRejectIncompleteFreshDeclarationGivenGovernedDeclarationsEnabled()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(true);
        var applicationId = Uuid.CreateVersion4();
        var request = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, "Payroll", "Run payroll");

        // Act
        var result = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request with { ApplicationId = applicationId }, applicationId),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(await scenario.ApplicationEventsAsync(applicationId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task ShouldAcceptLegacyDeclarationShapeGivenDisabledOrAbsentGate(bool? enabled)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(enabled);
        var applicationId = Uuid.CreateVersion4();
        var request = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, "Payroll", "Run payroll");

        // Act
        var result = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request with { ApplicationId = applicationId }, applicationId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var declared = Assert.IsType<ApplicationDeclared>(
            Assert.Single(await scenario.ApplicationEventsAsync(applicationId)).Event);
        Assert.Null(declared.ApplicationKind);
        Assert.Null(declared.Criticality);
        Assert.Null(declared.SystemOwnerPersonId);
        Assert.Null(declared.AccessOwnerPersonId);
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenExistingApplicationWithoutAcceptedDeclaration()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(true);
        var request = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, "Existing", "Existing app");

        // Act
        var result = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request with { ApplicationId = scenario.ExistingApplicationId }, scenario.ExistingApplicationId),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Single(await scenario.ApplicationEventsAsync(scenario.ExistingApplicationId));
    }

    [Fact]
    public async Task ShouldRecordTrimmedCompleteDeclarationGivenSameTenantPersonForBothOwners()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(true);
        var ownerId = await scenario.RecordPersonAsync("Owner");
        var applicationId = Uuid.CreateVersion4();
        var request = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, " Payroll ", " Run payroll ",
            " Attribution ", " internal ", ownerId, ownerId, true, " service ", " critical ");
        var context = new FixedRequestContext<DeclareApplication>(request with { ApplicationId = applicationId }, applicationId);

        // Act
        var result = await scenario.DeclareHandler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var declared = Assert.IsType<ApplicationDeclared>(
            Assert.Single(await scenario.ApplicationEventsAsync(applicationId)).Event);
        Assert.Equal(applicationId, declared.ApplicationId);
        Assert.Equal(scenario.TenantId, declared.TenantId);
        Assert.Equal("Payroll", declared.Name);
        Assert.Equal("Run payroll", declared.Purpose);
        Assert.Equal("Attribution", declared.OwnerReference);
        Assert.Equal("internal", declared.Classification);
        Assert.Equal("service", declared.ApplicationKind);
        Assert.Equal("critical", declared.Criticality);
        Assert.Equal(ownerId, declared.SystemOwnerPersonId);
        Assert.Equal(ownerId, declared.AccessOwnerPersonId);
        Assert.True(declared.IsRestricted);
        Assert.Equal(Now, declared.ChangedAt);
        Assert.Equal(ActorReference.ForMember(declared.ActorMemberId, declared.ActorDisplay),
            declared.StoredActor);
    }

    [Theory]
    [InlineData("missing", false)]
    [InlineData("missing", true)]
    [InlineData("foreign", false)]
    [InlineData("foreign", true)]
    [InlineData("empty", false)]
    [InlineData("empty", true)]
    public async Task ShouldRejectUnrecordedOwnerWithoutAppendGivenEnabledDeclaration(string state,
        bool invalidAccessOwner)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(true);
        var validOwner = await scenario.RecordPersonAsync("Owner");
        var invalidOwner = state == "empty" ? Uuid.Empty : Uuid.CreateVersion4();
        if (state == "foreign")
        {
            var person = new Person(Uuid.CreateVersion4(), invalidOwner);
            Assert.True(person.Record("Foreign Owner", null,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now).IsSuccess);
            await scenario.Writer.SaveAsync(person, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
        }
        var applicationId = Uuid.CreateVersion4();
        var request = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, "Payroll", "Run payroll",
            Classification: "internal", SystemOwnerPersonId: invalidAccessOwner ? validOwner : invalidOwner,
            AccessOwnerPersonId: invalidAccessOwner ? invalidOwner : validOwner,
            ApplicationKind: "service", Criticality: "critical");

        // Act
        var result = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(request with { ApplicationId = applicationId }, applicationId), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(await scenario.ApplicationEventsAsync(applicationId));
    }

    [Fact]
    public void ShouldConflictOnDirectRedeclarationGivenDeclaredApplication()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var systemOwner = Uuid.CreateVersion4();
        var accessOwner = Uuid.CreateVersion4();
        var application = new DeclaredApplication(tenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            Now, null, systemOwner, accessOwner).IsSuccess);

        // Act
        var replay = application.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            Now, null, systemOwner, accessOwner);
        var changedOwner = application.Declare("Payroll", "Run payroll", null, actorId,
            "Manager", Now, null, systemOwner, Uuid.CreateVersion4());
        var revised = application.Revise(1, "Payroll", "Run payroll", null, actorId,
            "Manager", Now.AddMinutes(1), null, accessOwner, null);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(replay.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(changedOwner.Error).Kind);
        Assert.Null(revised);
        var events = new AggregateScenario<DeclaredApplication>(application).PendingEvents;
        var declared = Assert.IsType<ApplicationDeclared>(events[0]);
        Assert.Equal(systemOwner, declared.SystemOwnerPersonId);
        Assert.Equal(accessOwner, declared.AccessOwnerPersonId);
        var revision = Assert.IsType<ApplicationRevised>(events[1]);
        Assert.Equal(accessOwner, revision.SystemOwnerPersonId);
        Assert.Null(revision.AccessOwnerPersonId);
    }

    [Fact]
    public async Task ShouldProjectOwnersAndMissingOwnerGapsGivenDeclarationAndRevision()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var legacyId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var systemOwner = Uuid.CreateVersion4();
        var accessOwner = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", Now, ApplicationFacts.Application(tenantId, applicationId), null, null, systemOwner));
            await directory.ApplyAsync(new ApplicationRevised(tenantId, applicationId, 2,
                "Payroll", "Run payroll", null, actorId, "Manager", Now.AddMinutes(1), ApplicationFacts.Application(tenantId, applicationId), null, null,
                systemOwner, accessOwner));
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, legacyId,
                "Legacy", "Declared before owners", "Operations", actorId, "Manager", Now, ApplicationFacts.Application(tenantId, legacyId), null));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, applicationId);
        var first = await directory.GetRevisionAsync(tenantId, applicationId, 1);
        var legacy = await directory.GetAsync(tenantId, legacyId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(systemOwner, current.SystemOwnerPersonId);
        Assert.Equal(accessOwner, current.AccessOwnerPersonId);
        Assert.DoesNotContain("system_owner_missing", current.Unresolved);
        Assert.DoesNotContain("access_owner_missing", current.Unresolved);
        Assert.NotNull(first);
        Assert.Equal(systemOwner, first.SystemOwnerPersonId);
        Assert.Null(first.AccessOwnerPersonId);
        Assert.Contains("access_owner_missing", first.Unresolved);
        Assert.NotNull(legacy);
        Assert.Null(legacy.SystemOwnerPersonId);
        Assert.Contains("system_owner_missing", legacy.Unresolved);
        Assert.Contains("access_owner_missing", legacy.Unresolved);
        Assert.Contains("owner_unverified", legacy.Unresolved);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectForeignTenantOwnerGivenApplicationWrite(bool revise,
        bool accessOwner)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var foreignTenantId = Uuid.CreateVersion4();
        var foreignOwnerId = Uuid.CreateVersion4();
        var foreignOwner = new Person(foreignTenantId, foreignOwnerId);
        Assert.True(foreignOwner.Record("Ada Lovelace", null,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now).IsSuccess);
        await scenario.Writer.SaveAsync(foreignOwner,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var requestId = Uuid.CreateVersion4();
        // Act
        RequestError? error;
        if (revise)
        {
            error = (await scenario.ReviseHandler.HandleAsync(scenario.ReviseContext(requestId,
                foreignOwnerId, accessOwner), CancellationToken.None)).Error;
        }
        else
        {
            var invalidOwner = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, "Payroll", "Run payroll",
                SystemOwnerPersonId: accessOwner ? null : foreignOwnerId,
                AccessOwnerPersonId: accessOwner ? foreignOwnerId : null);
            error = (await scenario.DeclareHandler.HandleAsync(
                new FixedRequestContext<DeclareApplication>(invalidOwner with { ApplicationId = requestId }, requestId),
                CancellationToken.None)).Error;
        }

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);
        Assert.Empty(await scenario.ApplicationEventsAsync(requestId));
    }

    [Fact]
    public async Task ShouldPreserveTenantOwnerReferencesGivenApplicationDeclarationAndRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var systemOwnerId = await scenario.RecordPersonAsync("System Owner");
        var accessOwnerId = await scenario.RecordPersonAsync("Access Owner");
        var applicationId = Uuid.CreateVersion4();
        var declaration = new DeclareApplication(Uuid.CreateVersion4(), scenario.TenantId, "Payroll", "Run payroll",
            SystemOwnerPersonId: systemOwnerId, AccessOwnerPersonId: accessOwnerId);
        var declared = await scenario.DeclareHandler.HandleAsync(
            new FixedRequestContext<DeclareApplication>(declaration with { ApplicationId = applicationId }, applicationId),
            CancellationToken.None);
        Assert.True(declared.IsSuccess);
        var revisionId = Uuid.CreateVersion4();
        var revision = new ReviseApplication(Uuid.CreateVersion4(), scenario.TenantId, applicationId, 1, "Payroll",
            "Run payroll", null, SystemOwnerPersonId: accessOwnerId,
            AccessOwnerPersonId: systemOwnerId);

        // Act
        var revised = await scenario.ReviseHandler.HandleAsync(
            new FixedRequestContext<ReviseApplication>(revision with { CommandId = revisionId }, revisionId),
            CancellationToken.None);

        // Assert
        Assert.True(revised.IsSuccess);
        var events = await scenario.ApplicationEventsAsync(applicationId);
        var applicationDeclared = Assert.IsType<ApplicationDeclared>(events[0].Event);
        Assert.Equal(systemOwnerId, applicationDeclared.SystemOwnerPersonId);
        Assert.Equal(accessOwnerId, applicationDeclared.AccessOwnerPersonId);
        var applicationRevised = Assert.IsType<ApplicationRevised>(events[1].Event);
        Assert.Equal(accessOwnerId, applicationRevised.SystemOwnerPersonId);
        Assert.Equal(systemOwnerId, applicationRevised.AccessOwnerPersonId);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        Scenario(ServiceProvider provider, Uuid tenantId, Uuid existingApplicationId)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            TenantId = tenantId;
            ExistingApplicationId = existingApplicationId;
        }

        public Uuid TenantId { get; }
        public Uuid ExistingApplicationId { get; }
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public FixedClock Clock => (FixedClock)_scope.ServiceProvider.GetRequiredService<TimeProvider>();
        IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();
        public DeclareApplicationHandler DeclareHandler =>
            ActivatorUtilities.CreateInstance<DeclareApplicationHandler>(_scope.ServiceProvider);
        public ReviseApplicationHandler ReviseHandler =>
            ActivatorUtilities.CreateInstance<ReviseApplicationHandler>(_scope.ServiceProvider);

        public static async Task<Scenario> CreateAsync(bool? governedDeclarationsEnabled = null)
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                    ["Fitz:ApplicationName"] = "compliance",
                    ["Compliance:Applications:GovernedDeclarationsEnabled"] =
                        governedDeclarationsEnabled?.ToString(),
                }).Build(), developerAuthentication: true);
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<TimeProvider>(new FixedClock());
            services.AddScoped(provider => RestrictedApplicationVisibilityFixture.Create(
                provider.GetRequiredService<IAggregateReader>(), organizationPermission: true));
            var existingId = Uuid.CreateVersion4();
            var scenario = new Scenario(services.BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true }), Uuid.CreateVersion4(),
                existingId);
            var existing = new DeclaredApplication(scenario.TenantId, existingId);
            Assert.True(existing.Declare("Existing", "Existing app", null,
                Uuid.CreateVersion4(), "Manager", Now).IsSuccess);
            await scenario.Writer.SaveAsync(existing,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            return scenario;
        }

        public async Task<Uuid> RecordPersonAsync(string displayName)
        {
            var personId = Uuid.CreateVersion4();
            var person = new Person(TenantId, personId);
            Assert.True(person.Record(displayName, null,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now).IsSuccess);
            await Writer.SaveAsync(person, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
            return personId;
        }

        public FixedRequestContext<ReviseApplication> ReviseContext(Uuid requestId,
            Uuid foreignOwnerId, bool accessOwner) =>
            new(new ReviseApplication(Uuid.CreateVersion4(), TenantId, ExistingApplicationId, 1, "Existing",
                "Existing app", null,
                SystemOwnerPersonId: accessOwner ? null : foreignOwnerId,
                AccessOwnerPersonId: accessOwner ? foreignOwnerId : null), requestId);

        public async Task<List<DomainEventRecord>> ApplicationEventsAsync(Uuid applicationId)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Events.ReadAsync(new EventStreamAddress(
                               TenantId.ToString(), "applications", applicationId.ToString()),
                               0, CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async Task<int> TenantApplicationEventCountAsync()
        {
            var count = 0;
            await foreach (var record in ((IDomainEventReader)Events).ReadAsync(
                               EventStreamPattern.ForPattern(TenantId.ToString(), "applications"),
                               ProjectionCheckpoint.Start.Cursor, CancellationToken.None))
                count++;
            return count;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    sealed class FixedRequestContext<TRequest>(TRequest request, Uuid requestId,
        ClaimsPrincipal? actor = null)
        : IRequestContext<TRequest> where TRequest : notnull
    {
        public TRequest Request => request;
        public ClaimsPrincipal Actor { get; } = actor ?? BdgrzActor();
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => requestId;
        public Uuid CorrelationId => requestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => requestId;
        public DateTimeOffset StartedAt { get; } = Now;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }

    static ClaimsPrincipal BdgrzActor(Uuid? userId = null, string display = "Manager") => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", (userId ?? Uuid.CreateVersion4()).ToString()),
            new Claim("email", display)],
        "BdgrzSession"));
}
