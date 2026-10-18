using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Owns one organization's OIDC connection configuration, test, rotation, and disable lifecycle.</summary>
public sealed class TenantIdentityProvider : Aggregate
{
    const int MaximumTests = 100;
    const int MaximumEventBytes = 128 * 1024;
    const int MaximumTextLength = 1000;
    static readonly HashSet<string> CredentialSchemes = new(StringComparer.Ordinal)
        { "vault", "secret", "aws-secretsmanager", "azure-key-vault", "gcp-secret-manager" };
    static readonly Dictionary<string, string> TestMessages = new(StringComparer.Ordinal)
    {
        ["metadata_unavailable"] = "The provider metadata endpoint could not be reached.",
        ["issuer_mismatch"] = "The discovered issuer does not match the configured issuer.",
        ["audience_mismatch"] = "The provider does not accept the configured client audience.",
        ["invalid_client"] = "The provider rejected the configured client credentials.",
        ["jwks_unavailable"] = "The provider signing-key endpoint could not be reached.",
        ["invalid_configuration"] = "The provider metadata is missing required OIDC endpoints.",
    };

    readonly Uuid _tenantId;
    string? _encryptedCredentialReference;
    TenantIdentityProviderView? _current;

    public long Revision => _current?.Revision ?? 0;
    public TenantIdentityProviderView? Current => _current;

    public TenantIdentityProvider(Uuid tenantId, Uuid providerConnectionId)
        : base(providerConnectionId, new EventStreamAddress(tenantId.ToString(), "tenant-identity-providers",
            providerConnectionId.ToString()))
    {
        _tenantId = tenantId;
        On<TenantIdentityProviderConfigured>(Apply);
        On<TenantIdentityProviderTestRequested>(Apply);
        On<TenantIdentityProviderTested>(Apply);
        On<TenantIdentityProviderCredentialsRotated>(Apply);
        On<TenantIdentityProviderDisabled>(Apply);
    }

