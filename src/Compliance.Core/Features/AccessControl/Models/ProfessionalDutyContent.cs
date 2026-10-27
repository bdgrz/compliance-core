using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

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

    public static void RequireIntent(FirmProfessionalDutyDesignation owner, ProfessionalDutyChangeAccepted accepted,
        ProfessionalDutyChangeIntent intent, VerifiedFirmProfessionalDutyAssertion evidence)
    {
        var operation = accepted.Operation;
        if (intent.ChangeKind is not (ProfessionalDutyChangeKind.Record or ProfessionalDutyChangeKind.Revoke) ||
            !ProfessionalDutyRules.ValidAssertion(evidence, intent.SignedAssertion) ||
            operation.CommandKind != (intent.ChangeKind == ProfessionalDutyChangeKind.Record
                ? nameof(RecordFirmProfessionalDutyDesignation) : nameof(RevokeFirmProfessionalDutyDesignation)) ||
            intent.VerifiedScope.Schema != VerifiedScopeSchema ||
            intent.Header.RootOperationId != operation.RootOperationId || intent.VerifiedAssertionId != operation.RootOperationId ||
            intent.Header.FirstOwner != owner.Stream || intent.Header.AffectedOwner != owner.Stream ||
            intent.Header.ExpectedRevisionOwner != owner.Stream || intent.Header.OriginalActor != operation.Actor ||
            intent.Header.Episode != operation.Episode || intent.Header.OriginalExpectedRevision != operation.OriginalExpectedRevision ||
            intent.Header.AuthoredIntentSha256 != operation.AuthoredIntentSha256 || intent.Header.AcceptedAt != operation.AcceptedAt ||
            evidence.UserId != owner.UserId || evidence.Duty != owner.Duty || evidence.TenantId != owner.TenantId ||
            evidence.AssertionId != operation.RootOperationId || evidence.DesignationId != intent.DesignationId ||
            intent.AuthorityRevision != (ulong)evidence.AuthorityRevision ||
            evidence.Status != (intent.ChangeKind == ProfessionalDutyChangeKind.Record ? "active" : "revoked") ||
            intent.ChangeKind == ProfessionalDutyChangeKind.Record != (intent.StaffEligibility is not null) ||
            ProfessionalDutyRules.EvidenceDigest(intent.SignedAssertion) != evidence.EvidenceDigest)
            throw new ImmutableContentIntegrityException("The frozen designation scope differs from its exact native admission and owner.");
    }

    public static async Task RequireAuthoredAsync(IImmutableContentStore content, ProfessionalDutyChangeIntent intent,
        CancellationToken ct)
    {
        if (intent.AuthoredChange.Schema != (intent.ChangeKind == ProfessionalDutyChangeKind.Record ? RecordCommandSchema : RevokeCommandSchema))
            throw new ImmutableContentIntegrityException("The authored duty command has another selected schema.");
        Uuid designation;
        Uuid assertion;
        string token;
        long revision;
        string digest;
        if (intent.ChangeKind == ProfessionalDutyChangeKind.Record)
        {
            var command = await content.ReadJsonAsync<RecordFirmProfessionalDutyDesignation>(Realm, intent.AuthoredChange, ct)
                .ConfigureAwait(false);
            (designation, assertion, token, revision, digest) = (command.DesignationId, command.AssertionId,
                command.SignedAssertion, command.ExpectedRevision, AuthoredIntentDigest.Compute(command));
        }
        else
        {
            var command = await content.ReadJsonAsync<RevokeFirmProfessionalDutyDesignation>(Realm, intent.AuthoredChange, ct)
                .ConfigureAwait(false);
            (designation, assertion, token, revision, digest) = (command.DesignationId, command.AssertionId,
                command.SignedAssertion, command.ExpectedRevision, AuthoredIntentDigest.Compute(command));
        }
        if (designation != intent.DesignationId || assertion != intent.VerifiedAssertionId ||
            token != intent.SignedAssertion || revision < 0 || (ulong)revision != intent.Header.OriginalExpectedRevision ||
            digest != intent.Header.AuthoredIntentSha256)
            throw new ImmutableContentIntegrityException("The authored duty command differs from its frozen native admission binding.");
    }

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
