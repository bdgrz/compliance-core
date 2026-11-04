using System.Text.Json;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityLegacyMutationTests
{
    static readonly DateOnly Today = new(2026, 11, 4);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("unchanged")]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("review")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("retire")]
    public async Task ShouldRefuseWithoutFactGivenLegacyServiceIdentityMutation(string change)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var owner = Uuid.CreateVersion4();
        var identity = new ServiceIdentity(tenant, Uuid.CreateVersion4());
        var status = change == "enable" ? "disabled" : "active";
        var terms = new ServiceIdentityTerms("Bot", "bot", "Approved purpose", null, status,
            "person", owner, Today.AddMonths(6));
        Assert.True(identity.Record(terms, Today, Author, Now).IsSuccess);
        var request = new ReviseServiceIdentity(tenant, identity.Id, 1, "Bot", "bot", "Approved purpose",
            "person", owner, Today.AddMonths(6), status);
        request = change switch
        {
            "unchanged" => request,
            "owner" => request with { OwnerId = Uuid.CreateVersion4() },
            "purpose" => request with { Purpose = "A new approved purpose" },
            "review" => request with { ReviewBy = Today.AddMonths(9) },
            "disable" => request with { LifecycleStatus = "disabled" },
            "enable" => request with { LifecycleStatus = "active" },
            "retire" => request with { LifecycleStatus = "retired" },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
        IRequestHandler<ReviseServiceIdentity> handler = new ReviseServiceIdentityHandler();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
            new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReviseServiceIdentity>(request, actor), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("named", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
        Assert.Equal(1, identity.Revision);
    }

    [Fact]
    public void ShouldPreserveOriginalRetryGivenLegacyServiceIdentityRevision()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var terms = new ServiceIdentityTerms("Bot", "bot", "Approved purpose", null, "active",
            "person", Uuid.CreateVersion4(), Today.AddMonths(6));
        DomainEvent[] historical =
        [
            new ServiceIdentityRecorded(tenant, id, terms, Author, Now),
            new ServiceIdentityRevised(tenant, id, 2, terms with { LifecycleStatus = "disabled" }, Author, Now),
        ];
        var replayed = NativeFacts.Replay(new ServiceIdentity(tenant, id), historical.Select((fact, index) =>
            DomainEventSeed.Attach(RoundTrip(fact), id, (ulong)index + 1)));

        // Act
        var retry = replayed.Record(terms, Today, Author, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(2, replayed.Revision);
        Assert.Empty(new AggregateScenario<ServiceIdentity>(replayed).PendingEvents);
    }

    static DomainEvent RoundTrip(DomainEvent fact) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default), fact.GetType(),
        ComplianceCoreJsonContext.Default)!;

}
