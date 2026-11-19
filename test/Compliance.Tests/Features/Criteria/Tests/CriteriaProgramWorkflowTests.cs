using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Handlers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

/// <summary>
/// Exercises the individual domain owners and program handler together. Participant
/// dispatch and source qualification are represented by explicitly supplied test evidence.
/// </summary>
public sealed class CriteriaProgramWorkflowTests
{
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new(UserId.ToString(), "bdgrz");
    static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExposeAuthoringRequestsGivenHttpAndMcpRegistration()
    {
        // Arrange
        await using var app = WebApplication.CreateSlimBuilder().Build();
        var services = new ServiceCollection();
        var portia = services.AddPortia();
        Type[] requests = [typeof(DraftCriteriaEdition), typeof(ProposeCriterionChange), typeof(BeginCriteriaPublication)];

        // Act
        app.MapCriteriaAuthoringEndpoints();
        portia.AddCriteriaAuthoringMcpTools();
        using var provider = services.BuildServiceProvider();
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Select(endpoint => endpoint.Metadata.GetMetadata<PortiaOpenApiOperation>()).OfType<PortiaOpenApiOperation>().ToArray();
        var tools = provider.GetServices<McpToolRegistration>().ToArray();

        // Assert
        foreach (var request in requests)
        {
            Assert.Single(routes, operation => operation.RequestType == request);
            Assert.Single(tools, tool => tool.RequestType == request);
        }
    }

    [Fact]
    public async Task ShouldCreateProgramWithPublishedEditionGivenIndependentOwnerDecisions()
    {
        // Arrange: the client supplies the edition, operation, attempt, and program IDs.
        var editionId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion4();
        var entryOperationId = Uuid.CreateVersion4();
        var publicationAttemptId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        const string label = "edition_1";
        const string identifier = "CC6.1";
        var draft = new DraftCriteriaEdition(editionId, draftId, "tsc", label);
        var edition = new CriteriaEdition(editionId, draftId);
        var draftAdmission = CriteriaAdmission(edition.Stream, draftId, draft);
        Assert.True(edition.Draft(draftId, "tsc", label, Actor, draftAdmission).IsSuccess);
        var editionScenario = new AggregateScenario<CriteriaEdition>(edition);
        var requested = Assert.IsType<CriteriaEditionDraftRequested>(Assert.Single(editionScenario.PendingEvents));

        var labelOwner = new EditionLabelClaim(label, editionId, draftId);
        Assert.True(labelOwner.Reserve(editionId, draftId, 1, requested.Metadata.EventId, Actor).IsSuccess);
        var labelScenario = new AggregateScenario<EditionLabelClaim>(labelOwner);
        var reserved = Assert.IsType<EditionLabelReserved>(Assert.Single(labelScenario.PendingEvents));
        Assert.True(edition.ActivateDraft(draftId, 1, reserved.Metadata.EventId).IsSuccess);
        var activated = Assert.IsType<CriteriaEditionDraftActivated>(editionScenario.PendingEvents[^1]);
        Assert.True(labelOwner.Confirm(editionId, draftId, 1, activated.Metadata.EventId).IsSuccess);
        var confirmed = Assert.IsType<EditionLabelConfirmed>(labelScenario.PendingEvents[^1]);
        Assert.True(edition.CompleteDraft(draftId, 1, confirmed.Metadata.EventId).IsSuccess);
        var drafted = Assert.IsType<CriteriaEditionDrafted>(editionScenario.PendingEvents[^1]);

        var intent = new CriterionChangeIntent(editionId, draftId, identifier, entryOperationId,
            0, "create", "criterion", "security", null, null, "Original product summary.");
        var entry = new CriteriaEntry(editionId, identifier, entryOperationId);
        var entryAdmission = CriteriaAdmission(entry.Stream, entryOperationId, intent);
        var operation = new CriteriaEntryOperation(editionId, identifier, entryOperationId);
        Assert.True(operation.Claim(intent, 1, Actor, entryAdmission).IsSuccess);
        var claim = Assert.IsType<CriterionEntryOperationClaimed>(
            Assert.Single(new AggregateScenario<CriteriaEntryOperation>(operation).PendingEvents));
        Assert.True(entry.Propose(intent, 1, Actor, entryAdmission, null, claim).IsSuccess);
        var entryScenario = new AggregateScenario<CriteriaEntry>(entry);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(entryScenario.PendingEvents));
        var decision = new CriteriaEntryDecisionEvidence("draft", drafted.Metadata.EventId, 2, 3, 5);
        Assert.True(entry.Decide(entryOperationId, 1, proposal.Metadata.EventId, true, null, decision).IsSuccess);

