using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.History;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ExaminationEngagementNativeHistoryTests
{
    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("examination")]
    [InlineData("actor")]
    [InlineData("clock")]
    [InlineData("revision")]
    [InlineData("boundary")]
    [InlineData("terms")]
    public async Task ShouldRefuseValidStoredBodyGivenAnotherNativeScopeActorClockOrRevision(string field)
    {
        // Arrange
        await using var original = new ExaminationScenario();
        var rules = Rules();
        await original.SaveAsync(rules);
        var created = Assert.Single(await original.FactsAsync(rules));
        var state = await ExaminationEngagementContentReader.ReadAsync(original.Content, created);
        var view = state.Engagement;
        var changed = field switch
        {
            "tenant" => view with { TenantId = Uuid.CreateVersion4() },
            "program" => view with { ProgramId = Uuid.CreateVersion4() },
            "examination" => view with { ExaminationEngagementId = Uuid.CreateVersion4() },
            "actor" => view with { ActorMemberId = Uuid.CreateVersion4() },
            "clock" => view with { UpdatedAt = view.UpdatedAt.AddTicks(1) },
            "revision" => view with { Revision = view.Revision + 1 },
            "boundary" => view with { BoundaryId = Uuid.CreateVersion4() },
            "terms" => view with { AuditFirm = " " },
            _ => throw new InvalidOperationException("Unsupported body fault.")
        };
        var invalid = await ExaminationScenario.ReplaceStateAsync(original.Content, created,
            new ExaminationEngagementState(changed, null));
        // Both hydration and raw history see this same real stored native tuple and valid immutable content hash.
        var events = new InMemoryEventStore();
        await events.AppendAsync(new ExaminationEngagement(view.TenantId, view.ProgramId, rules.Id).Stream, 0, [invalid]);
        await using var corrupted = new ExaminationScenario(events, original.Content);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await corrupted.Examinations.ReadAsync(view.TenantId, view.ProgramId, rules.Id));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Theory]
    [InlineData("missing", true)]
    [InlineData("stream", false)]
    [InlineData("offset", false)]
    [InlineData("unselected_payload", false)]
    public async Task ShouldRefuseUnqualifiedHistoryGivenMissingOrContradictoryHydratedPrefix(string fault, bool transient)
    {
        // Arrange
        await using var scenario = new ExaminationScenario();
        var rules = Rules();
        var initial = rules.Current!;
        for (var revision = 2; revision <= 4; revision++)
            Assert.Null(rules.Revise(rules.Revision, Terms(initial) with { AuditFirm = $"Audit firm {revision}" },
                initial.ActorMemberId, initial.ActorDisplay, initial.UpdatedAt.AddMinutes(revision)));
        await scenario.SaveAsync(rules);
        var source = await scenario.FactsAsync(rules);
        var native = await scenario.Reader.HydrateAsync(new ExaminationEngagement(initial.TenantId,
            initial.ProgramId, rules.Id));
        var alternative = new AlteredEvents(scenario.Events, record => fault switch
        {
            "missing" => null,
            "stream" => record with
            {
                Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(),
                record.Stream.Area, record.Stream.Resource)
            },
            "offset" => record with { ResourceOffset = record.ResourceOffset + 1 },
            "unselected_payload" when record.Event.Metadata.EventId == source[1].Metadata.EventId => record with
            { Event = Changed((ExaminationEngagementRevised)record.Event, ((IExaminationEngagementFact)source[2]).Change) },
            _ => record
        });

        // Act
        var failure = await Record.ExceptionAsync(async () => await ExaminationEngagementHistory.ReadAsync(alternative, native));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(transient, failure is ImmutableContentUnavailableException);
    }

    [Fact]
    public async Task ShouldUseCapturedNativeAndPhysicalCutGivenLaterEngagementRevision()
    {
        // Arrange
        await using var scenario = new ExaminationScenario();
        var rules = Rules();
        await scenario.SaveAsync(rules);
        var initial = rules.Current!;
        var captured = await scenario.Reader.HydrateAsync(new ExaminationEngagement(initial.TenantId, initial.ProgramId, rules.Id));
        Assert.Null(rules.Revise(1, Terms(initial) with { AuditFirm = "Later firm" }, initial.ActorMemberId,
            initial.ActorDisplay, initial.UpdatedAt.AddMinutes(1)));
        await scenario.SaveAsync(rules);

        // Act
        var prior = await ExaminationEngagementHistory.ReadAsync(scenario.Events, captured);
        var current = await scenario.Examinations.ReadAsync(initial.TenantId, initial.ProgramId, rules.Id);

        // Assert
        Assert.Single(prior.Facts);
        Assert.Equal(1, captured.Revision);
        Assert.Equal(2, current.Owner.Revision);
        Assert.Equal("Later firm", current.View!.AuditFirm);
    }

    [Fact]
    public async Task ShouldPreserveExactHistoricalBaselineGivenLineageBeyondFormerInlineHistoryLimit()
    {
        // Arrange
        await using var scenario = new ExaminationScenario();
        var rules = Rules();
        var original = rules.Current!;
        var first = Uuid.CreateVersion4();
        var preview = Preview(1);
        Assert.Null(rules.FreezeBaseline(1, first, preview, "Confirmed exact sources", original.ActorMemberId,
            original.ActorDisplay, original.UpdatedAt.AddMinutes(1)));
        for (var version = 2; version <= 12; version++)
            Assert.Null(rules.AmendBaseline(rules.Revision, Uuid.CreateVersion4(), Preview(version),
                [original.BoundaryId], [], $"Amend exact source {version}", original.ActorMemberId,
                original.ActorDisplay, original.UpdatedAt.AddMinutes(version)));
        await scenario.SaveAsync(rules);
        TypeIBaselinePreview Preview(int version) => TypeIBaselinePreviewTestFactory.Create(original.TenantId,
            original.ProgramId, rules.Id, [new TypeIBaselineSourceReference("approved_boundary", original.BoundaryId,
                Uuid.CreateVersion4(), version, new string('a', 64))]);

        // Act
        var snapshot = await scenario.Examinations.ReadAsync(original.TenantId, original.ProgramId, rules.Id);
        var prior = await scenario.Examinations.ReadBaselineAsync(original.TenantId, original.ProgramId, rules.Id, first);

        // Assert
        Assert.Equal(13, snapshot.Owner.Revision);
        Assert.Equal(12, snapshot.Baseline!.Revision);
        Assert.Equal(11, snapshot.Baseline.AmendmentDepth);
        Assert.Equal(first, prior!.TypeIBaselineId);
        Assert.Equal(1, prior.Sources[0].Revision);
        Assert.Equal(preview.ContentSha256, prior.ContentSha256);
        Assert.NotEqual(prior.TypeIBaselineId, snapshot.Baseline.TypeIBaselineId);
    }

    static ExaminationEngagementRules Rules()
    {
        var rules = new ExaminationEngagementRules(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.Null(rules.Create(new ExaminationEngagementContent("type_i", "Audit firm", new DateOnly(2026, 9, 30),
            null, ["security"], Uuid.CreateVersion4(), []), Uuid.CreateVersion4(), "Engagement lead", DateTimeOffset.UtcNow));
        return rules;
    }
    static ExaminationEngagementContent Terms(ExaminationEngagementView view) => new(view.EngagementType, view.AuditFirm,
        view.PeriodStart, view.PeriodEnd, view.TrustServicesCategories, view.BoundaryId, view.ManagementContactMemberIds);
    static ExaminationEngagementRevised Changed(ExaminationEngagementRevised original,
        Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef content)
    {
        var changed = original with { Change = content };
        changed.AttachMetadata(original.Metadata);
        return changed;
    }
    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
    }
}
