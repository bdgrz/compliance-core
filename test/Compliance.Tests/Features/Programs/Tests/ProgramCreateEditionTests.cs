using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Handlers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramCreateEditionTests
{
    [Fact]
    public void ShouldExposeSelectedEditionGivenCreateProgramWireContract()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var command = new CreateProgram(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "SOC 2", Plan(), edition);

        // Act
        var json = JsonSerializer.Serialize(command, ComplianceCoreJsonContext.Default.CreateProgram);
        using var document = JsonDocument.Parse(json);

        // Assert
        Assert.True(document.RootElement.TryGetProperty("edition_id", out var selected));
        Assert.Equal(edition.ToString(), selected.GetString());
    }

    [Fact]
    public void ShouldRejectMissingEditionGivenCreateProgramWireContract()
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        var tenant = Uuid.CreateVersion4();
        var json = $"{{\"program_id\":\"{program}\",\"tenant_id\":\"{tenant}\",\"name\":\"SOC 2\",\"plan\":{{\"target_readiness_date\":null,\"target_type_i_as_of_date\":null,\"target_type_ii_start_date\":null,\"target_type_ii_end_date\":null,\"readiness_advisor\":null,\"audit_firm\":null}}}}";

        // Act
        var failure = Record.Exception(() => JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.CreateProgram));

        // Assert
        Assert.IsType<JsonException>(failure);
    }

    [Fact]
    public async Task ShouldBindPublishedEditionAndReturnOriginalResultGivenExactCreateRetry()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var proof = new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1);
        var authority = new PublicationAuthority(proof);
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);

        // Act
        var first = await bus.SendAsync(command, actor);
        var retry = await bus.SendAsync(command with { Name = " SOC 2 " }, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);
        var physicalFacts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program, includeFences: true);
        var rules = await scope.ServiceProvider.GetRequiredService<ProgramReader>()
            .RulesAsync(tenant, program);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(program, retry.Value.ProgramId);
        var created = Assert.IsType<ProgramCreated>(Assert.Single(facts));
        Assert.Equal(edition, created.CriteriaEditionId);
        Assert.Equal(proof, created.CriteriaPublication);
        Assert.NotNull(created.AccessAdmission);
        var fence = Assert.IsType<ProgramCreationAttemptFenced>(physicalFacts[0]);
        Assert.Equal(1UL, fence.Generation);
        Assert.Equal(fence.Generation, created.CreationGeneration);
        Assert.Equal(edition, rules.CriteriaEditionId);
    }

    [Fact]
    public async Task ShouldRejectStaleGenerationAndChangedIntentGivenCreationFence()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        await using var provider = Services(new PublicationAuthority(
            new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1)));
        await using var scope = provider.CreateAsyncScope();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateProgram(program, tenant, "SOC 2", Plan(), edition), actor);
        Assert.True(result.IsSuccess, result.Error?.Message);
        var physical = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program, includeFences: true);
        var fence = Assert.IsType<ProgramCreationAttemptFenced>(physical[0]);
        var owner = new ComplianceProgram(tenant, program, program);
        new AggregateScenario<ComplianceProgram>(owner).Given(fence);

        // Act
        var stale = owner.PrepareCreate(0, fence.Operation);
        var changed = owner.PrepareCreate(fence.Generation,
            fence.Operation with { AuthoredIntentSha256 = new string('a', 64) });
        var exact = owner.PrepareCreate(fence.Generation, fence.Operation);
        var reusedAdmission = ProgramAdmissionEvidence.Matches(fence.AccessAdmission, tenant, program,
            owner.Stream, fence.Operation, fence.Generation + 1);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.True(exact.IsSuccess);
        Assert.False(reusedAdmission);
        Assert.False(owner.IsCreated);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public async Task ShouldRejectChangedEditionGivenCreatedProgramIdentity()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var firstEdition = Uuid.CreateVersion4();
        var secondEdition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(
            new CriteriaPublicationProof(firstEdition, Uuid.CreateVersion4(), 1),
            new CriteriaPublicationProof(secondEdition, Uuid.CreateVersion4(), 1));
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), firstEdition);
        var first = await bus.SendAsync(command, actor);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var changed = await bus.SendAsync(command with { EditionId = secondEdition }, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(firstEdition, Assert.IsType<ProgramCreated>(Assert.Single(facts)).CriteriaEditionId);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenPublicationSourceUnavailableAfterCreation()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1));
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);
        var first = await bus.SendAsync(command, actor);
        Assert.True(first.IsSuccess, first.Error?.Message);
        authority.Unavailable = true;

        // Act
        var retry = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.IsType<ProgramCreated>(Assert.Single(facts));
    }

    [Fact]
    public async Task ShouldAcceptOneEditionGivenCompetingCreatesForOneProgramIdentity()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var firstEdition = Uuid.CreateVersion4();
        var secondEdition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(
            new CriteriaPublicationProof(firstEdition, Uuid.CreateVersion4(), 1),
            new CriteriaPublicationProof(secondEdition, Uuid.CreateVersion4(), 1));
        await using var provider = Services(authority);
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var first = new CreateProgram(program, tenant, "SOC 2", Plan(), firstEdition);
        var second = first with { EditionId = secondEdition };

        // Act
        async Task<Result<ProgramRegistration>> SendAsync(CreateProgram command)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command, actor);
        }

        var results = await Task.WhenAll(Task.Run(() => SendAsync(first)), Task.Run(() => SendAsync(second)));
        await using var resultScope = provider.CreateAsyncScope();
        var facts = await ReadProgramEventsAsync(resultScope.ServiceProvider, tenant, program);

        // Assert
        Assert.Single(results, value => value.IsSuccess);
        Assert.Single(results, value => !value.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, results.Single(value => !value.IsSuccess).Error?.Kind);
        var created = Assert.IsType<ProgramCreated>(Assert.Single(facts));
        Assert.Contains(created.CriteriaEditionId, new Uuid?[] { firstEdition, secondEdition });
    }

    [Fact]
    public async Task ShouldRefuseWithoutProgramFactGivenEditionIsNotPublished()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var authority = new PublicationAuthority();
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), Uuid.CreateVersion4());

        // Act
        var result = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(facts);
    }

    [Fact]
    public async Task ShouldAllowSameProgramIdentityGivenEditionIsPublishedAfterInitialRefusal()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority();
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);
        var before = await bus.SendAsync(command, actor);
        Assert.False(before.IsSuccess);
        Assert.Empty(await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program));
        authority.Add(new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1));

        // Act
        var after = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.True(after.IsSuccess, after.Error?.Message);
        Assert.Equal(program, after.Value.ProgramId);
        Assert.Equal(edition, Assert.IsType<ProgramCreated>(Assert.Single(facts)).CriteriaEditionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutProgramFactGivenPublicationProofIsMalformed(bool missingEventId)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var returned = missingEventId
            ? new CriteriaPublicationProof(edition, Uuid.Empty, 0)
            : new CriteriaPublicationProof(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0);
        var authority = new PublicationAuthority { OverrideProof = returned };
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);

        // Act
        var result = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(facts);
    }

    [Fact]
    public async Task ShouldRefuseWithoutProgramFactGivenEditionIdentityIsEmpty()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var authority = new PublicationAuthority();
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), Uuid.Empty);

        // Act
        var result = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(facts);
    }

    [Fact]
    public async Task ShouldRefuseWithoutProgramFactGivenSourceAdmissionDeniesCreation()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1));
        var admission = new ProgramAdmissionAuthority { Deny = true };
        await using var provider = Services(authority, admission);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);

        // Act
        var result = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(facts);
    }

    [Fact]
    public async Task ShouldReturnRetryableUnavailableWithoutProgramFactGivenAdmissionIsUnavailable()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1));
        var admission = new ProgramAdmissionAuthority { Unavailable = true };
        await using var provider = Services(authority, admission);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);

        // Act
        var result = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program, includeFences: true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Empty(facts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutProgramFactGivenAdmissionIsBoundToDifferentIntent(bool wrongTarget)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1));
        var admission = new ProgramAdmissionAuthority { WrongTarget = wrongTarget, WrongIntent = !wrongTarget };
        await using var provider = Services(authority, admission);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);

        // Act
        var result = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(facts);
    }

    [Fact]
    public async Task ShouldNotDiscloseOriginalResultGivenAuthorizationRevokedBeforeExactRetry()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1));
        var admission = new ProgramAdmissionAuthority();
        await using var provider = Services(authority, admission);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), edition);
        var first = await bus.SendAsync(command, actor);
        Assert.True(first.IsSuccess, first.Error?.Message);
        admission.Deny = true;

        // Act
        var retry = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.IsType<ProgramCreated>(Assert.Single(facts));
    }

    [Fact]
    public async Task ShouldRemainRetryableWithoutProgramFactGivenPublicationAuthorityIsUnavailable()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var authority = new PublicationAuthority { Unavailable = true };
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var command = new CreateProgram(program, tenant, "SOC 2", Plan(), Uuid.CreateVersion4());

        // Act
        var result = await bus.SendAsync(command, actor);
        var facts = await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error?.IsTransient);
        Assert.Empty(facts);
    }

    [Fact]
    public async Task ShouldRetainUnknownEditionGivenLegacyProgramCreation()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var authority = new PublicationAuthority(new CriteriaPublicationProof(edition, Uuid.CreateVersion4(), 1));
        await using var provider = Services(authority);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var result = await bus.SendAsync(new CreateProgram(program, tenant, "SOC 2", Plan(), edition), actor);
        Assert.True(result.IsSuccess, result.Error?.Message);
        var current = Assert.IsType<ProgramCreated>(Assert.Single(
            await ReadProgramEventsAsync(scope.ServiceProvider, tenant, program)));
        var historicalJson = $"{{\"program_id\":\"{program}\",\"tenant_id\":\"{tenant}\",\"name\":\"SOC 2\",\"plan\":{{\"target_readiness_date\":null,\"target_type_i_as_of_date\":null,\"target_type_ii_start_date\":null,\"target_type_ii_end_date\":null,\"readiness_advisor\":null,\"audit_firm\":null}}}}";
        var historicalDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(historicalJson)))
            .ToLowerInvariant();
        var historical = current with
        {
            CriteriaEditionId = null,
            CriteriaPublication = null,
            AccessAdmission = null,
            CreationGeneration = 0,
            Operation = current.Operation with { AuthoredIntentSha256 = historicalDigest },
        };
        historical.AttachMetadata(current.Metadata with { AggregateVersion = 1 });
        var events = new InMemoryEventStore();
        await events.AppendAsync(new ComplianceProgram(tenant, program).Stream, 0, [historical]);
        IServiceCollection legacyServices = new ServiceCollection();
        legacyServices.AddSingleton<IEventStore>(events);
        legacyServices.AddSingleton<IDomainEventReader>(events);
        legacyServices.AddPortia();
        legacyServices.AddVerifiedProgramReader();
        await using var legacyProvider = legacyServices.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var legacyScope = legacyProvider.CreateAsyncScope();
        var legacyReader = new ProgramReader(legacyScope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            events, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());

        // Act
        var history = await legacyReader.HistoryAsync(tenant, program);
        var replay = await legacyReader.RulesAsync(tenant, program);

        // Assert
        var created = Assert.IsType<ProgramCreated>(Assert.Single(history.Facts));
        Assert.Null(created.CriteriaEditionId);
        Assert.Null(created.CriteriaPublication);
        Assert.Null(history.Owner.CriteriaEditionId);
        Assert.Null(replay.CriteriaEditionId);
        Assert.Equal(historicalDigest, created.Operation.AuthoredIntentSha256);
    }

    static ServiceProvider Services(ICriteriaPublicationAuthority authority,
        IAccessAdmissionAuthority? admission = null) =>
        ProgramManagementServices.Build(new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateProgramHandler>(),
            services =>
            {
                services.AddSingleton(authority);
                services.AddSingleton(admission ?? new ProgramAdmissionAuthority());
            });

    static ProgramPlan Plan() => new(null, null, null, null, null, null);

    static async Task<List<DomainEvent>> ReadProgramEventsAsync(IServiceProvider services,
        Uuid tenant, Uuid program, bool includeFences = false)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in services.GetRequiredService<IEventStore>().ReadAsync(
                           new ComplianceProgram(tenant, program).Stream, 0, CancellationToken.None))
            if (includeFences || record.Event is not ProgramCreationAttemptFenced)
                facts.Add(record.Event);
        return facts;
    }

    sealed class PublicationAuthority(params CriteriaPublicationProof[] proofs) : ICriteriaPublicationAuthority
    {
        readonly Dictionary<Uuid, CriteriaPublicationProof> _proofs = proofs.ToDictionary(value => value.EditionId);
        public bool Unavailable { get; set; }
        public CriteriaPublicationProof? OverrideProof { get; init; }

        public void Add(CriteriaPublicationProof proof) => _proofs.Add(proof.EditionId, proof);

        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Unavailable)
                return ValueTask.FromResult(Result<CriteriaPublicationProof>.Failure(
                    new RequestError(RequestErrorKind.Conflict, "Publication authority is unavailable.", true)));
            if (OverrideProof is { } returned)
                return ValueTask.FromResult(Result<CriteriaPublicationProof>.Success(returned));
            return ValueTask.FromResult(_proofs.TryGetValue(editionId, out var proof)
                ? Result<CriteriaPublicationProof>.Success(proof)
                : Result<CriteriaPublicationProof>.Failure(
                    new RequestError(RequestErrorKind.NotFound, "Edition is not published.")));
        }
    }

    sealed class ProgramAdmissionAuthority : IAccessAdmissionAuthority
    {
        public bool Deny { get; set; }
        public bool Unavailable { get; init; }
        public bool WrongTarget { get; init; }
        public bool WrongIntent { get; init; }

        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext actor,
            Uuid operationId, string target, string intentSha256, CancellationToken ct = default) =>
            ValueTask.FromResult<AccessAdmissionEvidence?>(null);

        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext actor,
            Uuid tenantId, Uuid programId, Uuid operationId, string target, string intentSha256,
            CancellationToken ct = default) => ValueTask.FromResult<AccessAdmissionEvidence?>(null);

        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext actor,
            Uuid tenantId, Uuid programId, string target, string intentSha256,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Unavailable)
                throw new AccessAdmissionUnavailableException();
            if (Deny)
                return ValueTask.FromResult<AccessAdmissionEvidence?>(null);
            var subject = actor.Actor.Claims.Single(claim => claim.Type == "sub").Value;
            var issuer = actor.Actor.Claims.Single(claim => claim.Type == "iss").Value;
            if (!Uuid.TryParse(subject, CultureInfo.InvariantCulture, out var userId))
                throw new InvalidOperationException("The test actor does not have a valid subject.");
            static AccessAdmissionSourceVersion Source(string realm, string area, string resource) =>
                new(new EventStreamAddress(realm, area, resource).ToString(), "test-generation", 1);
            var actualTarget = WrongTarget
                ? new EventStreamAddress(tenantId.ToString(), "programs", Uuid.CreateVersion4().ToString()).ToString()
                : target;
            var actualDigest = WrongIntent ? (intentSha256 == new string('0', 64) ? new string('1', 64) : new string('0', 64)) : intentSha256;
            var evidence = new AccessAdmissionEvidence(userId, subject, issuer, programId, actualTarget,
                actualDigest, new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "policy-1",
                Source("bdgrz", "identities", userId.ToString()),
                Source("bdgrz", "policies", "current"))
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Tenant = Source("bdgrz", "tenants", tenantId.ToString()),
                Member = Source(tenantId.ToString(), "members", userId.ToString()),
                Primary = Source(tenantId.ToString(), "primary", "current"),
            };
            return ValueTask.FromResult<AccessAdmissionEvidence?>(evidence);
        }
    }
}
