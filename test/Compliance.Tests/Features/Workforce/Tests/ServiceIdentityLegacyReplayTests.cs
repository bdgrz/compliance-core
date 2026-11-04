using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityLegacyReplayTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly DateOnly Today = new(2026, 11, 4);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("owner", false)]
    [InlineData("purpose", false)]
    [InlineData("review", false)]
    [InlineData("disable", false)]
    [InlineData("enable", false)]
    [InlineData("retire", false)]
    [InlineData("owner", true)]
    [InlineData("purpose", true)]
    [InlineData("review", true)]
    [InlineData("disable", true)]
    [InlineData("enable", true)]
    [InlineData("retire", true)]
    public void ShouldPreserveTerminalLatchGivenSerializedLegacyRetirement(string operation, bool reactivated)
    {
        // Arrange
        var identity = new ServiceIdentity(Tenant, Uuid.CreateVersion4());
        var facts = new List<DomainEvent>
        {
            new ServiceIdentityRecorded(Tenant, identity.Id, Terms("active"), Author, Now),
            new ServiceIdentityRevised(Tenant, identity.Id, 2, Terms("retired"), Author, Now.AddMinutes(1)),
        };
        if (reactivated)
            facts.Add(new ServiceIdentityRevised(Tenant, identity.Id, 3, Terms("active"), Author, Now.AddMinutes(2)));
        identity = NativeFacts.Replay(identity, facts.Select((fact, index) =>
            DomainEventSeed.Attach(RoundTrip(fact), identity.Id, (ulong)index + 1)));
        var revision = identity.Revision;

        // Act
        var failure = operation switch
        {
            "owner" => identity.ChangeOwner(revision, "team", Uuid.CreateVersion4(), Author, Now),
            "purpose" => identity.ChangePurpose(revision, "A new purpose", Author, Now),
            "review" => identity.Review(revision, Today.AddYears(1), null, Author, Now),
            "disable" => identity.Disable(revision, null, Author, Now),
            "enable" => identity.Enable(revision, Author, Now),
            "retire" => identity.Retire(revision, null, Author, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(reactivated ? 3 : 2, identity.Revision);
        Assert.Empty(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    static DomainEvent RoundTrip(DomainEvent fact) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default), fact.GetType(),
        ComplianceCoreJsonContext.Default)!;

    static ServiceIdentityTerms Terms(string status) => new("Bot", "bot", "Approved purpose", null,
        status, "person", Owner, Today.AddMonths(6));
}
