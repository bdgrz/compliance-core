using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
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
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProductClientServiceNativeHistoryTests
{
    static readonly DateTimeOffset At = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("duplicate", false)]
    [InlineData("duplicate", true)]
    [InlineData("program", false)]
    [InlineData("program", true)]
    [InlineData("actor", false)]
    [InlineData("actor", true)]
    public async Task ShouldRejectMalformedClosedHistoryGivenDefaultOrRegisteredExecutorRead(string malformed, bool execute)
    {
        // Arrange
        await using var original = new ProductClientServiceFixture();
        var rules = Rules();
        Assert.Null(rules.Revise(1, "Second", "Purpose", "Owner", Uuid.CreateVersion4(), "Reviser", At.AddMinutes(1)));
        await original.PublishAsync(rules);
        var history = await original.Services.HistoryAsync(rules.TenantId, rules.Id);
        var first = Assert.IsType<ClientServiceCreated>(history.Facts[0]);
        var second = Assert.IsType<ClientServiceRevised>(history.Facts[1]);
        var corrupt = malformed switch
        {
            "duplicate" => second with
            {
                Operation = second.Operation with
                {
                    LogicalId = first.Operation.LogicalId,
                    RootOperationId = first.Operation.LogicalId,
                }
            },
            "program" => second with { ProgramId = Uuid.CreateVersion4() },
            _ => second with { Operation = second.Operation with { Actor = second.Operation.Actor with { Kind = "system" } } },
        };
        corrupt.AttachMetadata(second.Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(history.Owner.Stream, 0, [first, corrupt]);
        await using var fixture = new ProductClientServiceFixture(events);
        var reached = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (execute)
                _ = await fixture.Executor.ExecuteAsync(new ClientService(rules.TenantId, rules.Id), _ =>
                {
                    reached = true;
                    return AggregateOutcome.Discard(Result.Success);
                }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            else
                _ = await fixture.Reader.HydrateAsync(new ClientService(rules.TenantId, rules.Id));
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(reached);
    }

    [Fact]
    public async Task ShouldKeepRetirementTerminalAndProgramIdentityGivenReplayedService()
    {
        // Arrange
        await using var fixture = new ProductClientServiceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var service = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(tenant, program, user);
        var actor = ProgramManagementServices.Actor(user);
        var create = new CreateClientService(service, tenant, program, "Payroll", "Process payroll", "Operations");
        var retire = new RetireClientService(Uuid.CreateVersion4(), tenant, service, 1, "Service ended");
        var created = await fixture.Operations.CreateAsync(new RequestContext<CreateClientService>(create, actor), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True((await fixture.Operations.RetireAsync(new RequestContext<RetireClientService>(retire, actor), CancellationToken.None)).IsSuccess);
        var before = await fixture.Services.HistoryAsync(tenant, service);

        // Act
        var revised = await fixture.Operations.ReviseAsync(new RequestContext<ReviseClientService>(
            new ReviseClientService(Uuid.CreateVersion4(), tenant, service, 2, "Other", "Purpose", "Owner"), actor), CancellationToken.None);
        var retiredAgain = await fixture.Operations.RetireAsync(new RequestContext<RetireClientService>(
            retire with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2 }, actor), CancellationToken.None);
        var rebound = await fixture.Operations.CreateAsync(new RequestContext<CreateClientService>(
            create with { ProgramId = Uuid.CreateVersion4() }, actor), CancellationToken.None);
        var originalCreate = await fixture.Operations.CreateAsync(new RequestContext<CreateClientService>(create, actor), CancellationToken.None);
        var originalRetire = await fixture.Operations.RetireAsync(new RequestContext<RetireClientService>(retire, actor), CancellationToken.None);
        var after = await fixture.Services.HistoryAsync(tenant, service);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, revised.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, retiredAgain.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, rebound.Error?.Kind);
        Assert.True(originalCreate.IsSuccess, originalCreate.Error?.Message);
        Assert.Equal(created.Value, originalCreate.Value);
        Assert.True(originalRetire.IsSuccess, originalRetire.Error?.Message);
        Assert.False(after.Owner.IsActive);
        Assert.Equal(program, after.Owner.ProgramId);
        Assert.Equal(before.Owner.NativeSequence, after.Owner.NativeSequence);
        Assert.Equal(2, after.Facts.Count);
    }

    [Fact]
    public async Task ShouldRetainOriginalCreateAndRetirementResponseGivenUnavailableAuthoredBodies()
    {
        // Arrange
        await using var fixture = new ProductClientServiceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var service = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(tenant, program, user);
        var actor = ProgramManagementServices.Actor(user);
        var creation = new CreateClientService(service, tenant, program, "Payroll", "Process payroll", "Operations");
        var first = await fixture.Operations.CreateAsync(new RequestContext<CreateClientService>(creation, actor), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var revision = await fixture.Operations.ReviseAsync(new RequestContext<ReviseClientService>(
            new ReviseClientService(Uuid.CreateVersion4(), tenant, service, 1, "Second", "Monthly payroll", "Operations"), actor), CancellationToken.None);
        Assert.True(revision.IsSuccess, revision.Error?.Message);
        var retire = new RetireClientService(Uuid.CreateVersion4(), tenant, service, 2, "Service ended");
        var retired = await fixture.Operations.RetireAsync(new RequestContext<RetireClientService>(retire, actor), CancellationToken.None);
        Assert.True(retired.IsSuccess, retired.Error?.Message);
        var store = new UnavailableAuthoredContent(fixture.Content);
        var operations = new ProductClientServiceOperations(
            new NativeOperationExecutor(fixture.Reader, fixture.Executor, store),
            fixture.Reader, store, TimeProvider.System);

        // Act
        var replay = await operations.CreateAsync(new RequestContext<CreateClientService>(creation with { Name = " Payroll " }, actor), CancellationToken.None);
        var retireReplay = await operations.RetireAsync(new RequestContext<RetireClientService>(retire, actor), CancellationToken.None);
        var history = await fixture.Services.HistoryAsync(tenant, service);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.True(retireReplay.IsSuccess, retireReplay.Error?.Message);
        Assert.False(history.Owner.IsActive);
        Assert.Equal(3, history.Owner.Revision);
        Assert.Equal(3UL, history.Owner.NativeSequence);
        Assert.Equal(3, history.Facts.Count);
        Assert.Equal(0, store.AuthoredReads);
    }

    [Fact]
    public async Task ShouldRejectAcceptedCreationRetryGivenUnselectedDuplicateRevision()
    {
        // Arrange
        await using var original = new ProductClientServiceFixture();
        var user = Uuid.CreateVersion4();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var rules = new ProductClientServiceRules(tenant, Uuid.CreateVersion4());
        await original.SeedProgramAsync(tenant, program, user);
        Assert.True(rules.Create(program, "Payroll", "Process payroll", "Operations", RbacIds.Member(tenant, user),
            "Original author", At).IsSuccess);
        Assert.Null(rules.Revise(1, "Second", "Purpose", "Owner", Uuid.CreateVersion4(), "Second", At.AddMinutes(1)));
        Assert.Null(rules.Revise(2, "Third", "Purpose", "Owner", Uuid.CreateVersion4(), "Third", At.AddMinutes(2)));
        await original.PublishAsync(rules);
        var history = await original.Services.HistoryAsync(tenant, rules.Id);
        var second = Assert.IsType<ClientServiceRevised>(history.Facts[1]);
        var third = Assert.IsType<ClientServiceRevised>(history.Facts[2]);
        var malformed = third with
        {
            Operation = third.Operation with
            {
                LogicalId = second.Operation.LogicalId,
                RootOperationId = second.Operation.LogicalId,
            }
        };
        malformed.AttachMetadata(third.Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(history.Owner.Stream, 0, [history.Facts[0], second, malformed]);
        await using var fixture = new ProductClientServiceFixture(events);
        var request = new CreateClientService(rules.Id, tenant, program, "Payroll", "Process payroll", "Operations");
        var actor = ProgramManagementServices.Actor(user);
        var candidate = RootOperation.Provenance(rules.Id, nameof(CreateClientService), history.Owner.Stream,
            ActorReference.ForMember(RbacIds.Member(tenant, user), "Current display"), null,
            Bdgrz.Compliance.Features.Kernel.Services.AuthoredIntentDigest.Compute(request), At.AddDays(1));
        Assert.Equal(AcceptedOperationBinding.Of(Assert.IsType<ClientServiceCreated>(history.Facts[0]).Operation),
            AcceptedOperationBinding.Of(candidate));

        // Act
        var result = await fixture.Operations.CreateAsync(new RequestContext<CreateClientService>(request, actor), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Equal("The client service's exact immutable source cannot be verified.", result.Error.Message);
    }

    [Fact]
    public async Task ShouldRejectAppendGivenUnfinalizedSelectedOwnerCut()
    {
        // Arrange
        await using var fixture = new ProductClientServiceFixture();
        var rules = Rules();
        await fixture.PublishAsync(rules);
        var history = await fixture.Services.HistoryAsync(rules.TenantId, rules.Id);
        var logical = Uuid.CreateVersion4();
        var owner = new ClientService(rules.TenantId, rules.Id, logical);
        new AggregateScenario<ClientService>(owner).Given(history.Facts.ToArray());
        var change = new ProductClientServiceChange(nameof(ReviseClientService), 2, owner.ProgramId!.Value,
            new ProductClientServiceContent("Later", "Purpose", "Owner"), null, ActorReference.ForMember(Uuid.CreateVersion4(), "Reviser"),
            At.AddMinutes(1));
        var fact = Assert.IsType<ClientServiceRevised>(await ProductClientServiceSeeds.FreezeAsync(fixture.Content, rules.TenantId,
            rules.Id, change, 2, owner.CurrentContent, logical));

        // Act
        var result = owner.Revise(fact.Operation, fact.Content, fact.FirstResponse!, fact.OperationResult!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<ClientService>(owner).PendingEvents);
        Assert.Single((await fixture.Services.HistoryAsync(rules.TenantId, rules.Id)).Facts);
    }

    static ProductClientServiceRules Rules()
    {
        var rules = new ProductClientServiceRules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var created = rules.Create(Uuid.CreateVersion4(), "Payroll", "Process payroll", "Operations",
            Uuid.CreateVersion4(), "Owner", At);
        Assert.True(created.IsSuccess, created.Error?.Message);
        return rules;
    }

    sealed class UnavailableAuthoredContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int AuthoredReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema != ProductClientServiceContent.Schema && reference.Schema != ProductClientServiceRetirement.Schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            AuthoredReads++;
            return Task.FromException<ReadOnlyMemory<byte>>(new ImmutableContentUnavailableException("The authored service body is unavailable."));
        }
    }
}
