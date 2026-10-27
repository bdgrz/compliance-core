using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Evidence.Projections;

/// <summary>Internal exact registration binding; the opaque payload digest supplies no access or disclosure authority.</summary>
sealed record EvidenceArtifactMetadataSnapshot(EvidenceArtifactMetadataView Metadata,
    Uuid RegistrationEventId, string RegistrationPayloadSha256, DateTimeOffset? AvailabilityRecordedAt);
