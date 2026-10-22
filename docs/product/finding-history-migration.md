# Finding history migration runbook

The `finding-history-migration` command is a same-image operator entry point for moving one program's fenced `RemediationLedger` history into per-finding streams. Each `step` invocation performs one bounded action: start and fence, import one physical source offset, or verify parity and complete. Run `status` between steps to inspect the durable cursor.

The command starts the Fitz connection required by the event store. It does not start the API, request workers, projectors, or platform-operator bootstrap. It does not run automatically during deployment or startup.

## Check migration state

Run inside the application image with the same Fitz configuration used by the service:

```sh
dotnet Bdgrz.Compliance.App.dll finding-history-migration status \
  --tenant-id <tenant-uuid> \
  --program-id <program-uuid> \
  --migration-id <stable-migration-uuid>
```

The JSON result reports the requested and active migration IDs, whether the fence has started, whether parity is complete, the next source offset, the fence offset, the imported fact count, and the rolling source digest. A different active migration ID is reported as a failure; do not choose a new ID to work around it.

## Advance one step

Use the same stable migration ID for every invocation. The process is authorized internally as `migration:FindingHistoryV1`; no caller-supplied actor or generic system identity is accepted.

```sh
dotnet Bdgrz.Compliance.App.dll finding-history-migration step \
  --tenant-id <tenant-uuid> \
  --program-id <program-uuid> \
  --migration-id <stable-migration-uuid>
```

Repeat `status` and `step` until `isComplete` is true. A successful step prints the updated durable status. If the process exits after an uncertain reply, rerun with the same IDs; the command reads the durable cursor and the handler accepts only the exact latest retry or the next source offset.

## Operational boundary

This runbook documents the command; it does not authorize or perform a production migration. Before an approved execution, verify the tenant and program IDs, confirm the selected migration ID with the service owner, and plan for the source-fence conflict to reject legacy finding writes while migration is in progress. Retain the legacy stream and its readers until deployment-specific parity has been verified.
