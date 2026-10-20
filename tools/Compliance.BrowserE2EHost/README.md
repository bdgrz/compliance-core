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
    "protocol": "jsonl-v2"
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
newline, then read one UTF-8 JSON response line. The protocol identifier is `jsonl-v2`. A request
consumes one matching delivery. The recipient scope is required so messages for two users or
tenants that share an email address cannot cross:

```json
{"op":"consume","delivery":"email_verification","email":"person@example.com","user_id":"<user UUID>","timeout_ms":30000}
```

Recovery uses the same `user_id` field. Invitation requests use the tenant the acceptance UI is
currently viewing:

```json
{"op":"consume","delivery":"tenant_invitation","email":"person@example.com","tenant_id":"<tenant UUID>","timeout_ms":30000}
```

`delivery` is `email_verification`, `email_recovery`, or `tenant_invitation`. `timeout_ms` is
optional and defaults to 30000; the accepted range is 0 through 60000. `email` is normalized with
the same rules as the application before matching. `user_id` is required for verification and
recovery; `tenant_id` is required for invitations. Missing, malformed, or mismatched scope fields
return `{"status":"invalid_request"}`.

A successful response is `{"status":"ok","token":"..."}`. A wait that expires returns
`{"status":"timeout"}`. A zero-timeout lookup never creates mailbox state. Positive-timeout
waiters and their empty keys are removed on timeout. The mailbox retains at most 256 live keys,
with at most one pending token per key (256 pending tokens total). Up to 64 additional tokens can
be in active responses, bounded by the connection limit. A new consume key at capacity returns
`{"status":"capacity_exceeded"}`. When a newer delivery arrives before a token is consumed, it
replaces the prior pending token for that logical key. A delivery that needs a new key at capacity
fails with a sanitized capacity exception so the delivery reactor records failure.

Malformed requests or request lines that are not completed within five seconds return
`{"status":"invalid_request"}`. The mailbox serves at most 64 clients at a time; excess clients
receive `{"status":"busy"}`. A token can be consumed once.

The socket's parent directory is mode `0700`, and the socket is mode `0600`. The mailbox has no
HTTP route. Keep response tokens in runner memory, enter them through the real UI, and do not
include them in test logs, traces, screenshots, or videos.
