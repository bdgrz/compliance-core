using Bdgrz.Compliance.Features.Evidence.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence;

sealed record EvidenceRedactionReadSnapshot(EvidenceRedaction Redaction, EvidenceRedactionSourcePair Sources,
    EvidenceRedactionHistorySnapshot History);
