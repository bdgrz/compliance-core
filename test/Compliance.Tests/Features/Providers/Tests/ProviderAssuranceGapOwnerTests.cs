using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class ProviderAssuranceGapOwnerTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("root")]
    [InlineData("cause")]
    [InlineData("kind")]
    [InlineData("episode")]
    [InlineData("revision")]
    [InlineData("affected")]
    [InlineData("revision_owner")]
    [InlineData("first_owner")]
    [InlineData("actor")]
    [InlineData("time")]
    [InlineData("content_schema")]
    [InlineData("response_schema")]
    [InlineData("result_schema")]
    public void ShouldRecordOnlyValidNativeRootGivenFreshGap(string defect)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var provider = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var owner = new ProviderAssuranceGap(tenant, provider, gapId);
        var scenario = new AggregateScenario<ProviderAssuranceGap>(owner);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Member");
        var at = NativeFacts.Now;
        var operation = owner.Provenance(gapId, nameof(RecordProviderCoverageGap), actor, "initial", null,
            new string('a', 64), at) with
        { FirstOwner = null };
        var foreign = new ProviderAssuranceGap(tenant, provider, Uuid.CreateVersion4()).Stream;
        operation = defect switch
        {
            "root" => operation with { RootOperationId = Uuid.CreateVersion4() },
            "cause" => operation with { CausationEventId = Uuid.CreateVersion4() },
            "kind" => operation with { CommandKind = "Other" },
            "episode" => operation with { Episode = "other" },
            "revision" => operation with { OriginalExpectedRevision = 1 },
            "affected" => operation with { AffectedOwner = foreign },
            "revision_owner" => operation with { ExpectedRevisionOwner = foreign },
            "first_owner" => operation with { FirstOwner = foreign },
            _ => operation,
        };
        var eventActor = defect == "actor" ? ActorReference.ForMember(Uuid.CreateVersion4(), "Other") : actor;
        var recordedAt = defect == "time" ? at.AddMinutes(1) : at;
        var content = NativeFacts.Ref(defect == "content_schema" ? "wrong" : ProviderAssuranceContent.GapRecordedSchema);
        var response = NativeFacts.Ref(defect == "response_schema" ? "wrong" : FirstResponses.Schema);
        var resultContent = NativeFacts.Ref(defect == "result_schema" ? "wrong" : ProviderAssuranceContent.GapRegistrationSchema);

        // Act
        var result = owner.Record(operation, 1, content, eventActor, recordedAt, response, resultContent);

        // Assert
        if (defect == "valid")
        {
            Assert.True(result.IsSuccess);
            Assert.Single(scenario.PendingEvents);
            Assert.True(owner.IsOpen);
            return;
        }
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.Exists);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("root")]
    [InlineData("cause")]
    [InlineData("kind")]
    [InlineData("episode")]
    [InlineData("revision")]
    [InlineData("actor")]
    [InlineData("time")]
    [InlineData("content_schema")]
    public async Task ShouldCloseOnlyWithValidNativeRootGivenVerifiedOpenGap(string defect)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var registration = await host.RecordGapAsync();
        var recorded = await host.GapContentAsync();
        var commandId = Uuid.CreateVersion4();
        var owner = await host.GapAsync(registration.GapId, commandId);
        var scenario = new AggregateScenario<ProviderAssuranceGap>(owner);
        var actor = owner.Recorded!.Operation.Actor;
        var at = NativeFacts.Now.AddDays(1);
        var operation = owner.Provenance(commandId, nameof(CloseProviderCoverageGap), actor, "current",
            (ulong)owner.Revision, new string('b', 64), at) with
        { FirstOwner = null };
        operation = defect switch
        {
            "root" => operation with { RootOperationId = Uuid.CreateVersion4() },
            "cause" => operation with { CausationEventId = Uuid.CreateVersion4() },
            "kind" => operation with { CommandKind = "Other" },
            "episode" => operation with { Episode = "other" },
            "revision" => operation with { OriginalExpectedRevision = (ulong)owner.Revision + 1 },
            _ => operation,
        };
        var eventActor = defect == "actor" ? ActorReference.ForMember(Uuid.CreateVersion4(), "Other") : actor;
        var closedAt = defect == "time" ? at.AddMinutes(1) : at;
        var closure = new ProviderCoverageGapClosureContent("coverage_restored", "other", Uuid.CreateVersion4(), 1, "Restored");
        var content = NativeFacts.Ref(defect == "content_schema" ? "wrong" : ProviderAssuranceContent.GapClosedSchema);

        // Act
        var result = owner.Close(operation, owner.Revision, recorded, closure, content, eventActor, closedAt,
            NativeFacts.Ref(FirstResponses.Schema), NativeFacts.Ref(ProviderAssuranceContent.GapRegistrationSchema));

        // Assert
        if (defect == "valid")
        {
            Assert.True(result.IsSuccess);
            Assert.Single(scenario.PendingEvents);
            Assert.False(owner.IsOpen);
            return;
        }
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.True(owner.IsOpen);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("logical")]
    [InlineData("risk")]
    [InlineData("acceptance")]
    [InlineData("root")]
    [InlineData("cause")]
    [InlineData("kind")]
    [InlineData("revision")]
    [InlineData("affected")]
    [InlineData("revision_owner")]
    [InlineData("first_owner")]
    [InlineData("intent_schema")]
    [InlineData("response_schema")]
    public async Task ShouldAdmitOnlyValidPairAndNativeRootGivenVerifiedOpenGap(string defect)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var registration = await host.RecordGapAsync();
        var commandId = defect == "logical" ? Uuid.Empty : Uuid.CreateVersion4();
        var risk = defect == "risk" ? Uuid.Empty : Uuid.CreateVersion4();
        var acceptance = defect == "acceptance" ? Uuid.Empty : Uuid.CreateVersion4();
        var pair = ProviderAssuranceGap.PairEpisode(risk, acceptance);
        var owner = await host.HydrateAsync(new ProviderAssuranceGap(host.Tenant, host.ProviderId,
            registration.GapId, commandId, selectedPair: pair));
        var scenario = new AggregateScenario<ProviderAssuranceGap>(owner);
        var actor = owner.Recorded!.Operation.Actor;
        var operation = owner.Provenance(commandId, nameof(LinkProviderCoverageGapRiskAcceptance), actor, pair,
            (ulong)owner.Revision, new string('b', 64), NativeFacts.Now) with
        { FirstOwner = null };
        var foreign = new ProviderAssuranceGap(host.Tenant, host.ProviderId, Uuid.CreateVersion4()).Stream;
        operation = defect switch
        {
            "root" => operation with { RootOperationId = Uuid.CreateVersion4() },
            "cause" => operation with { CausationEventId = Uuid.CreateVersion4() },
            "kind" => operation with { CommandKind = "Other" },
            "revision" => operation with { OriginalExpectedRevision = (ulong)owner.Revision + 1 },
            "affected" => operation with { AffectedOwner = foreign },
            "revision_owner" => operation with { ExpectedRevisionOwner = foreign },
            "first_owner" => operation with { FirstOwner = foreign },
            _ => operation,
        };
        var intent = NativeFacts.Ref(defect == "intent_schema" ? "wrong" : ProviderRiskLinkIntent.Schema);
        var response = NativeFacts.Ref(defect == "response_schema" ? "wrong" : FirstResponses.Schema);

        // Act
        var result = owner.AdmitLink(operation, owner.Revision, risk, acceptance, intent, response);

        // Assert
        if (defect == "valid")
        {
            Assert.True(result.IsSuccess);
            Assert.Single(scenario.PendingEvents);
            Assert.Equal(2, owner.Revision);
            return;
        }
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsOpen);
    }
}
