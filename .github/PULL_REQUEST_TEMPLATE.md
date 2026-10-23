## Summary

<!-- Issue link, the observable gap, and what this PR changes. One slice per PR. -->

Closes #

## Validation

- [ ] Focused failing test added first: `<TestClass>.<Should...Given...>`
- [ ] `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore`
- [ ] `dotnet build Compliance.slnx -c Release --no-restore`
- [ ] `dotnet test Compliance.slnx -c Release --no-build --no-restore --filter "Category!=BrokerIntegration&Category!=WebIntegration"`
- [ ] `node tools/validate-aggregate-spec.mjs` and `node tools/check-implementation-coverage.mjs`
- [ ] WebIntegration class run locally for route changes; BrokerIntegration class run locally for Fitz adapter changes

## Contract and operations

**Authentication and authorization:** <!-- authorizer, policy, principal; none if unchanged -->

**Wire compatibility:** <!-- operationIds added/removed/changed, new required fields, 200→202 changes, linked client issue; none if unchanged -->

**Persistence:** <!-- new or removed discriminators, stream/KV routes, payload budgets; state when pre-cutover streams need a separate reset authorization -->

**Deployment:** <!-- configuration, hosted services, workers; none if unchanged -->

**Review and Jev evidence:** <!-- adversarial review of the exact head: findings and resolution; Jev preflight model version, probabilities, disagreements and adopted decision -->
