using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Handlers;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Controls.Tests;

public sealed class ControlNativeBoundaryRedTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCreateBusinessValidLargeControlGivenImmutableContentBoundary()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var content = Content("Large") with
        {
            Description = new string('d', 8000),
            ImplementationNarrative = new string('n', 12000),
            ExpectedEvidenceDescriptions = Enumerable.Range(0, 20).Select(_ => new string('e', 1990)).ToArray(),
        };
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(content).Length > 48 * 1024);
        var user = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "AC-LARGE");
        await using var source = new ControlSourceFixture();
        await source.SeedProgramAsync(tenant, program, user);
        var handler = new CreateControlDraftHandler(source.Operations(), source.Reader, new ValidApplicability());

        // Act
        var result = await handler.HandleAsync(ControlSourceFixture.Context(
            new CreateControlDraft(tenant, program, "AC-LARGE", content), user), CancellationToken.None);
        var facts = await source.FactsAsync(tenant, id);
        var view = await source.Controls.ReadAsync(tenant, program, id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(JsonSerializer.Serialize(content with { Applicability = [] }), JsonSerializer.Serialize(view.CurrentContent));
        var fact = Assert.IsType<ControlDraftCreated>(Assert.Single(facts));
        Assert.Equal(ControlFactContent.DraftSchema, fact.Content.Schema);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(fact,
            ComplianceCoreJsonContext.Default.ControlDraftCreated).Length < 8 * 1024);
        var recovered = await source.Content.ReadJsonAsync<ControlDraftContent>(tenant.ToString(), fact.Content);
        Assert.Equal(JsonSerializer.Serialize(content with { Applicability = [] }), JsonSerializer.Serialize(recovered));
    }

    [Fact]
    public async Task ShouldReplayOriginalRevisionGivenLaterControlContentAndStableLogicalRequest()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "AC-REPLAY-NATIVE");
        await using var provider = ProgramManagementServices.Build(new RecordingPermissionAuthorizer(true),
            portia => portia.AddRequestHandler<CreateControlDraftHandler>().AddRequestHandler<ReviseControlDraftHandler>(),
            services => services.AddSingleton<IControlApplicabilityReferenceValidator>(new ValidApplicability()));
        await ProgramManagementServices.SeedAsync(provider, new ProgramRules(tenant, program), owner =>
        {
            Assert.Null(owner.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(tenant, user), "Lead", Now));
            return Result.Success;
        });
        await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(user))
            .GivenMetadata(new RequestMetadata(id, Uuid.CreateVersion4(), null))
            .When(new CreateControlDraft(tenant, program, "AC-REPLAY-NATIVE", Content("Initial")))
            .ExpectSuccess(new ControlRegistration(id, "AC-REPLAY-NATIVE", 1));
        var logical = Uuid.CreateVersion4();
        var revision = new ReviseControlDraft(logical, tenant, program, id, 1, Content("Second"));
        await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(user))
            .GivenMetadata(new RequestMetadata(logical, Uuid.CreateVersion4(), null)).When(revision).ExpectSuccess();
        await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(user))
            .When(new ReviseControlDraft(Uuid.CreateVersion4(), tenant, program, id, 2, Content("Third"))).ExpectSuccess();

        // Act
        await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(user))
            .GivenMetadata(new RequestMetadata(logical, Uuid.CreateVersion4(), null)).When(revision)
            // Assert
            .ExpectSuccess();
    }

    static ControlDraftContent Content(string title) => new(title, "Review access", "Management reviews access",
        "The security lead reviews access quarterly", ["Dated review record"]);

    sealed class ValidApplicability : IControlApplicabilityReferenceValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Success);
    }
}
