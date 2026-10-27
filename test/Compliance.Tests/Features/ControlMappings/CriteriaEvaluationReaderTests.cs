using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

/// <summary>
///     The owner-stream readers discover lines from committed facts only, scope them to one program, expose a fence
///     that changes with any write, and fail closed when an operation id names two owners.
/// </summary>
public sealed class CriteriaEvaluationReaderTests
{
    static readonly DateTimeOffset At = NativeFacts.Now;

    static ServiceProvider Provider() => ProgramManagementServices.Build(new RecordingPermissionAuthorizer(allowed: true),
        static _ => { });

    [Fact]
    public async Task ShouldScopeMappingLinesToTheProgramAndMoveTheFenceGivenEveryWrite()
    {
        // Arrange
        await using var provider = Provider();
        var (tenant, program, other) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var (control, version, edition, member) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4());
        var first = await MappingSeeds.ProposeMappingAsync(provider, tenant, program, control, version, edition,
            "CC6.1", 0, "Restricts access.", "All systems.", member, "Author", At);
        await MappingSeeds.ProposeMappingAsync(provider, tenant, other, control, version, edition, "CC6.1", 0,
            "Another program.", "All systems.", member, "Author", At);
        await using var scope = provider.CreateAsyncScope();
        var mappings = scope.ServiceProvider.GetRequiredService<ControlMappingReader>();

        // Act
        var before = await mappings.ReadProgramAsync(tenant, program);
        var again = await mappings.ReadProgramAsync(tenant, program);
        await MappingSeeds.ReviewMappingAsync(provider, tenant, program, control, edition, "CC6.1", first, 1, "accept",
            "Fits.", Uuid.CreateVersion4(), "Reviewer", At.AddMinutes(1));
        var after = await mappings.ReadProgramAsync(tenant, program);

        // Assert
        Assert.Equal(before.Fence, again.Fence);
        Assert.NotEqual(before.Fence, after.Fence);
        Assert.Equal(1, before.Fence.Facts);
        Assert.Equal(2, after.Fence.Facts);
        var view = Assert.Single(after.Mappings);
        Assert.Equal(first, view.MappingId);
        Assert.Equal("active", view.Status);
        Assert.Equal(program, view.ProgramId);
    }

    [Fact]
    public async Task ShouldFailClosedGivenOneMappingProposalIdOnTwoTuples()
    {
        // Arrange
        await using var provider = Provider();
        var (tenant, program, control, version, edition, member) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var shared = Uuid.CreateVersion4();
        await MappingSeeds.ProposeMappingAsync(provider, tenant, program, control, version, edition, "CC6.1", 0,
            "One.", "All systems.", member, "Author", At, mappingId: shared);
        await MappingSeeds.ProposeMappingAsync(provider, tenant, program, control, version, edition, "CC6.2", 0,
            "Two.", "All systems.", member, "Author", At, mappingId: shared);
        await using var scope = provider.CreateAsyncScope();
        var mappings = scope.ServiceProvider.GetRequiredService<ControlMappingReader>();

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await mappings.LocateAsync(tenant, program, shared));
        var unknown = await mappings.LocateAsync(tenant, program, Uuid.CreateVersion4());
        var otherProgram = await mappings.LocateAsync(tenant, Uuid.CreateVersion4(), shared);

        // Assert
        Assert.Contains("more than one", failure.Message, StringComparison.Ordinal);
        Assert.Null(unknown);
        Assert.Null(otherProgram);
    }

    [Fact]
    public async Task ShouldScopeApplicabilityLinesAndFailClosedGivenOneDecisionIdOnTwoCriteria()
    {
        // Arrange
        await using var provider = Provider();
        var (tenant, program, other, edition, member) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var shared = Uuid.CreateVersion4();
        await MappingSeeds.ProposeNotApplicableAsync(provider, tenant, program, edition, "CC6.1", 0, "One.", member,
            "Author", At, shared);
        await MappingSeeds.ProposeNotApplicableAsync(provider, tenant, program, edition, "CC6.2", 0, "Two.", member,
            "Author", At, shared);
        await MappingSeeds.ProposeNotApplicableAsync(provider, tenant, other, edition, "CC6.1", 0, "Elsewhere.", member,
            "Author", At);
        await using var scope = provider.CreateAsyncScope();
        var applicability = scope.ServiceProvider.GetRequiredService<ApplicabilityReader>();

        // Act
        var snapshot = await applicability.ReadProgramAsync(tenant, program);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await applicability.LocateAsync(tenant, program, shared));

        // Assert
        Assert.Equal(2, snapshot.Decisions.Count);
        Assert.Equal(2, snapshot.Fence.Facts);
        Assert.Contains("more than one", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldComposeEvaluationsPerProgramFromTheirOwnStreamsGivenManyEvaluations()
    {
        // Arrange
        await using var provider = Provider();
        var (tenant, program, other, control, version, plan, member) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4());
        var steps = new[]
        {
            new EvaluationProcedureStep("design", "inspection", [new EvaluationInspectedItem("record", "p", "v1")],
                "Defined."),
        };
        var ids = Enumerable.Range(0, 25).Select(static _ => Uuid.CreateVersion4()).ToArray();
        foreach (var id in ids)
            await EvaluationSeeds.StartAsync(provider, tenant, program, control, id, version, plan, 1, steps, member,
                "Owner", At);
        await EvaluationSeeds.StartAsync(provider, tenant, other, control, Uuid.CreateVersion4(), version, plan, 1,
            steps, member, "Owner", At);
        await using var scope = provider.CreateAsyncScope();
        var evaluations = scope.ServiceProvider.GetRequiredService<ControlEvaluationReader>();

        // Act
        var snapshot = await evaluations.ReadProgramAsync(tenant, program);
        var listed = snapshot.ReadControl(control);

        // Assert
        Assert.Equal(26, snapshot.Fence.Facts);
        Assert.Equal(25, snapshot.Evaluations.Count);
        Assert.Equal(ids.Order(), listed.Select(static view => view.EvaluationId).Order());
        Assert.All(listed, view => Assert.Equal(1, view.Revision));
        Assert.NotNull(snapshot.Read(control, ids[7]));
        Assert.Null(snapshot.Read(Uuid.CreateVersion4(), ids[7]));
        Assert.Empty(snapshot.AwaitingReview());
    }
}
