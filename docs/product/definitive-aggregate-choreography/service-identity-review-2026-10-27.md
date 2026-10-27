# ServiceIdentity aggregate review

ServiceIdentity owns one tenant-scoped, caller-identified non-human identity. M0-D06 requires one accountable person or team, an approved purpose, and an annual review date; the catalog also constrains identity kind, lifecycle, expiry, and canonical terms. The existing command methods enforce those rules and exact expected revision for revisions.

The prior reducers trusted `ServiceIdentityRecorded` and `ServiceIdentityRevised` payloads. Replay now checks tenant and identity IDs, rejects duplicate creation and revisions without a creation, requires contiguous revision numbers, and validates canonical terms against the UTC date of each fact. The owner remains a single identity stream; accountable-owner existence is checked by the existing handler against the authoritative workforce owners.

Validation: `dotnet build src/Compliance.Core/Compliance.Core.csproj -c Release --no-restore --nologo` passed with 0 warnings and errors; whitespace verification passed. No tests were added or run for this slice. Focused malformed-history tests for wrong tenant/identity, duplicate creation, revision gaps, and invalid terms remain necessary before calling this aggregate complete. No event names or contracts changed.
