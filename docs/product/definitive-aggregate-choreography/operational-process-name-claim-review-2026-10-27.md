# OperationalProcessNameClaim aggregate review

The dependency-ordered backlog places `OperationalProcessNameClaim` after `OperationalProcess` in group 3. The selected identity is `(tenantId, normalizedName)`, and the owner decides authoritative active normalized-name uniqueness.

The owner-specific holder check requires an operational-process stream in the claim's tenant and inventory-registers area, with a non-empty operational-process identity. `Claim` and `Release` enforce this before raising a fact; both reducers enforce it while replaying. The shared `NameClaim` base continues to enforce name occupancy and exact holder-generation rules.

Jev 1.14.0 assigned 0.74 probability that these owner-specific checks belong in `OperationalProcessNameClaim`; this is advisory. The exact [request](jev-review-2026-10-27-operational-process-name-claim/request.json) and [response](jev-review-2026-10-27-operational-process-name-claim/response.json) are retained.

Validation: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~OperationalProcessNameClaimTests'` passed 2/2. The tests cover rejected cross-tenant claim and release requests and confirm that rejection emits no additional fact. No malformed-history test was added. Full catalog and choreography conformance remains unqualified.
