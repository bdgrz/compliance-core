using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Artifacts.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

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
        var noDesignation = await bus.DispatchAsync(new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            1, 1, 0, new string('a', 64), "Synthetic ratification source", 0), http, CancellationToken.None);

        // Act
        var designated = await bus.DispatchAsync(new RecordFirmProfessionalDutyDesignation(
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "synthetic invalid assertion", 0), http, CancellationToken.None);
        var deniedTransport = await bus.SendAsync(new RecordFirmProfessionalDutyDesignation(
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "synthetic invalid assertion", 0), Actor);
        var rules = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic authored rules");
        var draft = await bus.SendAsync(new ReviseIndependenceRules(Uuid.CreateVersion4(), 0, rules), Actor);
        var ratification = await bus.DispatchAsync(new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            1, 1, 0, IndependenceSourceDigest.RuleContent(rules), "Synthetic ratification evidence", 0),
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
        await ProgramManagementServices.SeedAsync(provider, new IndependenceRuleCatalogRules(), catalog =>
            catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence, content, operatorActor, Now));
        var seededRules = await scope.ServiceProvider.GetRequiredService<IndependenceRuleCatalogReader>().ReadAsync();
        Assert.NotNull(seededRules.Current);
        var ratificationId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider, new IndependenceRuleRatificationRules(), catalog =>
            catalog.Ratify(Uuid.CreateVersion4(), ratificationId, 1, 1, catalog.Sequence, IndependenceSourceDigest.RuleContent(content),
                "Synthetic ratification evidence", staff, seededDuty.Value!, ActorReference.ForFirmStaff(User, "Synthetic ratifier"), Now));
        var seededRatification = await scope.ServiceProvider.GetRequiredService<IndependenceRuleRatificationReader>().ReadAsync();
        Assert.Equal(ratificationId, seededRatification.Active!.RatificationId);
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

    [Fact]
    public async Task ShouldReturnOriginalRatificationGivenImmediateExactHandlerRetry()
    {
        // Arrange
        var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(-1);
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var seeded = await SeedRatifiedRulesAsync(scope.ServiceProvider, at, at.AddMinutes(59));
        var rules = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Second version for retry proof");
        await ProgramManagementServices.SeedAsync(seeded.Services, new IndependenceRuleCatalogRules(), current =>
            current.ReviseRules(Uuid.CreateVersion4(), 1, rules, seeded.OperatorActor, at));
        Assert.Equal(2, (await seeded.Services.GetRequiredService<IndependenceRuleCatalogReader>().ReadAsync()).Current!.Version);
        var request = new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, 1,
            IndependenceSourceDigest.RuleContent(rules), "Original retry decision", 1);
        var context = new Cntryl.Portia.RequestContext<RatifyIndependenceRuleVersion>(request,
            new RequestDispatchContext(Actor, new HttpInvocation("PUT", "/synthetic/ratify", null, "ratify-retry")));
        var handler = ActivatorUtilities.CreateInstance<RatifyIndependenceRuleVersionHandler>(scope.ServiceProvider);
        var first = await handler.HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        var changed = await handler.HandleAsync(new Cntryl.Portia.RequestContext<RatifyIndependenceRuleVersion>(
            request with { ExpectedRevision = request.ExpectedRevision + 1 }, context), CancellationToken.None);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new IndependenceRuleRatificationCatalog());
        Assert.Equal(2, current.Sequence);
    }

    [Fact]
    public async Task ShouldReplayOriginalPersonalDecisionBeforeLaterCatalogStateAndRequireCurrentDutyGivenHistoricalRetry()
    {
        // Arrange
        var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(-1);
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var source = await SeedRatifiedRulesAsync(scope.ServiceProvider, at, at.AddMinutes(59));
        var ratifications = scope.ServiceProvider.GetRequiredService<IndependenceRuleRatificationReader>();
        var initial = await ratifications.ReadAsync();
        var node = initial.Content!;
        var view = initial.Active!;
        var original = new RatifyIndependenceRuleVersion(node.RequestId, view.RatificationId, view.RuleVersion,
            view.ObservedRuleCatalogSequence, 0, view.RuleContentDigest, view.SourceReference, 0);
        var context = new Cntryl.Portia.RequestContext<RatifyIndependenceRuleVersion>(original,
            new RequestDispatchContext(Actor, new HttpInvocation("PUT", "/synthetic/ratify", null, "ratify-history-retry")));
        var content = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], "Later catalog source");
        await ProgramManagementServices.SeedAsync(provider, new IndependenceRuleCatalogRules(), rules =>
            rules.ReviseRules(Uuid.CreateVersion4(), rules.Sequence, content, source.OperatorActor, at));
        var currentDuty = await scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null, CancellationToken.None);
        Assert.NotNull(currentDuty);
        await ProgramManagementServices.SeedAsync(provider, new IndependenceRuleRatificationRules(), rules =>
            rules.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, rules.Sequence, IndependenceSourceDigest.RuleContent(content),
                "Later personal decision", currentDuty.Staff, currentDuty.Designation, ActorReference.ForFirmStaff(User, "Ratifier"), at));
        var handler = ActivatorUtilities.CreateInstance<RatifyIndependenceRuleVersionHandler>(scope.ServiceProvider);

        // Act
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var changed = await handler.HandleAsync(new Cntryl.Portia.RequestContext<RatifyIndependenceRuleVersion>(
            original with { SourceReference = "Changed historical intent" }, context), CancellationToken.None);
        var revokedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(source.DesignationId, User, FirmProfessionalDuty.RuleRatifier, null,
            2, "revoked", source.InitialAssertion.Evidence.AssertionId, revokedAt.AddMinutes(-1), revokedAt.AddMinutes(30));
        var revoked = await ProfessionalDutyFacts.RevokeAsync(scope.ServiceProvider, assertion, source.OperatorActor, revokedAt);
        var denied = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(view, replay.Value);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(2, (await ratifications.ReadAsync()).Sequence);
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
        await ProgramManagementServices.SeedAsync(services, new IndependenceRuleCatalogRules(), catalog =>
            catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence, content, operatorActor, at));
        var seededRules = await services.GetRequiredService<IndependenceRuleCatalogReader>().ReadAsync();
        Assert.NotNull(seededRules.Current);
        var ratificationId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(services, new IndependenceRuleRatificationRules(), catalog =>
            catalog.Ratify(Uuid.CreateVersion4(), ratificationId, 1, 1, catalog.Sequence, IndependenceSourceDigest.RuleContent(content),
                "Synthetic ratification evidence", staff, seededDuty.Value!, ActorReference.ForFirmStaff(User, "Synthetic ratifier"), at));
        var seededRatification = await services.GetRequiredService<IndependenceRuleRatificationReader>().ReadAsync();
        Assert.Equal(ratificationId, seededRatification.Active!.RatificationId);
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
