# Browser E2E host

The Playwright runner starts this process once per browser journey. The process owns the test
Fitz broker and storage stack, the real `WebApplicationFactory<Program>` TestServer, the
loopback Kestrel forwarder, and the private delivery mailbox. The browser talks to the app over
HTTP; it never talks to the TestServer handler directly.

## Start and stop

From the repository root, Node starts the host as a child process:

```js
const child = spawn("dotnet", [
  "run",
  "--project",
  "tools/Compliance.BrowserE2EHost/Compliance.BrowserE2EHost.csproj",
  "--no-launch-profile",
], { stdio: ["pipe", "pipe", "inherit"] });
```

Read stdout lines until one is JSON with `type: "ready"`. `dotnet run` may print build output
before the ready message. The ready message is emitted only after the broker, TestServer,
forwarder, and mailbox have started:

```json
{
  "type": "ready",
  "apiBaseUri": "http://127.0.0.1:49152",
  "mailbox": {
    "transport": "unix",
    "path": "/tmp/bdgrz-e2e-mailbox-<random>/mailbox.sock",
    "protocol": "jsonl-v1"
  }
}
```

The listener is the API target, bound to IPv4 loopback on an ephemeral port. Keep the Playwright
page `baseURL` pointed at the Vite UI and pass `apiBaseUri` to Vite's proxy-target environment
setting before starting the UI server. The `mailbox.path` is valid only for the lifetime of this
child process.

In a `finally` block, send `{"command":"shutdown"}` plus a newline to the child's stdin, then
wait for exit. If the process does not exit, send `SIGTERM`. Shutdown stops Kestrel and the
TestServer, tears down the private Compose project and its volumes, and removes the mailbox
socket and directory. `SIGINT` and `SIGTERM` also trigger that cleanup.

## Delivery mailbox protocol

Connect to the ready message's Unix socket path. Send one UTF-8 JSON object followed by a
newline, then read one UTF-8 JSON response line. A request consumes the next matching delivery
once:

```json
{"op":"consume","delivery":"email_verification","email":"person@example.com","timeout_ms":30000}
```

`delivery` is `email_verification`, `email_recovery`, or `tenant_invitation`. `timeout_ms` is
optional and defaults to 30000; the accepted range is 0 through 60000. A successful response is
`{"status":"ok","token":"..."}`. A wait that expires returns `{"status":"timeout"}`;
malformed requests or request lines that are not completed within five seconds return
`{"status":"invalid_request"}`. The mailbox serves at most 64 clients at a time; excess clients
receive `{"status":"busy"}`. A token can be consumed once.

The socket's parent directory is mode `0700`, and the socket is mode `0600`. The mailbox has no
HTTP route. Keep response tokens in runner memory, enter them through the real UI, and do not
include them in test logs, traces, screenshots, or videos.
