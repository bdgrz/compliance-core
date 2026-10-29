using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class AuditorResponseNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBoundedCommandAuthorityGivenManyValidDraftVersions()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        for (var index = 0; index < 128; index++)
        {
            var owner = await fixture.ResponseAsync(requestId, responseId);
            var revised = await fixture.Handler<ReviseAuditResponseDraftHandler>().HandleAsync(fixture.Context(
                new ReviseAuditResponseDraft(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, requestId,
                    responseId, owner.Revision, $"Complete version {index + 2}.", [])), CancellationToken.None);
            Assert.True(revised.IsSuccess, revised.Error?.Message);
        }

        // Act
        var saved = await fixture.ResponseAsync(requestId, responseId);
        var retained = typeof(AuditorResponse).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(saved) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(129, saved.BusinessVersion);
        Assert.Equal(129, saved.Revision);
        Assert.Equal("draft", saved.Status);
        Assert.NotNull(saved.Created);
        Assert.False(saved.Uses.HasActiveUse);
    }
    [Fact]
    public async Task ShouldKeepOriginalFenceAndSelectedGrantGivenManyResolvedParentGenerations()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        Assert.True((await fixture.DraftAsync(requestId, responseId)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(responseId)).State);
        Assert.True((await fixture.SubmitAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.ReviewAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.DeliverAsync(requestId, responseId)).IsSuccess);
        Assert.True((await fixture.AcceptAsync(requestId, responseId)).IsSuccess);
        var original = await fixture.ResponseAsync(requestId, responseId);
        var parent = new AuditorRequest(fixture.TenantId, fixture.ProgramId, requestId).Stream;
        var facts = new List<DomainEvent>();
        ReservationKey? selected = null;
        for (var index = 0; index < 64; index++)
        {
            var root = Uuid.CreateVersion4();
            var generation = index + 1L;
            var operation = new OperationProvenance(Uuid.CreateVersion4(), root, null, "Response resolution use",
                ActorReference.ForMember(fixture.MemberId, "Author"), original.Stream, "initial", null,
                new string('a', 64), AuditorRequestFixture.Now, original.Stream, original.Stream, parent);
            var intent = NativeFacts.Ref(SourceUseAuthorizationIntent.Schema);
            facts.Add(new AuditorResponseRequestResolutionUseAuthorized(original.Stream, original.Revision,
                generation, intent, null, operation, null, null));
            facts.Add(new AuditorResponseRequestResolutionUseSettled(original.Stream, original.Revision,
                generation, intent, null, operation with { LogicalId = Uuid.CreateVersion4() }, null, null,
                SourceUseSettlementDisposition.ParentClosed));
            selected ??= new ReservationKey(root, parent, (ulong)generation, original.Stream,
                SourceUseReservations.PurposeName(SourceUsePurpose.RequestResolution));
        }
        var attached = facts.Select((fact, index) => DomainEventSeed.Attach(fact, original.Id,
            original.Version + (ulong)index + 1)).ToArray();
        await fixture.Events.AppendAsync(original.Stream, original.CommittedStreamPosition, attached, CancellationToken.None);

        // Act
        var saved = await fixture.Reader.HydrateAsync(new AuditorResponse(fixture.TenantId, fixture.ProgramId,
            requestId, responseId).ForSourceUses(selected!));
        var retained = typeof(AuditorResponse).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(saved) is ICollection { Count: > 1 }).ToArray();
        var fenced = saved.PrepareRevise(saved.Revision);

        // Assert
        Assert.Empty(retained);
        Assert.Equal(original.Revision, saved.Revision);
        Assert.Equal(original.Disposition!.Metadata.EventId, saved.Disposition!.Metadata.EventId);
        Assert.False(saved.Uses.HasActiveUse);
        Assert.Equal(64UL, saved.Uses.Generation);
        Assert.True(saved.Uses.Find(selected!)!.IsSettled);
        Assert.Equal(NativeFactReference.Of(original.Stream, attached[0]), saved.Uses.GrantOf(selected!)!.GrantEvent);
        Assert.Equal(RequestErrorKind.Conflict, fenced.Error?.Kind);
        Assert.Contains("fenced by a resolved request generation", fenced.Error!.Message, StringComparison.Ordinal);
    }

}
