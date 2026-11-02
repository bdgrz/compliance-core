using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using System.Collections;
using System.Reflection;
using System.Text.Json;
namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

/// <summary>
///     The native applicability line aggregate itself: tuple identity and the exact append cut each prepared fact is bound to.
/// </summary>
public sealed class CriterionApplicabilityAggregateTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();

    static CriterionApplicability NewOwner(string criterion = "CC6.2") =>
        new(TenantId, ProgramId, EditionId, criterion);

    static CriterionApplicabilityRules ProposedAndAccepted()
    {
        var rules = new CriterionApplicabilityRules(TenantId, ProgramId, EditionId, "CC6.2");
        var decisionId = Uuid.CreateVersion4();
        var proposer = ActorReference.ForMember(ProposerId, "Member");
        var reviewer = ActorReference.ForMember(ReviewerId, "Member");
        Assert.True(rules.Propose(MappingRuleFacts.Root(rules, decisionId, nameof(ProposeCriterionNotApplicable), 0, ProposerId),
            "No accounts are approved.", ProposerId, proposer, NativeFacts.Now).IsSuccess);
        Assert.True(rules.Review(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(ReviewCriterionApplicability), 1, ReviewerId),
            decisionId, "accept", "Supported.", ReviewerId, reviewer, NativeFacts.Now.AddMinutes(1), null).IsSuccess);
        return rules;
    }

    static async Task<(MappingLinePrepared Prepared, ImmutableContentRef First)> PrepareAsync(
        CriterionApplicability owner, CriterionApplicabilityRules replayed, object original, IImmutableContentStore content)
    {
        var rebound = MappingNativeSeeds.Rebind(original);
        replayed.Apply(rebound.Change);
        var (response, result) = rebound.Change is CriterionNotApplicableProposedChange proposed
            ? await NativeContent.FreezeAsync(content, TenantId, CriterionApplicability.RegistrationSchema,
                new CriterionApplicabilityRegistration(proposed.DecisionId, proposed.Revision, proposed.VersionNumber))
            : await NativeContent.FreezeUnitAsync(content, TenantId);
        var prepared = await MappingLineContent.PrepareJsonAsync(content, owner, rebound.Authored, rebound.Change,
            MappingLineContent.Current(replayed), MappingLineContent.Header(replayed), result, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, TenantId.ToString(), response, CancellationToken.None);
        return (prepared, first);
    }

    [Fact]
    public void ShouldDeriveStableIdentityFromTheTupleGivenSameAndDifferentTuples()
    {
        // Arrange
        var same = NewOwner();
        var again = NewOwner();

        // Act
        var otherCriterion = NewOwner("CC6.1");
        var otherEdition = new CriterionApplicability(TenantId, ProgramId, Uuid.CreateVersion4(), "CC6.2");
        var otherProgram = new CriterionApplicability(TenantId, Uuid.CreateVersion4(), EditionId, "CC6.2");

        // Assert
        Assert.Equal(same.Id, again.Id);
        Assert.Equal(CriterionApplicability.IdentityOf(TenantId, ProgramId, EditionId, "CC6.2"), same.Id);
        Assert.Equal(TenantId.ToString(), same.Stream.Realm);
        Assert.Equal(4, new[] { same.Id, otherCriterion.Id, otherEdition.Id, otherProgram.Id }.Distinct().Count());
    }

    [Fact]
    public async Task ShouldRefuseSecondApplyAndKeepRevisionGivenPreparedFactAlreadyAppended()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var history = ProposedAndAccepted();
        var owner = NewOwner();
        var (prepared, first) = await PrepareAsync(owner, new CriterionApplicabilityRules(TenantId, ProgramId, EditionId,
            "CC6.2"), history.Changes[0], content);
        Assert.True(owner.Apply(prepared, first).IsSuccess);

        // Act
        var again = owner.Apply(prepared, first);

        // Assert
        Assert.False(again.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, again.Error!.Kind);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public async Task ShouldRefuseAppendAndKeepRevisionGivenFactPreparedAgainstALaterLineRevision()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var history = ProposedAndAccepted();
        var ahead = NewOwner();
        var aheadRules = new CriterionApplicabilityRules(TenantId, ProgramId, EditionId, "CC6.2");
        var proposal = await PrepareAsync(ahead, aheadRules, history.Changes[0], content);
        Assert.True(ahead.Apply(proposal.Prepared, proposal.First).IsSuccess);
        var review = await PrepareAsync(ahead, aheadRules, history.Changes[1], content);
        var behind = NewOwner();

        // Act
        var stale = behind.Apply(review.Prepared, review.First);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(0, behind.Revision);
        Assert.Null(behind.CurrentContent);
        Assert.True(ahead.Apply(review.Prepared, review.First).IsSuccess);
        Assert.Equal(2, ahead.Revision);
    }
}
