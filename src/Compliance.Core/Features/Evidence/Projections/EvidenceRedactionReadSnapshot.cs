using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence.Projections;

sealed record EvidenceRedactionReadSnapshot(EvidenceRedaction Redaction, EvidenceRedactionSourcePair Sources,
    EvidenceRedactionHistorySnapshot History);
