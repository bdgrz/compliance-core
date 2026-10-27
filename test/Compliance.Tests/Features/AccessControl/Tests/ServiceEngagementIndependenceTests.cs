using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ServiceEngagementIndependenceTests
{
    [Fact]
    public async Task ShouldRetainEveryRevisionGivenCreateAmendProposeWithdrawAndClose()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var next = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        var content = IndependenceHost.Draft(lead);

        // Act
        var created = await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0, content));
        var proposed = await host.SendAsync(new ProposeServiceEngagementStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, next.StaffMemberId, 1));
        var amended = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 2,
            content with { EngagementLeadStaffMemberId = next.StaffMemberId, Scope = "Revised scope" }));
        var withdrawn = await host.SendAsync(new WithdrawServiceEngagementStaffProposal(Uuid.CreateVersion4(),
            host.Tenant, engagement, lead.StaffMemberId, 3, "Lead changed"));
        var closed = await host.SendAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 4,
            "Canceled draft"));
        var history = await host.SendAsync(new GetServiceEngagementHistory(host.Tenant, engagement));
        var current = await host.SendAsync(new GetServiceEngagement(host.Tenant, engagement));

        // Assert
        foreach (var step in new[] { created, proposed, amended, withdrawn, closed })
            Assert.True(step.IsSuccess, step.Error?.Message);
        Assert.Equal([1L, 2, 3, 4, 5], history.Value!.Select(view => view.Revision));
        Assert.Equal("Synthetic scope", history.Value[0].Content.Scope);
        Assert.Equal("Revised scope", history.Value[^1].Content.Scope);
        Assert.Equal(["draft", "draft", "draft", "draft", "closed"], history.Value.Select(view => view.Status));
        Assert.Equal(IndependenceHost.Json(history.Value[^1]), IndependenceHost.Json(current.Value));
        Assert.All(history.Value, view => Assert.False(view.ProfessionalAccessGranted));
        Assert.All(current.Value!.Staff, proposal =>
        {
            Assert.Equal("withdrawn", proposal.ProposalState);
            Assert.False(proposal.IsCurrent);
        });
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Equal(5UL, owner.Revision);
        Assert.Equal("closed", owner.Status);
    }

    [Fact]
    public async Task ShouldAcceptMutationsBeyondFormerCapsGivenMoreThanOneHundredEngagementsAndRevisions()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)))).IsSuccess);

        // Act
        for (var revision = 1; revision <= 105; revision++)
        {
            var amended = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant,
                engagement, revision, IndependenceHost.Draft(lead, "Scope " + revision)));
            Assert.True(amended.IsSuccess, amended.Error?.Message);
        }

        for (var index = 0; index < 101; index++)
            Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 0,
                IndependenceHost.Draft(lead)))).IsSuccess);
        var history = await host.SendAsync(new GetServiceEngagementHistory(host.Tenant, engagement));
        var list = await host.SendAsync(new ListServiceEngagements(host.Tenant));

        // Assert
        Assert.Equal(106, history.Value!.Count);
        Assert.Equal(106, history.Value[^1].Revision);
        Assert.Equal(102, list.Value!.Count);
        Assert.Equal(list.Value.Select(view => view.EngagementId.ToString()).Order(StringComparer.Ordinal),
            list.Value.Select(view => view.EngagementId.ToString()));
    }

    [Fact]
    public async Task ShouldReturnOriginalViewAndAppendNothingGivenExactRetryAfterLaterChange()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        var create = new CreateServiceEngagement(host.Tenant, engagement, 0, IndependenceHost.Draft(lead));
        var amend = new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            IndependenceHost.Draft(lead, "Revised scope"));
        var firstCreate = await host.SendAsync(create);
        var firstAmend = await host.SendAsync(amend);
        Assert.True((await host.SendAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 2,
            "Canceled draft"))).IsSuccess);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        await FirmStaffFacts.SetStatusAsync(host.Provider, lead.StaffMemberId, false);
        var retriedCreate = await host.SendAsync(create);
        var retriedAmend = await host.SendAsync(amend);

        // Assert
        Assert.Equal(IndependenceHost.Json(firstCreate.Value), IndependenceHost.Json(retriedCreate.Value));
        Assert.Equal(IndependenceHost.Json(firstAmend.Value), IndependenceHost.Json(retriedAmend.Value));
        Assert.Equal(1, retriedCreate.Value!.Revision);
        Assert.Equal("Revised scope", retriedAmend.Value!.Content.Scope);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var change = (await host.EventsAsync<ServiceEngagementMutationRecorded>(stream))[1];
        Assert.Equal(200, (await FirstResponses.ReplayAsync(host.Content, host.Tenant.ToString(), change.FirstResponse!))
            .HttpStatus);
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenSameCommandIdentityWithChangedIntentActorOrRevision()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        var content = IndependenceHost.Draft(lead);
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0, content))).IsSuccess);
        var command = Uuid.CreateVersion4();
        var amend = new AmendServiceEngagement(command, host.Tenant, engagement, 1, content with { Scope = "Revised" });
        Assert.True((await host.SendAsync(amend)).IsSuccess);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var otherContent = await host.SendAsync(amend with { Content = content with { Scope = "Different" } });
        var otherActor = await host.SendAsync(amend, IndependenceHost.ActorFor(Uuid.CreateVersion4()));
        var otherRevision = await host.SendAsync(amend with { ExpectedRevision = 2 });
        var otherKind = await host.SendAsync(new CloseServiceEngagement(command, host.Tenant, engagement, 1, "Reason"));
        var createChanged = await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            content with { Scope = "Different create" }));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherContent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherKind.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, createChanged.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldCompareOwnRevisionOnlyGivenOtherEngagementsAndClientServicesChange()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)))).IsSuccess);

        // Act: unrelated owners move on; this engagement's revision is untouched.
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Draft(lead)))).IsSuccess);
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var amended = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            IndependenceHost.Draft(lead, "Revised")));
        var stale = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            IndependenceHost.Draft(lead, "Stale")));
        var created = await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 3,
            IndependenceHost.Draft(lead)));

        // Assert
        Assert.True(amended.IsSuccess, amended.Error?.Message);
        Assert.Equal(2, amended.Value!.Revision);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, created.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRefuseTransitionsGivenUnknownClosedOrWrongStateEngagements()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var other = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        var content = IndependenceHost.Draft(lead);
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0, content))).IsSuccess);

        // Act
        var unknown = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant,
            Uuid.CreateVersion4(), 1, content));
        var duplicateCreate = await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            content with { Scope = "Another draft" }));
        var attestLead = await host.RegisterStaffAsync("attest");
        var practiceRewrite = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant,
            engagement, 1, IndependenceHost.Draft(attestLead)));
        var withdrawLead = await host.SendAsync(new WithdrawServiceEngagementStaffProposal(Uuid.CreateVersion4(),
            host.Tenant, engagement, lead.StaffMemberId, 1, "Reason"));
        var withdrawUnknown = await host.SendAsync(new WithdrawServiceEngagementStaffProposal(Uuid.CreateVersion4(),
            host.Tenant, engagement, other.StaffMemberId, 1, "Reason"));
        Assert.True((await host.SendAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            "Canceled"))).IsSuccess);
        var amendClosed = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant,
            engagement, 2, content));
        var proposeClosed = await host.SendAsync(new ProposeServiceEngagementStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, other.StaffMemberId, 2));
        var closeClosed = await host.SendAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement,
            2, "Again"));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, duplicateCreate.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, practiceRewrite.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, withdrawLead.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, withdrawUnknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, amendClosed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, proposeClosed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, closeClosed.Error?.Kind);
        Assert.Equal(2UL, (await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement))).Revision);
    }

    [Theory]
    [InlineData(true, "advisory")]
    [InlineData(false, "attest")]
    public async Task ShouldRefuseTentativeProposalGivenInactiveOrMismatchedDirectoryPractice(bool inactive,
        string practice)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var staff = await host.RegisterStaffAsync(practice);
        if (inactive)
            await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, false);
        var engagement = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)))).IsSuccess);

        // Act
        var result = await host.SendAsync(new ProposeServiceEngagementStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, staff.StaffMemberId, 1));

        // Assert
        Assert.Equal(inactive ? RequestErrorKind.NotFound : RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1UL, (await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement))).Revision);
    }

    [Fact]
    public async Task ShouldRejectCreateGivenUnknownLeadInvalidContentOrNegativeRevision()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var content = IndependenceHost.Draft(lead);

        // Act
        var unknownLead = await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 0,
            content with { EngagementLeadStaffMemberId = Uuid.CreateVersion4() }));
        var invalid = await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 0,
            content with { Scope = " " }));
        var nullContent = await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 0, null!));
        var negative = await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), -1, content));
        var empty = await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.Empty, 0, content));
        var practice = await host.SendAsync(new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 0,
            content with { Practice = "attest" }));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, unknownLead.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, nullContent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, negative.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, empty.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, practice.Error?.Kind);
    }

    [Fact]
    public async Task ShouldKeepOneCreationGivenConcurrentEqualCreateRequests()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var request = new CreateServiceEngagement(host.Tenant, Uuid.CreateVersion4(), 0, IndependenceHost.Draft(lead));

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var scope = host.Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request, host.Actor);
        }));

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Single(results.Select(result => IndependenceHost.Json(result.Value)).Distinct());
        Assert.Single(await host.EventsAsync<ServiceEngagementMutationRecorded>(
            new ServiceEngagementIndependence(host.Tenant, request.EngagementId).Stream));
    }

    [Fact]
    public async Task ShouldRefuseRevocationWithoutAppendGivenUnknownOrNotAcceptedEngagement()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)))).IsSuccess);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var unknown = await host.SendAsync(new RevokeServiceEngagementActualStaff(Uuid.CreateVersion4(), host.Tenant,
            Uuid.CreateVersion4(), lead.StaffMemberId, 0, "Reason"));
        var draft = await host.SendAsync(new RevokeServiceEngagementActualStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, lead.StaffMemberId, 1, "Reason"));
        var stale = await host.SendAsync(new RevokeServiceEngagementActualStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, lead.StaffMemberId, 0, "Reason"));
        var invalid = await host.SendAsync(new RevokeServiceEngagementActualStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, Uuid.Empty, 1, "Reason"));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, draft.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public void ShouldCloseWholeAcceptanceAndKeepRemovedAssignmentsGivenLeadOrAllRevocation()
    {
        // Arrange
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic client administrator");
        var lead = Uuid.CreateVersion4();
        var other = Uuid.CreateVersion4();
        var acceptance = Acceptance(lead, other);

        // Act
        var removed = ServiceEngagementRules.Revoke(acceptance, lead, other, "Removed", actor, IndependenceHost.At);
        var leadRemoved = ServiceEngagementRules.Revoke(acceptance, lead, lead, "Lead removed", actor, IndependenceHost.At);
        var closed = ServiceEngagementRules.Revoke(acceptance, lead, null, "Closed", actor, IndependenceHost.At);

        // Assert
        Assert.Equal(2, removed.Revision);
        Assert.Equal("active", removed.Status);
        Assert.Equal([true, false], removed.Assignments.Select(item => item.IsCurrent));
        Assert.Equal("closed", leadRemoved.Status);
        Assert.All(leadRemoved.Assignments, item => Assert.False(item.IsCurrent));
        Assert.Equal("closed", closed.Status);
        Assert.Equal("Closed", closed.ChangeReason);
        Assert.Equal(2, closed.Assignments.Count);
        Assert.Equal(actor, closed.ChangedBy);
    }

    [Fact]
    public void ShouldRefuseForeignOutOfOrderOrReopenedReplayGivenCorruptedEngagementFacts()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var engagement = Uuid.CreateVersion4();
        var owner = new ServiceEngagementIndependence(tenant, engagement);
        var reference = new ImmutableContentRef(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json",
            IndependenceContent.EngagementSchema);
        OperationProvenance Provenance(Uuid id, string kind) => new(id, id, null, kind,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic"), owner.Stream, "initial", 0,
            new string('a', 64), IndependenceHost.At, owner.Stream, owner.Stream);
        ServiceEngagementMutationRecorded Fact(Uuid id, string kind, ulong revision) => new(tenant, id, kind, reference,
            Provenance(id, kind), revision, reference, reference);
        var create = Fact(engagement, ServiceEngagementIndependence.CreateKind, 1);
        var close = Fact(Uuid.CreateVersion4(), ServiceEngagementIndependence.CloseKind, 2);
        AggregateScenario<ServiceEngagementIndependence> Fresh() => new(new ServiceEngagementIndependence(tenant, engagement));

        // Act
        var foreign = () => Fresh().Given(create with { TenantId = Uuid.CreateVersion4() });
        var amendFirst = () => Fresh().Given(Fact(Uuid.CreateVersion4(), ServiceEngagementIndependence.AmendKind, 1));
        var wrongIdentity = () => Fresh().Given(Fact(Uuid.CreateVersion4(), ServiceEngagementIndependence.CreateKind, 1));
        var skipped = () => Fresh().Given(create, Fact(Uuid.CreateVersion4(), ServiceEngagementIndependence.AmendKind, 3));
        var reopened = () => Fresh().Given(create, close,
            Fact(Uuid.CreateVersion4(), ServiceEngagementIndependence.AmendKind, 3));
        var secondCreate = () => Fresh().Given(create, Fact(engagement, ServiceEngagementIndependence.CreateKind, 2));
        var tagMismatch = () => Fresh().Given(create with { Intent = ServiceEngagementIndependence.CloseKind });

        // Assert
        foreach (var replay in new[] { foreign, amendFirst, wrongIdentity, skipped, reopened, secondCreate, tagMismatch })
            Assert.Throws<InvalidOperationException>(replay);
    }

    static ServiceEngagementAcceptanceView Acceptance(Uuid lead, Uuid other)
    {
        var actor = ActorReference.ForFirmStaff(Uuid.CreateVersion4(), "Synthetic partner");
        var rules = new IndependenceRuleVersionView(1, IndependenceHost.DefaultRules, actor, IndependenceHost.At, true);
        EngagementActualAssignmentView Assignment(Uuid staff) => new(staff, Uuid.CreateVersion4(), "attest", 1,
            IndependenceHost.At, true, actor, IndependenceHost.At);
        return new ServiceEngagementAcceptanceView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1, "Synthetic authority", null,
            Uuid.CreateVersion4(), null, null, null, null, new EngagementAcceptanceSourceTimes(IndependenceHost.At,
                IndependenceHost.At, null, null), rules, [], "allowed", "not_applicable", [],
            [Assignment(lead), Assignment(other)], "active", actor, IndependenceHost.At);
    }
}
