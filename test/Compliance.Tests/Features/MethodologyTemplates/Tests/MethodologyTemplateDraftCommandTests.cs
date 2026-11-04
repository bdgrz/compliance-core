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
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateDraftCommandTests
{
    static readonly Uuid Author = Uuid.CreateVersion4();
    static readonly Uuid Reviewer = Uuid.CreateVersion4();
    static readonly Uuid Approver = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldAttributeMcpAuthorshipAndPersonalHttpDecisionsGivenIndependentOperators()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var revise = fixture.Revision([Entry()]);
        var review = new ReviewMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, 1, true, "Reviewed");
        var approve = new ApproveMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, 1);

        // Act
        var authored = await fixture.Bus.DispatchAsync(revise, Context(Author, true));
        var reviewed = await fixture.Bus.DispatchAsync(review, Context(Reviewer));
        var approved = await fixture.Bus.DispatchAsync(approve, Context(Approver));
        var owner = await fixture.OwnerAsync();
        var facts = await fixture.FactsAsync();

        // Assert
        Assert.True(authored.IsSuccess, authored.Error?.Message);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.Equal("approved", owner.Status);
        Assert.Equal(Author, Assert.Single(owner.RevisionAuthors));
        Assert.Equal(Author, Assert.Single(facts.OfType<MethodologyTemplateDraftRevisedV4>()).RevisedBy);
        Assert.Equal(Reviewer, Assert.Single(facts.OfType<MethodologyTemplateDraftAccepted>()).ReviewedBy);
        Assert.Equal(Approver, Assert.Single(facts.OfType<MethodologyTemplateDraftApproved>()).ApprovedBy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutFactGivenMcpReviewOrApproval(bool approval)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var authored = await fixture.Bus.DispatchAsync(fixture.Revision([Entry()]), Context(Author, true));
        Assert.True(authored.IsSuccess, authored.Error?.Message);
        if (approval)
        {
            var reviewed = await fixture.Bus.DispatchAsync(new ReviewMethodologyTemplateDraft(Uuid.CreateVersion4(),
                fixture.Template, fixture.Draft, 1, true, "Reviewed"), Context(Reviewer));
            Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        }
        var before = (await fixture.OwnerAsync()).Version;
        IRequest request = approval
            ? new ApproveMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, 1)
            : new ReviewMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, 1, true, "Reviewed");

        // Act
        var result = await fixture.Bus.DispatchAsync(request, Context(approval ? Approver : Reviewer, true));
        var owner = await fixture.OwnerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Equal(before, owner.Version);
    }

    [Theory]
    [InlineData("revise", false)]
    [InlineData("review", false)]
    [InlineData("approve", false)]
    [InlineData("discard", false)]
    [InlineData("revise", true)]
    [InlineData("review", true)]
    [InlineData("approve", true)]
    [InlineData("discard", true)]
    public async Task ShouldRefuseWithoutFactGivenSystemOrNonOperatorCaller(string command, bool nonOperator)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = Request(fixture, command);
        var actor = nonOperator ? Person(Uuid.CreateVersion4()) : RequestActor.System;
        var context = new RequestDispatchContext(actor, new HttpInvocation("POST", "/synthetic/drafts", null, "denied"));
        var before = (await fixture.OwnerAsync()).Version;

        // Act
        var result = await fixture.Bus.DispatchAsync(request, context);
        var owner = await fixture.OwnerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Equal(before, owner.Version);
    }

    [Fact]
    public async Task ShouldRefuseBeforePublishingContentGivenLegacyClientContribution()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Revision([Entry() with { ContributionApprovalId = Uuid.CreateVersion4() }]);
        var before = (await fixture.OwnerAsync()).Version;

        // Act
        var result = await fixture.Bus.DispatchAsync(request, Context(Author, true));
        var owner = await fixture.OwnerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Equal(0, fixture.Content.Writes);
        Assert.Equal(before, owner.Version);
    }

    [Fact]
    public async Task ShouldKeepOriginalRevisionFactFreeGivenExactRetryOrChangedContentAtSameCommand()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Revision([Entry()]);

        // Act
        var revised = await fixture.Bus.DispatchAsync(request, Context(Author, true));
        var repeated = await fixture.Bus.DispatchAsync(request, Context(Author, true));
        var changed = await fixture.Bus.DispatchAsync(request with { Entries = [Entry() with { Title = "Changed intent" }] },
            Context(Author, true));
        var owner = await fixture.OwnerAsync();
        var facts = await fixture.FactsAsync();

        // Assert
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Single(facts.OfType<MethodologyTemplateDraftRevisedV4>());
        var entries = await fixture.Content.ReadJsonAsync<IReadOnlyList<MethodologyTemplateRecordContent>>(
            MethodologyTemplate.Realm, owner.EntriesContent!, CancellationToken.None);
        Assert.Equal("Review access", Assert.Single(entries).Title);
    }

    [Fact]
    public async Task ShouldRecordReasonedDiscardAndKeepRetryFactFreeGivenOperatorAuthoring()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = new DiscardMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, "Superseded plan");

        // Act
        var discarded = await fixture.Bus.DispatchAsync(request, Context(Author, true));
        var repeated = await fixture.Bus.DispatchAsync(request, Context(Author, true));
        var changed = await fixture.Bus.DispatchAsync(request with { Reason = "Another reason" }, Context(Author, true));
        var owner = await fixture.OwnerAsync();
        var facts = await fixture.FactsAsync();

        // Assert
        Assert.True(discarded.IsSuccess, discarded.Error?.Message);
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.False(changed.IsSuccess);
        Assert.Equal("discarded", owner.Status);
        Assert.Single(facts.OfType<MethodologyTemplateDraftDiscarded>());
    }

    static IRequest Request(Fixture fixture, string command) => command switch
    {
        "revise" => fixture.Revision([Entry()]),
        "review" => new ReviewMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, 0, true, "Review"),
        "approve" => new ApproveMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, 0),
        "discard" => new DiscardMethodologyTemplateDraft(Uuid.CreateVersion4(), fixture.Template, fixture.Draft, "Reason"),
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    static MethodologyTemplateRecordContent Entry() => new("access-review", "control", "Review access", "Review quarterly.", []);
    static ClaimsPrincipal Person(Uuid user) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "UnitTest"));
    static RequestDispatchContext Context(Uuid user, bool mcp = false) => new(Person(user), mcp
        ? new McpInvocation("synthetic.methodology.draft")
        : new HttpInvocation("POST", "/synthetic/drafts", null, "personal-decision"));

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public InMemoryEventStore Events { get; } = new();
        public MethodologyTemplateScenario Source { get; } = new(DateTimeOffset.UnixEpoch);
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public Uuid Draft { get; } = Uuid.CreateVersion4();
        public RecordingContentStore Content { get; }
        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();

        Fixture()
        {
            Content = new RecordingContentStore(Source.Content);
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IImmutableContentStore>(Content);
            services.AddSingleton<IPlatformOperatorAccess>(new Operators());
            services.AddPortia().AddRequestHandler<ReviseMethodologyTemplateDraftV3Handler>()
                .AddRequestHandler<ReviewMethodologyTemplateDraftHandler>()
                .AddRequestHandler<ApproveMethodologyTemplateDraftHandler>()
                .AddRequestHandler<DiscardMethodologyTemplateDraftHandler>()
                .AddRequestAuthorizer<MethodologyTemplateDraftCommandAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            IReadOnlyList<MethodologyTemplateRecordContent> entries = [];
            var content = await fixture.Source.Content.WriteJsonAsync(MethodologyTemplate.Realm,
                Uuid.CreateVersion4(), MethodologyTemplateDraft.EntriesSchema, entries, CancellationToken.None);
            var owner = new MethodologyTemplateDraft(fixture.Template, fixture.Draft);
            Assert.True(owner.Open(Uuid.CreateVersion4(), 0, entries, content).IsSuccess);
            await fixture._scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
                .SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return fixture;
        }

        public ReviseMethodologyTemplateDraftV3 Revision(IReadOnlyList<MethodologyTemplateRecordContent> entries) =>
            new(Uuid.CreateVersion4(), Template, Draft, 0, entries);
        public ValueTask<MethodologyTemplateDraft> OwnerAsync() => Reader.HydrateAsync(new MethodologyTemplateDraft(Template, Draft));
        public async Task<IReadOnlyList<DomainEvent>> FactsAsync()
        {
            var facts = new List<DomainEvent>();
            await foreach (var item in Events.ReadAsync(new MethodologyTemplateDraft(Template, Draft).Stream, 0))
                facts.Add(item.Event);
            return facts;
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await Source.DisposeAsync();
        }
    }

    sealed class Operators : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(userId == Author || userId == Reviewer || userId == Approver);
    }

    sealed class RecordingContentStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int Writes { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            Writes++;
            return inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
