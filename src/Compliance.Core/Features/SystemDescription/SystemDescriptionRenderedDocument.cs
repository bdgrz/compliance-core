using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed record SystemDescriptionRenderedDocument(byte[] Bytes, string MediaType, string FileExtension);
