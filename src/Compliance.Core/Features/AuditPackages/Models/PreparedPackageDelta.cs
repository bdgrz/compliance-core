using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>One unpublished candidate plus the local proofs its final atomic owner guard must bind.</summary>
sealed record PreparedPackageDelta(ImmutableContentRef Content, NativeFactReference? Build = null,
    NativeFactReference? Disclosure = null, IReadOnlyList<NativeFactReference>? Validations = null, AuditPackagePreparedProof? Proof = null);
