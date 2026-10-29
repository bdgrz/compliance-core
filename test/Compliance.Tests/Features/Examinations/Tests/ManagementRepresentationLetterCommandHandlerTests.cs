using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using LetterAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.ManagementRepresentationLetter;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
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
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Handlers;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ManagementRepresentationLetterCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRetainExactAvailableSignedArtifactGivenNamedSigner()
    {
        // Arrange
        await using var fixture = new Fixture();
        var letterId = await fixture.SeedLetterAsync();
        var signedArtifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.SignerUserId,
            new SignManagementRepresentationLetter(fixture.TenantId, fixture.ProgramId,
                letterId, fixture.SignerMemberId, signedArtifactId, Now.AddMinutes(2))),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new LetterAggregate(fixture.TenantId,
            fixture.ProgramId, letterId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var signature = Assert.Single(saved.Current!.Signatures);
        Assert.Equal(fixture.SignerMemberId, signature.SignerMemberId);
        Assert.Equal(signedArtifactId, signature.SignedArtifactId);
        Assert.Equal(new string('b', 64), signature.SignedArtifactSha256);
    }

    [Fact]
    public async Task ShouldRejectUnavailableSignedArtifactGivenNamedSigner()
    {
        // Arrange
        await using var fixture = new Fixture();
        var letterId = await fixture.SeedLetterAsync();
        var signedArtifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Malware);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.SignerUserId,
            new SignManagementRepresentationLetter(fixture.TenantId, fixture.ProgramId,
                letterId, fixture.SignerMemberId, signedArtifactId, Now.AddMinutes(2))),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new LetterAggregate(fixture.TenantId,
            fixture.ProgramId, letterId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(saved.Current!.Signatures);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectDifferentPersonalActorGivenNamedSignerIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var letterId = await fixture.SeedLetterAsync();
        var signedArtifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.OtherUserId,
            new SignManagementRepresentationLetter(fixture.TenantId, fixture.ProgramId,
                letterId, fixture.SignerMemberId, signedArtifactId, Now.AddMinutes(2))),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new LetterAggregate(fixture.TenantId,
            fixture.ProgramId, letterId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Signatures);
        Assert.Equal(1, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now.AddMinutes(3));
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid SignerUserId { get; } = Uuid.CreateVersion4();
        public Uuid OtherUserId { get; } = Uuid.CreateVersion4();
        public Uuid SignerMemberId => RbacIds.Member(TenantId, SignerUserId);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public SignManagementRepresentationLetterHandler Handler() =>
            new(Executor, Reader, _clock);

        public async Task<Uuid> SeedLetterAsync()
        {
            var letterId = Uuid.CreateVersion4();
            var letter = new LetterAggregate(TenantId, ProgramId, letterId);
            Assert.Null(letter.Record(new ManagementRepresentationLetterContent(ExaminationId,
                    "type_i", Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), Hash('c'),
                    Uuid.CreateVersion4(), 1, Hash('d'), "auditor", "letter-reference-1",
                    Uuid.CreateVersion4(), Hash('e'), new DateOnly(2026, 10, 1),
                    [new ManagementAssertionSigner(SignerMemberId, "ceo")]),
                RbacIds.Member(TenantId, UserId), "Compliance lead", Now));
            await Writer.SaveAsync(letter, new RequestDispatchContext(RequestActor.System));
            return letterId;
        }

        public async Task<Uuid> SeedArtifactAsync(EvidenceInspectionOutcome outcome)
        {
            var artifactId = Uuid.CreateVersion4();
            var artifact = new EvidenceArtifact(TenantId, artifactId);
            Assert.True(artifact.Register(new EvidenceArtifactContent("Signed representation letter", null,
                    "document", "Management signature capture", Now.AddMinutes(1),
                    new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "confidential"),
                Hash('b'), 256, ActorReference.ForMember(SignerMemberId, "Chief Executive"),
                Now.AddMinutes(1)).IsSuccess);
            Assert.Null(artifact.RecordInspection(outcome, Now.AddMinutes(2)));
            await Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));
            return artifactId;
        }

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
            new HttpInvocation("POST", "/api/v1/examinations/representation-letters/sign",
                "/api/v1/examinations/representation-letters/sign", "representation-letter-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "executive@example.test")], "BdgrzSession"));

        static string Hash(char value) => new(value, 64);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
