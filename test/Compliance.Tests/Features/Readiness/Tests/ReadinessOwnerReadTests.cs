using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Handlers;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessOwnerReadTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("readiness-rules/10")]
    [InlineData("readiness-rules/11")]
    public async Task ShouldPreserveOldRulesAndDeclaredSupportGapsGivenOwnerReplayAndFilteredReads(string historicalRule)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var readiness = new ReadinessReader(reader, content, scope.ServiceProvider.GetRequiredService<IDomainEventReader>());
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var runner = Uuid.CreateVersion4();
        var oldId = Uuid.CreateVersion4();
        var currentId = Uuid.CreateVersion4();
        var oldGap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risks", "risk_reviewed",
            "Original historical explanation.", [new("control_version", Uuid.CreateVersion4(), "1")]);
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, program, oldId,
            new ReadinessEvaluation([], [], [oldGap], historicalRule + "-recorded-fingerprint"), Now, runner, Now,
            historicalRule);
        var catalog = CriteriaCatalog.Platform;
        var sources = ReadinessSourceSet.Empty with
        {
            Boundaries = [ReadinessCatalogSupportGapTests.Boundary(tenant, program,
                ["security", "availability", "processing_integrity", "confidentiality", "privacy"], Now)],
        };
        var scopeSnapshot = new ReadinessAccessReviewScopeView(Uuid.CreateVersion4(), "excluded", false, null);
        var evaluation = ReadinessRules.Evaluate(program, Now, catalog.Edition.EditionId, [], [],
            new Dictionary<Uuid, ControlVersionView?>(), sources, catalog.Edition) with
        {
            AccessReviewScopes = [scopeSnapshot],
        };
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, program, currentId, evaluation, Now,
            runner, Now.AddMinutes(1));
        var privacy = Assert.Single(evaluation.Gaps, gap => gap.Subject == "privacy|privacy_lifecycle_unsupported");
        await ReadinessSeed.PlanAsync(reader, executor, content, tenant, program, privacy.GapId, runner,
            new DateOnly(2027, 3, 31), "Track the declared limitation.", runner, Now.AddMinutes(2));
        await ReadinessSeed.AnnotateAsync(reader, executor, content, tenant, program, currentId, privacy.GapId,
            "Acknowledged limitation.", runner, Now.AddMinutes(3));

        // Act
        var old = await readiness.ReadViewAsync(tenant, program, oldId);
        var current = await readiness.ReadViewAsync(tenant, program, currentId);
        var replayed = await new ReadinessReader(reader, content, scope.ServiceProvider.GetRequiredService<IDomainEventReader>()).ReadViewAsync(tenant, program, currentId);
        var filtered = await new ListReadinessGapsHandler(readiness).HandleAsync(
            new RequestContext<ListReadinessGaps>(new ListReadinessGaps(tenant, program, currentId, "planned",
                OwnerMemberId: runner, Kind: "catalog_support_gap", RuleId: "catalog_support_declared",
                Subject: privacy.Subject), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.NotNull(old);
        Assert.Equal(historicalRule, old.RuleVersion);
        Assert.Equal(historicalRule + "-recorded-fingerprint", old.InputFingerprint);
        Assert.Equal(oldGap.Explanation, Assert.Single(old.Gaps).Explanation);
        Assert.Equal(oldGap.Sources, old.Gaps[0].Sources);
        Assert.NotNull(current);
        Assert.NotNull(replayed);
        Assert.Equal(ReadinessRules.Version, current.RuleVersion);
        Assert.Equal(evaluation.InputFingerprint, current.InputFingerprint);
        Assert.Equal(scopeSnapshot, Assert.Single(current.AccessReviewScopes));
        Assert.Equal(6, current.Gaps.Count(gap => gap.Kind == "catalog_support_gap"));
        Assert.True(filtered.IsSuccess, filtered.Error?.Message);
        Assert.Equal(privacy.GapId, Assert.Single(filtered.Value.Items).GapId);
        Assert.Equal(runner, filtered.Value.Items[0].Plan!.OwnerMemberId);
        Assert.Equal(current.InputFingerprint, replayed.InputFingerprint);
        Assert.Equal(current.Gaps.Select(gap => gap.GapId), replayed.Gaps.Select(gap => gap.GapId));
        Assert.Null(await readiness.ReadViewAsync(Uuid.CreateVersion4(), program, currentId));
        Assert.Null(await readiness.ReadViewAsync(tenant, Uuid.CreateVersion4(), currentId));
    }

    [Fact]
    public async Task ShouldReadCurrentPlansAndHistoricalDecisionsGivenIndependentOwnerStreams()
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var readiness = new ReadinessReader(reader, content, scope.ServiceProvider.GetRequiredService<IDomainEventReader>());
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var runner = Uuid.CreateVersion4();
        var owner = Uuid.CreateVersion4();
        var gap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risks", "risk_reviewed",
            "No risk review is available.", []);
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, program, first,
            new ReadinessEvaluation([], [], [gap], new string('a', 64)), Now, runner, Now);
        await ReadinessSeed.PlanAsync(reader, executor, content, tenant, program, gap.GapId, owner,
            new DateOnly(2027, 3, 31), "Complete the risk review.", runner, Now.AddMinutes(1));
        var decision = await ReadinessSeed.DecideAsync(reader, executor, content, tenant, program, first,
            ReadinessDecisionKind.Readiness, ProgramReadiness.Proceed, "The gap is owned.", owner, Now.AddMinutes(2));
        var entry = await ReadinessSeed.DecideAsync(reader, executor, content, tenant, program, first,
            ReadinessDecisionKind.TypeIEntry, ProgramReadiness.Defer, "Review first.", owner, Now.AddMinutes(3));
        await ReadinessSeed.AnnotateAsync(reader, executor, content, tenant, program, first, gap.GapId,
            "Please attach supporting notes.", owner, Now.AddMinutes(4));
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, program, second,
            new ReadinessEvaluation([], [], [], new string('b', 64)), Now.AddMinutes(5), runner, Now.AddMinutes(5));

        // Act
        var current = await readiness.ReadViewAsync(tenant, program, first);
        var programOwner = await readiness.ReadProgramAsync(tenant, program);
        var summaries = await readiness.ReadSummariesAsync(tenant, programOwner);
        var annotations = await readiness.ReadAnnotationsAsync(tenant, programOwner, first);
        var entries = await readiness.ReadEntryDecisionsAsync(tenant, programOwner);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(programOwner.Revision, current.Revision);
        Assert.Equal(owner, Assert.Single(current.Gaps).Plan!.OwnerMemberId);
        Assert.Equal(decision, current.Decision!.DecisionId);
        Assert.Equal(entry, current.TypeIEntryDecision!.DecisionId);
        Assert.Equal(second, summaries[0].AssessmentId);
        Assert.Equal(first, summaries[1].AssessmentId);
        Assert.Equal(ProgramReadiness.Proceed, summaries[1].DecisionOutcome);
        Assert.False(Assert.Single(annotations!).Current);
        Assert.Equal(entry, Assert.Single(entries).DecisionId);
        Assert.Null(await readiness.ReadViewAsync(Uuid.CreateVersion4(), program, first));
        Assert.Null(await readiness.ReadViewAsync(tenant, Uuid.CreateVersion4(), first));
    }
}