    public CommandFailure? Configure(TenantIdentityProviderConfigurationContent configuration,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset configuredAt)
    {
        if (_current is { } existing)
            return SameConfiguration(existing, configuration, actorMemberId, actorDisplay, configuredAt)
                ? null : CommandFailure.StateConflict("A tenant identity-provider connection can be configured only once.");
        if (!ValidIdentity() || !ValidActor(actorMemberId, actorDisplay, configuredAt) ||
            NormalizeConfiguration(configuration) is not { } normalized)
            return Invalid("A tenant identity provider requires a secure issuer, matching audience, client ID, and secret-store reference.");
        var state = NewState(normalized, _tenantId, Id, actorMemberId, actorDisplay.Trim(), configuredAt);
        var ev = new TenantIdentityProviderConfigured(_tenantId, Id, state.Revision, actorMemberId,
            normalized, Freeze(state), configuredAt);
        if (!Fits(ev))
            return Invalid("The identity-provider configuration exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? RequestTest(long expectedRevision, string testKey,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset requestedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        testKey = Clean(testKey);
        if (current.PendingTest is { } pending)
            return pending.TestKey == testKey && pending.CredentialRevision == current.CredentialRevision &&
                pending.RequestedByMemberId == actorMemberId &&
                pending.RequestedByDisplay == Clean(actorDisplay)
                ? null : CommandFailure.StateConflict("A different identity-provider test is already pending.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "disabled" || current.TestHistory.Count >= MaximumTests ||
            !Token(testKey, 100) || current.TestHistory.Any(test => test.TestKey == testKey) ||
            !ValidActor(actorMemberId, actorDisplay, requestedAt) || requestedAt < current.UpdatedAt)
            return Invalid("A provider test request requires an active connection, a unique test key, and attributable timing.");

        var request = new TenantIdentityProviderTestRequestView(testKey, current.CredentialRevision,
            actorMemberId, actorDisplay.Trim(), requestedAt);
        var state = State(current, current.Status, current.CredentialRevision, current.TestHistory,
            current.LastRotationReference, current.DisabledRationale, actorMemberId,
            actorDisplay.Trim(), requestedAt, request);
        var ev = new TenantIdentityProviderTestRequested(_tenantId, Id, state.Revision,
            request, Freeze(state), requestedAt);
        if (!Fits(ev))
            return Invalid("The identity-provider test request exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? RecordTest(long expectedRevision, TenantIdentityProviderTestResultContent result,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset testedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        var normalized = NormalizeTestResult(result);
        if (result is not null && current.TestHistory.FirstOrDefault(item => item.TestKey == Clean(result.TestKey)) is { } prior)
            return SameTest(prior, normalized, current.CredentialRevision, actorMemberId, actorDisplay, testedAt)
                ? null : CommandFailure.StateConflict("An identity-provider test key cannot be reused for a different result.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "disabled" || current.TestHistory.Count >= MaximumTests || normalized is null ||
            current.PendingTest is not { } pending || pending.TestKey != normalized.TestKey ||
            pending.CredentialRevision != current.CredentialRevision || testedAt < pending.RequestedAt ||
            !ValidActor(actorMemberId, actorDisplay, testedAt) || testedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("A provider test requires an active connection and a safe, attributable test result.");
        var test = new TenantIdentityProviderTestView(normalized.TestKey, current.CredentialRevision,
            normalized.Outcome, normalized.FailureCode, normalized.ActionableMessage,
            actorMemberId, actorDisplay.Trim(), testedAt);
        var history = Append(current.TestHistory, test);
        var state = State(current, normalized.Outcome == "passed" ? "enabled" : "test_failed",
            current.CredentialRevision, history, current.LastRotationReference, null,
            actorMemberId, actorDisplay.Trim(), testedAt);
        var ev = new TenantIdentityProviderTested(_tenantId, Id, state.Revision, actorMemberId,
            normalized, Freeze(state), testedAt);
        if (!Fits(ev))
            return Invalid("The identity-provider test history exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? RotateCredentials(long expectedRevision, string encryptedCredentialReference,
        string rotationReference, Uuid actorMemberId, string actorDisplay, DateTimeOffset rotatedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        encryptedCredentialReference = Clean(encryptedCredentialReference);
        rotationReference = Clean(rotationReference);
        if (current.LastRotationReference == rotationReference)
            return _encryptedCredentialReference == encryptedCredentialReference &&
                current.ActorMemberId == actorMemberId && current.ActorDisplay == Clean(actorDisplay) &&
                current.UpdatedAt == rotatedAt
                ? null : CommandFailure.StateConflict("A credential-rotation reference cannot be reused.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status == "disabled" || current.CredentialRevision >= MaximumTests ||
            !ValidCredentialReference(encryptedCredentialReference) ||
            encryptedCredentialReference == _encryptedCredentialReference || !Token(rotationReference, 200) ||
            !ValidActor(actorMemberId, actorDisplay, rotatedAt) || rotatedAt < current.UpdatedAt)
            return CommandFailure.StateConflict("Credential rotation requires a new secret-store reference and an enabled connection lifecycle.");
        var credentialRevision = current.CredentialRevision + 1;
        var state = State(current, "pending_test", credentialRevision, current.TestHistory,
            rotationReference, null, actorMemberId, actorDisplay.Trim(), rotatedAt);
        var ev = new TenantIdentityProviderCredentialsRotated(_tenantId, Id, state.Revision,
            actorMemberId, encryptedCredentialReference, rotationReference, Freeze(state), rotatedAt);
        if (!Fits(ev))
            return Invalid("The credential-rotation event exceeds the bounded payload.");
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? Disable(long expectedRevision, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset disabledAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        rationale = Clean(rationale);
        if (current.Status == "disabled")
            return current.DisabledRationale == rationale && current.ActorMemberId == actorMemberId &&
                current.ActorDisplay == Clean(actorDisplay) && current.UpdatedAt == disabledAt
                ? null : CommandFailure.StateConflict("A disabled identity-provider connection cannot be changed.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (!Bounded(rationale, 1, MaximumTextLength) || !ValidActor(actorMemberId, actorDisplay, disabledAt) ||
            disabledAt < current.UpdatedAt)
            return Invalid("Disabling an identity-provider connection requires an attributable rationale.");
        var state = State(current, "disabled", current.CredentialRevision, current.TestHistory,
            current.LastRotationReference, rationale, actorMemberId, actorDisplay.Trim(), disabledAt);
        var ev = new TenantIdentityProviderDisabled(_tenantId, Id, state.Revision,
            actorMemberId, rationale, Freeze(state), disabledAt);
        if (!Fits(ev))
            return Invalid("The disabled identity-provider event exceeds the bounded payload.");
        RaiseEvent(ev);
        return null;
    }

    void Apply(TenantIdentityProviderConfigured ev)
    {
        if (_current is not null || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.Revision != 1 || ev.State is null ||
            NormalizeConfiguration(ev.Configuration) is not { } configuration ||
            !ValidCredentialReference(configuration.EncryptedCredentialReference) ||
            !ValidState(ev.State, _tenantId, Id) || ev.State.Status != "pending_test" ||
            ev.State.PendingTest is not null ||
            ev.State.Revision != ev.Revision || ev.State.CredentialRevision != 1 ||
            !SamePublicConfiguration(ev.State, configuration, ev.ActorMemberId ?? Uuid.Empty,
                ev.State.ActorDisplay, ev.OccurredAt) || ev.State.ActorMemberId != ev.ActorMemberId ||
            ev.State.UpdatedAt != ev.OccurredAt)
            throw new InvalidOperationException("The tenant identity-provider configuration event is invalid.");
        _encryptedCredentialReference = configuration.EncryptedCredentialReference;
        _current = Freeze(ev.State);
    }

    void Apply(TenantIdentityProviderTestRequested ev)
    {
        if (_current is not { } current || current.Status == "disabled" ||
            ev.TenantId != _tenantId || ev.SubjectId != Id || ev.Revision != Revision + 1 ||
            ev.State is null || ev.Request is null || ev.OccurredAt < current.UpdatedAt ||
            ev.Request.CredentialRevision != current.CredentialRevision ||
            current.TestHistory.Count >= MaximumTests ||
            current.TestHistory.Any(test => test.TestKey == ev.Request.TestKey) ||
            !ValidTestRequest(ev.Request, current, ev.OccurredAt) ||
            !ValidState(ev.State, _tenantId, Id) || ev.State.Revision != ev.Revision ||
            ev.State.Status != current.Status || ev.State.CredentialRevision != current.CredentialRevision ||
            ev.State.UpdatedAt != ev.OccurredAt || ev.State.PendingTest != ev.Request ||
            ev.State.TestHistory.Count != current.TestHistory.Count ||
            !HistoryPrefix(current.TestHistory, ev.State.TestHistory) ||
            !SameProviderDefinition(current, ev.State) ||
            ev.State.LastRotationReference != current.LastRotationReference ||
            ev.State.DisabledRationale != current.DisabledRationale ||
            ev.State.ActorMemberId != ev.Request.RequestedByMemberId ||
            ev.State.ActorDisplay != ev.Request.RequestedByDisplay)
            throw new InvalidOperationException("The tenant identity-provider test-request event is invalid.");
        _current = Freeze(ev.State);
    }

    void Apply(TenantIdentityProviderTested ev)
    {
        if (_current is not { } current || current.Status == "disabled" || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.Revision != Revision + 1 || ev.State is null || ev.ActorMemberId == Uuid.Empty ||
            current.PendingTest is not { } pending ||
            NormalizeTestResult(ev.Result) is not { } result || !ValidState(ev.State, _tenantId, Id) ||
            ev.State.Revision != ev.Revision || ev.State.UpdatedAt != ev.OccurredAt || ev.OccurredAt < current.UpdatedAt ||
            ev.OccurredAt < pending.RequestedAt || result.TestKey != pending.TestKey ||
            ev.State.PendingTest is not null ||
            ev.State.CredentialRevision != current.CredentialRevision ||
            !SameProviderDefinition(current, ev.State) ||
            ev.State.LastRotationReference != current.LastRotationReference ||
            ev.State.DisabledRationale is not null || ev.State.ActorMemberId != ev.ActorMemberId ||
            ev.State.TestHistory.Count != current.TestHistory.Count + 1 ||
            !HistoryPrefix(current.TestHistory, ev.State.TestHistory) ||
            ev.State.TestHistory[^1] is not { } test || test.TestKey != result.TestKey ||
            test.Outcome != result.Outcome || test.FailureCode != result.FailureCode ||
            test.ActionableMessage != result.ActionableMessage || test.CredentialRevision != current.CredentialRevision ||
            test.TestedByMemberId != ev.ActorMemberId || test.TestedByDisplay != ev.State.ActorDisplay ||
            test.TestedAt != ev.OccurredAt || ev.State.Status != (result.Outcome == "passed" ? "enabled" : "test_failed"))
            throw new InvalidOperationException("The tenant identity-provider test event is invalid.");
        _current = Freeze(ev.State);
    }

    void Apply(TenantIdentityProviderCredentialsRotated ev)
    {
        if (_current is not { } current || current.Status == "disabled" || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.Revision != Revision + 1 || ev.State is null || ev.ActorMemberId == Uuid.Empty ||
            !ValidCredentialReference(ev.EncryptedCredentialReference) ||
            ev.EncryptedCredentialReference == _encryptedCredentialReference || !Token(ev.RotationReference, 200) ||
            !ValidState(ev.State, _tenantId, Id) || ev.State.Revision != ev.Revision ||
            ev.State.CredentialRevision != current.CredentialRevision + 1 || ev.State.Status != "pending_test" ||
            ev.State.LastRotationReference != ev.RotationReference || ev.State.UpdatedAt != ev.OccurredAt ||
            ev.OccurredAt < current.UpdatedAt ||
            ev.State.PendingTest is not null ||
            ev.State.TestHistory.Count != current.TestHistory.Count || !SameProviderDefinition(current, ev.State) ||
            !HistoryPrefix(current.TestHistory, ev.State.TestHistory) ||
            ev.State.DisabledRationale is not null || ev.State.ActorMemberId != ev.ActorMemberId)
            throw new InvalidOperationException("The tenant identity-provider credential-rotation event is invalid.");
        _encryptedCredentialReference = ev.EncryptedCredentialReference;
        _current = Freeze(ev.State);
    }

    void Apply(TenantIdentityProviderDisabled ev)
    {
        if (_current is not { } current || current.Status == "disabled" || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.Revision != Revision + 1 || ev.State is null || ev.ActorMemberId == Uuid.Empty ||
            !Bounded(ev.Rationale, 1, MaximumTextLength) || !ValidState(ev.State, _tenantId, Id) ||
            ev.State.Revision != ev.Revision || ev.State.Status != "disabled" ||
            ev.State.DisabledRationale != ev.Rationale || ev.State.UpdatedAt != ev.OccurredAt ||
            ev.OccurredAt < current.UpdatedAt ||
            ev.State.PendingTest is not null ||
            ev.State.CredentialRevision != current.CredentialRevision || !SameProviderDefinition(current, ev.State) ||
            ev.State.LastRotationReference != current.LastRotationReference ||
            !HistoryPrefix(current.TestHistory, ev.State.TestHistory) ||
            ev.State.ActorMemberId != ev.ActorMemberId)
            throw new InvalidOperationException("The tenant identity-provider disable event is invalid.");
        _current = Freeze(ev.State);
    }

    bool ValidIdentity() => _tenantId != Uuid.Empty && Id != Uuid.Empty;

    static TenantIdentityProviderConfigurationContent? NormalizeConfiguration(
        TenantIdentityProviderConfigurationContent? configuration)
    {
        if (configuration is null || !Bounded(configuration.Issuer, 1, 500) ||
            !Uri.TryCreate(configuration.Issuer.Trim(), UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(issuer.Host) ||
            !string.IsNullOrEmpty(issuer.UserInfo) || !string.IsNullOrEmpty(issuer.Query) ||
            !string.IsNullOrEmpty(issuer.Fragment) || !Bounded(configuration.ClientId, 1, 300) ||
            !ValidCredentialReference(configuration.EncryptedCredentialReference) ||
            configuration.AllowedAudiences is null || configuration.AllowedAudiences.Count is < 1 or > 16 ||
            configuration.AllowedAudiences.Any(item => !Bounded(item, 1, 500)) ||
            configuration.AllowedAudiences.Select(item => item.Trim()).Distinct(StringComparer.Ordinal).Count() !=
                configuration.AllowedAudiences.Count ||
            !configuration.AllowedAudiences.Any(item => item.Trim() == configuration.ClientId.Trim()))
            return null;
        return new TenantIdentityProviderConfigurationContent(configuration.Issuer.Trim(),
            configuration.ClientId.Trim(), configuration.EncryptedCredentialReference.Trim(),
            Freeze(configuration.AllowedAudiences.Select(item => item.Trim())
                .OrderBy(item => item, StringComparer.Ordinal).ToArray()));
    }

    static TenantIdentityProviderTestResultContent? NormalizeTestResult(
        TenantIdentityProviderTestResultContent? result)
    {
        if (result is null || !Token(result.TestKey, 100))
            return null;
        var outcome = Clean(result.Outcome).ToLowerInvariant();
        if (outcome == "passed")
            return result.FailureCode is null && result.ActionableMessage is null
                ? result with { TestKey = result.TestKey.Trim(), Outcome = outcome }
                : null;
        var code = Clean(result.FailureCode).ToLowerInvariant();
        return outcome == "failed" && TestMessages.TryGetValue(code, out var message) &&
            result.ActionableMessage == message
            ? result with { TestKey = result.TestKey.Trim(), Outcome = outcome, FailureCode = code }
            : null;
    }

    static bool ValidState(TenantIdentityProviderView state, Uuid tenantId, Uuid providerId)
    {
        if (state.TenantId != tenantId || state.ProviderConnectionId != providerId || state.Revision <= 0 ||
            state.CredentialRevision <= 0 || NormalizeConfiguration(new TenantIdentityProviderConfigurationContent(
                state.Issuer, state.ClientId, "vault://internal/validation-only", state.AllowedAudiences)) is null ||
            state.TestHistory is null || state.TestHistory.Count > MaximumTests ||
            state.TestHistory.Any(item => !ValidTestView(item, state)) ||
            state.TestHistory.Select(item => item.TestKey).Distinct(StringComparer.Ordinal).Count() != state.TestHistory.Count ||
            state.Status is not ("pending_test" or "enabled" or "test_failed" or "disabled") ||
            state.Status == "enabled" && !HasCurrentTestOutcome(state, "passed") ||
            state.Status == "test_failed" && !HasCurrentTestOutcome(state, "failed") ||
            state.Status == "pending_test" && state.TestHistory.Count > 0 &&
                state.TestHistory[^1] is { } latest &&
                latest.CredentialRevision == state.CredentialRevision ||
            (state.Status == "disabled") != (state.DisabledRationale is not null) ||
            state.DisabledRationale is not null && !Bounded(state.DisabledRationale, 1, MaximumTextLength) ||
            state.LastRotationReference is not null && !Token(state.LastRotationReference, 200) ||
            state.PendingTest is not null && !ValidTestRequest(state.PendingTest, state, state.UpdatedAt) ||
            state.PendingTest is not null && state.Status == "disabled" ||
            state.PendingTest is not null && state.TestHistory.Any(test => test.TestKey == state.PendingTest.TestKey) ||
            !ValidActor(state.ConfiguredByMemberId, state.ConfiguredByDisplay, state.ConfiguredAt) ||
            !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt) ||
            state.ContentSha256 != StateDigest(state with { ContentSha256 = string.Empty }))
            return false;
        return true;
    }

    static bool ValidTestView(TenantIdentityProviderTestView test, TenantIdentityProviderView state) =>
        NormalizeTestResult(new TenantIdentityProviderTestResultContent(test.TestKey, test.Outcome,
            test.FailureCode, test.ActionableMessage)) is not null &&
        test.CredentialRevision > 0 && test.CredentialRevision <= state.CredentialRevision &&
        ValidActor(test.TestedByMemberId, test.TestedByDisplay, test.TestedAt) &&
        test.TestedAt >= state.ConfiguredAt;

    static bool ValidTestRequest(TenantIdentityProviderTestRequestView request,
        TenantIdentityProviderView state, DateTimeOffset occurredAt) =>
        Token(request.TestKey, 100) && request.CredentialRevision == state.CredentialRevision &&
        ValidActor(request.RequestedByMemberId, request.RequestedByDisplay, request.RequestedAt) &&
        request.RequestedAt >= state.ConfiguredAt && request.RequestedAt <= occurredAt;

    static bool HasCurrentTestOutcome(TenantIdentityProviderView state, string outcome) =>
        state.TestHistory.Count > 0 && state.TestHistory[^1] is { } latest &&
        latest.CredentialRevision == state.CredentialRevision &&
        latest.Outcome == outcome;

    static bool SameProviderDefinition(TenantIdentityProviderView prior, TenantIdentityProviderView next) =>
        prior.TenantId == next.TenantId && prior.ProviderConnectionId == next.ProviderConnectionId &&
        prior.Issuer == next.Issuer && prior.ClientId == next.ClientId &&
        prior.AllowedAudiences.SequenceEqual(next.AllowedAudiences) &&
        prior.ConfiguredByMemberId == next.ConfiguredByMemberId &&
        prior.ConfiguredByDisplay == next.ConfiguredByDisplay && prior.ConfiguredAt == next.ConfiguredAt;

    static bool HistoryPrefix(IReadOnlyList<TenantIdentityProviderTestView> prior,
        IReadOnlyList<TenantIdentityProviderTestView> next) => prior.Count <= next.Count &&
        prior.Zip(next).All(pair => Digest(pair.First) == Digest(pair.Second));

    static TenantIdentityProviderView NewState(TenantIdentityProviderConfigurationContent configuration,
        Uuid tenantId, Uuid providerId, Uuid actor, string display, DateTimeOffset at)
    {
        var state = new TenantIdentityProviderView(tenantId, providerId, "pending_test", 1, 1,
            configuration.Issuer, configuration.ClientId, configuration.AllowedAudiences, [], null,
            null, actor, display, at, actor, display, at, string.Empty, null);
        return state with { ContentSha256 = StateDigest(state) };
    }

    static TenantIdentityProviderView State(TenantIdentityProviderView current, string status,
        long credentialRevision, IReadOnlyList<TenantIdentityProviderTestView> testHistory,
        string? rotationReference, string? disabledRationale, Uuid actor, string display, DateTimeOffset at,
        TenantIdentityProviderTestRequestView? pendingTest = null)
    {
        var state = current with
        {
            Status = status,
            Revision = current.Revision + 1,
            CredentialRevision = credentialRevision,
            TestHistory = Freeze(testHistory.ToArray()),
            LastRotationReference = rotationReference,
            DisabledRationale = disabledRationale,
            ActorMemberId = actor,
            ActorDisplay = display,
            UpdatedAt = at,
            PendingTest = pendingTest,
            ContentSha256 = string.Empty,
        };
        return state with { ContentSha256 = StateDigest(state) };
    }

    bool SameConfiguration(TenantIdentityProviderView state,
        TenantIdentityProviderConfigurationContent? configuration, Uuid actor, string display, DateTimeOffset at) =>
        NormalizeConfiguration(configuration) is { } normalized &&
        normalized.EncryptedCredentialReference == _encryptedCredentialReference &&
        SamePublicConfiguration(state, normalized, actor, display, at);

    static bool SamePublicConfiguration(TenantIdentityProviderView state,
        TenantIdentityProviderConfigurationContent? configuration, Uuid actor, string display, DateTimeOffset at) =>
        NormalizeConfiguration(configuration) is { } normalized && state.Revision == 1 &&
        state.Issuer == normalized.Issuer && state.ClientId == normalized.ClientId &&
        state.AllowedAudiences.SequenceEqual(normalized.AllowedAudiences) &&
        state.ConfiguredByMemberId == actor && state.ConfiguredByDisplay == Clean(display) && state.ConfiguredAt == at;

    static bool SameTest(TenantIdentityProviderTestView prior, TenantIdentityProviderTestResultContent? result,
        long credentialRevision, Uuid actor, string display, DateTimeOffset at) => result is not null &&
        prior.TestKey == result.TestKey && prior.CredentialRevision == credentialRevision &&
        prior.Outcome == result.Outcome && prior.FailureCode == result.FailureCode &&
        prior.ActionableMessage == result.ActionableMessage && prior.TestedByMemberId == actor &&
        prior.TestedByDisplay == Clean(display) && prior.TestedAt == at;

    static bool ValidCredentialReference(string? value) => Bounded(value, 1, 1000) &&
        Uri.TryCreate(value!.Trim(), UriKind.Absolute, out var reference) &&
        CredentialSchemes.Contains(reference.Scheme) && !string.IsNullOrWhiteSpace(reference.Host) &&
        string.IsNullOrEmpty(reference.Query) && string.IsNullOrEmpty(reference.Fragment) &&
        string.IsNullOrEmpty(reference.UserInfo);

    static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value).Length <= MaximumEventBytes;
    static string StateDigest(TenantIdentityProviderView state)
    {
        var legacyContent = new
        {
            state.TenantId,
            state.ProviderConnectionId,
            state.Status,
            state.Revision,
            state.CredentialRevision,
            state.Issuer,
            state.ClientId,
            state.AllowedAudiences,
            state.TestHistory,
            state.LastRotationReference,
            state.DisabledRationale,
            state.ConfiguredByMemberId,
            state.ConfiguredByDisplay,
            state.ConfiguredAt,
            state.ActorMemberId,
            state.ActorDisplay,
            state.UpdatedAt,
        };
        if (state.PendingTest is null)
            return Digest(legacyContent);
        return Digest(new
        {
            state.TenantId,
            state.ProviderConnectionId,
            state.Status,
            state.Revision,
            state.CredentialRevision,
            state.Issuer,
            state.ClientId,
            state.AllowedAudiences,
            state.TestHistory,
            state.LastRotationReference,
            state.DisabledRationale,
            state.ConfiguredByMemberId,
            state.ConfiguredByDisplay,
            state.ConfiguredAt,
            state.ActorMemberId,
            state.ActorDisplay,
            state.UpdatedAt,
            state.PendingTest,
        });
    }
    static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int min, int max) => value is not null && value.Trim().Length >= min &&
        value.Trim().Length <= max && !value.Any(char.IsControl);
    static bool Token(string? value, int max) => Bounded(value, 1, max) && value!.Trim().All(character =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty && Bounded(display, 1, 200) && at != default;
    static TenantIdentityProviderView Freeze(TenantIdentityProviderView state) => state with
    { AllowedAudiences = Freeze(state.AllowedAudiences.ToArray()), TestHistory = Freeze(state.TestHistory.ToArray()) };
    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("tenant identity provider", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T item) => Freeze(values.Append(item).ToArray());
}
