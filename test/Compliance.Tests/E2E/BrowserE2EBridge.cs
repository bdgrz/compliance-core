using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Tests.E2E;

sealed class LoopbackTestServerProxy : IAsyncDisposable
{
    static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "TE",
        "Trailer", "Transfer-Encoding", "Upgrade",
    };

    readonly WebApplication _application;
    readonly HttpMessageInvoker _testServer;

    LoopbackTestServerProxy(WebApplication application, HttpMessageInvoker testServer, Uri baseUri)
    {
        _application = application;
        _testServer = testServer;
        BaseUri = baseUri;
    }

    public System.Uri BaseUri { get; }

    public static async Task<LoopbackTestServerProxy> StartAsync(HttpMessageHandler testServerHandler,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(testServerHandler);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var application = builder.Build();
        var invoker = new HttpMessageInvoker(testServerHandler, disposeHandler: true);
        application.Run(context => ForwardAsync(context, invoker));

        try
        {
            await application.StartAsync(ct).ConfigureAwait(false);
            var addresses = application.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses;
            var address = addresses?.SingleOrDefault()
                ?? throw new InvalidOperationException("The browser bridge did not expose one Kestrel address.");
            var baseUri = new System.Uri(address, UriKind.Absolute);
            if (baseUri.Scheme != Uri.UriSchemeHttp ||
                !IPAddress.TryParse(baseUri.Host, out var addressIp) ||
                !IPAddress.IsLoopback(addressIp))
            {
                throw new InvalidOperationException(
                    "The browser bridge must bind its HTTP listener to an IPv4 or IPv6 loopback address.");
            }

            return new LoopbackTestServerProxy(application, invoker, baseUri);
        }
        catch
        {
            invoker.Dispose();
            await application.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    static async Task ForwardAsync(HttpContext context, HttpMessageInvoker testServer)
    {
        var pathAndQuery = context.Request.PathBase.Add(context.Request.Path).ToUriComponent() +
                           context.Request.QueryString.ToUriComponent();
        var uri = new System.Uri($"http://localhost{pathAndQuery}", UriKind.Absolute);
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), uri);
        request.Headers.Host = context.Request.Host.Value;

        var hasContent = context.Request.ContentLength is > 0 ||
                         context.Request.Headers.ContainsKey("Transfer-Encoding") ||
                         context.Request.Headers.ContainsKey("Content-Type") ||
                         HttpMethods.IsPost(context.Request.Method) ||
                         HttpMethods.IsPut(context.Request.Method) ||
                         HttpMethods.IsPatch(context.Request.Method);
        if (hasContent)
            request.Content = new StreamContent(context.Request.Body);

        foreach (var header in context.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) || header.Key.Equals("Host",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) &&
                request.Content is not null)
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        using var response = await testServer.SendAsync(request, context.RequestAborted)
            .ConfigureAwait(false);
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers)
            CopyResponseHeader(context.Response, header.Key, header.Value);
        foreach (var header in response.Content.Headers)
            CopyResponseHeader(context.Response, header.Key, header.Value);

        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted)
            .ConfigureAwait(false);
    }

    static void CopyResponseHeader(HttpResponse response, string name, IEnumerable<string> values)
    {
        if (!HopByHopHeaders.Contains(name))
            response.Headers[name] = new Microsoft.Extensions.Primitives.StringValues(values.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _application.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await _application.DisposeAsync().ConfigureAwait(false);
            _testServer.Dispose();
        }
    }
}

