using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>
/// The platform seed for M0-D02: 2017 Trust Services Criteria identifiers (2022 revised
/// points of focus edition) with original Bdgrz summaries. No AICPA text is stored.
/// </summary>
static class PlatformCriteriaCatalog
{
    static readonly Uuid EditionId = Uuid.Parse("0f3c5a52-9d8e-5b4a-8f21-20172022a0f1",
        CultureInfo.InvariantCulture);
    const string SourceUrl =
        "https://www.aicpa-cima.com/resources/download/2017-trust-services-criteria-with-revised-points-of-focus-2022";
    const string FocusGap =
        "Only selected points of focus are mapped, under local bdgrz:focus identifiers; the source does not number them.";

    public static CriteriaCatalog Create() => new(SeedEdition(), [.. EnumerateSeedEntries()]);

    internal static CriteriaCatalogEdition SeedEdition() =>
        new(EditionId, "tsc", "2017_tsc_2022_pof",
            new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero), true,
            "All 61 numbered 2017 criteria with original summaries. Points of focus are partially mapped.",
            SourceUrl, "identifiers_and_original_summaries",
            [
                new("security", "points_of_focus_partial", FocusGap),
                new("availability", "points_of_focus_partial", FocusGap),
                new("confidentiality", "points_of_focus_partial", FocusGap),
                new("processing_integrity", "points_of_focus_partial", FocusGap),
                new("privacy", "points_of_focus_partial", FocusGap),
                new("privacy", "privacy_lifecycle_unsupported",
                    "Privacy criteria may be mapped to controls, but personal-information lifecycle support is not yet available (#349)."),
            ]);

    internal static IEnumerable<Criterion> EnumerateSeedEntries()
    {
        yield return C("CC1.1", "security",
            "Leadership sets expectations for honest, ethical conduct and acts on departures.");
        yield return C("CC1.2", "security",
            "Board-level oversight of controls comes from people independent of day-to-day management.");
        yield return C("CC1.3", "security",
            "A clear org chart, reporting lines, and decision rights exist for running the business.");
        yield return C("CC1.4", "security",
            "Hiring, training, and retention keep the right skills in place for control roles.");
        yield return C("CC1.5", "security",
            "People are measured and held to account for the control duties assigned to them.");
        yield return C("CC2.1", "security",
            "Teams have accurate, timely information to operate and evaluate controls.");
        yield return C("CC2.2", "security", "Staff know their control duties and have a channel to raise concerns.");
        yield return C("CC2.3", "security",
            "Customers, vendors, and regulators hear what they need to know about how controls work.");
        yield return C("CC3.1", "security", "Goals are written clearly enough that threats to them can be spotted.");
        yield return C("CC3.2", "security",
            "A recurring risk review lists what could go wrong and decides how to handle each item.");
        yield return C("CC3.3", "security",
            "Fraud scenarios, including insider misuse and incentives to cheat, are part of risk reviews.");
        yield return C("CC3.4", "security",
            "Big shifts such as new systems, vendors, or leaders trigger a fresh look at risk.");
        yield return C("CC4.1", "security",
            "Periodic testing and routine monitoring show whether each control actually works.");
        yield return C("CC4.2", "security",
            "Control failures are logged, escalated to owners, and tracked until fixed.");
        yield return C("CC5.1", "security",
            "Controls are picked to bring identified risks down to a tolerable level.");
        yield return C("CC5.2", "security", "Technical safeguards cover the systems that business goals depend on.");
        yield return C("CC5.3", "security",
            "Each control has an owner, a written rule, and a routine that shows it is followed.");
        yield return C("CC6.1", "security",
            "Systems and data are protected by access controls, network boundaries, and encryption.");
        yield return C("CC6.2", "security",
            "Accounts are created only after approval and disabled when no longer needed.");
        yield return C("CC6.3", "security", "Permissions follow least privilege and are reviewed as roles change.");
        yield return C("CC6.4", "security", "Data centers and hardware are locked down to authorized people only.");
        yield return C("CC6.5", "security",
            "Retired devices and media are wiped or destroyed before leaving company hands.");
        yield return C("CC6.6", "security", "Internet-facing systems are shielded from outside attacks.");
        yield return C("CC6.7", "security",
            "Data is encrypted or otherwise protected in transit and on portable media.");
        yield return C("CC6.8", "security",
            "Malware and unapproved software are blocked or caught on endpoints and servers.");
        yield return C("CC7.1", "security",
            "Scanning and monitoring find configuration drift and new vulnerabilities.");
        yield return C("CC7.2", "security", "Logs and alerts surface suspicious activity for someone to investigate.");
        yield return C("CC7.3", "security", "Security alerts are triaged to decide whether they are real incidents.");
        yield return C("CC7.4", "security",
            "A playbook guides containment, fixes, and communication for confirmed incidents.");
        yield return C("CC7.5", "security",
            "After an incident, service is restored and lessons feed back into controls.");
        yield return C("CC8.1", "security",
            "Code and infrastructure changes are approved, tested, and tracked before release.");
        yield return C("CC9.1", "security",
            "Continuity plans and insurance cushion the impact of major outages or disasters.");
        yield return C("CC9.2", "security", "Suppliers and partners are vetted and watched for the risks they bring.");
        yield return C("A1.1", "availability", "Capacity is tracked so growth in demand does not cause outages.");
        yield return C("A1.2", "availability",
            "Backups, redundancy, and environmental protections keep services recoverable.");
        yield return C("A1.3", "availability", "Disaster recovery is rehearsed so teams know restoration works.");
        yield return C("C1.1", "confidentiality",
            "Sensitive business data is labeled and handled so only the right people see it.");
        yield return C("C1.2", "confidentiality",
            "Sensitive business data is securely deleted once its retention period ends.");
        yield return C("PI1.1", "processing_integrity",
            "Processing specs and data definitions are documented for the people who rely on them.");
        yield return C("PI1.2", "processing_integrity",
            "Incoming data is validated so bad input does not flow into processing.");
        yield return C("PI1.3", "processing_integrity", "Jobs run as designed, and errors are caught and corrected.");
        yield return C("PI1.4", "processing_integrity",
            "Reports and outputs are complete, accurate, and reach only intended recipients.");
        yield return C("PI1.5", "processing_integrity", "Stored records stay intact and match what was processed.");
        yield return C("P1.1", "privacy", "A privacy notice tells people what is done with their personal data.");
        yield return C("P2.1", "privacy",
            "People are offered choices about their data, and their decisions are recorded.");
        yield return C("P3.1", "privacy", "Only the personal data needed for stated purposes is gathered.");
        yield return C("P3.2", "privacy",
            "Where consent is required, it is clearly obtained before data is gathered.");
        yield return C("P4.1", "privacy", "Personal data is used only for the purposes people were told about.");
        yield return C("P4.2", "privacy", "Personal data is kept no longer than it is needed.");
        yield return C("P4.3", "privacy", "Personal data is securely erased at the end of its useful life.");
        yield return C("P5.1", "privacy", "People can view the personal data held about them.");
        yield return C("P5.2", "privacy",
            "Individuals can get mistakes in their records fixed, including in copies shared with others.");
        yield return C("P6.1", "privacy",
            "Sharing with outside parties happens only with a valid basis and the person's awareness.");
        yield return C("P6.2", "privacy",
            "Each release of personal data to others is logged so it can be traced later.");
        yield return C("P6.3", "privacy", "Leaks or improper sharing of personal data are logged when discovered.");
        yield return C("P6.4", "privacy",
            "Vendors that handle personal data are bound by privacy commitments and checked.");
        yield return C("P6.5", "privacy",
            "Vendors must alert the organization quickly when they mishandle personal data.");
        yield return C("P6.6", "privacy", "Affected people and authorities are told about breaches as required.");
        yield return C("P6.7", "privacy", "People can learn what data is held on them and who it has gone to.");
        yield return C("P7.1", "privacy", "Personal data is kept correct and current for how it is used.");
        yield return C("P8.1", "privacy",
            "Privacy complaints and questions are received, answered, and tracked to resolution.");
        yield return F("cc6-1:asset-inventory", "security", "CC6.1",
            "An up-to-date list of hardware, software, and data stores shows what needs protection.");
        yield return F("cc6-2:access-removal", "security", "CC6.2",
            "Leavers and role-changers lose access within a defined window.");
        yield return F("a1-2:backup-restoration", "availability", "A1.2",
            "Test restores prove backups are usable, not just present.");
        yield return F("c1-2:disposal-verification", "confidentiality", "C1.2",
            "Evidence shows that sensitive data scheduled for destruction was actually destroyed.");
        yield return F("pi1-2:input-edits", "processing_integrity", "PI1.2",
            "Forms and feeds reject missing or malformed fields at the point of entry.");
        yield return F("p4-2:retention-schedule", "privacy", "P4.2",
            "A published schedule says how long each kind of personal data is kept.");
    }

    static Criterion C(string identifier, string category, string summary) =>
        new(EditionId, identifier, identifier, category, "criterion", null, summary);

    static Criterion F(string localKey, string category, string parent, string summary) =>
        new(EditionId, "bdgrz:focus:" + localKey, null, category, "point_of_focus", parent,
            summary);
}
