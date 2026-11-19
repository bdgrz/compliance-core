using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>A seed step result and its exact binding fact, when applicable.</summary>
readonly record struct CriteriaSeedWriteResult(CriteriaSeedWriteStatus Status, Uuid EvidenceId);
