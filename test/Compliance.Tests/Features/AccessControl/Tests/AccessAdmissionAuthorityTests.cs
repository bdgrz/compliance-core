using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AccessAdmissionAuthorityTests
{
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string Target = "stream://bdgrz/criteria-editions/edition";

    [Fact]
    public async Task ShouldReturnBoundEvidenceGivenQualifiedTrustedOperatorPath()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var evidence = await fixture.AdmitAsync();

        // Assert
        Assert.NotNull(evidence);
        Assert.Equal(fixture.UserId, evidence.UserId);
        Assert.Equal(fixture.OperationId, evidence.OperationId);
        Assert.Equal(Target, evidence.Target);
        Assert.Equal(Digest, evidence.IntentSha256);
        Assert.Equal(At, evidence.AdmittedAt);
        Assert.Equal(12, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldDenyGivenQualifiedCurrentOperatorRevocation()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Set(AccessAdmissionStableRead.Slot.Operator,
            new AccessAdmissionSemanticEvaluator.OperatorGrant(fixture.UserId, false));

        // Act
        var evidence = await fixture.AdmitAsync();

        // Assert
        Assert.Null(evidence);
    }

    [Fact]
    public async Task ShouldDenyGivenCredentialHasExpiredAtAdmission()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Trusted.Credential = fixture.Trusted.Credential with { ExpiresAt = At };

        // Act
        var evidence = await fixture.AdmitAsync();

        // Assert
        Assert.Null(evidence);
    }

    [Theory]
    [InlineData("credential")]
    [InlineData("route")]
    [InlineData("source")]
    [InlineData("policy")]
    public async Task ShouldReportUnavailableGivenUnqualifiedRequiredAuthority(string missing)
    {
        // Arrange
        var fixture = new Fixture();
        if (missing == "credential")
            fixture.Trusted.CredentialQualified = false;
        if (missing == "route")
            fixture.Trusted.RouteQualified = false;
        if (missing == "source")
            fixture.Source.Qualified = false;
        if (missing == "policy")
            fixture.Set(AccessAdmissionStableRead.Slot.Policy,
            new AccessAdmissionSemanticEvaluator.Policy("unsupported", Digest, true, true, true, true));

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenCredentialDoesNotBindPrincipal()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Trusted.Credential = fixture.Trusted.Credential with { UserId = Uuid.CreateVersion4() };

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenCredentialLacksQualifiedIdentity()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Trusted.Credential = fixture.Trusted.Credential with { IdentityId = Uuid.Empty };

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenRequiredProviderIsMissing()
    {
        // Arrange
        var fixture = new Fixture();
        var authority = new AccessAdmissionAuthority(null, fixture.Trusted, fixture.Source, new FixedClock(), "v1", Digest);

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await authority.AdmitCriteriaOperatorAsync(new RequestDispatchContext(fixture.Actor), fixture.OperationId, Target, Digest));
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldPropagateCancellationGivenCancellationIsRequestedBeforeAdmission()
    {
        // Arrange
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Authority.AdmitCriteriaOperatorAsync(new RequestDispatchContext(fixture.Actor), fixture.OperationId, Target, Digest, cancellation.Token));
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenRouteQualificationIsLostDuringCapture()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Source.OnRead = () => fixture.Trusted.RouteQualified = false;

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldReportUnavailableWithoutSourceReadsGivenOversizedOperatorPath()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Trusted.Path = fixture.Trusted.Path.Concat([fixture.Trusted.Path[0]]).ToArray();

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldAdmitGivenCertifiedBdgrzIdentityOnlyPolicy()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseIdentityOnly();

        // Act
        var evidence = await fixture.AdmitAsync();

        // Assert
        Assert.NotNull(evidence);
        Assert.Null(evidence.Session);
        Assert.Null(evidence.Account);
        Assert.Null(evidence.GlobalVeto);
        Assert.Equal(6, fixture.Source.Reads);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("account")]
    [InlineData("global")]
    [InlineData("unknown")]
    public async Task ShouldRemainUnavailableGivenRequiredOrUnknownVetoMissing(string requirement)
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseIdentityOnly();
        fixture.Set(AccessAdmissionStableRead.Slot.Policy, new AccessAdmissionSemanticEvaluator.Policy("v1", Digest, true,
            requirement == "session" ? true : requirement == "unknown" ? null : false,
            requirement == "account", requirement == "global", true));

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldRemainUnavailableGivenRequiredSessionHasNoCredentialSessionId()
    {
        // Arrange
        var fixture = new Fixture();
        var session = fixture.Trusted.Path.Single(item => item.Role == AccessAdmissionStableRead.Slot.Session);
        fixture.UseIdentityOnly();
        fixture.Trusted.Path = fixture.Trusted.Path.Append(session).ToArray();
        fixture.Set(AccessAdmissionStableRead.Slot.Policy,
            new AccessAdmissionSemanticEvaluator.Policy("v1", Digest, true, true, false, false, true));

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Theory]
    [InlineData("uncertified")]
    [InlineData("wrong_identity")]
    [InlineData("foreign_scheme")]
    [InlineData("policy_not_enabled")]
    public async Task ShouldRemainUnavailableGivenIdentityOnlyModeIsNotBoundAndAuthorized(string failure)
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseIdentityOnly();
        if (failure == "uncertified")
            fixture.Trusted.Credential = fixture.Trusted.Credential with { ExactIdentityBindingCertified = false };
        if (failure == "wrong_identity")
            fixture.Trusted.Credential = fixture.Trusted.Credential with { IdentityId = Uuid.CreateVersion4() };
        if (failure == "foreign_scheme")
            fixture.Trusted.Credential = fixture.Trusted.Credential with { Scheme = (AccessAdmissionSemanticEvaluator.CredentialScheme)99 };
        if (failure == "policy_not_enabled")
            fixture.Set(AccessAdmissionStableRead.Slot.Policy,
            new AccessAdmissionSemanticEvaluator.Policy("v1", Digest, true, false, false, false));

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldDenyGivenIdentityWasRevokedBeforeIdentityOnlyAdmission()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseIdentityOnly();
        Assert.True(fixture.Identity.Revoke(Uuid.CreateVersion4(), At.AddMinutes(-1)).IsSuccess);

        // Act
        var evidence = await fixture.AdmitAsync();

        // Assert
        Assert.Null(evidence);
    }

    [Fact]
    public async Task ShouldRemainUnavailableGivenPolicyChangesDuringCapture()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.UseIdentityOnly();
        fixture.Source.OnRead = () =>
        {
            if (fixture.Source.Reads != 4)
                return;
            var stream = fixture.Source.Streams[AccessAdmissionStableRead.Slot.Policy];
            var before = fixture.Source.Values[stream];
            fixture.Source.Values[stream] = before with { Version = before.Version with { Position = before.Version.Position + 1 } };
        };

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () => await fixture.AdmitAsync());
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenProgramAdmissionIsUnimplemented()
    {
        // Arrange
        var fixture = new Fixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();

        // Act
        // Assert
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.Authority.AdmitProgramCreationAsync(new RequestDispatchContext(fixture.Actor), tenant, program, Target, Digest));
        await Assert.ThrowsAsync<AccessAdmissionUnavailableException>(async () =>
            await fixture.Authority.AdmitProgramSelectionAsync(new RequestDispatchContext(fixture.Actor), tenant, program, fixture.OperationId, Target, Digest));
    }

    sealed class Fixture
    {
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid OperationId { get; } = Uuid.CreateVersion4();
        public UserIdentity Identity { get; }
        public ClaimsPrincipal Actor { get; }
        public Source Source { get; } = new();
        public Trusted Trusted { get; }
        public AccessAdmissionAuthority Authority { get; }

        public Fixture()
        {
            var identity = new UserIdentity("provider", "subject");
            Identity = identity;
            Assert.True(identity.Register(UserId, null).IsSuccess);
            var session = Uuid.CreateVersion4();
            Actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", UserId.ToString()), new Claim("iss", "bdgrz")], "test"));
            var path = new AccessAdmissionStableRead.Participant[]
            {
                new(AccessAdmissionStableRead.Slot.Identity, identity.Stream.ToString()),
                new(AccessAdmissionStableRead.Slot.Policy, "stream://bdgrz/policy/current"),
                new(AccessAdmissionStableRead.Slot.Session, "stream://bdgrz/sessions/session"),
                new(AccessAdmissionStableRead.Slot.Account, "stream://bdgrz/accounts/user"),
                new(AccessAdmissionStableRead.Slot.GlobalVeto, "stream://bdgrz/veto/user"),
                new(AccessAdmissionStableRead.Slot.Operator, "stream://bdgrz/operators/user")
            };
            Trusted = new(new(UserId, identity.Id, session, UserId.ToString(), "bdgrz",
                At.AddMinutes(-1), At.AddMinutes(1)), path);
            foreach (var participant in path)
                Source.Streams.Add(participant.Role, participant.Stream);
            Set(AccessAdmissionStableRead.Slot.Identity, identity);
            Set(AccessAdmissionStableRead.Slot.Policy, new AccessAdmissionSemanticEvaluator.Policy("v1", Digest, true, true, true, true));
            Set(AccessAdmissionStableRead.Slot.Session, new AccessAdmissionSemanticEvaluator.Session(UserId, identity.Id, session, true));
            Set(AccessAdmissionStableRead.Slot.Account, new AccessAdmissionSemanticEvaluator.ActorVeto(UserId, false));
            Set(AccessAdmissionStableRead.Slot.GlobalVeto, new AccessAdmissionSemanticEvaluator.ActorVeto(UserId, false));
            Set(AccessAdmissionStableRead.Slot.Operator, new AccessAdmissionSemanticEvaluator.OperatorGrant(UserId, true));
            Authority = new(Trusted, Trusted, Source, new FixedClock(), "v1", Digest);
        }

        public void Set(AccessAdmissionStableRead.Slot slot, object state) =>
            Source.Values[Source.Streams[slot]] = new(new(Source.Streams[slot], "generation", 1), state);

        public void UseIdentityOnly()
        {
            ((ClaimsIdentity)Actor.Identity!).AddClaim(new Claim("user_identity_id", Identity.Id.ToString()));
            Trusted.Credential = Trusted.Credential with
            {
                SessionId = Uuid.Empty,
                Scheme = AccessAdmissionSemanticEvaluator.CredentialScheme.BdgrzIdentityOnly,
                ExactIdentityBindingCertified = true
            };
            Trusted.Path = Trusted.Path.Where(participant => participant.Role is not
                (AccessAdmissionStableRead.Slot.Session or AccessAdmissionStableRead.Slot.Account or AccessAdmissionStableRead.Slot.GlobalVeto)).ToArray();
            Set(AccessAdmissionStableRead.Slot.Policy, new AccessAdmissionSemanticEvaluator.Policy("v1", Digest, true,
                false, false, false, true));
        }

        public ValueTask<AccessAdmissionEvidence?> AdmitAsync() =>
            Authority.AdmitCriteriaOperatorAsync(new RequestDispatchContext(Actor), OperationId, Target, Digest);
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => At;
    }

    sealed class Trusted(AccessAdmissionSemanticEvaluator.Credential credential,
        IReadOnlyList<AccessAdmissionStableRead.Participant> path) : ITrustedAdmissionCredentialResolver, ITrustedAdmissionOperatorPathResolver
    {
        public bool CredentialQualified { get; set; } = true;
        public bool RouteQualified { get; set; } = true;
        bool ITrustedAdmissionCredentialResolver.IsQualified => CredentialQualified;
        bool ITrustedAdmissionOperatorPathResolver.IsQualified => RouteQualified;
        public AccessAdmissionSemanticEvaluator.Credential Credential { get; set; } = credential;
        public IReadOnlyList<AccessAdmissionStableRead.Participant> Path { get; set; } = path;
        public ValueTask<AccessAdmissionSemanticEvaluator.Credential?> ResolveAsync(IRequestContext context, CancellationToken ct) =>
            ValueTask.FromResult<AccessAdmissionSemanticEvaluator.Credential?>(Credential);
        public ValueTask<IReadOnlyList<AccessAdmissionStableRead.Participant>?> ResolveAsync(
            AccessAdmissionSemanticEvaluator.Credential actor, CancellationToken ct) =>
            ValueTask.FromResult<IReadOnlyList<AccessAdmissionStableRead.Participant>?>(Path);
    }

    sealed class Source : AccessAdmissionStableRead.ISource
    {
        public bool Qualified { get; set; } = true;
        public bool IsFreshnessQualified => Qualified;
        public int Reads { get; private set; }
        public Action? OnRead { get; set; }
        public Dictionary<AccessAdmissionStableRead.Slot, string> Streams { get; } = [];
        public Dictionary<string, AccessAdmissionStableRead.Observation> Values { get; } = [];
        public ValueTask<AccessAdmissionStableRead.Observation?> ReadCurrentAsync(string stream, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Reads++;
            OnRead?.Invoke();
            return ValueTask.FromResult(Values.GetValueOrDefault(stream));
        }
    }
}
