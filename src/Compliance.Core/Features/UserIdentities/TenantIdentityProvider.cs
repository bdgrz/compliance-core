using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Owns one organization's OIDC connection: its configuration, the credential epoch and the decision to enable it
///     after a successful test of the current epoch. Test and rotation history is not retained here: a test request is
///     an independently identified native operation, so neither the number of tests nor the number of credential
///     rotations has a lifetime ceiling, and every fact stays a bounded delta.
/// </summary>
public sealed class TenantIdentityProvider : Aggregate
{
    const int MaximumEventBytes = DomainEventPayload.MaximumBytes;
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
    bool _configured;
    string _status = "unconfigured";
    long _credentialRevision;
    string _issuer = string.Empty;
    string _clientId = string.Empty;
    IReadOnlyList<string> _audiences = [];
    ImmutableContentRef? _credential;
    string? _lastRotationReference;
    string? _disabledRationale;
    DateTimeOffset _updatedAt;
    PendingTest? _pending;

    /// <summary>Hydrates the connection of a tenant.</summary>
    public TenantIdentityProvider(Uuid tenantId, Uuid providerConnectionId)
        : this(tenantId, providerConnectionId, Uuid.Empty, null)
    {
    }

    /// <summary>
    ///     Hydrates the connection for one logical operation: <paramref name="selectedLogicalId" /> and
    ///     <paramref name="selectedCommandKind" /> select the accepted fact that retry resolution compares against.
    /// </summary>
    public TenantIdentityProvider(Uuid tenantId, Uuid providerConnectionId, Uuid selectedLogicalId,
        string? selectedCommandKind)
        : base(providerConnectionId, new EventStreamAddress(tenantId.ToString(), "tenant-identity-providers",
            providerConnectionId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId, selectedCommandKind);
        On<TenantIdentityProviderConfigured>(Apply);
        On<TenantIdentityProviderTestRequested>(Apply);
        On<TenantIdentityProviderTested>(Apply);
        On<TenantIdentityProviderCredentialsRotated>(Apply);
        On<TenantIdentityProviderDisabled>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The logical revision of the connection: the number of facts it holds.</summary>
    public long Revision => (long)Version;

    /// <summary>True once the connection was configured.</summary>
    public bool IsConfigured => _configured;

    /// <summary>"pending_test", "enabled", "test_failed" or "disabled"; "unconfigured" before the first fact.</summary>
    public string Status => _status;

    /// <summary>The credential epoch, starting at one and advancing with every rotation.</summary>
    public long CredentialRevision => _credentialRevision;

    /// <summary>The configured issuer.</summary>
    public string Issuer => _issuer;

    /// <summary>The configured client id.</summary>
    public string ClientId => _clientId;

    /// <summary>The configured audiences, sorted.</summary>
    public IReadOnlyList<string> AllowedAudiences => _audiences;

    /// <summary>The protected content of the current credential epoch's secret-store pointer.</summary>
    public ImmutableContentRef? Credential => _credential;

    /// <summary>The rotation reference of the latest rotation.</summary>
    public string? LastRotationReference => _lastRotationReference;

    /// <summary>Why the connection was disabled.</summary>
    public string? DisabledRationale => _disabledRationale;

    /// <summary>The key of the test awaiting its result, if any.</summary>
    public string? PendingTestKey => _pending?.Fact.TestKey;

    /// <summary>The credential epoch the pending test targets.</summary>
    public long? PendingTestCredentialRevision => _pending?.Fact.CredentialRevision;

    /// <summary>The event id of the pending test request, once it is a stored fact.</summary>
    public Uuid? PendingTestRequestEventId => _pending?.Fact.Metadata?.EventId;

    /// <summary>The member who requested the pending test.</summary>
    public Uuid? PendingTestRequestedBy => _pending?.Fact.RequestedByMemberId;

    /// <summary>Validates and normalizes a connection definition; null when it is not acceptable.</summary>
    public static TenantIdentityProviderConfiguration? NormalizeConfiguration(string? issuer, string? clientId,
        string? credentialReference, IReadOnlyList<string>? audiences)
    {
        if (!Bounded(issuer, 1, 500) || !Uri.TryCreate(issuer!.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || !Bounded(clientId, 1, 300) ||
            !ValidCredentialReference(credentialReference) ||
            audiences is null || audiences.Count is < 1 or > 16 || audiences.Any(item => !Bounded(item, 1, 500)) ||
            audiences.Select(item => item.Trim()).Distinct(StringComparer.Ordinal).Count() != audiences.Count ||
            !audiences.Any(item => item.Trim() == clientId!.Trim()))
            return null;
        return new TenantIdentityProviderConfiguration(issuer.Trim(), clientId!.Trim(),
            Freeze(audiences.Select(item => item.Trim()).OrderBy(item => item, StringComparer.Ordinal).ToArray()),
            credentialReference!.Trim());
    }

    /// <summary>True for a secret-store pointer of an accepted scheme.</summary>
    public static bool ValidCredentialReference(string? value) => Bounded(value, 1, 1000) &&
        Uri.TryCreate(value!.Trim(), UriKind.Absolute, out var reference) &&
        CredentialSchemes.Contains(reference.Scheme) && !string.IsNullOrWhiteSpace(reference.Host) &&
        string.IsNullOrEmpty(reference.Query) && string.IsNullOrEmpty(reference.Fragment) &&
        string.IsNullOrEmpty(reference.UserInfo);

    /// <summary>Validates and normalizes a test result; null when it is not a safe, known result.</summary>
    public static TenantIdentityProviderTestResultContent? NormalizeTestResult(
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

    /// <summary>Checks a configuration against hydrated state without raising anything.</summary>
    public CommandFailure? PrepareConfigure(long expectedRevision, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset configuredAt)
    {
        if (_configured)
            return CommandFailure.StateConflict("A tenant identity-provider connection can be configured only once.");
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || !ValidActor(actorMemberId, actorDisplay, configuredAt))
            return Invalid("A tenant identity provider requires a tenant, a connection and an attributable actor.");
        return expectedRevision != Revision ? StaleRevision() : null;
    }

    /// <summary>Accepts the configuration as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? Configure(OperationProvenance operation, ImmutableContentRef firstResponse,
        long expectedRevision, TenantIdentityProviderConfiguration configuration, ImmutableContentRef credential,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset configuredAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (PrepareConfigure(expectedRevision, actorMemberId, actorDisplay, configuredAt) is { } failure)
            return failure;
        var fact = new TenantIdentityProviderConfigured(_tenantId, Id, configuration.Issuer, configuration.ClientId,
            configuration.AllowedAudiences, credential, actorMemberId, actorDisplay.Trim(), configuredAt, operation,
            firstResponse, null);
        if (!Fits(fact))
            return Invalid("The identity-provider configuration exceeds the bounded event payload.");
        RaiseEvent(fact);
        return null;
    }

    /// <summary>Checks a test request against hydrated state without raising anything.</summary>
    public CommandFailure? PrepareRequestTest(long expectedRevision, string testKey, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset requestedAt)
    {
        if (!_configured)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        if (_pending is not null)
            return CommandFailure.StateConflict("A different identity-provider test is already pending.");
        if (expectedRevision != Revision)
            return StaleRevision();
        return _status == "disabled" || !Token(testKey, 100) || testKey != testKey.Trim() ||
               !ValidActor(actorMemberId, actorDisplay, requestedAt) || requestedAt < _updatedAt
            ? Invalid("A provider test request requires an active connection, a test key and attributable timing.")
            : null;
    }

    /// <summary>Accepts the test request as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? RequestTest(OperationProvenance operation, ImmutableContentRef firstResponse,
        long expectedRevision, string testKey, Uuid actorMemberId, string actorDisplay, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (PrepareRequestTest(expectedRevision, testKey, actorMemberId, actorDisplay, requestedAt) is { } failure)
            return failure;
        RaiseEvent(new TenantIdentityProviderTestRequested(_tenantId, Id, testKey, _credentialRevision,
            actorMemberId, actorDisplay.Trim(), requestedAt, operation, firstResponse, null));
        return null;
    }

