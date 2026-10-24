using System.Security.Claims;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.NativeResponses;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

public sealed class NativeFirstResponseBridgeTests
{
    const string BodySchema = "native-http-probe-response.v1";
    const string OriginalBytes = "{ \"state\":\"pending\", \"sequence\":1 }";

    [Fact]
    public async Task ShouldReturnOriginalPendingStatusAndBytesGivenRetryAfterOwnerProgress()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = Uuid.CreateVersion4();
        var first = await fixture.AdmitAsync(command);
        await fixture.EmitAsync(first);
        var firstBytes = fixture.Bytes();
        Assert.Equal(202, fixture.Http.Response.StatusCode);
        await fixture.Executor.ExecuteAsync(new AcceptanceProbe(fixture.Owner), owner => AggregateOutcome.CommitOnSuccess(owner.Edit("completed")), new RequestDispatchContext(RequestActor.System));

        // Act
        var retry = await fixture.AdmitAsync(command);
        await fixture.EmitAsync(retry);

        // Assert
        Assert.Equal(202, fixture.Http.Response.StatusCode);
        Assert.Equal("application/json", fixture.Http.Response.ContentType);
        Assert.Equal(Encoding.UTF8.GetBytes(OriginalBytes), firstBytes);
        Assert.Equal(firstBytes, fixture.Bytes());
        Assert.Equal(1, (await fixture.Reader.HydrateAsync(new AcceptanceProbe(fixture.Owner))).Admissions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnTypedConflictGivenMissingOrCorruptFrozenBody(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        var result = await fixture.AdmitAsync(Uuid.CreateVersion4());
        fixture.Content.FaultSchema = BodySchema;
        fixture.Content.Unavailable = unavailable;

        // Act
        await fixture.EmitAsync(result);

        // Assert
        Assert.Equal(409, fixture.Http.Response.StatusCode);
        Assert.Equal(unavailable ? "true" : "false", fixture.Http.Response.Headers[ResultHttpExtensions.TransientHeaderName].ToString());
        Assert.DoesNotContain(OriginalBytes, Encoding.UTF8.GetString(fixture.Bytes()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyOnlyExactHttpReplayGivenUnreadableFirstDescriptor(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = Uuid.CreateVersion4();
        Assert.True((await fixture.AdmitAsync(command)).IsSuccess);
        fixture.Content.FaultSchema = FirstResponses.Schema;
        fixture.Content.Unavailable = unavailable;

        // Act
        var result = await fixture.AdmitAsync(command);
        await fixture.EmitAsync(result);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(409, fixture.Http.Response.StatusCode);
        Assert.Equal(unavailable ? "true" : "false", fixture.Http.Response.Headers[ResultHttpExtensions.TransientHeaderName].ToString());
        Assert.Equal(1, (await fixture.Reader.HydrateAsync(new AcceptanceProbe(fixture.Owner))).Admissions);
    }

    [Fact]
    public async Task ShouldPreserveIntegrityExceptionGivenNonHttpReplayWithObserverRegistered()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = Uuid.CreateVersion4();
        Assert.True((await fixture.AdmitAsync(command)).IsSuccess);
        fixture.Content.FaultSchema = FirstResponses.Schema;
        fixture.Accessor.HttpContext = null;

        // Act
        var failure = await Record.ExceptionAsync(async () => await fixture.AdmitAsync(command, direct: true));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveErrorGivenHandlerOrCustomCallbackRefusalAfterAdmission(bool custom)
    {
        // Arrange
        await using var fixture = new Fixture();
        var accepted = await fixture.AdmitAsync(Uuid.CreateVersion4());
        var result = custom ? accepted : Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Current authorization refused the result."));
        fixture.Content.FaultSchema = BodySchema;
        var reads = fixture.Content.Reads;

        // Act
        await fixture.EmitAsync(result, custom ? static (_, _) => Results.StatusCode(403) : null);

        // Assert
        Assert.Equal(403, fixture.Http.Response.StatusCode);
        Assert.Equal(reads, fixture.Content.Reads);
    }

    [Theory]
    [InlineData("child")]
    [InlineData("trace")]
    [InlineData("path")]
    [InlineData("method")]
    public async Task ShouldIgnoreNonRootAdmissionGivenInternalChildOrDifferentHttpIngress(string mismatch)
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        var result = await fixture.AdmitAsync(Uuid.CreateVersion4(), mismatch: mismatch);
        await fixture.EmitAsync(result);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(fixture.Http.Features.Get<NativeFirstResponseCapture>());
        Assert.Equal(200, fixture.Http.Response.StatusCode);
        Assert.Equal("{}", Encoding.UTF8.GetString(fixture.Bytes()));
    }

    [Fact]
    public async Task ShouldRejectAmbiguousNativeRootsGivenTwoDifferentOwnersForSameTopLevelOperation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = Uuid.CreateVersion4();
        Assert.True((await fixture.AdmitAsync(command)).IsSuccess);

        // Act
        var result = await fixture.AdmitAsync(command, otherOwner: Uuid.CreateVersion4(), reuseHttp: true);
        await fixture.EmitAsync(result);

        // Assert
        Assert.Equal(409, fixture.Http.Response.StatusCode);
        Assert.True(fixture.Http.Features.Get<NativeFirstResponseCapture>()!.Ambiguous);
    }

    [Fact]
    public async Task ShouldFailClosedWithoutProtectedReadGivenUnqualifiedProtectedResponse()
    {
        // Arrange
        await using var fixture = new Fixture();
        var result = await fixture.AdmitAsync(Uuid.CreateVersion4(), protectedBody: true);
        var reads = fixture.Content.BodyReads;

        // Act
        await fixture.EmitAsync(result);

        // Assert
        Assert.Equal(409, fixture.Http.Response.StatusCode);
        Assert.Equal(reads, fixture.Content.BodyReads);
        Assert.Equal("false", fixture.Http.Response.Headers[ResultHttpExtensions.TransientHeaderName].ToString());
    }

    [Fact]
    public async Task ShouldPreserveCustomLocationGivenSuccessfulNativeResultCallback()
    {
        // Arrange
        await using var fixture = new Fixture();
        var result = await fixture.AdmitAsync(Uuid.CreateVersion4());

        // Act
        await fixture.EmitAsync(result, static (http, _) =>
        {
            http.Response.Headers.Location = "/status/original";
            return Results.Json(new { state = "completed" }, statusCode: 200);
        });

        // Assert
        Assert.Equal(202, fixture.Http.Response.StatusCode);
        Assert.Equal("/status/original", fixture.Http.Response.Headers.Location.ToString());
        Assert.Equal(OriginalBytes, Encoding.UTF8.GetString(fixture.Bytes()));
    }

    [Theory]
    [InlineData("tenants", false, false)]
    [InlineData("tenants", true, true)]
    [InlineData("other", false, true)]
    public async Task ShouldMatchMemberGivenCanonicalGlobalTenantOwner(string area, bool differentRoute, bool ambiguous)
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = Uuid.CreateVersion4();
        Assert.True((await fixture.AdmitAsync(command)).IsSuccess);

        // Act
        fixture.ObserveGlobalTenantMember(command, area, differentRoute);

        // Assert
        Assert.Equal(ambiguous, fixture.Http.Features.Get<NativeFirstResponseCapture>()!.Ambiguous);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly Uuid _user = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public HttpContextAccessor Accessor { get; } = new();
        public FaultContent Content { get; }
        public DefaultHttpContext Http { get; private set; } = null!;
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>().Configure(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ComplianceCoreJsonContext.Default));
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            Content = new FaultContent(new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));
            services.AddSingleton<IImmutableContentStore>(Content);
            services.AddSingleton<IHttpContextAccessor>(Accessor);
            services.AddSingleton<INativeOperationTransportObserver, NativeOperationHttpObserver>();
            services.AddScoped<NativeOperationExecutor>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }
        public async Task<Result<UnitResult>> AdmitAsync(Uuid command, string? mismatch = null, bool direct = false,
            Uuid? otherOwner = null, bool reuseHttp = false, bool protectedBody = false)
        {
            if (!reuseHttp)
            {
                Http = new DefaultHttpContext { RequestServices = _scope.ServiceProvider, TraceIdentifier = Uuid.CreateVersion4().ToString() };
                Http.Request.Method = "PUT";
                Http.Request.Path = "/native/" + command;
                Http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", _user.ToString()), new Claim("email", "original@example.test")], "UnitTest"));
                Http.Response.Body = new MemoryStream();
                Http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new NativeFirstResponseEndpoint(typeof(NativeHttpProbeRequest))), "native probe"));
            }
            Accessor.HttpContext = direct ? null : Http;
            RequestInvocation invocation = direct ? new DirectInvocation() : new HttpInvocation(mismatch == "method" ? "POST" : "PUT",
                mismatch == "path" ? "/other" : Http.Request.Path.Value!, null, mismatch == "trace" ? "other" : Http.TraceIdentifier);
            IExecutionContext context = mismatch == "child"
                ? new Cntryl.Portia.RequestContext<NativeHttpChildRequest>(new NativeHttpChildRequest(command), new RequestDispatchContext(Http.User, invocation, new RequestMetadata(command, command, null)))
                : new Cntryl.Portia.RequestContext<NativeHttpProbeRequest>(new NativeHttpProbeRequest(command), new RequestDispatchContext(Http.User, invocation, new RequestMetadata(command, command, null)));
            var id = otherOwner ?? Owner;
            var stream = AcceptanceProbe.StreamOf(id);
            var provenance = new OperationProvenance(command, command, null,
                mismatch == "child" ? nameof(NativeHttpChildRequest) : nameof(NativeHttpProbeRequest), ActorReference.ForPlatformOperator(_user, "Original"),
                stream, "initial", 0, new string('a', 64), new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero), stream, stream);
            var native = await _scope.ServiceProvider.GetRequiredService<NativeOperationExecutor>().ExecuteAsync(new NativeOperation<AcceptanceProbe>(
                AcceptedOperationBinding.Of(provenance), () => new AcceptanceProbe(id, command), owner => owner.Acceptance,
                async (_, ct) =>
                {
                    ResponseBodyReference body;
                    if (protectedBody)
                        body = new ProtectedResponseBody(new ProtectedResponseContentRef(Uuid.CreateVersion4(), new string('a', 64), 32, BodySchema, "key-v1"));
                    else
                        body = new StandardResponseBody(await Content.WriteVerifiedAsync(AcceptanceProbe.Realm, Uuid.CreateVersion4(), BodySchema,
                            "application/json", Encoding.UTF8.GetBytes(OriginalBytes), ct));
                    return Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(202, "application/json", BodySchema, body));
                }, (owner, first) => owner.Admit(provenance, first, "pending", 0)), context);
            return !native.IsSuccess ? Result<UnitResult>.Failure(native.Error!) : native.Value is AcceptedAppendResult.Conflict
                ? Result<UnitResult>.Failure(AcceptedAppendResult.ConflictError) : Result<UnitResult>.Success(new UnitResult());
        }
        public async Task EmitAsync(Result<UnitResult> result, Func<HttpContext, Result<UnitResult>, IResult?>? custom = null)
        {
            var config = new PortiaEndpointConfiguration<NativeHttpProbeRequest, UnitResult>();
            if (custom is null)
                config.OnNativeResult();
            else
                config.OnNativeResult(custom);
            var mapped = await config.ResultHandler!(Http, result, CancellationToken.None);
            await mapped!.ExecuteAsync(Http);
        }
        public void ObserveGlobalTenantMember(Uuid command, string area, bool differentRoute)
        {
            Accessor.HttpContext = Http;
            var original = Http.Features.Get<NativeFirstResponseCapture>()!;
            var tenant = Uuid.CreateVersion4();
            var stream = new EventStreamAddress("bdgrz", area, tenant.ToString());
            var binding = original.Binding with
            {
                Realm = stream.Realm,
                FirstOwner = stream,
                OriginalActor = ActorReference.ForMember(RbacIds.Member(tenant, _user), "Original"),
            };
            Http.Request.RouteValues["tenant_id"] = differentRoute ? Uuid.CreateVersion4().ToString() : tenant.ToString();
            Http.Features.Set<NativeFirstResponseCapture>(null);
            var context = new Cntryl.Portia.RequestContext<NativeHttpProbeRequest>(new NativeHttpProbeRequest(command),
                new RequestDispatchContext(Http.User, new HttpInvocation("PUT", Http.Request.Path.Value!, null, Http.TraceIdentifier),
                    new RequestMetadata(command, command, null)));
            new NativeOperationHttpObserver(Accessor).Observe(binding, context,
                new AcceptedAppendResult.Replayed(original.Fact with { Stream = stream }, original.FirstResponse, original.Response));
        }
        public byte[] Bytes() => ((MemoryStream)Http.Response.Body).ToArray();
        public async ValueTask DisposeAsync()
        {
            Accessor.HttpContext = null;
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
    sealed class FaultContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public string? FaultSchema { get; set; }
        public bool Unavailable { get; set; }
        public int Reads { get; private set; }
        public int BodyReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType, ReadOnlyMemory<byte> bytes,
            CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            Reads++;
            if (reference.Schema == BodySchema)
                BodyReads++;
            if (reference.Schema == FaultSchema)
                throw Unavailable ? new ImmutableContentUnavailableException("The response is not visible yet.") : new ImmutableContentIntegrityException("The response digest differs.");
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
