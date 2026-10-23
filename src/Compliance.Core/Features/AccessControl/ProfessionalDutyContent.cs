using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads and writes the immutable content referenced by professional-duty facts.</summary>
static class ProfessionalDutyContent
{
    /// <summary>Realm of every platform professional-duty stream and its content.</summary>
    public const string Realm = "bdgrz";

    /// <summary>Schema of the complete designation view carried by a recorded fact.</summary>
    public const string RecordedSchema = "FirmProfessionalDutyDesignationRecorded.DesignationContent.v1";

    /// <summary>Schema of the complete designation view carried by a revoked fact.</summary>
    public const string RevokedSchema = "FirmProfessionalDutyDesignationRevoked.DesignationContent.v1";

    /// <summary>Schema of the verified assertion frozen at admission.</summary>
    public const string VerifiedScopeSchema = "verified-firm-professional-duty-assertion.v1";

    /// <summary>Schema of the normalized record command.</summary>
    public const string RecordCommandSchema = "record-firm-professional-duty-designation.v1";

    /// <summary>Schema of the normalized revoke command.</summary>
    public const string RevokeCommandSchema = "revoke-firm-professional-duty-designation.v1";

    public static string DesignationSchema(ProfessionalDutyChangeKind kind) =>
        kind == ProfessionalDutyChangeKind.Record ? RecordedSchema : RevokedSchema;

    public static Task<ImmutableContentRef> WriteDesignationAsync(IImmutableContentStore content, Uuid contentId,
        ProfessionalDutyChangeKind kind, FirmProfessionalDutyDesignationView view, CancellationToken ct) =>
        content.WriteJsonAsync(Realm, contentId, DesignationSchema(kind), view, ct);

    public static Task<ImmutableContentRef> WriteResultAsync(IImmutableContentStore content, Uuid contentId,
        ProfessionalDutyChangeKind kind, ImmutableContentRef designation, CancellationToken ct) =>
        content.WriteJsonAsync(Realm, contentId, "mutation-result-content.v1",
            new MutationResultContent(DesignationSchema(kind), new StandardResponseBody(designation)), ct);

    public static Task<FirmProfessionalDutyDesignationView> ReadDesignationAsync(IImmutableContentStore content,
        ImmutableContentRef reference, CancellationToken ct) =>
        content.ReadJsonAsync<FirmProfessionalDutyDesignationView>(Realm, reference, ct);

    public static Task<ProfessionalDutyChangeIntent> ReadIntentAsync(IImmutableContentStore content,
        ImmutableContentRef reference, CancellationToken ct) =>
        content.ReadJsonAsync<ProfessionalDutyChangeIntent>(Realm, reference, ct);

    public static Task<VerifiedFirmProfessionalDutyAssertion> ReadVerifiedScopeAsync(IImmutableContentStore content,
        ImmutableContentRef reference, CancellationToken ct) =>
        content.ReadJsonAsync<VerifiedFirmProfessionalDutyAssertion>(Realm, reference, ct);
}