sealed class UnixDomainTokenMailbox : IEmailChallengeDelivery, ITenantInvitationDelivery,
    IAsyncDisposable
{
    const int MaximumRequestBytes = 4096;
    const int MaximumTimeoutMilliseconds = 60_000;
    const int DefaultMaximumConcurrentConnections = 64;
    const int DefaultMaximumDeliveryKeys = 256;
    static readonly TimeSpan DefaultRequestLineReadDeadline = TimeSpan.FromSeconds(5);
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    readonly string _directoryPath = Path.Combine("/tmp",
        $"bdgrz-e2e-mailbox-{Guid.NewGuid():N}");
    readonly string _socketPath;
    readonly CancellationTokenSource _stopping = new();
    readonly object _deliveryGate = new();
    readonly Dictionary<DeliveryKey, DeliverySlot> _deliveries = [];
    readonly int _maximumDeliveryKeys;
    readonly ConcurrentDictionary<long, Task> _connections = new();
    readonly SemaphoreSlim _connectionSlots;
    readonly TimeSpan _requestLineReadDeadline;
    readonly Func<Socket, CancellationToken, Task>? _afterAcceptAsync;
    Socket? _listener;
    Task? _acceptLoop;
    long _connectionId;
    int _pendingTokenCount;
    int _started;
    int _disposed;

    public UnixDomainTokenMailbox() : this(DefaultMaximumConcurrentConnections,
        DefaultRequestLineReadDeadline, maximumDeliveryKeys: DefaultMaximumDeliveryKeys)
    {
    }

    internal UnixDomainTokenMailbox(int maximumConcurrentConnections, TimeSpan requestLineReadDeadline,
        Func<Socket, CancellationToken, Task>? afterAcceptAsync = null,
        int maximumDeliveryKeys = DefaultMaximumDeliveryKeys)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentConnections);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestLineReadDeadline, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDeliveryKeys);
        _connectionSlots = new SemaphoreSlim(maximumConcurrentConnections,
            maximumConcurrentConnections);
        _requestLineReadDeadline = requestLineReadDeadline;
        _afterAcceptAsync = afterAcceptAsync;
        _maximumDeliveryKeys = maximumDeliveryKeys;
        _socketPath = Path.Combine(_directoryPath, "mailbox.sock");
    }

    public string SocketPath => _socketPath;

    public async Task StartAsync()
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The browser E2E mailbox requires Unix domain sockets.");
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            throw new InvalidOperationException("The browser E2E mailbox has already started.");

        try
        {
            Directory.CreateDirectory(_directoryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
            File.SetUnixFileMode(_socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            _listener.Listen(backlog: 16);
            _acceptLoop = AcceptConnectionsAsync(_stopping.Token);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    ValueTask IEmailChallengeDelivery.SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
        string token, CancellationToken ct)
    {
        _ = challengeId;
        return QueueDeliveryAsync("email_verification", userId, emailAddress, token, ct);
    }

    ValueTask IEmailChallengeDelivery.SendRecoveryAsync(Uuid challengeId, Uuid userId,
        string emailAddress, string token, CancellationToken ct)
    {
        _ = challengeId;
        return QueueDeliveryAsync("email_recovery", userId, emailAddress, token, ct);
    }

    ValueTask IEmailChallengeDelivery.SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId,
        string emailAddress, CancellationToken ct)
    {
        _ = challengeId;
        _ = userId;
        _ = emailAddress;
        ct.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    ValueTask ITenantInvitationDelivery.SendAsync(Uuid attemptId, Uuid tenantId,
        string emailAddress, string token, CancellationToken ct)
    {
        _ = attemptId;
        return QueueDeliveryAsync("tenant_invitation", tenantId, emailAddress, token, ct);
    }

    ValueTask QueueDeliveryAsync(string delivery, Uuid scopeId, string emailAddress, string token,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (scopeId == Uuid.Empty)
            throw new ArgumentException("A recipient scope is required.", nameof(scopeId));
        if (!EmailAddresses.TryNormalize(emailAddress, out var normalizedEmail))
            throw new ArgumentException("A valid recipient address is required.", nameof(emailAddress));

        var key = new DeliveryKey(delivery, normalizedEmail, scopeId);
        lock (_deliveryGate)
        {
            if (!_deliveries.TryGetValue(key, out var slot))
            {
                if (_deliveries.Count >= _maximumDeliveryKeys ||
                    _pendingTokenCount >= _maximumDeliveryKeys)
                    throw new MailboxCapacityExceededException();

                slot = new DeliverySlot();
                _deliveries.Add(key, slot);
            }

            if (slot.Waiters.First is { } firstWaiter)
            {
                slot.Waiters.RemoveFirst();
                firstWaiter.Value.Node = null;
                firstWaiter.Value.Completion.TrySetResult(new MailboxResponse("ok", token));
                RemoveIfEmpty(key, slot);
            }
            else if (slot.PendingToken is null)
            {
                if (_pendingTokenCount >= _maximumDeliveryKeys)
                {
                    RemoveIfEmpty(key, slot);
                    throw new MailboxCapacityExceededException();
                }

                slot.PendingToken = token;
                _pendingTokenCount++;
            }
            else
            {
                // Keep only the newest pending challenge for a recipient and delivery type.
                slot.PendingToken = token;
            }
        }
        return ValueTask.CompletedTask;
    }

    async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        var listener = _listener ?? throw new InvalidOperationException("The mailbox listener is missing.");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var socket = await listener.AcceptAsync(ct).ConfigureAwait(false);
                if (_afterAcceptAsync is not null)
                    await _afterAcceptAsync(socket, ct).ConfigureAwait(false);

                bool connectionSlotAcquired;
                try
                {
                    connectionSlotAcquired = _connectionSlots.Wait(0, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    socket.Dispose();
                    throw;
                }

                if (!connectionSlotAcquired)
                {
                    await RejectBusyConnectionAsync(socket, ct).ConfigureAwait(false);
                    continue;
                }

                var connectionId = Interlocked.Increment(ref _connectionId);
                var registration = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var connection = HandleAndRemoveAsync(connectionId, socket, registration.Task, ct);
                _connections[connectionId] = connection;
                // Ensure a quickly completed connection cannot remove itself before insertion.
                registration.SetResult();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
        }
        catch (SocketException) when (ct.IsCancellationRequested)
        {
        }
    }

    async Task HandleAndRemoveAsync(long connectionId, Socket socket, Task registered,
        CancellationToken ct)
    {
        try
        {
            await registered.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            await HandleConnectionAsync(socket, ct).ConfigureAwait(false);
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            _connectionSlots.Release();
        }
    }

    static async Task RejectBusyConnectionAsync(Socket socket, CancellationToken ct)
    {
        using var stream = new NetworkStream(socket, ownsSocket: true);
        await TryWriteErrorAsync(stream, "busy", ct).ConfigureAwait(false);
    }

    async Task HandleConnectionAsync(Socket socket, CancellationToken ct)
    {
        using var stream = new NetworkStream(socket, ownsSocket: true);
        try
        {
            string? requestLine;
            using (var requestReadDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                requestReadDeadline.CancelAfter(_requestLineReadDeadline);
                try
                {
                    requestLine = await ReadRequestLineAsync(stream, requestReadDeadline.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    await TryWriteErrorAsync(stream, "invalid_request", ct).ConfigureAwait(false);
                    return;
                }
            }

            var response = requestLine is null
                ? new MailboxResponse("invalid_request")
                : await HandleRequestAsync(requestLine, ct).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, JsonOptions) + "\n");
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (JsonException)
        {
            await TryWriteErrorAsync(stream, "invalid_request", ct).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            await TryWriteErrorAsync(stream, "invalid_request", ct).ConfigureAwait(false);
        }
    }

    async Task<MailboxResponse> HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(requestLine);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetString(root, "op", out var operation) || operation != "consume" ||
            !TryGetString(root, "delivery", out var delivery) ||
            !TryGetString(root, "email", out var email) ||
            string.IsNullOrWhiteSpace(email))
            return new MailboxResponse("invalid_request");

        if (delivery is not ("email_verification" or "email_recovery" or "tenant_invitation"))
            return new MailboxResponse("invalid_request");

        var scopeField = delivery == "tenant_invitation" ? "tenant_id" : "user_id";
        var otherScopeField = delivery == "tenant_invitation" ? "user_id" : "tenant_id";
        if (root.TryGetProperty(otherScopeField, out _) ||
            !TryGetString(root, scopeField, out var rawScopeId) ||
            !Uuid.TryParse(rawScopeId, out var scopeId) || scopeId == Uuid.Empty ||
            !EmailAddresses.TryNormalize(email, out var normalizedEmail))
            return new MailboxResponse("invalid_request");

        var timeout = 30_000;
        if (root.TryGetProperty("timeout_ms", out var timeoutElement) &&
            (timeoutElement.ValueKind != JsonValueKind.Number ||
             !timeoutElement.TryGetInt32(out timeout) ||
             timeout is < 0 or > MaximumTimeoutMilliseconds))
            return new MailboxResponse("invalid_request");

        var key = new DeliveryKey(delivery, normalizedEmail, scopeId);
        Waiter waiter;
        lock (_deliveryGate)
        {
            if (_deliveries.TryGetValue(key, out var slot))
            {
                if (slot.PendingToken is { } token)
                {
                    slot.PendingToken = null;
                    _pendingTokenCount--;
                    RemoveIfEmpty(key, slot);
                    return new MailboxResponse("ok", token);
                }

                if (timeout == 0)
                    return new MailboxResponse("timeout");

                waiter = new Waiter();
                waiter.Node = slot.Waiters.AddLast(waiter);
            }
            else
            {
                // A zero-timeout miss is lookup-only. Do not allocate an empty slot.
                if (timeout == 0)
                    return new MailboxResponse("timeout");
                if (_deliveries.Count >= _maximumDeliveryKeys)
                    return new MailboxResponse("capacity_exceeded");

                var newSlot = new DeliverySlot();
                waiter = new Waiter();
                waiter.Node = newSlot.Waiters.AddLast(waiter);
                _deliveries.Add(key, newSlot);
            }
        }

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        wait.CancelAfter(timeout);
        using var registration = wait.Token.Register(() => CompleteWaiter(key, waiter,
            ct.IsCancellationRequested ? "closed" : "timeout"));
        return await waiter.Completion.Task.ConfigureAwait(false);
    }

    void CompleteWaiter(DeliveryKey key, Waiter waiter, string status)
    {
        lock (_deliveryGate)
        {
            if (waiter.Node is not { } node)
                return;

            if (_deliveries.TryGetValue(key, out var slot))
            {
                slot.Waiters.Remove(node);
                RemoveIfEmpty(key, slot);
            }

            waiter.Node = null;
            waiter.Completion.TrySetResult(new MailboxResponse(status));
        }
    }

    void RemoveIfEmpty(DeliveryKey key, DeliverySlot slot)
    {
        if (slot.PendingToken is null && slot.Waiters.Count == 0)
            _deliveries.Remove(key);
    }

    static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? string.Empty;
        return true;
    }

    static async Task<string?> ReadRequestLineAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var current = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(current.AsMemory(), ct).ConfigureAwait(false);
            if (read == 0)
                return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
            if (current[0] == (byte)'\n')
                return Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\r');
            if (buffer.Length == MaximumRequestBytes)
                throw new InvalidDataException("The mailbox request is too large.");
            buffer.WriteByte(current[0]);
        }
    }

    static async Task TryWriteErrorAsync(Stream stream, string status, CancellationToken ct)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new MailboxResponse(status),
                JsonOptions) + "\n");
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or
                                          ObjectDisposedException or SocketException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _stopping.Cancel();
        _listener?.Dispose();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
            }
        }

        try
        {
            await Task.WhenAll(_connections.Values).ConfigureAwait(false);
        }
        finally
        {
            lock (_deliveryGate)
            {
                _deliveries.Clear();
                _pendingTokenCount = 0;
            }
            _connectionSlots.Dispose();
            _stopping.Dispose();
            if (File.Exists(_socketPath))
                File.Delete(_socketPath);
            if (Directory.Exists(_directoryPath))
                Directory.Delete(_directoryPath);
        }
    }

    readonly record struct DeliveryKey(string Delivery, string Email, Uuid ScopeId);
    sealed class DeliverySlot
    {
        public string? PendingToken { get; set; }
        public LinkedList<Waiter> Waiters { get; } = new();
    }

    sealed class Waiter
    {
        public TaskCompletionSource<MailboxResponse> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node { get; set; }
    }

    sealed class MailboxCapacityExceededException
        : InvalidOperationException
    {
        public MailboxCapacityExceededException()
            : base("The browser E2E mailbox has reached its delivery capacity")
        {
        }
    }

    sealed record MailboxResponse(string Status, string? Token = null);
}

