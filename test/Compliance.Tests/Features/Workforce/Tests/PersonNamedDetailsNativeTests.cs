using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class PersonNamedDetailsNativeTests
{
    [Fact]
    public async Task ShouldPublishNamedFactGivenChangedPersonDetails()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId, fixture.PersonId, 1,
            "Ada King", "ada@example.com");

        // Act
        var result = await ChangeAsync(fixture, request);
        var facts = await FactsAsync(fixture);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var changed = Assert.IsType<PersonDetailsChanged>(facts[^1]);
        Assert.Equal(fixture.TenantId, changed.Header.TenantId);
        Assert.Equal(fixture.PersonId, changed.Header.PersonId);
        Assert.Equal(2, changed.Header.Revision);
        Assert.True(changed.Changed);
        Assert.Equal(new PersonDetailChanges(true, false, false), changed.ChangedFields);
        Assert.Equal(nameof(ChangePersonDetails), changed.Operation.CommandKind);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var actual = await scope.ServiceProvider.GetRequiredService<PersonReader>().ReadAsync(fixture.TenantId, fixture.PersonId);
        Assert.Equal("Ada King", actual.View!.DisplayName);
    }

    [Fact]
    public async Task ShouldReturnOriginalReceiptWithoutFactGivenExactIdentifiedRetry()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId, fixture.PersonId, 1,
            "Ada King", "ada@example.com");
        Assert.True((await ChangeAsync(fixture, request)).IsSuccess);
        var before = await FactsAsync(fixture);

        // Act
        var result = await ChangeAsync(fixture, request);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before, await FactsAsync(fixture));
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenChangedIntentReusesAcceptedIdentity()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId, fixture.PersonId, 1,
            "Ada King", "ada@example.com");
        Assert.True((await ChangeAsync(fixture, request)).IsSuccess);
        var before = await FactsAsync(fixture);

        // Act
        var result = await ChangeAsync(fixture, request with { DisplayName = "Another name" });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, await FactsAsync(fixture));
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenUnchangedNewDetailsIntent()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var before = await FactsAsync(fixture);
        var request = new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId, fixture.PersonId, 1,
            "Ada", "ada@example.com");

        // Act
        var result = await ChangeAsync(fixture, request);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, await FactsAsync(fixture));
    }

    [Fact]
    public async Task ShouldKeepPrivateValuesOutOfNamedFactGivenLargePersonalContactChange()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var contact = new PersonalContactDetails(null, new string('7', 450_000));
        var request = new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId, fixture.PersonId, 1,
            "Ada", "ada@example.com", contact);

        // Act
        var result = await ChangeAsync(fixture, request);
        var facts = await FactsAsync(fixture);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var changed = Assert.IsType<PersonDetailsChanged>(facts[^1]);
        Assert.Equal(new PersonDetailChanges(false, false, true), changed.ChangedFields);
        Assert.True(DomainEventPayload.Size(changed) <= DomainEventPayload.MaximumBytes);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var value = await scope.ServiceProvider.GetRequiredService<PersonReader>().ReadAsync(fixture.TenantId, fixture.PersonId);
        Assert.Equal(contact, value.View!.PersonalContact);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutFactGivenStaleOrMissingNativePerson(bool missing)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var before = await FactsAsync(fixture);
        var request = new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId,
            missing ? Uuid.CreateVersion4() : fixture.PersonId, missing ? 1 : 9, "Ada King", "ada@example.com");

        // Act
        var result = await ChangeAsync(fixture, request);

        // Assert
        Assert.Equal(missing ? RequestErrorKind.NotFound : RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, await FactsAsync(fixture));
    }

    [Fact]
    public async Task ShouldRefuseHistoricalReadGivenNamedFieldFlagsDifferFromVerifiedValues()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        Assert.True((await ChangeAsync(fixture, new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.PersonId, 1, "Ada King", "ada@example.com"))).IsSuccess);
        var facts = (await FactsAsync(fixture)).ToArray();
        var original = Assert.IsType<PersonDetailsChanged>(facts[^1]);
        var altered = original with { ChangedFields = new PersonDetailChanges(false, true, false) };
        altered.AttachMetadata(original.Metadata);
        facts[^1] = altered;
        var different = new InMemoryEventStore();
        await different.AppendAsync(new Person(fixture.TenantId, fixture.PersonId).Stream, 0, facts);
        await using var originalScope = fixture.Provider.CreateAsyncScope();
        var content = originalScope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(different);
        services.AddSingleton<IDomainEventReader>(different);
        services.AddSingleton(content);
        services.AddPortia();
        services.AddPersonNativeFixtures();
        await using var provider = services.BuildServiceProvider();
        var reader = new PersonReader(provider.GetRequiredService<IAggregateReader>(), different, content);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            _ = await reader.ReadAsync(fixture.TenantId, fixture.PersonId));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    static async Task<Result<UnitResult>> ChangeAsync(AttestWorkforceManagementWallTests.Fixture fixture, ChangePersonDetails request)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var handler = new ChangePersonDetailsHandler(scope.ServiceProvider.GetRequiredService<PersonOperations>());
        return await handler.HandleAsync(new RequestContext<ChangePersonDetails>(request,
            ProgramManagementServices.Actor(fixture.UserId),
            new HttpInvocation("POST", "/synthetic/person/details", "/synthetic/person/details", "synthetic")), CancellationToken.None);
    }

    static async Task<IReadOnlyList<DomainEvent>> FactsAsync(AttestWorkforceManagementWallTests.Fixture fixture)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var facts = new List<DomainEvent>();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(new Person(fixture.TenantId, fixture.PersonId).Stream, 0))
            facts.Add(record.Event);
        return facts;
    }
}
