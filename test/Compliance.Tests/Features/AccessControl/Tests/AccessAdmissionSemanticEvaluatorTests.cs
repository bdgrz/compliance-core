using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AccessAdmissionSemanticEvaluatorTests
{
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ShouldAdmitOperatorGivenCompleteStableCurrentPath()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var decision = await fixture.Evaluator.EvaluateOperatorAsync(fixture.Candidate);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Allowed, decision.Status);
        Assert.NotNull(decision.Evidence);
        Assert.Equal(1, decision.Evidence.FormatVersion);
        Assert.Equal(Hash, decision.Evidence.PolicyContentSha256);
        Assert.Equal(At, decision.Evidence.AdmittedAt);
        Assert.Equal(fixture.UserId, decision.Evidence.UserId);
        Assert.Equal(12, fixture.Source.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldApplyExplicitPolicyGivenQualifiedDevelopmentIdentity(bool allowed)
    {
        // Arrange
        var fixture = new Fixture();
        var identity = new UserIdentity("bdgrz-development", "developer");
        Assert.True(identity.Register(fixture.UserId, null).IsSuccess);
        fixture.Source.Streams[AccessAdmissionStableRead.Slot.Identity] = identity.Stream.ToString();
        fixture.Set(AccessAdmissionStableRead.Slot.Identity, identity);
        fixture.Set(AccessAdmissionStableRead.Slot.Session,
            new AccessAdmissionSemanticEvaluator.Session(fixture.UserId, identity.Id, fixture.SessionId, true));
        fixture.Set(AccessAdmissionStableRead.Slot.Policy, new AccessAdmissionSemanticEvaluator.Policy("v1", Hash,
            true, true, true, true, AllowDevelopmentIdentities: allowed));
        var participants = fixture.Candidate.Participants.Select(p => p.Role == AccessAdmissionStableRead.Slot.Identity
            ? p with { Stream = identity.Stream.ToString() } : p).ToArray();
        var candidate = fixture.Candidate with
        {
            Credential = fixture.Candidate.Credential with { IdentityId = identity.Id },
            Participants = participants
        };

        // Act
        var decision = await fixture.Evaluator.EvaluateOperatorAsync(candidate);

        // Assert
        Assert.Equal(allowed ? AccessAdmissionSemanticEvaluator.Status.Allowed : AccessAdmissionSemanticEvaluator.Status.Denied,
            decision.Status);
    }

    [Fact]
    public async Task ShouldDenyGivenCredentialExpiresAtAdmissionInstant()
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Candidate with { Credential = fixture.Candidate.Credential with { ExpiresAt = At } };

        // Act
        var decision = await fixture.Evaluator.EvaluateOperatorAsync(request);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Denied, decision.Status);
        Assert.Null(decision.Evidence);
    }

    [Fact]
    public async Task ShouldDenyGivenSessionBelongsToAnotherIdentity()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Set(AccessAdmissionStableRead.Slot.Session,
            new AccessAdmissionSemanticEvaluator.Session(fixture.UserId, Uuid.CreateVersion4(), fixture.SessionId, true));

        // Act
        var decision = await fixture.Evaluator.EvaluateOperatorAsync(fixture.Candidate);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Denied, decision.Status);
    }

    [Fact]
    public async Task ShouldDenyGivenCurrentGlobalVeto()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Set(AccessAdmissionStableRead.Slot.GlobalVeto, new AccessAdmissionSemanticEvaluator.ActorVeto(fixture.UserId, true));

        // Act
        var decision = await fixture.Evaluator.EvaluateOperatorAsync(fixture.Candidate);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Denied, decision.Status);
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenUnsupportedPolicyGeneration()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Set(AccessAdmissionStableRead.Slot.Policy, new AccessAdmissionSemanticEvaluator.Policy("new-policy", Hash, true, true, true, true));

        // Act
        var decision = await fixture.Evaluator.EvaluateOperatorAsync(fixture.Candidate);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Unavailable, decision.Status);
        Assert.Null(decision.Evidence);
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenUnqualifiedSource()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.Source.Qualified = false;

        // Act
        var decision = await fixture.Evaluator.EvaluateOperatorAsync(fixture.Candidate);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Unavailable, decision.Status);
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task ShouldDenyProgramCreationGivenInactiveTenant()
    {
        // Arrange
        var fixture = new Fixture();
        var tenantId = Uuid.CreateVersion4();
        var tenant = new Tenant(tenantId);
        var member = new Member(tenantId, fixture.UserId);
        var primary = new PrimaryOrgAdmins(tenantId);
        var path = fixture.Candidate.Participants.Where(participant => participant.Role != AccessAdmissionStableRead.Slot.Operator)
            .ToList();
        fixture.Add(path, AccessAdmissionStableRead.Slot.Tenant, tenant.Stream.ToString(), tenant);
        fixture.Add(path, AccessAdmissionStableRead.Slot.Member, member.Stream.ToString(), member);
        fixture.Add(path, AccessAdmissionStableRead.Slot.Primary, primary.Stream.ToString(), primary);
        var request = new AccessAdmissionSemanticEvaluator.ProgramCandidate(fixture.Candidate with { Participants = path },
            tenantId, Uuid.Empty, Uuid.Empty, true);

        // Act
        var decision = await fixture.Evaluator.EvaluateProgramCreationAsync(request);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Denied, decision.Status);
        Assert.Null(decision.Evidence);
    }

    [Fact]
    public async Task ShouldReportUnavailableGivenProgramPathOmitsMembership()
    {
        // Arrange
        var fixture = new Fixture();
        var request = new AccessAdmissionSemanticEvaluator.ProgramCandidate(fixture.Candidate,
            Uuid.CreateVersion4(), Uuid.Empty, Uuid.Empty, true);

        // Act
        var decision = await fixture.Evaluator.EvaluateProgramCreationAsync(request);

        // Assert
        Assert.Equal(AccessAdmissionSemanticEvaluator.Status.Unavailable, decision.Status);
        Assert.Equal(0, fixture.Source.Reads);
    }

    sealed class Fixture
    {
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid SessionId { get; } = Uuid.CreateVersion4();
        public Source Source { get; } = new();
        public AccessAdmissionSemanticEvaluator Evaluator { get; }
        public AccessAdmissionSemanticEvaluator.Candidate Candidate { get; }

        public Fixture()
        {
            var identity = new UserIdentity("provider", "subject");
            Assert.True(identity.Register(UserId, null).IsSuccess);
            var path = new AccessAdmissionStableRead.Participant[]
            {
                new(AccessAdmissionStableRead.Slot.Identity, identity.Stream.ToString()),
                new(AccessAdmissionStableRead.Slot.Policy, "stream://bdgrz/policy/current"),
                new(AccessAdmissionStableRead.Slot.Session, "stream://bdgrz/sessions/session"),
                new(AccessAdmissionStableRead.Slot.Account, "stream://bdgrz/accounts/user"),
                new(AccessAdmissionStableRead.Slot.GlobalVeto, "stream://bdgrz/veto/user"),
                new(AccessAdmissionStableRead.Slot.Operator, "stream://bdgrz/operators/user")
            };
            Candidate = new(new(UserId, identity.Id, SessionId, UserId.ToString(), "bdgrz", At.AddMinutes(-1), At.AddMinutes(1)),
                Uuid.CreateVersion4(), "stream://bdgrz/criteria-editions/edition", Hash, path);
            foreach (var participant in path)
                Source.Streams.Add(participant.Role, participant.Stream);
            Set(AccessAdmissionStableRead.Slot.Identity, identity);
            Set(AccessAdmissionStableRead.Slot.Policy, new AccessAdmissionSemanticEvaluator.Policy("v1", Hash, true, true, true, true));
            Set(AccessAdmissionStableRead.Slot.Session, new AccessAdmissionSemanticEvaluator.Session(UserId, identity.Id, SessionId, true));
            Set(AccessAdmissionStableRead.Slot.Account, new AccessAdmissionSemanticEvaluator.ActorVeto(UserId, false));
            Set(AccessAdmissionStableRead.Slot.GlobalVeto, new AccessAdmissionSemanticEvaluator.ActorVeto(UserId, false));
            Set(AccessAdmissionStableRead.Slot.Operator, new AccessAdmissionSemanticEvaluator.OperatorGrant(UserId, true));
            Evaluator = new(new(Source, new FixedClock()), "v1", Hash);
        }

        public void Set(AccessAdmissionStableRead.Slot slot, object state) =>
            Source.Values[Source.Streams[slot]] = new(new(Source.Streams[slot], "generation", 1), state);

        public void Add(List<AccessAdmissionStableRead.Participant> path, AccessAdmissionStableRead.Slot slot,
            string stream, object state)
        {
            path.Add(new(slot, stream));
            Source.Streams.Add(slot, stream);
            Set(slot, state);
        }
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => At;
    }

    sealed class Source : AccessAdmissionStableRead.ISource
    {
        public bool Qualified { get; set; } = true;
        public bool IsFreshnessQualified => Qualified;
        public int Reads { get; private set; }
        public Dictionary<AccessAdmissionStableRead.Slot, string> Streams { get; } = [];
        public Dictionary<string, AccessAdmissionStableRead.Observation> Values { get; } = [];

        public ValueTask<AccessAdmissionStableRead.Observation?> ReadCurrentAsync(string stream, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Reads++;
            return ValueTask.FromResult(Values.GetValueOrDefault(stream));
        }
    }
}