/// <summary>
///     Starts the browser E2E dependencies in one process: a private Fitz stack, the actual
///     <see cref="WebApplicationFactory{TEntryPoint}"/> TestServer, loopback Kestrel forwarding,
///     and a private Unix socket for one-time delivery-code consumption.
/// </summary>
public sealed class BrowserE2EHost : IAsyncDisposable
{
    readonly BrokerStackFixture? _ownedBroker;
    readonly UnixDomainTokenMailbox _mailbox;
    readonly WebApplicationFactory<Program> _factory;
    readonly LoopbackTestServerProxy _proxy;
    int _disposed;

    BrowserE2EHost(BrokerStackFixture? ownedBroker, UnixDomainTokenMailbox mailbox,
        WebApplicationFactory<Program> factory, LoopbackTestServerProxy proxy)
    {
        _ownedBroker = ownedBroker;
        _mailbox = mailbox;
        _factory = factory;
        _proxy = proxy;
    }

    public System.Uri BaseUri => _proxy.BaseUri;
    public string MailboxSocketPath => _mailbox.SocketPath;

    public static async Task<BrowserE2EHost> StartAsync(CancellationToken ct = default)
    {
        var broker = new BrokerStackFixture();
        try
        {
            await broker.StartAsync(ct).ConfigureAwait(false);
            return await StartCoreAsync(broker.WebSocketEndpoint, broker, ct).ConfigureAwait(false);
        }
        catch
        {
            await broker.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static Task<BrowserE2EHost> StartForTestingAsync(string webSocketEndpoint,
        CancellationToken ct = default) => StartCoreAsync(webSocketEndpoint, null, ct);

    static async Task<BrowserE2EHost> StartCoreAsync(string webSocketEndpoint,
        BrokerStackFixture? ownedBroker, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webSocketEndpoint);
        var mailbox = new UnixDomainTokenMailbox();
        WebApplicationFactory<Program>? factory = null;
        LoopbackTestServerProxy? proxy = null;
        try
        {
            await mailbox.StartAsync().ConfigureAwait(false);
            factory = E2EAppFactory.Create(webSocketEndpoint)
                .WithWebHostBuilder(builder =>
                {
                    builder.UseContentRoot(Path.Combine(FindRepositoryRoot(), "src", "Compliance.App"));
                    builder.UseSetting("COMPLIANCE_HOST_MODE", "standalone");
                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<IEmailChallengeDelivery>();
                        services.AddSingleton<IEmailChallengeDelivery>(mailbox);
                        services.RemoveAll<ITenantInvitationDelivery>();
                        services.AddSingleton<ITenantInvitationDelivery>(mailbox);
                    });
                });

            var server = factory.Server;
            proxy = await LoopbackTestServerProxy.StartAsync(server.CreateHandler(), ct)
                .ConfigureAwait(false);
            return new BrowserE2EHost(ownedBroker, mailbox, factory, proxy);
        }
        catch
        {
            if (proxy is not null)
                await proxy.DisposeAsync().ConfigureAwait(false);
            factory?.Dispose();
            await mailbox.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Compliance.slnx")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "The browser E2E host must run from a checkout containing Compliance.slnx.");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            await _proxy.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                _factory.Dispose();
            }
            finally
            {
                try
                {
                    if (_ownedBroker is not null)
                        await _ownedBroker.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    await _mailbox.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