        // Simulate rehydration with the publication attempt selected on both owners.
        var editionHistory = editionScenario.PendingEvents.ToArray();
        var publicationEdition = new CriteriaEdition(editionId, publicationAttemptId);
        new AggregateScenario<CriteriaEdition>(publicationEdition).Given(editionHistory);
        var publication = new BeginCriteriaPublication(editionId, publicationAttemptId, identifier, entry.CurrentIncarnationId);
        var publicationAdmission = CriteriaAdmission(publicationEdition.Stream, publicationAttemptId, publication);
        Assert.True(publicationEdition.BeginPublication(publicationAttemptId, identifier, entry.CurrentIncarnationId,
            Actor, publicationAdmission).IsSuccess);
        var publicationScenario = new AggregateScenario<CriteriaEdition>(publicationEdition);
        var started = Assert.IsType<CriteriaPublicationStarted>(Assert.Single(publicationScenario.PendingEvents));
        var witness = new CriteriaEntry(editionId, identifier, selectedPublicationAttemptId: publicationAttemptId);
        new AggregateScenario<CriteriaEntry>(witness).Given(entryScenario.PendingEvents.ToArray());
        Assert.True(witness.PinWitness(started).IsSuccess);
        var pinned = Assert.IsType<CriterionWitnessPinned>(Assert.Single(new AggregateScenario<CriteriaEntry>(witness).PendingEvents));
        Assert.True(publicationEdition.Publish(publicationAttemptId, pinned.Metadata.EventId).IsSuccess);
        var published = Assert.IsType<CriteriaEditionPublished>(publicationScenario.PendingEvents[^1]);

        // The program handler uses a test publication authority: source verification
        // and asynchronous participant workers are covered by their own tests.
        var proof = new CriteriaPublicationProof(editionId, published.Metadata.EventId, (ulong)(editionHistory.Length + 1));
        await using var services = ProgramManagementServices.Build(new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateProgramHandler>(), registrations =>
            {
                registrations.AddSingleton<ICriteriaPublicationAuthority>(new PublicationAuthority(proof));
                registrations.AddSingleton<IAccessAdmissionAuthority>(new ProgramAdmission());
            });
        await using var scope = services.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(UserId);
        var create = new CreateProgram(programId, tenantId, "SOC 2", new ProgramPlan(null, null, null, null, null, null), editionId);

        // Act: retry with newly resolved handlers over the same in-memory store.
        var first = await bus.SendAsync(create, actor);
        await using var retryScope = services.CreateAsyncScope();
        var retry = await retryScope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(create, actor);
        var facts = new List<DomainEvent>();
        await foreach (var item in scope.ServiceProvider.GetRequiredService<IEventStore>().ReadAsync(
            new ComplianceProgram(tenantId, programId).Stream, 0, CancellationToken.None))
            if (item.Event is ProgramCreated)
                facts.Add(item.Event);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(programId, first.Value.ProgramId);
        Assert.Equal(first.Value, retry.Value);
        var created = Assert.IsType<ProgramCreated>(Assert.Single(facts));
        Assert.Equal(editionId, created.CriteriaEditionId);
        Assert.Equal(proof, created.CriteriaPublication);
        Assert.Equal(publicationAttemptId, published.PublicationAttemptId);
        Assert.Equal(pinned.Metadata.EventId, published.WitnessPinEventId);
    }

    static AccessAdmissionEvidence CriteriaAdmission<T>(EventStreamAddress target, Uuid operation, T intent)
        where T : notnull
    {
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
        return new AccessAdmissionEvidence(UserId, Actor.Subject, Actor.Issuer, operation, target.ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1), Now, "policy-v1", source, source)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = source,
        };
    }

    sealed class PublicationAuthority(CriteriaPublicationProof proof) : ICriteriaPublicationAuthority
    {
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(editionId == proof.EditionId
                ? Result<CriteriaPublicationProof>.Success(proof)
                : Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.NotFound, "Edition not found.")));
    }

    sealed class ProgramAdmission : IAccessAdmissionAuthority
    {
        public ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext context, Uuid operationId,
            string target, string intentSha256, CancellationToken ct = default) => ValueTask.FromResult<AccessAdmissionEvidence?>(null);
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext context, Uuid tenantId,
            Uuid programId, Uuid operationId, string target, string intentSha256, CancellationToken ct = default) =>
            ValueTask.FromResult<AccessAdmissionEvidence?>(null);
        public ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext context, Uuid tenantId,
            Uuid programId, string target, string intentSha256, CancellationToken ct = default)
        {
            static AccessAdmissionSourceVersion Source(string realm, string area, string resource) =>
                new(new EventStreamAddress(realm, area, resource).ToString(), "v1", 1);
            return ValueTask.FromResult<AccessAdmissionEvidence?>(new AccessAdmissionEvidence(UserId, Actor.Subject,
                Actor.Issuer, programId, target, intentSha256, Now, "policy-v1",
                Source("bdgrz", "identities", UserId.ToString()), Source("bdgrz", "policies", "current"))
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Tenant = Source("bdgrz", "tenants", tenantId.ToString()),
                Member = Source(tenantId.ToString(), "members", UserId.ToString()),
                Primary = Source(tenantId.ToString(), "primary", "current"),
            });
        }
    }
}
