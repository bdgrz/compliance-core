using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaAuthoringRegistrationTests
{
    [Fact]
    public void ShouldRegisterOnlyTypedAuthoringHandlersAndReadersGivenOptIn()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddPortia();
        var hostedBefore = services.Count(item => item.ServiceType == typeof(IHostedService));
        Type[] requests =
        [
            typeof(DraftCriteriaEdition), typeof(ChooseInitialEditionLabel), typeof(CancelCriteriaEditionDraftRequest),
            typeof(BeginCriteriaEditionRename), typeof(CancelCriteriaEditionRename), typeof(AbandonCriteriaEditionDraft),
            typeof(BeginCriteriaPublication), typeof(ProposeCriterionChange), typeof(CorrectCriterionSummary),
            typeof(SetCriteriaEditionField), typeof(SetCriteriaEditionDisclosure), typeof(GetCriteriaEditionDraft),
            typeof(GetCriteriaOperation), typeof(GetCriteriaEditionValueOperation),
        ];

        // Act
        var returned = builder.AddCriteriaAuthoringServices();
        _ = builder.AddCriteriaAuthoringServices();
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetServices<RequestHandlerRegistration>().ToArray();

        // Assert
        Assert.Same(builder, returned);
        Assert.Equal(requests.Length, registrations.Length);
        foreach (var request in requests)
        {
            var registration = Assert.Single(registrations, item => item.RequestType == request);
            Assert.Equal(request == typeof(GetCriteriaEditionDraft)
                ? typeof(CriteriaEditionDraftView) : typeof(CriteriaOperationReceipt), registration.ResultType);
        }
        Assert.Contains(services, item => item.ServiceType == typeof(ICriteriaOperationReader) &&
            item.ImplementationType == typeof(SourceCriteriaOperationReader));
        Assert.Contains(services, item => item.ServiceType == typeof(ICriteriaEditionValueOperationReader) &&
            item.ImplementationType == typeof(SourceCriteriaEditionValueOperationReader));
        Assert.Equal(hostedBefore, services.Count(item => item.ServiceType == typeof(IHostedService)));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(ICriteriaAuthoringReadiness) ||
            item.ServiceType == typeof(IAccessAdmissionAuthority) || item.ServiceType == typeof(ICriteriaFreshEntryHead) ||
            item.ServiceType == typeof(ICriteriaFreshEditionHead) || item.ServiceType == typeof(ICriteriaSourceCutCoverage) ||
            item.ServiceType == typeof(ICriteriaParticipantCausalOrder) || item.ServiceType == typeof(ICriteriaPublicationAuthority));
    }

    [Fact]
    public async Task ShouldReturnRetryableUnavailableBeforeAccessingInfrastructureGivenNoQualification()
    {
        // Arrange
        var services = new ServiceCollection();
        var infrastructure = new UnusedInfrastructure();
        services.AddSingleton<IAggregateReader>(infrastructure);
        services.AddSingleton<IAggregateExecutor>(infrastructure);
        services.AddSingleton<ICriteriaExactSourceReader>(infrastructure);
        services.AddPortia().AddCriteriaAuthoringServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Uuid.CreateVersion4().ToString()), new Claim("iss", "bdgrz")], "session"));
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var draft = new DraftCriteriaEdition(edition, operation, "tsc", "edition_1");
        var entry = new ProposeCriterionChange(edition, Uuid.CreateVersion4(), "CC6.1", operation,
            0, "create", "criterion", "security", null, null, "summary");
        var field = new SetCriteriaEditionField(edition, Uuid.CreateVersion4(), "coverage_note", operation,
            0, "summary", null);

        // Act
        var draftResult = await scope.ServiceProvider.GetRequiredService<CriteriaEditionOperations>()
            .DraftAsync(new RequestContext<DraftCriteriaEdition>(draft, actor), CancellationToken.None);
        var entryResult = await scope.ServiceProvider.GetRequiredService<CriteriaEntryOperations>()
            .ProposeAsync(new RequestContext<ProposeCriterionChange>(entry, actor), CancellationToken.None);
        var fieldResult = await scope.ServiceProvider.GetRequiredService<CriteriaEditionValueOperations>()
            .SetFieldAsync(new RequestContext<SetCriteriaEditionField>(field, actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, draftResult.Error?.Kind);
        Assert.True(draftResult.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, entryResult.Error?.Kind);
        Assert.True(entryResult.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, fieldResult.Error?.Kind);
        Assert.True(fieldResult.Error?.IsTransient);
        Assert.Equal(0, infrastructure.Calls);
    }

    [Fact]
    public void ShouldFailHandlerResolutionGivenRequiredInfrastructureIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPortia().AddCriteriaAuthoringServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act and assert
        Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<CriteriaEntryOperations>());
    }

    sealed class UnusedInfrastructure : IAggregateReader, IAggregateExecutor, ICriteriaExactSourceReader
    {
        public int Calls { get; private set; }

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        { Calls++; throw new InvalidOperationException("Unexpected read."); }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where TAggregate : Aggregate
        { Calls++; throw new InvalidOperationException("Unexpected write."); }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate
        { Calls++; throw new InvalidOperationException("Unexpected write."); }

        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default)
        { Calls++; throw new InvalidOperationException("Unexpected read."); }

        public IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            CancellationToken ct = default)
        { Calls++; throw new InvalidOperationException("Unexpected read."); }
    }
}