    /// <summary>
    ///     Checks a worker's result against hydrated state without raising anything. The result must answer the exact
    ///     pending request of the current credential epoch at the revision the request was accepted at.
    /// </summary>
    public CommandFailure? PrepareRecordTest(long expectedRevision, Uuid testRequestEventId,
        TenantIdentityProviderTestResultContent? result, ActorReference testedBy, DateTimeOffset testedAt)
    {
        if (!_configured)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        var normalized = NormalizeTestResult(result);
        if (_status == "disabled" || normalized is null || testRequestEventId == Uuid.Empty ||
            expectedRevision != Revision || _pending is not { } pending || pending.Fact.TestKey != normalized.TestKey ||
            pending.Fact.Metadata?.EventId != testRequestEventId || pending.Fact.CredentialRevision != _credentialRevision ||
            testedAt < pending.Fact.RequestedAt || !ValidTester(testedBy, testedAt) || testedAt < _updatedAt)
            return CommandFailure.StateConflict(
                "A provider test requires an active connection and a safe, attributable result of the exact pending request.");
        return null;
    }

    /// <summary>Accepts the result as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? RecordTest(OperationProvenance operation, ImmutableContentRef firstResponse,
        long expectedRevision, Uuid testRequestEventId, TenantIdentityProviderTestResultContent result,
        ActorReference testedBy, DateTimeOffset testedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (PrepareRecordTest(expectedRevision, testRequestEventId, result, testedBy, testedAt) is { } failure)
            return failure;
        RaiseEvent(new TenantIdentityProviderTested(_tenantId, Id, testRequestEventId, _credentialRevision, testedBy,
            NormalizeTestResult(result)!, testedAt, operation, firstResponse, null));
        return null;
    }

    /// <summary>Checks a credential rotation against hydrated state without raising anything.</summary>
    public CommandFailure? PrepareRotate(long expectedRevision, ImmutableContentRef credential, string rotationReference,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset rotatedAt)
    {
        if (!_configured)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        rotationReference = Clean(rotationReference);
        if (_status == "disabled" || credential.Sha256 == _credential?.Sha256 || !Token(rotationReference, 200) ||
            rotationReference == _lastRotationReference || !ValidActor(actorMemberId, actorDisplay, rotatedAt) ||
            rotatedAt < _updatedAt)
            return CommandFailure.StateConflict(
                "Credential rotation requires a new secret-store reference, a new rotation reference and an enabled connection lifecycle.");
        return null;
    }

    /// <summary>Accepts the rotation as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? RotateCredentials(OperationProvenance operation, ImmutableContentRef firstResponse,
        long expectedRevision, ImmutableContentRef credential, string rotationReference, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset rotatedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (PrepareRotate(expectedRevision, credential, rotationReference, actorMemberId, actorDisplay, rotatedAt)
            is { } failure)
            return failure;
        RaiseEvent(new TenantIdentityProviderCredentialsRotated(_tenantId, Id, credential, Clean(rotationReference),
            _credentialRevision + 1, actorMemberId, actorDisplay.Trim(), rotatedAt, operation, firstResponse, null));
        return null;
    }

    /// <summary>Checks a disable against hydrated state without raising anything.</summary>
    public CommandFailure? PrepareDisable(long expectedRevision, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset disabledAt)
    {
        if (!_configured)
            return CommandFailure.MissingRecord("The tenant identity-provider connection was not found.");
        if (_status == "disabled")
            return CommandFailure.StateConflict("A disabled identity-provider connection cannot be changed.");
        if (expectedRevision != Revision)
            return StaleRevision();
        return !Bounded(Clean(rationale), 1, MaximumTextLength) || !ValidActor(actorMemberId, actorDisplay, disabledAt) ||
               disabledAt < _updatedAt
            ? Invalid("Disabling an identity-provider connection requires an attributable rationale.")
            : null;
    }

    /// <summary>Accepts the disable as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? Disable(OperationProvenance operation, ImmutableContentRef firstResponse,
        long expectedRevision, string rationale, Uuid actorMemberId, string actorDisplay, DateTimeOffset disabledAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (PrepareDisable(expectedRevision, rationale, actorMemberId, actorDisplay, disabledAt) is { } failure)
            return failure;
        RaiseEvent(new TenantIdentityProviderDisabled(_tenantId, Id, Clean(rationale), actorMemberId,
            actorDisplay.Trim(), disabledAt, operation, firstResponse, null));
        return null;
    }

    void Apply(TenantIdentityProviderConfigured ev)
    {
        Acceptance.Observe(ev);
        if (_configured || ev.TenantId != _tenantId || ev.ProviderConnectionId != Id ||
            NormalizeConfiguration(ev.Issuer, ev.ClientId, "vault://internal/validation-only", ev.AllowedAudiences)
                is not { } configuration || ev.Operation.Owner != Stream ||
            !ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.ConfiguredAt))
            throw new InvalidOperationException("The tenant identity-provider configuration event is invalid.");
        _configured = true;
        _status = "pending_test";
        _credentialRevision = 1;
        _issuer = configuration.Issuer;
        _clientId = configuration.ClientId;
        _audiences = configuration.AllowedAudiences;
        _credential = ev.CredentialReference;
        _updatedAt = ev.ConfiguredAt;
    }

    void Apply(TenantIdentityProviderTestRequested ev)
    {
        Acceptance.Observe(ev);
        if (!_configured || _status == "disabled" || _pending is not null || ev.TenantId != _tenantId ||
            ev.ProviderConnectionId != Id || ev.CredentialRevision != _credentialRevision ||
            !Token(ev.TestKey, 100) || !ValidActor(ev.RequestedByMemberId, ev.RequestedByDisplay, ev.RequestedAt) ||
            ev.RequestedAt < _updatedAt)
            throw new InvalidOperationException("The tenant identity-provider test-request event is invalid.");
        _pending = new PendingTest(ev);
        _updatedAt = ev.RequestedAt;
    }

    void Apply(TenantIdentityProviderTested ev)
    {
        Acceptance.Observe(ev);
        if (!_configured || _status == "disabled" || _pending is not { } pending || ev.TenantId != _tenantId ||
            ev.ProviderConnectionId != Id || ev.CredentialRevision != _credentialRevision ||
            NormalizeTestResult(ev.Result) is not { } result || result.TestKey != pending.Fact.TestKey ||
            !ValidTester(ev.TestedByActor, ev.TestedAt) || ev.TestedAt < pending.Fact.RequestedAt ||
            ev.TestedAt < _updatedAt)
            throw new InvalidOperationException("The tenant identity-provider test event is invalid.");
        _status = result.Outcome == "passed" ? "enabled" : "test_failed";
        _pending = null;
        _updatedAt = ev.TestedAt;
    }

    void Apply(TenantIdentityProviderCredentialsRotated ev)
    {
        Acceptance.Observe(ev);
        if (!_configured || _status == "disabled" || ev.TenantId != _tenantId || ev.ProviderConnectionId != Id ||
            ev.CredentialRevision != _credentialRevision + 1 || !Token(ev.RotationReference, 200) ||
            !ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.RotatedAt) || ev.RotatedAt < _updatedAt)
            throw new InvalidOperationException("The tenant identity-provider credential-rotation event is invalid.");
        _status = "pending_test";
        _credentialRevision = ev.CredentialRevision;
        _credential = ev.CredentialReference;
        _lastRotationReference = ev.RotationReference;
        _pending = null;
        _updatedAt = ev.RotatedAt;
    }

    void Apply(TenantIdentityProviderDisabled ev)
    {
        Acceptance.Observe(ev);
        if (!_configured || _status == "disabled" || ev.TenantId != _tenantId || ev.ProviderConnectionId != Id ||
            !Bounded(ev.Rationale, 1, MaximumTextLength) || !ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.DisabledAt) ||
            ev.DisabledAt < _updatedAt)
            throw new InvalidOperationException("The tenant identity-provider disable event is invalid.");
        _status = "disabled";
        _disabledRationale = ev.Rationale;
        _pending = null;
        _updatedAt = ev.DisabledAt;
    }

    static bool Fits(DomainEvent value) => DomainEventPayload.Size(value) <= MaximumEventBytes;
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int min, int max) => value is not null && value.Trim().Length >= min &&
        value.Trim().Length <= max && !value.Any(char.IsControl);
    static bool Token(string? value, int max) => Bounded(value, 1, max) && value!.Trim().All(character =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty &&
        Bounded(display, 1, 200) && at != default;
    static bool ValidTester(ActorReference? actor, DateTimeOffset at)
    {
        if (actor is null || at == default || !Bounded(actor.Display, 1, 200))
            return false;
        if (actor.Kind == "member")
            return Uuid.TryParse(actor.Id, out var id) && id != Uuid.Empty;
        return actor.Kind == "system_process" && actor.Id.Length is > 8 and <= 200 &&
            actor.Id.StartsWith("reactor:", StringComparison.Ordinal);
    }

    CommandFailure StaleRevision() => CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("tenant identity provider", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);

    sealed record PendingTest(TenantIdentityProviderTestRequested Fact);
}
