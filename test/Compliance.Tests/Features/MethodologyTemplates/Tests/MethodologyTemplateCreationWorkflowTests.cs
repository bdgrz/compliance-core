using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateCreationWorkflowTests
{
    [Fact]
    public async Task ShouldCreateIdleLineWithOriginalOperatorAndAdmissionProofGivenCommittedNameClaim()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request("Firm methodology");

        // Act
        var admitted = await fixture.SendAsync(request);
        var claim = await fixture.ClaimAsync(request.Name);
        var created = await fixture.CreateAsync(claim);
        var repeated = await fixture.CreateAsync(claim);
        var owner = await fixture.Source.Reader.HydrateAsync(new MethodologyTemplate(request.TemplateId));
        var facts = await fixture.Source.FactsAsync(request.TemplateId);
        var intent = await fixture.Source.Content.ReadJsonAsync<MethodologyTemplateCreationIntent>(MethodologyTemplate.Realm,
            claim.Fact.Intent, CancellationToken.None);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.True(owner.IsLineCreated);
        Assert.Null(owner.OpenDraftId);
        Assert.Equal(0, owner.PublishedVersionNumber);
        var fact = Assert.IsType<MethodologyTemplateCreatedV4>(Assert.Single(facts));
        Assert.Equal(fixture.Operator, fact.CreatedBy);
        Assert.NotNull(fact.NameAdmission);
        Assert.Equal(claim.Fact.Metadata.EventId, fact.NameAdmission.EventId);
        Assert.Equal(claim.Stream, fact.NameAdmission.Source);
        Assert.Equal(fixture.Operator, intent.CreatedBy);
    }

    [Fact]
    public async Task ShouldRefuseSecondTargetWithoutClaimGivenEquivalentNameAlreadyAdmitted()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = fixture.Request("Firm methodology");
        Assert.True((await fixture.SendAsync(first)).IsSuccess);
        var second = fixture.Request(" FIRM METHODOLOGY ");

        // Act
        var refused = await fixture.SendAsync(second);
        var facts = await fixture.NameFactsAsync(first.Name);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error.Kind);
        Assert.Single(facts.OfType<MethodologyTemplateNameClaimed>());
        var owner = await fixture.Source.Reader.HydrateAsync(new MethodologyTemplate(second.TemplateId));
        Assert.False(owner.IsLineCreated);
    }

    [Fact]
    public async Task ShouldFreeOnlyLosingNameAndPreserveReacquisitionGivenTwoNamesRaceForOneIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var winner = fixture.Request("Original name");
        var loser = fixture.Request("Losing name") with { TemplateId = winner.TemplateId };
        Assert.True((await fixture.SendAsync(winner)).IsSuccess);
        Assert.True((await fixture.SendAsync(loser)).IsSuccess);
        var winningClaim = await fixture.ClaimAsync(winner.Name);
        var losingClaim = await fixture.ClaimAsync(loser.Name);

        // Act
        var created = await fixture.CreateAsync(winningClaim);
        var refused = await fixture.CreateAsync(losingClaim);
        var free = await fixture.NameAsync(loser.Name);
        var next = fixture.Request(loser.Name);
        var reacquired = await fixture.SendAsync(next);
        var delayed = await fixture.CreateAsync(losingClaim);
        var oldAdmission = await fixture.SendAsync(loser);
        var current = await fixture.NameAsync(loser.Name);
        var outcomes = await fixture.NameFactsAsync(loser.Name);

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(refused.IsSuccess, refused.Error?.Message);
        Assert.Null(free.Holder);
        Assert.True(reacquired.IsSuccess, reacquired.Error?.Message);
        Assert.True(delayed.IsSuccess, delayed.Error?.Message);
        Assert.True(oldAdmission.IsSuccess, oldAdmission.Error?.Message);
        Assert.Equal(new MethodologyTemplate(next.TemplateId).Stream, current.Holder);
        var outcome = Assert.Single(outcomes.OfType<MethodologyTemplateCreationRefused>());
        Assert.True(outcome.ReleasedName);
        Assert.Equal(losingClaim.Fact.Metadata.EventId, outcome.ClaimEventId);
        Assert.Equal(2, outcomes.OfType<MethodologyTemplateNameClaimed>().Count());
    }

    [Fact]
    public async Task ShouldRetainUsedNameGivenSameIdentityAndNameWithChangedPurpose()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = fixture.Request("Firm methodology");
        Assert.True((await fixture.SendAsync(first)).IsSuccess);
        Assert.True((await fixture.CreateAsync(await fixture.ClaimAsync(first.Name))).IsSuccess);
        var changed = first with { CommandId = Uuid.CreateVersion4(), Purpose = "Another purpose" };
        Assert.True((await fixture.SendAsync(changed)).IsSuccess);
        var claim = await fixture.ClaimAsync(changed.Name);

        // Act
        var refused = await fixture.CreateAsync(claim);
        var name = await fixture.NameAsync(changed.Name);
        var facts = await fixture.NameFactsAsync(changed.Name);

        // Assert
        Assert.True(refused.IsSuccess, refused.Error?.Message);
        Assert.Equal(new MethodologyTemplate(first.TemplateId).Stream, name.Holder);
        Assert.False(Assert.Single(facts.OfType<MethodologyTemplateCreationRefused>()).ReleasedName);
    }

    [Fact]
    public async Task ShouldReleaseLosingNameGivenTargetIdentityAlreadyBelongsToLegacyLine()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request("Unused new name");
        var legacy = new MethodologyTemplateContent("Legacy name", "Original legacy", "soc2",
            [new("access-review", "control", "Access review", "Review quarterly.", [])]);
        Assert.True((await fixture.Source.CreateAsync(new CreateMethodologyTemplate(request.TemplateId, legacy, "Create"), fixture.Operator)).IsSuccess);
        Assert.True((await fixture.SendAsync(request)).IsSuccess);
        var claim = await fixture.ClaimAsync(request.Name);

        // Act
        var refused = await fixture.CreateAsync(claim);
        var name = await fixture.NameAsync(request.Name);
        var facts = await fixture.NameFactsAsync(request.Name);
        var original = await fixture.Source.Views.ReadAsync(request.TemplateId);

        // Assert
        Assert.True(refused.IsSuccess, refused.Error?.Message);
        Assert.Null(name.Holder);
        Assert.True(Assert.Single(facts.OfType<MethodologyTemplateCreationRefused>()).ReleasedName);
        Assert.NotNull(original);
        Assert.Equal("Legacy name", Assert.Single(original.Versions).Content.Name);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("realm")]
    [InlineData("event")]
    [InlineData("position")]
    public async Task ShouldRefuseWithoutCreationGivenForgedAdmissionReference(string fault)
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request("Firm methodology");
        Assert.True((await fixture.SendAsync(request)).IsSuccess);
        var claim = await fixture.ClaimAsync(request.Name);
        var create = Fixture.Creation(claim);
        create = fault switch
        {
            "target" => create with { TemplateId = Uuid.CreateVersion4() },
            "realm" => create with { Claim = create.Claim with { Stream = new EventStreamAddress("wrong-realm", claim.Stream.Area, claim.Stream.Resource) } },
            "event" => create with { Claim = create.Claim with { EventId = Uuid.CreateVersion4() } },
            "position" => create with { ClaimPosition = create.ClaimPosition + 20 },
            _ => create,
        };
        create = create with { CommandId = Uuid.CreateVersion5(create.Claim.EventId, nameof(CreateMethodologyTemplateLine)) };

        // Act
        if (fault is "event" or "position")
            await Assert.ThrowsAsync<SourceFactVerificationException>(() => fixture.Bus.DispatchAsync(create,
                new RequestDispatchContext(RequestActor.System)).AsTask());
        else
        {
            var result = await fixture.Bus.DispatchAsync(create, new RequestDispatchContext(RequestActor.System));
            Assert.False(result.IsSuccess);
        }
        var owner = await fixture.Source.Reader.HydrateAsync(new MethodologyTemplate(request.TemplateId));

        // Assert
        Assert.False(owner.IsLineCreated);
        Assert.Equal(0UL, owner.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutEffectGivenPersonalCallerToInternalCreationOrRefusal(bool refusal)
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request("Firm methodology");
        Assert.True((await fixture.SendAsync(request)).IsSuccess);
        var claim = await fixture.ClaimAsync(request.Name);
        var create = Fixture.Creation(claim);
        var existingId = Uuid.CreateVersion4();
        IRequest command = refusal ? new RefuseMethodologyTemplateCreation(request.TemplateId, create.Claim,
            create.ClaimPosition, new NativeFactReference(new MethodologyTemplate(request.TemplateId).Stream, existingId, 1), 0,
            Uuid.CreateVersion5(create.Claim.EventId, $"{nameof(RefuseMethodologyTemplateCreation)}:{existingId}")) : create;

        // Act
        var result = await fixture.Bus.DispatchAsync(command, new RequestDispatchContext(Person(fixture.Operator),
            new McpInvocation("methodology.internal")));
        var owner = await fixture.Source.Reader.HydrateAsync(new MethodologyTemplate(request.TemplateId));
        var facts = await fixture.NameFactsAsync(request.Name);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.False(owner.IsLineCreated);
        Assert.Empty(facts.OfType<MethodologyTemplateCreationRefused>());
    }

    [Fact]
    public async Task ShouldPropagateRefusalFailureAndKeepClaimUntilRetryGivenDownstreamOutcomeCannotCommit()
    {
        // Arrange
        await using var fixture = new Fixture();
        var winner = fixture.Request("Original name");
        var loser = fixture.Request("Losing name") with { TemplateId = winner.TemplateId };
        Assert.True((await fixture.SendAsync(winner)).IsSuccess);
        Assert.True((await fixture.SendAsync(loser)).IsSuccess);
        Assert.True((await fixture.CreateAsync(await fixture.ClaimAsync(winner.Name))).IsSuccess);
        var claim = await fixture.ClaimAsync(loser.Name);
        var failure = new RequestError(RequestErrorKind.Conflict, "Outcome source is pending.", true);
        var responses = new ReactorScenario().RespondTo<RefuseMethodologyTemplateCreation>(Result.Failure(failure));
        var handler = new CreateMethodologyTemplateLineHandler(fixture.Source.Executor,
            new EventStoreSourceFactResolver(fixture.Source.Events), fixture.Source.Content,
            new MethodologyTemplateLineReader(new EventStoreSnapshotReader(fixture.Source.Events), fixture.Source.Content), responses.Requests);

        // Act
        var refused = await handler.HandleAsync(new RequestContext<CreateMethodologyTemplateLine>(Fixture.Creation(claim),
            RequestActor.System), CancellationToken.None);
        var held = await fixture.NameAsync(loser.Name);
        var retried = await fixture.CreateAsync(claim);
        var current = await fixture.NameAsync(loser.Name);
        var facts = await fixture.NameFactsAsync(loser.Name);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Same(failure, refused.Error);
        Assert.Equal(new MethodologyTemplate(loser.TemplateId).Stream, held.Holder);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Null(current.Holder);
        var sent = Assert.Single(responses.SentRequests.OfType<RefuseMethodologyTemplateCreation>());
        Assert.Equal(sent.CommandId, Assert.Single(facts.OfType<MethodologyTemplateCreationRefused>()).Operation.LogicalId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutClaimGivenSystemOrNonOperatorCaller(bool system)
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request("Firm methodology");
        var actor = system ? RequestActor.System : Person(Uuid.CreateVersion4());

        // Act
        var result = await fixture.Bus.DispatchAsync(request, new RequestDispatchContext(actor, new McpInvocation("methodology.create")));
        var facts = await fixture.NameFactsAsync(request.Name);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Empty(facts);
    }

    [Fact]
    public async Task ShouldRefuseChangedIntentAcrossNameOwnersGivenSameCreationCommandIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = fixture.Request("Original name");
        Assert.True((await fixture.SendAsync(first)).IsSuccess);

        // Act
        var changed = await fixture.SendAsync(first with { Name = "Changed name" });
        var facts = await fixture.NameFactsAsync("Changed name");

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.Empty(facts);
    }

    static ClaimsPrincipal Person(Uuid user) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "UnitTest"));
    sealed record ClaimedName(EventStreamAddress Stream, MethodologyTemplateNameClaimed Fact, ulong Position);
    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public MethodologyTemplateScenario Source { get; } = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        public Uuid Operator { get; } = Uuid.CreateVersion4();
        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Source.Events);
            services.AddSingleton<IImmutableContentStore>(Source.Content);
            services.AddSingleton<TimeProvider>(Source.Clock);
            services.AddSingleton<IPlatformOperatorAccess>(new Operators(Operator));
            services.AddSingleton<ISourceFactResolver>(new EventStoreSourceFactResolver(Source.Events));
            services.AddSingleton<IAuthoritativeSnapshotReader>(new EventStoreSnapshotReader(Source.Events));
            services.AddScoped<NativeOperationExecutor>();
            services.AddScoped<MethodologyTemplateLineReader>();
            services.AddPortia().AddRequestHandler<CreateMethodologyTemplateV3Handler>()
                .AddRequestHandler<CreateMethodologyTemplateLineHandler>()
                .AddRequestHandler<RefuseMethodologyTemplateCreationHandler>()
                .AddRequestAuthorizer<MethodologyTemplateCreationAuthorizer>()
                .AddRequestAuthorizer<MethodologyTemplateCreationReactionAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }
        public CreateMethodologyTemplateV3 Request(string name) => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), name, "Original firm purpose");
        public ValueTask<Result> SendAsync(CreateMethodologyTemplateV3 request) => Bus.DispatchAsync(request,
            new RequestDispatchContext(Person(Operator), new McpInvocation("methodology.create")));
        public static CreateMethodologyTemplateLine Creation(ClaimedName claim) => new(Uuid.Parse(claim.Fact.Owner.Resource,
            System.Globalization.CultureInfo.InvariantCulture), NativeFactReference.Of(claim.Stream, claim.Fact), claim.Position,
            Uuid.CreateVersion5(claim.Fact.Metadata.EventId, nameof(CreateMethodologyTemplateLine)));
        public ValueTask<Result> CreateAsync(ClaimedName claim) => Bus.DispatchAsync(Creation(claim), new RequestDispatchContext(RequestActor.System));
        public ValueTask<MethodologyTemplateNameClaim> NameAsync(string name) => Source.Reader.HydrateAsync(new MethodologyTemplateNameClaim(name));
        public async Task<ClaimedName> ClaimAsync(string name)
        {
            ClaimedName? result = null;
            var stream = MethodologyTemplateNameClaim.AddressOf(name);
            await foreach (var record in Source.Events.ReadAsync(stream, 0))
                if (record.Event is MethodologyTemplateNameClaimed claimed)
                    result = new ClaimedName(stream, claimed, record.ResourceOffset);
            return result ?? throw new InvalidOperationException("The admitted name claim was not committed.");
        }
        public async Task<IReadOnlyList<DomainEvent>> NameFactsAsync(string name)
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in Source.Events.ReadAsync(MethodologyTemplateNameClaim.AddressOf(name), 0))
                facts.Add(record.Event);
            return facts;
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await Source.DisposeAsync();
        }
    }
    sealed class Operators(Uuid user) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(userId == user);
    }
}
