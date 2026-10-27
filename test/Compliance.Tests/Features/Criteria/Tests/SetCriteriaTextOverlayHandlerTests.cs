using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

/// <summary>
///     The per-criterion overlay revision line: required command id and expected revision, exact retries that return
///     the original registration, changed intent that conflicts, independent criteria and complete immutable content.
/// </summary>
public sealed class SetCriteriaTextOverlayHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    static readonly CriteriaTextOverlayContent Content = new("Synthetic supplied text",
        "Test supplier", "Test license", new CriteriaOverlayUsageFlags(true, true));

    [Fact]
    public async Task ShouldKeepOverlayAuthorityBoundedGivenManySuppliedTextRevisions()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        for (var revision = 0; revision < 32; revision++)
        {
            var result = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), revision,
                Content with { Text = $"Supplied source revision {revision + 1}" }));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        // Act
        var owner = await scenario.OwnerAsync();
        var events = await scenario.ReadEventsAsync();
        var first = await scenario.ReadContentAsync(Assert.IsType<CriteriaTextOverlayEntryRevised>(events[0].Event).OverlayContent);
        var latest = await scenario.ReadContentAsync(Assert.IsType<CriteriaTextOverlayEntryRevised>(events[^1].Event).OverlayContent);

        // Assert
        Assert.Equal(32, owner.Revision);
        Assert.Equal(32, events.Count);
        Assert.Equal("Supplied source revision 1", first.Text);
        Assert.Equal("Supplied source revision 32", latest.Text);
        Assert.All(typeof(CriteriaTextOverlay).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(owner) is ICollection { Count: > 2 },
                $"The overlay command owner retains lifetime supplied-text history in {field.Name}."));
    }

    [Fact]
    public async Task ShouldReturnOriginalRevisionGivenReplayAfterLaterRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var firstRequest = scenario.Request(Uuid.CreateVersion4(), expectedRevision: 0, Content);
        var first = await scenario.HandleAsync(firstRequest);
        Assert.True(first.IsSuccess);
        var second = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), expectedRevision: 1,
            Content with { Text = "Revised synthetic text" }));
        Assert.True(second.IsSuccess);

        // Act
        var retry = await scenario.HandleAsync(firstRequest);
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(1, retry.Value?.Revision);
        Assert.Equal(2, second.Value?.Revision);
        Assert.Equal(2, events.Count);
        Assert.Equal(2, Assert.IsType<CriteriaTextOverlayEntryRevised>(events[^1].Event).Revision);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionWithoutWritingGivenOverlayChanged()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0, Content))).IsSuccess);
        Assert.True((await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 1,
            Content with { Text = "Revised synthetic text" }))).IsSuccess);

        // Act
        var stale = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 1,
            Content with { Text = "Stale synthetic text" }));
        var firstAgain = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0, Content));
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, firstAgain.Error?.Kind);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ShouldNotWriteGivenUnknownCriterionOrWrongEdition()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var unknown = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0, Content,
            identifier: "UNKNOWN"));
        var wrongEdition = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0, Content,
            edition: Uuid.CreateVersion4()));
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, wrongEdition.Error?.Kind);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ShouldConflictGivenSameCommandWithChangedIntentOrRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var commandId = Uuid.CreateVersion4();
        var request = scenario.Request(commandId, 0, Content);
        Assert.True((await scenario.HandleAsync(request)).IsSuccess);

        // Act
        var changedText = await scenario.HandleAsync(scenario.Request(commandId, 0,
            Content with { Text = "Different intent" }));
        var changedRevision = await scenario.HandleAsync(scenario.Request(commandId, 1, Content));
        var changedFlags = await scenario.HandleAsync(scenario.Request(commandId, 0,
            Content with { UsageFlags = new CriteriaOverlayUsageFlags(false, false) }));
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedText.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedFlags.Error?.Kind);
        Assert.Single(events);
    }

    [Fact]
    public async Task ShouldKeepCriteriaIndependentGivenDifferentIdentifiersAtTheSameRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var access = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0, Content));
        var accounts = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0, Content,
            identifier: "CC6.2"));
        var accessAgain = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 1,
            Content with { Text = "Revised" }));

        // Assert
        Assert.Equal(1, access.Value?.Revision);
        Assert.Equal(1, accounts.Value?.Revision);
        Assert.Equal(2, accessAgain.Value?.Revision);
        Assert.Equal(2, (await scenario.ReadEventsAsync()).Count);
        Assert.Single(await scenario.ReadEventsAsync("CC6.2"));
        Assert.NotEqual(scenario.StreamOf("CC6.1"), scenario.StreamOf("CC6.2"));
    }

    [Fact]
    public async Task ShouldAppendExactlyOneRevisionGivenTwoWritersAtTheSameExpectedRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var left = scenario.Request(Uuid.CreateVersion4(), 0, Content with { Text = "Left writer" });
        var right = scenario.Request(Uuid.CreateVersion4(), 0, Content with { Text = "Right writer" });

        // Act
        var results = await Task.WhenAll(scenario.HandleAsync(left), scenario.HandleAsync(right));
        var loser = results[0].IsSuccess ? right : left;
        var recovered = await scenario.HandleAsync(scenario.Request(loser.Request.CommandId, 1, loser.Request.Content));

        // Assert
        Assert.Single(results, static result => result.IsSuccess);
        Assert.Single(results, static result => result.Error?.Kind == RequestErrorKind.Conflict);
        Assert.Equal(2, recovered.Value?.Revision);
        Assert.Equal(2, (await scenario.ReadEventsAsync()).Count);
    }

    [Fact]
    public async Task ShouldKeepCompleteMaximumUnicodeTextInImmutableContentGivenBoundaryRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var text = string.Concat(Enumerable.Repeat("日本語", 5333)) + "é";
        Assert.Equal(CriteriaTextOverlayRules.MaximumTextLength, text.Length);

        // Act
        var result = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0, Content with { Text = text }));
        var tooLong = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 1,
            Content with { Text = text + "x" }));
        var fact = Assert.IsType<CriteriaTextOverlayEntryRevised>(Assert.Single(await scenario.ReadEventsAsync()).Event);
        var stored = await scenario.ReadContentAsync(fact.OverlayContent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, tooLong.Error?.Kind);
        Assert.Equal(text, stored.Text);
        Assert.Equal(Content.Supplier, stored.Supplier);
    }

    [Theory]
    [InlineData("", "Supplier", "License")]
    [InlineData("Text", "", "License")]
    [InlineData("Text", "Supplier", "")]
    [InlineData("Text", "Supplier\u0001", "License")]
    public async Task ShouldRejectOverlayWithoutWritingGivenMissingOrInvalidSourceFacts(string text, string supplier,
        string license)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.HandleAsync(scenario.Request(Uuid.CreateVersion4(), 0,
            new CriteriaTextOverlayContent(text, supplier, license, new CriteriaOverlayUsageFlags(false, false))));
        var events = await scenario.ReadEventsAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(events);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly Uuid _userId = Uuid.CreateVersion4();

        Scenario(ServiceProvider provider) => _provider = provider;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();

        public static Task<Scenario> CreateAsync() => Task.FromResult(new Scenario(ProgramManagementServices.Build(
            new RecordingPermissionAuthorizer(allowed: true), static _ => { },
            static services => services.AddSingleton<ICriteriaCatalog>(CriteriaCatalog.Platform))));

        public FixedRequestContext Request(Uuid commandId, long expectedRevision, CriteriaTextOverlayContent content,
            string identifier = "CC6.1", Uuid? edition = null) =>
            new(new SetCriteriaTextOverlay(commandId, TenantId, edition ?? CriteriaCatalog.Platform.Edition.EditionId,
                identifier, expectedRevision, content), Actor(_userId), commandId);

        public async Task<Result<CriteriaTextOverlayRegistration>> HandleAsync(FixedRequestContext context)
        {
            await using var scope = _provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var handler = new SetCriteriaTextOverlayHandler(services.GetRequiredService<NativeOperationExecutor>(),
                CriteriaCatalog.Platform,
                services.GetRequiredService<IImmutableContentStore>(), new FixedTimeProvider(Now));
            return await handler.HandleAsync(context, CancellationToken.None);
        }

        public async Task<CriteriaTextOverlay> OwnerAsync()
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
                .HydrateAsync(new CriteriaTextOverlay(TenantId, CriteriaCatalog.Platform.Edition.EditionId, "CC6.1"));
        }

        public EventStreamAddress StreamOf(string identifier) => new CriteriaTextOverlay(TenantId,
            CriteriaCatalog.Platform.Edition.EditionId, identifier).Stream;

        public async Task<List<DomainEventRecord>> ReadEventsAsync(string identifier = "CC6.1")
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in _provider.GetRequiredService<IEventStore>().ReadAsync(StreamOf(identifier), 0,
                               CancellationToken.None))
                records.Add(record);
            return records;
        }

        public Task<CriteriaTextOverlayContent> ReadContentAsync(Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference) =>
            NativeContent.ReadAsync<CriteriaTextOverlayContent>(_provider.GetRequiredService<IImmutableContentStore>(),
                TenantId, reference);

        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }

    sealed class FixedRequestContext(SetCriteriaTextOverlay request, ClaimsPrincipal actor, Uuid requestId)
        : IRequestContext<SetCriteriaTextOverlay>
    {
        public SetCriteriaTextOverlay Request => request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => requestId;
        public Uuid CorrelationId => requestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => requestId;
        public DateTimeOffset StartedAt { get; } = Now;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("name", "Synthetic editor")], "UnitTest"));

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
