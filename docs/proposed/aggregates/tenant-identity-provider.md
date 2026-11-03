# TenantIdentityProvider

- Classification: `business_aggregate`
- Source area: `UserIdentities` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + ProviderConnectionId with immutable tenant binding. Selected CommandId and TestKey bind exact configuration, test, key and rotation episodes.

**Invariant:** Connection enables only with successful test of current credential epoch; rotation invalidates pending/old result; disabled connection stays terminal.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ConfigureTenantSignInConnection](../commands/tenant-identity-provider.md#configuretenantsigninconnection) | http | CommandId |
| [DisableTenantSignInConnection](../commands/tenant-identity-provider.md#disabletenantsigninconnection) | http | CommandId |
| [RecordSignInConnectionTestResult](../commands/tenant-identity-provider.md#recordsigninconnectiontestresult) | command | CommandId |
| [RotateTenantSignInCredentials](../commands/tenant-identity-provider.md#rotatetenantsignincredentials) | http | CommandId |
| [TestTenantSignInConnection](../commands/tenant-identity-provider.md#testtenantsigninconnection) | http | TestKey |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantIdentityProviderConfigured](../events/tenant-identity-provider.md#tenantidentityproviderconfigured) | bdgrz.useridentities.tenant-identity-provider-configured | 2 |
| [TenantIdentityProviderCredentialsRotated](../events/tenant-identity-provider.md#tenantidentityprovidercredentialsrotated) | bdgrz.useridentities.tenant-identity-provider-credentials-rotated | 2 |
| [TenantIdentityProviderDisabled](../events/tenant-identity-provider.md#tenantidentityproviderdisabled) | bdgrz.useridentities.tenant-identity-provider-disabled | 2 |
| [TenantIdentityProviderTestRequested](../events/tenant-identity-provider.md#tenantidentityprovidertestrequested) | bdgrz.user-identities.tenant-identity-provider-test-requested | 1 |
| [TenantIdentityProviderTested](../events/tenant-identity-provider.md#tenantidentityprovidertested) | bdgrz.useridentities.tenant-identity-provider-tested | 3 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/tenant-identity-provider.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
