using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

/// <summary>
///     Design trace LOCK-12 and the canonical revise DTO: one authored shape, whichever transport or queue carries
///     the update, preserving the difference between keeping a field (omitted) and clearing it (supplied null).
/// </summary>
public sealed class ApplicationUpdateIntentTests
{
    [Fact]
    public async Task ShouldPreserveKeepVersusClearGivenApplicationUpdateCrossesMcpAndQueue()
    {
        // Arrange: governed revisions make an omitted field keep its value; the application has owners already.
        await using var host = new ApplicationInventoryHost(governedRevisions: true);
        var owner = await host.RecordPersonAsync("Owner");
        var applicationId = Uuid.CreateVersion4();
        var declared = await host.Declare.HandleAsync(host.Context(new DeclareApplication(applicationId, host.Tenant,
            "Payroll", "Run payroll", "Finance", "internal", owner, owner, false, "service", "critical")),
            CancellationToken.None);
        Assert.True(declared.IsSuccess, declared.Error?.Message);
        var key = Uuid.CreateVersion4();
        var http = Http(host, applicationId, key, classification: ValueOf.Omitted);
        var mcp = await ForwardAsync(Mcp(host, applicationId, key, ValueOf.Omitted));

        // Act: step 1, omission crosses a serialized canonical queue DTO and executes.
        var httpIntent = ApplicationUpdateAuthoredIntent.From(http);
        var queued = JsonSerializer.Deserialize(JsonSerializer.Serialize(httpIntent,
            ComplianceCoreJsonContext.Default.ApplicationUpdateAuthoredIntent),
            ComplianceCoreJsonContext.Default.ApplicationUpdateAuthoredIntent)!;
        var viaPublicSerializer = JsonSerializer.Deserialize(JsonSerializer.Serialize(
            http, ComplianceCoreJsonContext.Default.ReviseApplication), ComplianceCoreJsonContext.Default.ReviseApplication)!;
        var kept = await host.Revise.HandleAsync(host.Context(queued.ToRequest(key)), CancellationToken.None);
        var afterKeep = await host.ApplicationEventsAsync(applicationId);

        // Assert: the presence flag survives the queue although the command's own helper is not a wire member.
        Assert.False(queued.ClassificationIsSupplied);
        Assert.Equal(httpIntent, ApplicationUpdateAuthoredIntent.From(mcp));
        Assert.False(viaPublicSerializer.ClassificationIntent.IsSupplied);
        Assert.True(kept.IsSuccess, kept.Error?.Message);
        var revised = Assert.IsType<ApplicationRevised>(afterKeep[^1].Event);
        Assert.Equal("internal", revised.Classification);
        Assert.Equal(owner, revised.SystemOwnerPersonId);
        Assert.Equal(owner, revised.AccessOwnerPersonId);
        Assert.Equal(key, revised.Operation.LogicalId);

        // Act: step 2, the same command id with an explicit null is a different authored intent.
        var clearedHttp = Http(host, applicationId, key, classification: ValueOf.Null);
        var conflict = await host.Revise.HandleAsync(host.Context(clearedHttp), CancellationToken.None);

        // Assert: conflict before any mutation.
        Assert.NotEqual(httpIntent, ApplicationUpdateAuthoredIntent.From(clearedHttp));
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error?.Kind);
        Assert.Equal(AcceptedAppendResult.ConflictError.Message, conflict.Error!.Message);
        Assert.Equal(afterKeep, await host.ApplicationEventsAsync(applicationId));
    }

    [Fact]
    public async Task ShouldMatchCanonicalTupleGivenNewCommandExplicitlyClearingThroughHttpAndMcp()
    {
        // Arrange: ungoverned revisions let an explicit null clear the field.
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync(classification: "internal");
        var key = Uuid.CreateVersion4();
        var http = Http(host, applicationId, key, classification: ValueOf.Null);
        var mcpInput = Mcp(host, applicationId, key, ValueOf.Null);

        // Act
        var forwarded = await ForwardAsync(mcpInput);
        var cleared = await host.Revise.HandleAsync(host.Context(forwarded), CancellationToken.None);

        // Assert: supplied-null is part of the tuple on both transports and the typed identity is the same.
        Assert.True(ApplicationUpdateAuthoredIntent.From(http).ClassificationIsSupplied);
        Assert.Equal(ApplicationUpdateAuthoredIntent.From(http), ApplicationUpdateAuthoredIntent.From(forwarded));
        Assert.Equal(key, ((IIdentifiedRequest)mcpInput).RequestId);
        Assert.Equal(((IIdentifiedRequest)mcpInput).RequestId, ((IIdentifiedRequest)forwarded).RequestId);
        Assert.Equal(key, forwarded.CommandId);
        Assert.True(cleared.IsSuccess, cleared.Error?.Message);
        Assert.Null(Assert.IsType<ApplicationRevised>((await host.ApplicationEventsAsync(applicationId))[^1].Event)
            .Classification);
    }

    [Fact]
    public void ShouldKeepIntentsApartGivenOmittedAndExplicitNullForEveryPresenceField()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var application = Uuid.CreateVersion4();
        var omitted = new ReviseApplication(Uuid.CreateVersion4(), tenant, application, 1, "Payroll", "Run payroll", null);

        // Act
        var classification = omitted with { ClassificationIntent = new ApplicationRevisionField(true) };
        var system = omitted with { SystemOwnerIntent = new ApplicationRevisionField(true) };
        var access = omitted with { AccessOwnerIntent = new ApplicationRevisionField(true) };
        var digests = new[] { omitted, classification, system, access }
            .Select(request => AuthoredIntentDigest.Compute(ApplicationUpdateAuthoredIntent.From(request))).ToArray();

        // Assert
        Assert.Equal(4, digests.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ShouldTrimAuthoredTextAndIgnoreCommandIdGivenCanonicalIntent()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var application = Uuid.CreateVersion4();
        var padded = new ReviseApplication(Uuid.CreateVersion4(), tenant, application, 1, " Payroll ", " Run payroll ",
            " Finance ", " internal ", ApplicationKind: " service ");
        var trimmed = new ReviseApplication(Uuid.CreateVersion4(), tenant, application, 1, "Payroll", "Run payroll",
            "Finance", "internal", ApplicationKind: "service")
        {
            ClassificationIntent = new ApplicationRevisionField(true),
        };

        // Act
        var paddedIntent = ApplicationUpdateAuthoredIntent.From(padded with
        {
            ClassificationIntent = new ApplicationRevisionField(true),
        });

        // Assert
        Assert.Equal(ApplicationUpdateAuthoredIntent.From(trimmed), paddedIntent);
    }

    /// <summary>The command the MCP adapter hands to the bus for an input.</summary>
    static async Task<ReviseApplication> ForwardAsync(ReviseApplicationMcpInput input)
    {
        var bus = new CapturingBus();
        var context = new Cntryl.Portia.RequestContext<ReviseApplicationMcpInput>(input,
            new RequestDispatchContext(ApplicationInventoryHost.ActorFor(Uuid.CreateVersion4())));
        await new ReviseApplicationMcpInputHandler(bus).HandleAsync(context, CancellationToken.None);
        return Assert.IsType<ReviseApplication>(Assert.Single(bus.Forwarded));
    }

    enum ValueOf
    {
        Omitted,
        Null,
    }

    static ReviseApplication Http(ApplicationInventoryHost host, Uuid applicationId, Uuid commandId, ValueOf classification) =>
        new(commandId, host.Tenant, applicationId, 1, "Payroll", "Run payroll", "Finance")
        {
            // The HTTP binder records presence beside the nullable value: an explicit null is supplied.
            ClassificationIntent = new ApplicationRevisionField(classification == ValueOf.Null),
        };

    static ReviseApplicationMcpInput Mcp(ApplicationInventoryHost host, Uuid applicationId, Uuid commandId,
        ValueOf classification) => classification == ValueOf.Null
        ? new ReviseApplicationMcpInput
        {
            CommandId = commandId,
            TenantId = host.Tenant,
            ApplicationId = applicationId,
            ExpectedRevision = 1,
            Name = "Payroll",
            Purpose = "Run payroll",
            OwnerReference = "Finance",
            // Assigning the member, even to null, is how the MCP binder reports a supplied field.
            Classification = null,
        }
        : new ReviseApplicationMcpInput
        {
            CommandId = commandId,
            TenantId = host.Tenant,
            ApplicationId = applicationId,
            ExpectedRevision = 1,
            Name = "Payroll",
            Purpose = "Run payroll",
            OwnerReference = "Finance",
        };
}
