using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Independence;

public sealed class SeparationOfDutiesWaiverProofTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid WaiverId = Uuid.CreateVersion4();
    static readonly Uuid RequesterId = Uuid.CreateVersion4();
    static readonly Uuid BeneficiaryId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset RequestedAt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ApprovedAt = RequestedAt.AddMinutes(5);
    static readonly DateTimeOffset ExpiresAt = RequestedAt.AddDays(7);
    static readonly SeparationOfDutiesWaiverScope Recorded = new(
        SeparationOfDutiesRecordTypes.Boundary, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3,
        SeparationOfDutiesActions.Review);

    static readonly SeparationOfDutiesWaiverProofScope Exact = new(TenantId, WaiverId, BeneficiaryId,
        BeneficiaryId, Recorded.Action, Recorded.RecordType, Recorded.RecordId, Recorded.VersionId,
        Recorded.Revision);

    [Fact]
    public async Task ShouldResolveProofGivenExactScopeInsideValidity()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();

        // Act
        var result = await fixture.Resolver.ResolveAsync(Exact, ApprovedAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Exact, result.Value.ToScope());
        Assert.Equal(ApprovedAt, result.Value.ValidFrom);
        Assert.Equal(ExpiresAt, result.Value.ValidUntil);
        Assert.Equal(1UL, result.Value.ApprovalFact.ResourcePosition);
        Assert.True((await fixture.Resolver.VerifyAsync(result.Value, ExpiresAt.AddTicks(-1))).IsSuccess);
    }

    [Fact]
    public async Task ShouldDenyGivenWrongTargetRevision()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();

        // Act
        var result = await fixture.Resolver.ResolveAsync(
            Exact with { TargetRevision = Exact.TargetRevision + 1 }, ApprovedAt);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("subject")]
    [InlineData("action")]
    [InlineData("target")]
    [InlineData("type")]
    [InlineData("version")]
    public async Task ShouldDenyGivenAnyOtherScopeFieldDiffers(string field)
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();
        var wrong = field switch
        {
            "actor" => Exact with { ActorId = RequesterId },
            "subject" => Exact with { SubjectId = RequesterId },
            "action" => Exact with { Action = SeparationOfDutiesActions.Approve },
            "target" => Exact with { TargetId = Uuid.CreateVersion4() },
            "type" => Exact with { TargetType = SeparationOfDutiesRecordTypes.Control },
            _ => Exact with { TargetVersionId = Uuid.CreateVersion4() },
        };

        // Act
        var result = await fixture.Resolver.ResolveAsync(wrong, ApprovedAt);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldDenyGivenAtOrAfterExpiry()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();

        // Act
        var result = await fixture.Resolver.ResolveAsync(Exact, ExpiresAt);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldDenyGivenBeforeValidFrom()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();

        // Act
        var result = await fixture.Resolver.ResolveAsync(Exact, ApprovedAt.AddTicks(-1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldDenyGivenWaiverNeverApproved()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync(approve: false);

        // Act
        var result = await fixture.Resolver.ResolveAsync(Exact, ApprovedAt);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldReturnValidationGivenMalformedScope()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();

        // Act
        var result = await fixture.Resolver.ResolveAsync(Exact with { Action = " " }, ApprovedAt);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Theory]
    [InlineData("valid_until")]
    [InlineData("valid_from")]
    [InlineData("target_revision")]
    [InlineData("actor")]
    public async Task ShouldRejectGivenForgedProofFields(string field)
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();
        var genuine = (await fixture.Resolver.ResolveAsync(Exact, ApprovedAt)).Value;
        var forged = field switch
        {
            "valid_until" => genuine with { ValidUntil = ExpiresAt.AddYears(10) },
            "valid_from" => genuine with { ValidFrom = RequestedAt },
            "target_revision" => genuine with { TargetRevision = 9 },
            _ => genuine with { ActorId = RequesterId },
        };

        // Act
        var result = await fixture.Resolver.VerifyAsync(forged, ApprovedAt);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectGivenApprovalLocatorForDifferentEvent()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();
        var genuine = (await fixture.Resolver.ResolveAsync(Exact, ApprovedAt)).Value;
        var recordedFact = await fixture.Facts.ResolveAsync(genuine.ApprovalFact.Source, 0);
        var forged = genuine with { ApprovalFact = recordedFact.Locator };

        // Act
        var result = await fixture.Resolver.VerifyAsync(forged, ApprovedAt);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRoundTripGivenSerializedProof()
    {
        // Arrange
        await using var fixture = await Fixture.SeedAsync();
        var proof = (await fixture.Resolver.ResolveAsync(Exact, ApprovedAt)).Value;

        // Act
        var json = JsonSerializer.Serialize(proof, ComplianceCoreJsonContext.Default.SeparationOfDutiesWaiverProof);
        var restored = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.SeparationOfDutiesWaiverProof);

        // Assert
        Assert.Equal(proof, restored);
        Assert.True((await fixture.Resolver.VerifyAsync(restored!, ApprovedAt)).IsSuccess);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        Fixture(InMemoryEventStore events)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Facts = new EventStoreSourceFactResolver(events);
            Resolver = new SeparationOfDutiesWaiverProofResolver(
                _scope.ServiceProvider.GetRequiredService<IAggregateReader>(), Facts);
        }

        public EventStoreSourceFactResolver Facts { get; }

        public SeparationOfDutiesWaiverProofResolver Resolver { get; }

        public static async Task<Fixture> SeedAsync(bool approve = true)
        {
            var fixture = new Fixture(new InMemoryEventStore());
            var waiver = new SeparationOfDutiesWaiver(TenantId, WaiverId);
            Assert.Null(waiver.Record(Recorded, BeneficiaryId, RequesterId, "Org Admin",
                "Only reviewer available.", RequestedAt, ExpiresAt));
            if (approve)
                Assert.Null(waiver.Approve(ApproverId, "Second Org Admin", ApprovedAt));
            await fixture._scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
                .SaveAsync(waiver, new RequestDispatchContext(RequestActor.System));
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
