using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Hosting.Persistence;

public sealed class PersistenceValidationRequestBusTests
{
    [Fact]
    public async Task ShouldReturnNontransientValidationGivenPreAppendFrameRejection()
    {
        // Arrange
        var inner = new ThrowingBus(new EventFrameSizeException(70_000));
        var bus = new PersistenceValidationRequestBus(inner);
        var metadata = new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null);
        var context = bus.CreateContext(RequestActor.System, metadata);

        // Act
        var command = await bus.DispatchAsync(new UnprotectedCompositionRequest(), context);
        var query = await bus.DispatchAsync(new GetTenant(Uuid.CreateVersion4()), context);

        // Assert
        Assert.False(command.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, command.Error.Kind);
        Assert.False(command.Error.IsTransient);
        Assert.False(query.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, query.Error.Kind);
        Assert.False(query.Error.IsTransient);
        Assert.Same(context, inner.LastContext);
        Assert.Same(metadata, inner.Metadata);
    }

    [Theory]
    [InlineData("serialization")]
    [InlineData("concurrency")]
    [InlineData("cancellation")]
    [InlineData("kv")]
    public async Task ShouldPreserveUnexpectedOrUnsettledFailureGivenOtherPersistenceException(string kind)
    {
        // Arrange
        Exception original = kind switch
        {
            "serialization" => new JsonException("Unknown event schema"),
            "concurrency" => new EventStreamConcurrencyException("Conflict"),
            "cancellation" => new OperationCanceledException(),
            _ => new KvFrameSizeException(70_000),
        };
        var bus = new PersistenceValidationRequestBus(new ThrowingBus(original));
        var context = bus.CreateContext(RequestActor.System);

        // Act
        var command = await Record.ExceptionAsync(async () =>
            await bus.DispatchAsync(new UnprotectedCompositionRequest(), context));
        var query = await Record.ExceptionAsync(async () =>
            await bus.DispatchAsync(new GetTenant(Uuid.CreateVersion4()), context));

        // Assert
        Assert.Same(original, command);
        Assert.Same(original, query);
    }

    [Fact]
    public async Task ShouldPreserveAuthorizationResultAndContextGivenAuthorizationOnlyDispatch()
    {
        // Arrange
        var inner = new ThrowingBus(new EventFrameSizeException(70_000));
        var bus = new PersistenceValidationRequestBus(inner);
        var context = bus.CreateContext(RequestActor.System);
        using var cancellation = new CancellationTokenSource();

        // Act
        var result = await bus.AuthorizeAsync(new UnprotectedCompositionRequest(), context, cancellation.Token);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Same(context, inner.LastContext);
        Assert.Equal(cancellation.Token, inner.Token);
    }

    sealed class ThrowingBus(Exception failure) : IRequestBus
    {
        public RequestDispatchContext? LastContext { get; private set; }
        public RequestMetadata? Metadata { get; private set; }
        public CancellationToken Token { get; private set; }
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null)
        {
            Metadata = metadata;
            return new RequestDispatchContext(actor, metadata: metadata);
        }
        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default)
        {
            LastContext = context;
            Token = ct;
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Denied")));
        }
        public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context, CancellationToken ct = default)
        {
            LastContext = context;
            return ValueTask.FromException<Result>(failure);
        }
        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default)
        {
            LastContext = context;
            return ValueTask.FromException<Result<TOut>>(failure);
        }
        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
