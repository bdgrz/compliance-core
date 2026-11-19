using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateRetirementCommandTests
{
    static readonly Uuid Operator = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRetireIdleUnpublishedLineAndKeepExactRetryFactFreeGivenCurrentOperator()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = new RetireMethodologyTemplateV3(Uuid.CreateVersion4(), fixture.Template, "Superseded methodology");

        // Act
        var retired = await fixture.Bus.DispatchAsync(request, Context(Operator));
        var repeated = await fixture.Bus.DispatchAsync(request, Context(Operator));
        var changed = await fixture.Bus.DispatchAsync(request with { Reason = "Another intent" }, Context(Operator));
        var owner = await fixture.OwnerAsync();
        var facts = await fixture.FactsAsync();

        // Assert
        Assert.True(retired.IsSuccess, retired.Error?.Message);
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.Equal("retired", owner.LineStatus);
        var fact = Assert.Single(facts.OfType<MethodologyTemplateRetiredV4>());
        Assert.Equal(Operator, fact.RetiredBy);
        Assert.Equal(request.Reason, fact.Reason);
    }

    [Fact]
    public async Task ShouldKeepOpenDraftWithoutRetirementFactGivenDraftingLine()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(true);
        var before = (await fixture.OwnerAsync()).Version;

        // Act
        var result = await fixture.Bus.DispatchAsync(new RetireMethodologyTemplateV3(Uuid.CreateVersion4(),
            fixture.Template, "Superseded methodology"), Context(Operator));
        var owner = await fixture.OwnerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(before, owner.Version);
        Assert.NotNull(owner.OpenDraftId);
        Assert.Empty((await fixture.FactsAsync()).OfType<MethodologyTemplateRetiredV4>());
    }

    [Theory]
    [InlineData("system")]
    [InlineData("non_operator")]
    [InlineData("removed_operator")]
    public async Task ShouldRefuseWithoutFactGivenCallerLacksCurrentPersonalOperatorAuthority(string caller)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = new RetireMethodologyTemplateV3(Uuid.CreateVersion4(), fixture.Template, "Superseded methodology");
        if (caller == "removed_operator")
        {
            Assert.True((await fixture.Bus.DispatchAsync(request, Context(Operator))).IsSuccess);
            fixture.Operators.Active = false;
        }
        var before = (await fixture.OwnerAsync()).Version;
        var context = caller == "system" ? new RequestDispatchContext(RequestActor.System)
            : Context(caller == "non_operator" ? Uuid.CreateVersion4() : Operator);

        // Act
        var result = await fixture.Bus.DispatchAsync(request, context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Equal(before, (await fixture.OwnerAsync()).Version);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("template")]
    [InlineData("reason")]
    public async Task ShouldRefuseWithoutFactGivenInvalidRetirementIdentityOrReason(string invalid)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = new RetireMethodologyTemplateV3(invalid == "command" ? Uuid.Empty : Uuid.CreateVersion4(),
            invalid == "template" ? Uuid.Empty : fixture.Template, invalid == "reason" ? " " : "Superseded methodology");
        var before = (await fixture.OwnerAsync()).Version;

        // Act
        var result = await fixture.Bus.DispatchAsync(request, Context(Operator));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Equal(before, (await fixture.OwnerAsync()).Version);
    }

    static RequestDispatchContext Context(Uuid user) => new(new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "UnitTest")),
        new McpInvocation("synthetic.methodology.retire"));

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public InMemoryEventStore Events { get; } = new();
        public Operators Operators { get; } = new();
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();

        Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IPlatformOperatorAccess>(Operators);
            services.AddPortia().AddRequestHandler<RetireMethodologyTemplateV3Handler>()
                .AddRequestAuthorizer<MethodologyTemplateRetirementAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public static async Task<Fixture> CreateAsync(bool openDraft = false)
        {
            var fixture = new Fixture();
            var owner = new MethodologyTemplate(fixture.Template);
            Assert.True(owner.CreateLine("Firm controls", "Reusable methodology", Operator).IsSuccess);
            if (openDraft)
            {
                var content = new ImmutableContentRef(Uuid.CreateVersion4(), ContentDigest.Sha256("[]"u8), 2,
                    "application/json", MethodologyTemplateDraft.EntriesSchema);
                Assert.True(owner.StartDraft(Uuid.CreateVersion4(), 0, [], content, Operator).IsSuccess);
            }
            await fixture._scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
                .SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return fixture;
        }

        public ValueTask<MethodologyTemplate> OwnerAsync() => _scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new MethodologyTemplate(Template));

        public async Task<IReadOnlyList<DomainEvent>> FactsAsync()
        {
            var facts = new List<DomainEvent>();
            await foreach (var item in Events.ReadAsync(new MethodologyTemplate(Template).Stream, 0))
                facts.Add(item.Event);
            return facts;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class Operators : IPlatformOperatorAccess
    {
        public bool Active { get; set; } = true;
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(Active && userId == Operator);
    }
}
