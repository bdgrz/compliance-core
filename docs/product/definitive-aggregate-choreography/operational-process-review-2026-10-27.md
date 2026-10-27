# OperationalProcess aggregate review

The dependency-ordered backlog places `OperationalProcess` after `Location`. The selected owner represents one tenant's operational process and owns its independent content/version lifecycle. The catalog requires each revision to match the prior revision; tenant identity is carried by `OperationalProcessRevisionRecorded`.

`OperationalProcess.ObserveOperationalProcessRevision` checks the fact tenant against the tenant selected when opening the owner, then delegates revision and pending-change validation to `NamedRecord`. This preserves the selected tenant boundary without changing the shared named-record abstraction. The existing name-change flow also remains responsible for active name uniqueness.

Jev 1.14.0 judged that rejecting a revision fact from another tenant is supported by this contract with Noul 0.96. This is advisory. The exact [request](jev-review-2026-10-27-operational-process/request.json) and [response](jev-review-2026-10-27-operational-process/response.json) are retained.

Validation: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~NamedRecordProcessTests'` passed 22/22. No malformed-history test was added. This focused review does not qualify every cataloged lifecycle or the full choreography.
