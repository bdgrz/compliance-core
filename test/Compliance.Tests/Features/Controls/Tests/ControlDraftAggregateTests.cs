using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Controls.Tests;

/// <summary>Direct ControlDraft owner tests for preparation admission against replayed native history.</summary>
public sealed class ControlDraftAggregateTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();

    [Fact]
    public void ShouldAdmitCreationGivenEmptyOwner()
    {
        // Arrange
        var id = ControlDraft.IdFor(TenantId, ProgramId, "AGG-NEW");
        var owner = new ControlDraft(TenantId, id);

        // Act
        var result = owner.Prepare(ProgramId, null, creating: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(owner.IsCreated);
        Assert.Equal(0, owner.Revision);
        Assert.Equal(id, owner.Id);
    }

    [Fact]
    public void ShouldReportNotFoundGivenEmptyOwnerWhenPreparingMutation()
    {
        // Arrange
        var id = ControlDraft.IdFor(TenantId, ProgramId, "AGG-EMPTY");
        var owner = new ControlDraft(TenantId, id);

        // Act
        var result = owner.Prepare(ProgramId, 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldConflictGivenExistingControlWhenPreparingCreation()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var id = await CreateAsync(source, "AGG-DUP");
        var owner = await source.Reader.HydrateAsync(new ControlDraft(TenantId, id));

        // Act
        var result = owner.Prepare(ProgramId, null, creating: true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, owner.NativeSequence);
    }

    [Fact]
    public async Task ShouldReportNotFoundGivenOtherProgramWhenPreparingMutation()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var id = await CreateAsync(source, "AGG-PROGRAM");
        var owner = await source.Reader.HydrateAsync(new ControlDraft(TenantId, id));

        // Act
        var result = owner.Prepare(Uuid.CreateVersion4(), 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal(ProgramId, owner.ProgramId);
    }

    [Fact]
    public async Task ShouldConflictGivenStaleExpectedRevisionAfterContentRevision()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var id = await CreateAsync(source, "AGG-STALE");
        await ReviseAsync(source, id);
        var owner = await source.Reader.HydrateAsync(new ControlDraft(TenantId, id));

        // Act
        var result = owner.Prepare(ProgramId, 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Contains("Current revision: 2", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public async Task ShouldAdmitMutationGivenCurrentOrOmittedExpectedRevision()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var id = await CreateAsync(source, "AGG-CURRENT");
        await ReviseAsync(source, id);
        var owner = await source.Reader.HydrateAsync(new ControlDraft(TenantId, id));

        // Act
        var current = owner.Prepare(ProgramId, 2);
        var omitted = owner.Prepare(ProgramId, null);

        // Assert
        Assert.True(current.IsSuccess);
        Assert.True(omitted.IsSuccess);
        Assert.True(owner.IsVisible);
        Assert.True(owner.HasOpenDraft);
        Assert.Equal(2, owner.NativeSequence);
    }

    [Fact]
    public async Task ShouldHideControlGivenReplayedDiscardWhenPreparingMutation()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var id = await CreateAsync(source, "AGG-DISCARD");
        var rules = await source.Controls.RulesAsync(TenantId, ProgramId, id);
        Assert.Null(rules.Discard(ProgramId, 1, "Not needed.", AuthorId, "Author", source.Clock.Now));
        await source.SeedAsync(rules);
        var owner = await source.Reader.HydrateAsync(new ControlDraft(TenantId, id));

        // Act
        var mutation = owner.Prepare(ProgramId, 1);
        var recreation = owner.Prepare(ProgramId, null, creating: true);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, mutation.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, recreation.Error!.Kind);
        Assert.True(owner.IsCreated);
        Assert.False(owner.IsVisible);
        Assert.False(owner.HasOpenDraft);
    }

    static async Task<Uuid> CreateAsync(ControlSourceFixture source, string identifier)
    {
        var id = ControlDraft.IdFor(TenantId, ProgramId, identifier);
        var rules = new ControlRules(TenantId, id);
        Assert.True(rules.Create(ProgramId, id, identifier, Content("Original"), AuthorId, "Author",
            source.Clock.Now).IsSuccess);
        await source.SeedAsync(rules);
        return id;
    }

    static async Task ReviseAsync(ControlSourceFixture source, Uuid id)
    {
        var rules = await source.Controls.RulesAsync(TenantId, ProgramId, id);
        Assert.Null(rules.Revise(ProgramId, 1, Content("Revised"), AuthorId, "Author",
            source.Clock.Now.AddMinutes(1)));
        await source.SeedAsync(rules);
    }

    static ControlDraftContent Content(string title) => new(title, "Review access", "Management reviews access",
        "The security lead reviews access quarterly", ["Dated review record"]);
}
