using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;

/// <summary>
///     The three name-change processes (provider, location, operational process) and their receivers over the
///     shared runner: the registered definitions equal the design catalog, names are claimed and released in
///     canonical order, conflicts abort with a fence, and every step replays exactly.
/// </summary>
public sealed class NamedRecordProcessTests
{
    static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "docs", "product", "audits", "2026-10-23", "target-reactor-contracts.json");

    static readonly NamedRecordKindDescriptor Provider = ProviderNames.Descriptor;
    static readonly NamedRecordKindDescriptor Location = LocationNames.Descriptor;
    static readonly NamedRecordKindDescriptor Process = OperationalProcessNames.Descriptor;

    [Theory]
    [InlineData("ProviderNameChangeProcess")]
    [InlineData("LocationNameChangeProcess")]
    [InlineData("OperationalProcessNameChangeProcess")]
    public void ShouldEqualDesignCatalogGivenRegisteredNameChangeProcess(string name)
    {
        // Arrange
        using var document = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var entry = document.RootElement.GetProperty("processes").EnumerateArray()
            .Single(process => process.GetProperty("process").GetString() == name);
        var definition = new[]
        {
            ProviderNameChangeProcess.Definition, LocationNameChangeProcess.Definition,
            OperationalProcessNameChangeProcess.Definition,
        }.Single(candidate => candidate.Name == name);

        // Act
        var rules = entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("rule").GetString());

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(entry.GetProperty("ordered").GetBoolean(), definition.Ordered);
        Assert.Equal(entry.GetProperty("rootSourceEvents").EnumerateArray().Select(root => root.GetString()),
            definition.RootSourceEvents);
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("stage").GetString()),
            definition.Stages.Select(stage => stage.Stage));
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("receivers")
                .EnumerateArray().Select(receiver => receiver.GetString()).ToArray()),
            definition.Stages.Select(stage => stage.Receivers.ToArray()));
        Assert.Equal(entry.GetProperty("abort").EnumerateArray().Select(abort => abort.GetString()), definition.Abort);
        Assert.Equal(rules, definition.Stages.Select(stage => stage.Rule));
    }

    [Fact]
    public async Task ShouldApplyCreationAndHoldNameGivenAcceptedProvider()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var id = Uuid.CreateVersion4();

        // Act
        var accepted = await host.RecordProviderAsync("Acme Cloud", id);
        var record = await host.DriveAsync(Provider, id);
        var owner = await host.ProviderAsync(id);
        var claim = await host.ProviderClaimAsync("  ACME cloud ");
        var facts = await host.FactsAsync(owner.Stream);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(id, accepted.Value!.OperationId);
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal(1, owner.Revision);
        Assert.Null(owner.Pending);
        Assert.Equal(owner.Stream, claim.Holder);
        Assert.Equal(1, claim.HeldGeneration);
        Assert.Collection(facts, fact => Assert.IsType<ProviderChangeAccepted>(fact),
            fact => Assert.IsType<ProviderRecorded>(fact));
        Assert.Equal(["ApplyProviderChange", ProcessRecord.FactKey(ProviderNames.ClaimReceiver, record.Targets[0])],
            record.Facts.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ShouldHoldBothNamesThroughApplyAndReleaseOnlyTheOldOneGivenProviderRename()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var id = Uuid.CreateVersion4();
        await host.RecordProviderAsync("Old Name", id);
        await host.DriveAsync(Provider, id);
        var revision = Uuid.CreateVersion4();

        // Act
        var accepted = await host.ReviseProviderAsync(id, "New Name", 1, revision);
        var prepared = await host.PrepareProcessAsync(Provider, revision);
        var record = await host.DriveAsync(Provider, revision);
        var owner = await host.ProviderAsync(id);
        var oldClaim = await host.ProviderClaimAsync("Old Name");
        var newClaim = await host.ProviderClaimAsync("New Name");
        var oldFacts = await host.FactsAsync(oldClaim.Stream);

        // Assert
        Assert.True(accepted.IsSuccess);
        Assert.Equal(2, prepared.Targets.Count);
        Assert.Equal(prepared.Targets.Order(StreamKeyOrder.Instance), prepared.Targets);
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal(2, owner.Revision);
        Assert.Null(oldClaim.Holder);
        Assert.Equal(owner.Stream, newClaim.Holder);
        Assert.Equal(2, newClaim.HeldGeneration);
        Assert.Collection(oldFacts, fact => Assert.IsType<ProviderNameClaimed>(fact),
            fact => Assert.IsType<ProviderNameClaimed>(fact), fact => Assert.IsType<ProviderNameReleased>(fact));
    }

    [Fact]
    public async Task ShouldRefuseBothAndKeepClaimsInCanonicalOrderGivenOppositeRenames()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var (first, second) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordProviderAsync("Alpha", first);
        await host.DriveAsync(Provider, first);
        await host.RecordProviderAsync("Beta", second);
        await host.DriveAsync(Provider, second);
        var (toBeta, toAlpha) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.ReviseProviderAsync(first, "Beta", 1, toBeta);
        await host.ReviseProviderAsync(second, "Alpha", 1, toAlpha);

        // Act
        var firstRecord = await host.PrepareProcessAsync(Provider, toBeta);
        var secondRecord = await host.PrepareProcessAsync(Provider, toAlpha);
        var firstDone = await host.DriveAsync(Provider, toBeta);
        var secondDone = await host.DriveAsync(Provider, toAlpha);
        var alpha = await host.ProviderClaimAsync("Alpha");
        var beta = await host.ProviderClaimAsync("Beta");
        var one = await host.ProviderAsync(first);
        var two = await host.ProviderAsync(second);

        // Assert
        Assert.Equal(firstRecord.Targets, secondRecord.Targets);
        Assert.Equal(firstRecord.Targets.Order(StreamKeyOrder.Instance), firstRecord.Targets);
        Assert.Equal(ProcessState.Aborted, firstDone.State);
        Assert.Equal(ProcessState.Aborted, secondDone.State);
        Assert.Equal(one.Stream, alpha.Holder);
        Assert.Equal(two.Stream, beta.Holder);
        Assert.Equal(1, one.Revision);
        Assert.Equal(1, two.Revision);
        Assert.Null(one.Pending);
        Assert.Null(two.Pending);
        Assert.Contains((await host.FactsAsync(one.Stream)), static fact => fact is ProviderChangeCanceled);
        Assert.Contains((await host.FactsAsync(two.Stream)), static fact => fact is ProviderChangeCanceled);
        // Neither process acquired the second key of the canonical order after the first was refused.
        var secondKey = firstRecord.Targets[1];
        var holders = new[] { alpha, beta }.Single(claim => claim.Stream.Resource ==
            NamedRecordProcessView.DecodeTarget(secondKey).Resource);
        Assert.Equal(1, (await host.FactsAsync(holders.Stream)).Count(static fact => fact is ProviderNameClaimed));
    }

    [Fact]
    public async Task ShouldCancelAndKeepOriginalHolderGivenDuplicateNameClaim()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var (first, duplicate) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordProviderAsync("Contoso", first);
        await host.DriveAsync(Provider, first);

        // Act
        var accepted = await host.RecordProviderAsync("  contoso ", duplicate);
        var record = await host.DriveAsync(Provider, duplicate);
        var retry = await host.RecordProviderAsync("  contoso ", duplicate);
        var claim = await host.ProviderClaimAsync("Contoso");
        var loser = await host.ProviderAsync(duplicate);
        var facts = await host.FactsAsync(loser.Stream);

        // Assert
        Assert.True(accepted.IsSuccess);
        Assert.Equal(accepted.Value, retry.Value);
        Assert.Equal(ProcessState.Aborted, record.State);
        var canceled = Assert.IsType<ProviderChangeCanceled>(facts[1]);
        var proof = await host.Content.ReadJsonAsync<NameChangeFailureProof>(host.TenantId.ToString(),
            canceled.SourceProofs!);
        Assert.Contains("claimed by another", proof.Reason, StringComparison.Ordinal);
        Assert.Equal((await host.ProviderAsync(first)).Stream, claim.Holder);
        Assert.Equal(0, loser.Revision);
        Assert.Null(loser.Pending);
        Assert.Collection(facts, fact => Assert.IsType<ProviderChangeAccepted>(fact),
            fact => Assert.IsType<ProviderChangeCanceled>(fact));
    }

    [Fact]
    public async Task ShouldLetExactlyOneWinGivenTwoConcurrentCreationsOfOneName()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var (left, right) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordProviderAsync("Race", left);
        await host.RecordProviderAsync("RACE", right);

        // Act
        var results = await Task.WhenAll(host.DriveAsync(Provider, left), host.DriveAsync(Provider, right));
        var claim = await host.ProviderClaimAsync("race");

        // Assert
        Assert.Single(results, static record => record.State == ProcessState.Completed);
        Assert.Single(results, static record => record.State == ProcessState.Aborted);
        var winner = (await host.ProviderAsync(left)).Revision == 1 ? left : right;
        Assert.Equal((await host.ProviderAsync(winner)).Stream, claim.Holder);
    }

    [Fact]
    public async Task ShouldReplayOriginalResponseGivenExactRetryAfterLaterChangeAndConflictOnChangedIntent()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var id = Uuid.CreateVersion4();
        var first = await host.RecordProviderAsync("Stable", id);
        await host.DriveAsync(Provider, id);
        var command = Uuid.CreateVersion4();
        var revised = await host.ReviseProviderAsync(id, "Stable Two", 1, command);
        await host.DriveAsync(Provider, command);

        // Act
        var retryCreate = await host.RecordProviderAsync("Stable", id);
        var retryRevise = await host.ReviseProviderAsync(id, "Stable Two", 1, command);
        var changedCreate = await host.RecordProviderAsync("Different", id);
        var changedRevise = await host.ReviseProviderAsync(id, "Stable Two", 1, command, host.Author);
        var changedExpectation = await host.ReviseProviderAsync(id, "Stable Two", 2, command);
        var owner = await host.ProviderAsync(id);

        // Assert
        Assert.Equal(first.Value, retryCreate.Value);
        Assert.Equal(revised.Value, retryRevise.Value);
        Assert.Equal(RequestErrorKind.Conflict, changedCreate.Error!.Kind);
        Assert.True(changedRevise.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changedExpectation.Error!.Kind);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, owner.Generation);
    }

    [Fact]
    public async Task ShouldKeepOtherProvidersIndependentGivenOneProviderChange()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var (one, two) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordProviderAsync("First", one);
        await host.DriveAsync(Provider, one);
        var before = (await host.ProviderAsync(one)).CommittedStreamPosition;

        // Act
        await host.RecordProviderAsync("Second", two);
        await host.DriveAsync(Provider, two);
        var after = await host.ProviderAsync(one);
        var other = await host.ProviderAsync(two);

        // Assert
        Assert.NotEqual(after.Stream, other.Stream);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Equal(1, other.Revision);
    }

    [Fact]
    public async Task ShouldReturnReplayedGivenRepeatedReceiverCommandAndResumeWithoutDuplicateFacts()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var id = Uuid.CreateVersion4();
        await host.RecordProviderAsync("Crash Test", id);
        var prepared = await host.PrepareProcessAsync(Provider, id);
        var claimCommand = await host.CommandAsync(Provider, prepared, ProviderNames.ClaimReceiver, prepared.Targets[0]);

        // Act
        var claimed = await host.SendAsync(claimCommand);
        var repeated = await host.SendAsync(claimCommand);
        var resumed = await host.ResumeAsync(Provider, id);
        var claim = await host.ProviderClaimAsync("Crash Test");
        var facts = await host.FactsAsync((await host.ProviderAsync(id)).Stream);

        // Assert
        var fact = Assert.IsType<ReceiverOutcome.Applied>(claimed.Value).Fact;
        Assert.Equal(fact, Assert.IsType<ReceiverOutcome.Replayed>(repeated.Value).Fact);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single(await host.FactsAsync(claim.Stream), static item => item is ProviderNameClaimed);
        Assert.Equal(2, facts.Count);
    }

    [Fact]
    public async Task ShouldNotReleaseNewHolderGivenStaleOldReleaseWithNewCommandId()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var (renamed, newcomer) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordProviderAsync("Reused", renamed);
        await host.DriveAsync(Provider, renamed);
        var command = Uuid.CreateVersion4();
        await host.ReviseProviderAsync(renamed, "Moved Away", 1, command);
        var record = await host.DriveAsync(Provider, command);
        await host.RecordProviderAsync("Reused", newcomer);
        await host.DriveAsync(Provider, newcomer);
        var oldTarget = record.Targets.Single(target => target.EndsWith(":REUSED", StringComparison.Ordinal));
        var original = (ReleaseProviderName)await host.CommandAsync(Provider, record, ProviderNames.ReleaseReceiver, oldTarget);

        // Act
        var replay = await host.SendAsync(original);
        var stale = await host.SendAsync(original with { CommandId = Uuid.CreateVersion4() });
        var claim = await host.ProviderClaimAsync("Reused");

        // Assert
        Assert.IsType<ReceiverOutcome.Replayed>(replay.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(stale.Value);
        Assert.Equal((await host.ProviderAsync(newcomer)).Stream, claim.Holder);
    }

    [Fact]
    public async Task ShouldSupersedeApplyAndNoOpCancelGivenFenceOrAppliedChange()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var (fenced, applied) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordProviderAsync("Fenced", fenced);
        await host.RecordProviderAsync("Applied", applied);
        var fencedRecord = await host.PrepareProcessAsync(Provider, fenced);
        var appliedRecord = await host.PrepareProcessAsync(Provider, applied);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(await host.CommandAsync(Provider, fencedRecord,
            ProviderNames.ClaimReceiver, fencedRecord.Targets[0]))).Value);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(await host.CommandAsync(Provider, appliedRecord,
            ProviderNames.ClaimReceiver, appliedRecord.Targets[0]))).Value);
        var fencedWithClaim = await WithClaimFactAsync(host, fencedRecord, "Fenced");
        var appliedWithClaim = await WithClaimFactAsync(host, appliedRecord, "Applied");
        var cancelFenced = await host.CommandAsync(Provider, fencedWithClaim with { Note = "refused" },
            ProviderNames.CancelReceiver);
        var applyFenced = await host.CommandAsync(Provider, fencedWithClaim, ProviderNames.ApplyReceiver);
        var applyApplied = await host.CommandAsync(Provider, appliedWithClaim, ProviderNames.ApplyReceiver);
        var cancelApplied = await host.CommandAsync(Provider, appliedWithClaim with { Note = "late" },
            ProviderNames.CancelReceiver);

        // Act
        var canceled = await host.SendAsync(cancelFenced);
        var lateApply = await host.SendAsync(applyFenced);
        var applied1 = await host.SendAsync(applyApplied);
        var lateCancel = await host.SendAsync(cancelApplied);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(canceled.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(lateApply.Value);
        Assert.IsType<ReceiverOutcome.Applied>(applied1.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(lateCancel.Value);
        Assert.Equal(0, (await host.ProviderAsync(fenced)).Revision);
        Assert.Equal(1, (await host.ProviderAsync(applied)).Revision);
    }

    [Fact]
    public async Task ShouldRejectReceiverGivenNonSystemPrincipal()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var id = Uuid.CreateVersion4();
        await host.RecordProviderAsync("Guarded", id);
        var prepared = await host.PrepareProcessAsync(Provider, id);
        var command = await host.CommandAsync(Provider, prepared, ProviderNames.ClaimReceiver, prepared.Targets[0]);

        // Act
        var denied = await host.SendAsync(command, host.Author);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error!.Kind);
        Assert.Empty(await host.FactsAsync((await host.ProviderClaimAsync("Guarded")).Stream));
    }

    [Fact]
    public async Task ShouldRefuseSecondChangeGivenPendingChangeAndStaleExpectedRevision()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var id = Uuid.CreateVersion4();
        await host.RecordProviderAsync("Pending", id);
        await host.DriveAsync(Provider, id);
        await host.ReviseProviderAsync(id, "Pending Two", 1);

        // Act
        var whilePending = await host.ReviseProviderAsync(id, "Pending Three", 1);
        var unknown = await host.ReviseProviderAsync(Uuid.CreateVersion4(), "Nothing", 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, whilePending.Error!.Kind);
        Assert.True(whilePending.Error.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error!.Kind);
    }

    static async Task<ProcessRecord> WithClaimFactAsync(NamedRecordHost host, ProcessRecord record, string name)
    {
        var claim = await host.ProviderClaimAsync(name);
        var fact = (await host.FactsAsync(claim.Stream)).Single();
        return record with
        {
            Facts = new Dictionary<string, NativeFactReference>
            {
                [ProcessRecord.FactKey(ProviderNames.ClaimReceiver, record.Targets.Single(target =>
                    NamedRecordProcessView.DecodeTarget(target).Resource == claim.Stream.Resource))] =
                    NativeFactReference.Of(claim.Stream, fact),
            },
        };
    }

    [Fact]
    public async Task ShouldRefuseStaleExpectedRevisionAndAppendNothingGivenAdvancedProvider()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        var id = Uuid.CreateVersion4();
        await host.RecordProviderAsync("Revisioned", id);
        await host.DriveAsync(Provider, id);
        var second = Uuid.CreateVersion4();
        await host.ReviseProviderAsync(id, "Revisioned Two", 1, second);
        await host.DriveAsync(Provider, second);
        var before = (await host.ProviderAsync(id)).CommittedStreamPosition;

        // Act
        var stale = await host.ReviseProviderAsync(id, "Revisioned Three", 1);
        var after = (await host.ProviderAsync(id)).CommittedStreamPosition;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Contains("Current revision: 2", stale.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    // --- locations

    [Fact]
    public async Task ShouldKeepKindGivenLocationRevisionThatOmitsIt()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        await host.SeedOwnerAsync();
        var id = Uuid.CreateVersion4();
        await host.RecordLocationAsync("Region One", "hosting_region", id);
        await host.DriveAsync(Location, id);
        var revision = Uuid.CreateVersion4();

        // Act
        await host.ReviseLocationAsync(id, "Region Renamed", 1, "active", revision);
        await host.DriveAsync(Location, revision);
        var owner = await host.LocationAsync(id);
        var content = System.Text.Json.JsonSerializer.Deserialize(
            (await host.Content.ReadVerifiedAsync(host.TenantId.ToString(), owner.Current!.Content)).Span,
            ComplianceCoreJsonContext.Default.LocationContent)!;

        // Assert
        Assert.Equal("hosting_region", content.Kind);
        Assert.Equal("Region Renamed", content.Name);
        Assert.Equal((await host.LocationClaimAsync("Region Renamed", "hosting_region")).Holder, owner.Stream);
        Assert.Null((await host.LocationClaimAsync("Region One", "hosting_region")).Holder);
    }

    [Fact]
    public async Task ShouldClaimWithinKindGivenSameNameOfDifferentLocationKinds()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        await host.SeedOwnerAsync();
        var (site, region, duplicate) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        await host.RecordLocationAsync("Frankfurt", "physical_site", site);
        await host.RecordLocationAsync("frankfurt", "hosting_region", region);
        await host.RecordLocationAsync("FRANKFURT", "physical_site", duplicate);
        var records = new[]
        {
            await host.DriveAsync(Location, site), await host.DriveAsync(Location, region),
            await host.DriveAsync(Location, duplicate),
        };
        var siteClaim = await host.LocationClaimAsync("Frankfurt", "physical_site");
        var regionClaim = await host.LocationClaimAsync("Frankfurt", "hosting_region");

        // Assert
        Assert.Equal([ProcessState.Completed, ProcessState.Completed, ProcessState.Aborted],
            records.Select(static record => record.State));
        Assert.Equal((await host.LocationAsync(site)).Stream, siteClaim.Holder);
        Assert.Equal((await host.LocationAsync(region)).Stream, regionClaim.Holder);
        Assert.NotEqual(siteClaim.Stream, regionClaim.Stream);
    }

    [Fact]
    public async Task ShouldReleaseOnRetirementAndRefuseReactivationGivenNewHolderOfTheName()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        await host.SeedOwnerAsync();
        var (original, newcomer) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordLocationAsync("HQ", "physical_site", original);
        await host.DriveAsync(Location, original);
        var retire = Uuid.CreateVersion4();
        await host.ReviseLocationAsync(original, "HQ", 1, "retired", retire);
        var retired = await host.DriveAsync(Location, retire);
        var freed = await host.LocationClaimAsync("HQ");

        // Act
        await host.RecordLocationAsync("HQ", "physical_site", newcomer);
        await host.DriveAsync(Location, newcomer);
        var reactivate = Uuid.CreateVersion4();
        await host.ReviseLocationAsync(original, "HQ", 2, "active", reactivate);
        var refused = await host.DriveAsync(Location, reactivate);
        var holder = await host.LocationClaimAsync("HQ");
        var owner = await host.LocationAsync(original);

        // Assert
        Assert.Equal(ProcessState.Completed, retired.State);
        Assert.Null(freed.Holder);
        Assert.Equal(ProcessState.Aborted, refused.State);
        Assert.Equal((await host.LocationAsync(newcomer)).Stream, holder.Holder);
        Assert.Equal(2, owner.Revision);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public async Task ShouldApplyFromTheAcceptedFactGivenRetiredLocationEditThatClaimsNothing()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        await host.SeedOwnerAsync();
        var id = Uuid.CreateVersion4();
        await host.RecordLocationAsync("Closed Site", "physical_site", id);
        await host.DriveAsync(Location, id);
        var retire = Uuid.CreateVersion4();
        await host.ReviseLocationAsync(id, "Closed Site", 1, "retired", retire);
        await host.DriveAsync(Location, retire);
        var edit = Uuid.CreateVersion4();

        // Act
        await host.ReviseLocationAsync(id, "Closed Site Renamed", 2, "retired", edit);
        var record = await host.DriveAsync(Location, edit);
        var owner = await host.LocationAsync(id);

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Empty(record.Targets);
        Assert.False(record.Facts.ContainsKey(LocationNames.ClaimReceiver));
        Assert.Equal(3, owner.Revision);
        Assert.Null((await host.LocationClaimAsync("Closed Site Renamed")).Holder);
    }

    // --- operational processes

    [Fact]
    public async Task ShouldRenameAndRefuseDuplicateGivenOperationalProcesses()
    {
        // Arrange
        await using var host = new NamedRecordHost();
        await host.SeedOwnerAsync();
        var (first, second) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await host.RecordProcessAsync("Backups", first);
        await host.DriveAsync(Process, first);
        await host.RecordProcessAsync("Restores", second);
        await host.DriveAsync(Process, second);
        var rename = Uuid.CreateVersion4();
        var conflict = Uuid.CreateVersion4();

        // Act
        await host.ReviseProcessAsync(first, "Offsite Backups", 1, "active", rename);
        var renamed = await host.DriveAsync(Process, rename);
        await host.ReviseProcessAsync(second, "offsite backups", 1, "active", conflict);
        var refused = await host.DriveAsync(Process, conflict);
        var oldName = await host.ProcessClaimAsync("Backups");
        var newName = await host.ProcessClaimAsync("Offsite Backups");

        // Assert
        Assert.Equal(ProcessState.Completed, renamed.State);
        Assert.Equal(ProcessState.Aborted, refused.State);
        Assert.Null(oldName.Holder);
        Assert.Equal((await host.ProcessOwnerAsync(first)).Stream, newName.Holder);
        Assert.Equal(1, (await host.ProcessOwnerAsync(second)).Revision);
        Assert.Equal((await host.ProcessClaimAsync("Restores")).Holder, (await host.ProcessOwnerAsync(second)).Stream);
    }

    // --- identities

    [Fact]
    public void ShouldDeriveClaimIdentityFromTypedPartsGivenTheDesignVector()
    {
        // Arrange
        var tenant = Uuid.Parse("00000000-0000-0000-0000-000000000001", System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var identity = AggregateIdentity.Create("ProviderNameClaim", tenant, "EXAMPLE");
        var address = ProviderNameClaim.AddressOf(tenant, "EXAMPLE");

        // Assert
        Assert.Equal("99758556-85f4-5674-870a-c23c7df99bfd", identity.ToString());
        Assert.Equal(identity.ToString(), address.Resource);
        Assert.Equal("provider-name-claims", address.Area);
    }
}
