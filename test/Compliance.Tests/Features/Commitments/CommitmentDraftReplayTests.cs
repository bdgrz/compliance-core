using System.Security.Claims;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentDraftReplayTests
{
    [Fact]
    public async Task ShouldCreateLegalLargeDraftGivenEscapedStatementAndContext()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = CommitmentDraft.IdFor(tenant, program, "service_commitment", "SC-LARGE");
        await using var provider = ProgramManagementServices.Build(new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateCommitmentDraftHandler>());
        await ProgramManagementServices.SeedAsync(provider, new ClientService(tenant, serviceId),
            service => service.Create(program, "Service", "Purpose", "Operations", user, "Author", DateTimeOffset.UtcNow));
        var request = new CreateCommitmentDraft(id, tenant, program, serviceId, "service_commitment", "SC-LARGE",
            new string('<', 16000), new string('<', 16000), "Signed MSA");

        // Act
        // Assert
        await Dispatch(provider, ProgramManagementServices.Actor(user), request, id)
            .ExpectSuccess(new CommitmentDraftRegistration(id, "service_commitment", "SC-LARGE", 1));
    }

    [Fact]
    public async Task ShouldReplayOriginalCreateAndRejectNewDraftGivenRetiredService()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(actorId);
        await using var provider = ProgramManagementServices.Build(
            new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateCommitmentDraftHandler>());
        await ProgramManagementServices.SeedAsync(provider, new ClientService(tenantId, serviceId),
            service => service.Create(programId, "Client service", "Purpose", "Operations",
                actorId, "Author", DateTimeOffset.UtcNow));
        var original = new CreateCommitmentDraft(Bdgrz.Compliance.Features.Commitments.CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-01"), tenantId, programId, serviceId,
            "service_commitment", "SC-01", "Statement", "Context", "Source A");
        var registration = new CommitmentDraftRegistration(CommitmentDraft.IdFor(tenantId,
            programId, "service_commitment", "SC-01"), "service_commitment", "SC-01", 1);
        await Dispatch(provider, actor, original, requestId).ExpectSuccess(registration);
        await ProgramManagementServices.SeedAsync(provider, new ClientService(tenantId, serviceId),
            service =>
            {
                Assert.Null(service.Retire(1, "Service ended", actorId, "Author",
                    DateTimeOffset.UtcNow));
                return Result.Success;
            });

        // Act
        // Assert
        await Dispatch(provider, actor, original, Uuid.CreateVersion4()).ExpectSuccess(registration);
        await Dispatch(provider, ProgramManagementServices.Actor(Uuid.CreateVersion4()), original, Uuid.CreateVersion4())
            .ExpectFailure(RequestErrorKind.Conflict);
        await Dispatch(provider, actor, original with { Identifier = "SC-02", DraftId = CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-02") },
            Uuid.CreateVersion4()).ExpectFailure(RequestErrorKind.NotFound);
        await Dispatch(provider, actor, original with { Statement = "Changed statement" },
            requestId).ExpectFailure(RequestErrorKind.Conflict);
        var draft = await ProgramManagementServices.HydrateAsync(provider,
            new CommitmentRules(tenantId, registration.DraftId));
        var rejectedDraft = await ProgramManagementServices.HydrateAsync(provider,
            new CommitmentRules(tenantId,
                CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-02")));
        Assert.Equal(1, draft.Revision);
        Assert.False(rejectedDraft.IsCreated);
    }

    static RequestExpectations<CommitmentDraftRegistration> Dispatch(IServiceProvider provider,
        ClaimsPrincipal actor, CreateCommitmentDraft request,
        Uuid requestId) => RequestScenario.For(provider)
        .GivenActor(actor)
        .GivenMetadata(new RequestMetadata(request.DraftId, requestId, null))
        .When(request)
        .ExpectAuthorized()
        .ExpectHandled();
}
