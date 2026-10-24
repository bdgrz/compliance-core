using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ProfessionalDutyGovernanceCompositionTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddMinutes(-1);
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Staff = Uuid.CreateVersion4();
    static readonly ClaimsPrincipal Actor = new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldDenyProfessionalDutyMutationGivenOnlyPlatformOperatorMetadataAuthority()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var http = new RequestDispatchContext(Actor,
            new HttpInvocation("PUT", "/synthetic/duty", "/synthetic/duty", "synthetic"));
        var staffRegistered = await FirmStaffFacts.RegisterThroughProcessAsync(provider, Actor,
            new RegisterFirmStaff(Staff, User, "attest", "Synthetic staff source", 0));
        Assert.True(staffRegistered.IsSuccess, staffRegistered.Error?.Message);
        var noDesignation = await bus.DispatchAsync(new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(),
            1, 1, 0, new string('a', 64), "Synthetic ratification source"), http, CancellationToken.None);

        // Act
        var designated = await bus.DispatchAsync(new RecordFirmProfessionalDutyDesignation(
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "synthetic invalid assertion", 0), http, CancellationToken.None);
        var deniedTransport = await bus.SendAsync(new RecordFirmProfessionalDutyDesignation(
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "synthetic invalid assertion", 0), Actor);
        var rules = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic authored rules");
        var draft = await bus.SendAsync(new ReviseIndependenceRules(0, rules), Actor);
        var ratification = await bus.DispatchAsync(new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(),
            1, 1, 0, IndependenceSourceDigest.RuleContent(rules), "Synthetic ratification evidence"),
            http, CancellationToken.None);
        var versions = await bus.SendAsync(new GetIndependenceRuleVersions(), Actor);
        var duties = await bus.DispatchAsync(new GetFirmProfessionalDutyCatalog(), http, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, noDesignation.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, designated.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, deniedTransport.Error?.Kind);
        Assert.True(draft.IsSuccess, draft.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, ratification.Error?.Kind);
        var version = Assert.Single(versions.Value!);
        Assert.False(version.IsRatified);
        Assert.Empty(duties.Value!.Designations);
    }

    [Fact]
    public async Task ShouldRetainAndRevokeCurrentDutyGivenSignedIssuerAssertions()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var duties = scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>();
        var http = new RequestDispatchContext(Actor,
            new HttpInvocation("PUT", "/synthetic/duty", "/synthetic/duty", "synthetic"));
        var staffRegistered = await FirmStaffFacts.RegisterThroughProcessAsync(provider, Actor,
            new RegisterFirmStaff(Staff, User, "attest", "Synthetic staff source", 0));
        Assert.True(staffRegistered.IsSuccess, staffRegistered.Error?.Message);
        var designationId = Uuid.CreateVersion4();
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(-1);
        var active = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, issuedAt, issuedAt.AddMinutes(30));
        var directDenied = await bus.SendAsync(new RecordFirmProfessionalDutyDesignation(designationId,
            active.Evidence.AssertionId, active.Token, 0), Actor);
        var mcpDenied = await bus.DispatchAsync(new RecordFirmProfessionalDutyDesignation(designationId,
            active.Evidence.AssertionId, active.Token, 0), new RequestDispatchContext(Actor, new McpInvocation("synthetic.duty.record")),
            CancellationToken.None);

        // Act
        var recorded = await ProfessionalDutyFacts.AdmitThroughProcessAsync(provider, Actor,
            new RecordFirmProfessionalDutyDesignation(designationId, active.Evidence.AssertionId, active.Token, 0),
            active);
        var currentBefore = await duties.ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null,
            CancellationToken.None);
        var revokedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var revokedAssertion = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 2, "revoked", active.Evidence.AssertionId,
            revokedAt, revokedAt.AddMinutes(30));
        var revoked = await ProfessionalDutyFacts.AdmitThroughProcessAsync(provider, Actor,
            new RevokeFirmProfessionalDutyDesignation(designationId, revokedAssertion.Evidence.AssertionId,
                revokedAssertion.Token, 1), revokedAssertion);
        var currentAfter = await duties.ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null,
            CancellationToken.None);
        await ProfessionalDutyFacts.ProjectDirectoryAsync(provider);
        var catalogAfterRevoke = await bus.DispatchAsync(new GetFirmProfessionalDutyCatalog(), http,
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, directDenied.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, mcpDenied.Error?.Kind);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.NotNull(currentBefore);
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.Null(currentAfter);
        var retained = Assert.Single(catalogAfterRevoke.Value!.Designations);
        Assert.False(retained.IsActive);
        Assert.Equal(revokedAssertion.Evidence.AssertionId, retained.AssertionId);
        Assert.Equal("revoked", retained.AuthorityStatus);
    }

    [Fact]
    public async Task ShouldNotReviveRatifiedRulesGivenRevokedAndReactivatedOrReplacedDutyLineage()
    {
        // Arrange
        var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var source = await SeedRatifiedRulesAsync(scope.ServiceProvider, at, at.AddMinutes(59));
        Assert.NotNull(await source.RulesReader.ReadActiveAsync(CancellationToken.None));

        // Act: a signed revocation ends the captured lineage.
        var revokedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var revocation = ProfessionalDutyAssertionTestIssuer.Issue(source.DesignationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 2, "revoked", source.InitialAssertion.Evidence.AssertionId,
            revokedAt.AddMinutes(-1), revokedAt.AddMinutes(29));
        var revoked = await ProfessionalDutyFacts.RevokeAsync(source.Services, revocation, source.OperatorActor, revokedAt);

        // Assert: neither reactivation of the same designation nor a replacement designation restores it.
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.Null(await source.RulesReader.ReadActiveAsync(CancellationToken.None));
        var reactivatedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var reactivation = ProfessionalDutyAssertionTestIssuer.Issue(source.DesignationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 3, "active", revocation.Evidence.AssertionId,
            reactivatedAt.AddMinutes(-1), reactivatedAt.AddMinutes(59));
        var reactivated = await ProfessionalDutyFacts.RecordAsync(source.Services, reactivation, source.Staff, source.OperatorActor, reactivatedAt);
        Assert.True(reactivated.IsSuccess, reactivated.Error?.Message);
        var currentReactivatedDuty = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        Assert.NotNull(currentReactivatedDuty);
        Assert.Equal(source.DesignationId, currentReactivatedDuty.Designation.DesignationId);
        Assert.Equal(3, currentReactivatedDuty.ContinuityStartRevision);
        Assert.Null(await source.RulesReader.ReadActiveAsync(CancellationToken.None));

        var reRevokedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var reRevocation = ProfessionalDutyAssertionTestIssuer.Issue(source.DesignationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 4, "revoked", reactivation.Evidence.AssertionId,
            reRevokedAt.AddMinutes(-1), reRevokedAt.AddMinutes(29));
        var revokedAgain = await ProfessionalDutyFacts.RevokeAsync(source.Services, reRevocation, source.OperatorActor, reRevokedAt);
        Assert.True(revokedAgain.IsSuccess, revokedAgain.Error?.Message);
        var replacementId = Uuid.CreateVersion4();
        var replacementAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var replacement = ProfessionalDutyAssertionTestIssuer.Issue(replacementId, User,
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null,
            replacementAt.AddMinutes(-1), replacementAt.AddMinutes(59));
        var replacementRecorded = await ProfessionalDutyFacts.RecordAsync(source.Services, replacement, source.Staff, source.OperatorActor, replacementAt);

        // Assert
        Assert.True(replacementRecorded.IsSuccess, replacementRecorded.Error?.Message);
        var currentReplacement = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        Assert.NotNull(currentReplacement);
        Assert.Equal(replacementId, currentReplacement.Designation.DesignationId);
        Assert.Null(await source.RulesReader.ReadActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ShouldNotPreserveRatifiedRulesGivenDutyExpiresBeforeRenewal()
    {
        // Arrange
        var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var expiresAt = at.AddMinutes(5);
        var clock = new MutableTimeProvider(at);
        await using var provider = Compose(clock);
        await using var scope = provider.CreateAsyncScope();
        var source = await SeedRatifiedRulesAsync(scope.ServiceProvider, at, expiresAt);
        Assert.NotNull(await source.RulesReader.ReadActiveAsync(CancellationToken.None));

        // Act
        clock.UtcNow = expiresAt.AddSeconds(1);
        var expiredDuty = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        var renewedAt = clock.GetUtcNow();
        var renewal = ProfessionalDutyAssertionTestIssuer.Issue(source.DesignationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 2, "active", source.InitialAssertion.Evidence.AssertionId,
            renewedAt.AddMinutes(-1), renewedAt.AddMinutes(59));
        var renewed = await ProfessionalDutyFacts.RecordAsync(source.Services, renewal, source.Staff, source.OperatorActor, renewedAt);

        // Assert
        Assert.Null(expiredDuty);
        Assert.True(renewed.IsSuccess, renewed.Error?.Message);
        var currentDuty = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        Assert.NotNull(currentDuty);
        Assert.Equal(2, currentDuty.ContinuityStartRevision);
        Assert.Null(await source.RulesReader.ReadActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ShouldNotPreserveRatifiedRulesGivenStaffDirectoryRevisionChangesBeforeRenewal()
    {
        // Arrange
        var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var clock = new MutableTimeProvider(at);
        await using var provider = Compose(clock);
        await using var scope = provider.CreateAsyncScope();
        var source = await SeedRatifiedRulesAsync(scope.ServiceProvider, at, at.AddMinutes(59));
        Assert.NotNull(await source.RulesReader.ReadActiveAsync(CancellationToken.None));

        // Act
        var deactivatedAt = at.AddMinutes(1);
        await FirmStaffFacts.SetStatusAsync(provider, Staff, false, "Synthetic deactivation", deactivatedAt);
        var reactivatedAt = at.AddMinutes(2);
        var updatedStaff = await FirmStaffFacts.SetStatusAsync(provider, Staff, true, "Synthetic reactivation",
            reactivatedAt);
        clock.UtcNow = reactivatedAt;
        var dutyBeforeRenewal = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        var renewedAssertion = ProfessionalDutyAssertionTestIssuer.Issue(source.DesignationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 2, "active", source.InitialAssertion.Evidence.AssertionId,
            reactivatedAt.AddMinutes(-1), reactivatedAt.AddMinutes(59));
        var renewed = await ProfessionalDutyFacts.RecordAsync(source.Services, renewedAssertion, updatedStaff, source.OperatorActor, reactivatedAt);

        // Assert
        Assert.Null(dutyBeforeRenewal);
        Assert.True(renewed.IsSuccess, renewed.Error?.Message);
        var currentDuty = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        Assert.NotNull(currentDuty);
        Assert.Equal(updatedStaff.Revision, currentDuty.Staff.Revision);
        Assert.Equal(2, currentDuty.ContinuityStartRevision);
        Assert.Null(await source.RulesReader.ReadActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ShouldPreserveRatifiedRulesGivenUninterruptedSignedRatifierRenewal()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var operatorActor = ActorReference.ForPlatformOperator(User, "Synthetic operator");
        var staff = await FirmStaffFacts.RegisterAsync(provider, Staff, User, "attest", "Synthetic staff source",
            operatorActor, Now);
        var designationId = Uuid.CreateVersion4();
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, Now, Now.AddHours(1));
        var seededDuty = await ProfessionalDutyFacts.RecordAsync(scope.ServiceProvider, assertion, staff, operatorActor, Now);
        Assert.True(seededDuty.IsSuccess, seededDuty.Error?.Message);
        var content = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic rules");
        var seededRules = await executor.ExecuteAsync(new IndependenceRuleCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence,
                content, operatorActor, Now)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededRules.IsSuccess, seededRules.Error?.Message);
        var seededRatification = await executor.ExecuteAsync(new IndependenceRuleRatificationCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1,
                catalog.Sequence, IndependenceSourceDigest.RuleContent(content), "Synthetic ratification evidence",
                staff, seededDuty.Value!, ActorReference.ForFirmStaff(User, "Synthetic ratifier"), Now)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededRatification.IsSuccess, seededRatification.Error?.Message);
        var rulesReader = scope.ServiceProvider.GetRequiredService<CurrentRatifiedIndependenceRulesReader>();
        Assert.NotNull(await rulesReader.ReadActiveAsync(CancellationToken.None));
        var renewedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var renewedAssertion = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 2, "active", assertion.Evidence.AssertionId,
            renewedAt.AddMinutes(-1), renewedAt.AddMinutes(59));

        // Act
        var renewal = await ProfessionalDutyFacts.RecordAsync(scope.ServiceProvider, renewedAssertion, staff, operatorActor, renewedAt);

        // Assert
        Assert.True(renewal.IsSuccess, renewal.Error?.Message);
        var currentOwner = await reader.HydrateAsync(
            new FirmProfessionalDutyDesignation(User, FirmProfessionalDuty.RuleRatifier, null), CancellationToken.None);
        var currentDesignation = await scope.ServiceProvider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<FirmProfessionalDutyDesignationView>("bdgrz", currentOwner.LatestContent!);
        Assert.Equal(2, currentDesignation.Revision);
        Assert.True(scope.ServiceProvider.GetRequiredService<IFirmProfessionalDutyAssertionVerifier>()
            .TryVerify(currentOwner.LatestSignedAssertion, DateTimeOffset.UtcNow, out _));
        var currentDuty = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        Assert.NotNull(currentDuty);
        Assert.Equal(1, currentDuty.ContinuityStartRevision);
        var preservedRules = Assert.IsType<IndependenceRuleVersionView>(
            await rulesReader.ReadActiveAsync(CancellationToken.None));
        Assert.Equal(1, preservedRules.Ratification?.DutyRevision);
        Assert.Equal(designationId, preservedRules.Ratification?.DutyDesignationId);
        Assert.Equal(User, preservedRules.Ratification?.UserId);
        Assert.Equal(Staff, preservedRules.Ratification?.StaffMemberId);
        Assert.Equal(staff.Revision, preservedRules.Ratification?.DirectoryStaffRevision);
    }

    static async Task<SeededRuleSource> SeedRatifiedRulesAsync(IServiceProvider services, DateTimeOffset at,
        DateTimeOffset expiresAt)
    {
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var reader = services.GetRequiredService<IAggregateReader>();
        var operatorActor = ActorReference.ForPlatformOperator(User, "Synthetic operator");
        var staff = await FirmStaffFacts.RegisterAsync(services, Staff, User, "attest", "Synthetic staff source",
            operatorActor, at);
        var designationId = Uuid.CreateVersion4();
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, at, expiresAt);
        var seededDuty = await ProfessionalDutyFacts.RecordAsync(services, assertion, staff, operatorActor, at);
        Assert.True(seededDuty.IsSuccess, seededDuty.Error?.Message);
        var content = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic rules");
        var seededRules = await executor.ExecuteAsync(new IndependenceRuleCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence,
                content, operatorActor, at)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededRules.IsSuccess, seededRules.Error?.Message);
        var seededRatification = await executor.ExecuteAsync(new IndependenceRuleRatificationCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1,
                catalog.Sequence, IndependenceSourceDigest.RuleContent(content), "Synthetic ratification evidence",
                staff, seededDuty.Value!, ActorReference.ForFirmStaff(User, "Synthetic ratifier"), at)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededRatification.IsSuccess, seededRatification.Error?.Message);
        return new SeededRuleSource(services, executor,
            services.GetRequiredService<CurrentRatifiedIndependenceRulesReader>(), operatorActor,
            staff, designationId, assertion);
    }

    static ServiceProvider Compose(TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        var configurationValues = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        };
        ProfessionalDutyAssertionTestIssuer.AddConfiguration(configurationValues);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IPlatformOperatorAccess>(new OperatorAccess(true));
        services.AddSingleton<IPlatformUserDirectoryReader>(new UserDirectory());
        if (clock is not null)
            services.AddSingleton<TimeProvider>(clock);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed record SeededRuleSource(IServiceProvider Services, IAggregateExecutor Executor,
        CurrentRatifiedIndependenceRulesReader RulesReader, ActorReference OperatorActor,
        FirmStaffMemberView Staff, Uuid DesignationId,
        ProfessionalDutyAssertionTestIssuer.SignedAssertion InitialAssertion);

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    sealed class OperatorAccess(bool allowed) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed);
    }

    sealed class UserDirectory : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(userId == User);
    }
}
