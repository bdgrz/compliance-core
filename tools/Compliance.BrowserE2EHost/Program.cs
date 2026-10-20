using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Tests.E2E;

if (OperatingSystem.IsWindows())
{
    await Console.Error.WriteLineAsync("The browser E2E host requires Unix domain sockets.");
    return 2;
}

using var shutdown = new CancellationTokenSource();
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    shutdown.Cancel();
});
using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
{
    context.Cancel = true;
    shutdown.Cancel();
});
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
Console.CancelKeyPress += cancelHandler;

BrowserE2EHost? browserHost = null;
try
{
    browserHost = await BrowserE2EHost.StartAsync(shutdown.Token);
    var ready = new ReadyMessage("ready", browserHost.BaseUri.ToString(),
        new MailboxMessage("unix", browserHost.MailboxSocketPath, "jsonl-v2"));
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(ready,
        new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    await Console.Out.FlushAsync();
    await WaitForRunnerStopAsync(shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
    if (browserHost is not null)
        await browserHost.DisposeAsync();
}

return 0;

static async Task WaitForRunnerStopAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        string? line;
        try
        {
            line = await Console.In.ReadLineAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }

        if (line is null)
            return;
        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("command", out var command) &&
                command.ValueKind == JsonValueKind.String && command.GetString() == "shutdown")
                return;
        }
        catch (JsonException)
        {
            // Non-command input is ignored; the runner can still stop the process with SIGTERM.
        }
    }
}

sealed record ReadyMessage([property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("apiBaseUri")] string ApiBaseUri, MailboxMessage Mailbox);

sealed record MailboxMessage(string Transport, string Path, string Protocol);
