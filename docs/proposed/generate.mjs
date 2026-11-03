import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const outDir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(outDir, "../..");
const tick = String.fromCharCode(96);
const sources = {
  aggregates: "docs/product/definitive-aggregate-choreography/aggregates.json",
  events: "docs/product/definitive-aggregate-choreography/events.json",
  choreography: "docs/product/definitive-aggregate-choreography/choreography.json",
  decisions: "docs/product/definitive-aggregate-choreography/decisions.json",
  eventReconciliation: "docs/product/definitive-aggregate-choreography/event-reconciliation.json",
  physicalPlan: "docs/product/definitive-aggregate-choreography/choreography/physical-plan.json",
  choreographyManifest: "docs/product/definitive-aggregate-choreography/choreography/manifest.json",
  commandSchemas: "docs/product/audits/2026-10-23/target-command-contracts.json",
  eventSchemas: "docs/product/audits/2026-10-23/target-event-contracts.json",
  readSchemas: "docs/product/audits/2026-10-23/operation-query-contracts.json",
  writeModelNames: "docs/proposed/data/write-model-names.json",
  writeModelDomainDecisions: "docs/proposed/data/write-model-domain-decisions.json",
};
const json = (file) => JSON.parse(fs.readFileSync(path.join(root, file), "utf8"));
const present = (file) => fs.existsSync(path.join(root, file));
const write = (file, content) => {
  const target = path.join(root, file);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, String(content).trimEnd() + "\n");
};
const writeJson = (file, value) => write(file, JSON.stringify(clean(value), null, 2));
const code = (value) => tick + String(value) + tick;

function clean(value) {
  if (Array.isArray(value)) return value.map(clean);
  if (value && typeof value === "object") return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, clean(item)]));
  if (typeof value === "string" && value.startsWith("/tmp/")) return "[Placeholder: source artifact was recorded outside this repository]";
  return value;
}

function slug(value) {
  return String(value).replace(/([a-z0-9])([A-Z])/g, "$1-$2").replace(/([A-Z])([A-Z][a-z])/g, "$1-$2").replace(/[^A-Za-z0-9]+/g, "-").replace(/^-|-$/g, "").toLowerCase();
}

function headingAnchor(value) {
  return String(value).toLowerCase().replace(/[^a-z0-9 -]/g, "").trim().replace(/\s+/g, "-");
}

function display(value) {
  if (value === undefined || value === "") return "[Placeholder: value not recorded]";
  if (value === null) return "null (recorded)";
  if (Array.isArray(value)) return value.length ? value.map(display).join(", ") : "None listed";
  if (typeof value === "object") return code(JSON.stringify(value).replaceAll("|", "&#124;").replaceAll("\n", " "));
  return String(value).replaceAll("|", "\\|").replaceAll("\n", " ");
}

function namedList(values, nameFor) {
  if (!Array.isArray(values) || values.length === 0) return "None listed";
  return values.map((value) => code(nameFor(value))).join(", ");
}

function table(headers, rows) {
  return ["| " + headers.join(" | ") + " |", "| " + headers.map(() => "---").join(" | ") + " |"]
    .concat(rows.map((row) => "| " + row.map(display).join(" | ") + " |")).join("\n");
}

function link(from, target, label) {
  const relative = path.relative(path.dirname(path.join(root, from)), path.join(root, target)).split(path.sep).join("/");
  return "[" + (label || path.basename(target)) + "](" + relative + ")";
}

function sourceLink(from, target) {
  if (!target || target.startsWith("/")) return "[Placeholder: source path is outside the repository]";
  if (!present(target)) return "[Placeholder: source file is not present in this checkout]";
  return link(from, target, target);
}

function routeText(value) {
  if (Array.isArray(value)) return value.map((route) => route.method + " " + route.path + (route.policy ? " [" + route.policy + "]" : "")).join("<br>");
  if (typeof value === "string") return value;
  if (value && typeof value === "object") return JSON.stringify(value);
  return "[Placeholder: route/channel not recorded]";
}

const aggregateCatalog = json(sources.aggregates);
const eventCatalog = json(sources.events);
const flowCatalog = json(sources.choreography);
const decisionCatalog = json(sources.decisions);
const eventReconciliationCatalog = json(sources.eventReconciliation);
const physicalPlanCatalog = json(sources.physicalPlan);
const choreographyManifestCatalog = json(sources.choreographyManifest);
const commandSchemaCatalog = json(sources.commandSchemas);
const eventSchemaCatalog = json(sources.eventSchemas);
const readSchemaCatalog = json(sources.readSchemas);
const writeModelNameCatalog = json(sources.writeModelNames);
const writeModelDomainDecisionCatalog = json(sources.writeModelDomainDecisions);
const sourceBusinessOwners = aggregateCatalog.businessOwners || [];
const sourceTechnicalOwners = aggregateCatalog.technicalOwners || [];
const sourceOwners = sourceBusinessOwners.concat(sourceTechnicalOwners);
const ownerNameOverrides = new Map((writeModelDomainDecisionCatalog.targetOwnerNameOverrides || []).map((item) => [item.sourceOwner, item]));
const targetOwnerName = (name) => ownerNameOverrides.get(name)?.targetOwner || name;
const targetOwnerSpecs = writeModelDomainDecisionCatalog.targetOwners || [];
const commandsBySourceOwner = new Map(sourceOwners.map((owner) => [owner.owner, owner.selectedCommandContracts || []]));
const reassignedCommands = new Map();
for (const spec of targetOwnerSpecs) {
  for (const command of spec.reassignedCommands || []) reassignedCommands.set(command, spec.owner);
}
const owners = sourceOwners.map((owner) => ({
  ...owner,
  sourceOwner: owner.owner,
  owner: targetOwnerName(owner.owner),
  selectedCommandContracts: (owner.selectedCommandContracts || [])
    .filter((item) => !reassignedCommands.has(item.command))
    .map((item) => ({ ...item, sourceOwner: owner.owner, owner: targetOwnerName(owner.owner) })),
})).concat(targetOwnerSpecs.map((spec) => {
  const domainDecision = (writeModelDomainDecisionCatalog.decisions || []).find((item) => item.id === "ATTEST_ENGAGEMENT_RECORD_PACKAGE");
  const sourceCommands = commandsBySourceOwner.get(spec.sourceOwner) || [];
  return {
    owner: spec.owner,
    sourceOwner: spec.sourceOwner,
    classification: spec.classification,
    businessAggregate: spec.businessAggregate,
    sourceCapabilityArea: spec.sourceCapabilityArea,
    baselineSourcePath: spec.baselineSourcePath,
    sourceReviewPath: spec.sourceReviewPath,
    status: spec.status,
    implementationStatus: spec.status,
    selectedIdentitySummary: spec.selectedIdentitySummary,
    invariant: spec.invariant,
    targetNote: spec.targetNote,
    sources: (spec.sources || []).map((source) => ({ path: source })),
    productRules: domainDecision ? [{ id: "M0-D27", doc: "m0-d27-attest-scope.md", rule: domainDecision.rule }] : [],
    selectedCommandContracts: sourceCommands
      .filter((item) => (spec.reassignedCommands || []).includes(item.command))
      .map((item) => ({ ...item, sourceOwner: spec.sourceOwner, owner: spec.owner })),
    targetEvents: spec.targetEvents || [],
    targetOnly: true,
  };
}));
const businessOwners = owners.filter((owner) => owner.businessAggregate || owner.classification === "business_aggregate");
const technicalOwners = owners.filter((owner) => !(owner.businessAggregate || owner.classification === "business_aggregate"));
const events = eventCatalog.events || [];
const sourceEventDispositions = writeModelDomainDecisionCatalog.sourceEventDispositions || [];
const sourceEventDispositionNames = new Set(sourceEventDispositions.map((item) => item.event));
const targetOnlyEvents = targetOwnerSpecs.flatMap((spec) => (spec.targetEvents || []).map((item) => ({
  event: item.event,
  sourceName: null,
  owners: [spec.owner],
  meaning: item.meaning,
  emissionKind: "proposed_target_business_fact",
  sourceStatus: "TARGET_ONLY_NO_SELECTED_SOURCE_EVENT_TYPE_OR_WIRE",
  targetOnly: true,
  nameReason: item.reason,
})));
const targetSourceEvents = events.filter((item) => !sourceEventDispositionNames.has(item.event));
const targetEventUniverse = targetSourceEvents.concat(targetOnlyEvents);
const flows = flowCatalog.edges || [];
const commandContracts = owners.reduce((all, owner) => all.concat(owner.selectedCommandContracts || []), []);
const eventSchemas = new Map((eventSchemaCatalog.events || []).map((item) => [item.event, item]));
const commandNameDecisions = writeModelNameCatalog.commands || {};
const eventNameDecisions = writeModelNameCatalog.events || {};
const reactorNameDecisions = writeModelNameCatalog.reactors || {};
function commandNameDecision(name) {
  if (!name) return { targetName: name, reason: "[Placeholder: command name not recorded]" };
  if (commandNameDecisions[name]) return commandNameDecisions[name];
  let match = name.match(/^Authorize(.+)Use$/);
  if (match) return { targetName: "Authorize" + match[1], reason: writeModelNameCatalog.commandPatterns[0].reason };
  match = name.match(/^Settle(.+)Use$/);
  if (match) return { targetName: "Close" + match[1] + "Authorization", reason: writeModelNameCatalog.commandPatterns[1].reason };
  return { targetName: name, reason: "The selected source name is retained." };
}
function eventNameDecision(name) {
  if (!name) return { targetName: name, reason: "[Placeholder: event name not recorded]" };
  if (eventNameDecisions[name]) return eventNameDecisions[name];
  let match = name.match(/^(.+)UseAuthorized$/);
  if (match) return { targetName: match[1] + "Authorized", reason: writeModelNameCatalog.eventPatterns[0].reason };
  match = name.match(/^(.+)UseSettled$/);
  if (match) return { targetName: match[1] + "AuthorizationClosed", reason: writeModelNameCatalog.eventPatterns[1].reason };
  return { targetName: name, reason: "The selected source name is retained." };
}
function reactorNameDecision(name) {
  if (!name) return { targetName: name, businessReason: "" };
  return reactorNameDecisions[name] || { targetName: name, businessReason: "" };
}
function commandName(name) { return commandNameDecision(name).targetName; }
function eventName(name) { return eventNameDecision(name).targetName; }
function reactorName(name) { return reactorNameDecision(name).targetName; }
function targetNameCollisions(items, sourceName, targetName) {
  const names = new Map();
  for (const item of items) {
    const target = targetName(item);
    const sources = names.get(target) || new Set();
    sources.add(sourceName(item));
    names.set(target, sources);
  }
  return [...names.entries()].filter(([, sources]) => sources.size > 1).map(([target, sources]) => ({ target, sources: [...sources] }));
}
const eventOwnerRows = new Map();
const flowOwnerRows = new Map();
const flowRootRows = new Map();
const flowUnassigned = [];

for (const event of targetSourceEvents) {
  const names = event.owners && event.owners.length ? event.owners.map(targetOwnerName) : ["unassigned"];
  for (const name of names) {
    const rows = eventOwnerRows.get(name) || [];
    rows.push(event);
    eventOwnerRows.set(name, rows);
  }
}
for (const event of targetOnlyEvents) {
  const rows = eventOwnerRows.get(event.owners[0]) || [];
  rows.push(event);
  eventOwnerRows.set(event.owners[0], rows);
}
for (const flow of flows) {
  for (const name of new Set([flow.sourceOwner, flow.targetOwner].filter(Boolean).map(targetOwnerName))) {
    const rows = flowOwnerRows.get(name) || [];
    rows.push(flow);
    flowOwnerRows.set(name, rows);
  }
  const roots = flow.currentRegisteredRootTypes || [];
  const names = roots.length ? roots : flow.plannedRootRole ? [String(flow.plannedRootRole)] : [];
  if (!names.length) flowUnassigned.push(flow);
  for (const name of new Set(names)) {
    const rows = flowRootRows.get(name) || [];
    rows.push(flow);
    flowRootRows.set(name, rows);
  }
}
const commandTargetCollisions = targetNameCollisions(commandContracts, (item) => item.command, (item) => commandName(item.command));
const eventTargetCollisions = targetNameCollisions(targetEventUniverse, (item) => item.event, (item) => eventName(item.event));
const ownerTargetCollisions = [...new Set(owners.map((item) => item.owner))].length === owners.length ? [] : owners.map((item) => item.owner).filter((name, index, all) => all.indexOf(name) !== index);
const reactorRoots = [...flowRootRows.keys()];
const reactorTargetCollisions = targetNameCollisions(reactorRoots, (item) => item, (item) => reactorName(item));
const unnamedReactors = reactorRoots.filter((item) => !reactorNameDecision(item).targetName || !reactorNameDecision(item).businessReason.trim());
if (commandTargetCollisions.length || eventTargetCollisions.length || ownerTargetCollisions.length || reactorTargetCollisions.length || unnamedReactors.length) {
  throw new Error("Write-model target names must be unique and every reactor needs a business reason: " + JSON.stringify({ commandTargetCollisions, eventTargetCollisions, ownerTargetCollisions, reactorTargetCollisions, unnamedReactors }));
}

function ownerArea(owner) {
  const candidates = [owner.baselineSourcePath].concat((owner.sources || []).map((item) => item.path));
  for (const candidate of candidates) {
    const match = candidate && candidate.match(/src\/Compliance\.[^/]+\/Features\/([^/]+)/);
    if (match) return match[1];
  }
  return owner.sourceCapabilityArea || "[Placeholder: source capability area is not recorded]";
}

function ownerPage(owner) {
  const id = slug(owner.owner);
  const output = "docs/proposed/aggregates/" + id + ".md";
  const data = "docs/proposed/data/aggregates/" + id + ".json";
  writeJson(data, owner);
  const commands = owner.selectedCommandContracts || [];
  const emitted = eventOwnerRows.get(owner.owner) || [];
  const ownerFlows = flowOwnerRows.get(owner.owner) || [];
  const commandRows = commands.map((item) => [
    link(output, "docs/proposed/commands/" + id + ".md#" + headingAnchor(commandName(item.command)), commandName(item.command)),
    item.kind || "[Placeholder: command kind not recorded]",
    item.logicalIdentity && item.logicalIdentity.field || "[Placeholder: identity field not recorded]",
  ]);
  const eventRows = emitted.map((item) => [
    link(output, "docs/proposed/events/" + id + ".md#" + headingAnchor(eventName(item.event)), eventName(item.event)),
    item.adoptedTargetWire && item.adoptedTargetWire.discriminator || "[Placeholder: discriminator not recorded]",
    item.adoptedTargetWire && item.adoptedTargetWire.version,
  ]);
  const flowRows = ownerFlows.map((item) => [
    link(output, "docs/proposed/reactors/" + slug((item.currentRegisteredRootTypes || [])[0] || item.plannedRootRole || "unassigned-architecture-rows") + ".md#" + slug(item.id), item.id),
    item.kind,
    targetOwnerName(item.sourceOwner),
    targetOwnerName(item.targetOwner),
    commandName(item.receiverCommand),
  ]);
  const productRules = (owner.productRules || []).map((item) => {
    const decision = item.doc ? "docs/product/decisions/" + item.doc : "";
    const citation = decision && present(decision) ? link(output, decision, item.doc) : "[Placeholder: product decision source is not present]";
    return "- **" + display(item.id) + "** — " + display(item.rule) + " Source: " + citation + ".";
  });
  const domainRule = (writeModelDomainDecisionCatalog.ownerRules || {})[owner.owner]
    || (writeModelDomainDecisionCatalog.ownerRules || {})[owner.sourceOwner]
    || {};
  const additionalDecision = [owner.decides, domainRule.decisionRule].filter(Boolean).join(" ");
  const compatibilityEvents = sourceEventDispositions.filter((item) => item.sourceOwner === (owner.sourceOwner || owner.owner) || item.targetOwner === owner.owner);
  const decisions = (owner.adoptedArchitectureDecisions || []).map((item) => "- " + code(item.decisionId || "[Placeholder: decision id not recorded]") + " — " + display(item.choice) + ".");
  const reviewName = id + "-review-2026-10-29.md";
  const reviewAlt = id + "-review-2026-10-27.md";
  const reviewPath = owner.sourceReviewPath && present(owner.sourceReviewPath)
    ? owner.sourceReviewPath
    : present("docs/product/definitive-aggregate-choreography/" + reviewName)
    ? "docs/product/definitive-aggregate-choreography/" + reviewName
    : present("docs/product/definitive-aggregate-choreography/" + reviewAlt)
      ? "docs/product/definitive-aggregate-choreography/" + reviewAlt
      : "";
  const identity = owner.selectedIdentitySummary || owner.selectedIdentityClarification || "[Placeholder: selected target identity is not recorded.]";
  write(output, [
    "# " + owner.owner,
    "",
    "- Classification: " + code(owner.classification || "[Placeholder: classification not recorded]"),
    "- Source area: " + code(ownerArea(owner)) + " (derived from source path; not a bounded-context ruling).",
    "- Business aggregate: " + code(owner.businessAggregate === undefined ? "[Placeholder: classification not recorded]" : owner.businessAggregate),
    "- Selected status: " + code(owner.status || owner.implementationStatus || "[Placeholder: status not recorded]"),
    owner.sourceOwner && owner.sourceOwner !== owner.owner ? "- Selected source owner: " + code(owner.sourceOwner) + "." : "",
    "",
    "## Identity and invariant",
    "",
    "**Selected identity:** " + identity,
    "",
    "**Invariant:** " + (owner.invariant || "[Placeholder: invariant not recorded.]"),
    "",
    "**Additional decision rule:** " + (additionalDecision || "[Placeholder: no owner-specific decision rule is recorded.]"),
    "",
    "**Must preserve:** " + (owner.mustPreserve || "[Placeholder: preservation rule not recorded.]"),
    "",
    "**Replay:** " + (owner.selectedReplayRule || "[Placeholder: replay rule not recorded.]"),
    "",
    "**Lifecycle:** " + (domainRule.lifecycle || "[Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]"),
    "",
    owner.targetNote ? "## Source-to-target ownership\n\n" + owner.targetNote : "",
    owner.targetOnly ? "**Target events:** These are proposed business facts only. No selected source event type, discriminator, version, or payload schema exists yet; define the event contracts before implementation." : "",
    compatibilityEvents.length ? "## Source event compatibility\n\n" + table(["Selected source event", "Source owner", "Target owner", "Disposition"], compatibilityEvents.map((item) => [code(item.event), code(item.sourceOwner), code(item.targetOwner), item.reason])) : "",
    "",
    "## Commands",
    "",
    commandRows.length ? table(["Command", "Kind", "Identity field"], commandRows) : "[Placeholder: no selected commands are listed for this owner.]",
    "",
    "## Events",
    "",
    eventRows.length ? table(["Event", "Discriminator", "Version"], eventRows) : "[Placeholder: no canonical events are listed for this owner.]",
    "",
    "## Cross-owner flows",
    "",
    flowRows.length ? table(["Row", "Kind", "Source owner", "Target owner", "Command"], flowRows) : "[Placeholder: no choreography row names this owner.]",
    "",
    "## Product rules",
    "",
    productRules.length ? productRules.join("\n") : "[Placeholder: product-rule traceability is not recorded for this owner.]",
    "",
    "## Adopted architecture decisions",
    "",
    decisions.length ? decisions.join("\n") : "[Placeholder: no adopted architecture-decision reference is recorded.]",
    "",
    "## Evidence and complete record",
    "",
    "- Baseline source: " + (owner.baselineSourcePath ? sourceLink(output, owner.baselineSourcePath) : "[Placeholder: baseline source not recorded]"),
    "- Aggregate review: " + (reviewPath ? link(output, reviewPath) : "[Placeholder: no owner-specific review file is present]"),
    "- Full selected record: " + link(output, data, "JSON"),
    "",
    "> Target definition is separate from runtime qualification. Implementation status recorded by the source: " + code(owner.implementationStatus || "[Placeholder: implementation status not recorded]") + ".",
  ].join("\n"));
}

function commandPage(owner) {
  const id = slug(owner.owner);
  const output = "docs/proposed/commands/" + id + ".md";
  const contracts = owner.selectedCommandContracts || [];
  const sourceOwner = owner.sourceOwner || owner.owner;
  const audit = (commandSchemaCatalog.commands || []).filter((item) => item.owner === sourceOwner || (item.sourceRoots || []).includes(sourceOwner));
  const data = "docs/proposed/data/commands/" + id + ".json";
  writeJson(data, {
    owner: owner.owner,
    selectedContracts: contracts.map((item) => ({ ...item, sourceName: item.command, targetName: commandName(item.command) })),
    sourceContractEvidence: audit,
  });
  const rows = contracts.map((item) => [
    commandName(item.command),
    item.command,
    item.kind || "[Placeholder: kind not recorded]",
    item.logicalIdentity && item.logicalIdentity.field || "[Placeholder: identity field not recorded]",
    (item.fields || []).length,
    routeText(item.http),
  ]);
  const details = contracts.flatMap((item) => {
    const sourceEvidence = audit.find((record) => record.command === item.command) || {};
    const identity = item.logicalIdentity || {};
    const responses = item.responseContract || sourceEvidence.responseContract || {};
    const revision = item.expectedRevisionScope || sourceEvidence.expectedRevisionScope || {};
    const handlerSources = item.sourceHandlers || sourceEvidence.sourceHandlers || [];
    const fields = item.fields || [];
    const http = item.http || [];
    return [
      "### " + commandName(item.command),
      "",
      table(["Selected contract detail", "Value"], [
        ["Target command name", commandName(item.command)],
        ["Selected source command name", item.command],
        ["Name decision", commandNameDecision(item.command).reason],
        ["Target owner", owner.owner],
        ["Selected source owner", item.sourceOwner || item.owner || sourceOwner],
        ["Contract record status", item.status || sourceEvidence.status],
        ["Kind", item.kind],
        ["Logical identity", [identity.field, identity.type, identity.required === undefined ? "[Placeholder: required status not recorded]" : "required=" + identity.required, identity.origin, identity.rule].filter(Boolean).join("; ") || "[Placeholder: logical identity not recorded]"],
        ["Entry point", item.entryPoint || sourceEvidence.entryPoint],
        ["MCP", item.mcp === undefined ? sourceEvidence.mcp === undefined ? "[Placeholder: MCP availability not recorded]" : sourceEvidence.mcp : item.mcp],
        ["Selected semantic events", (item.events || sourceEvidence.events || []).map(eventName)],
        ["Protocols", item.protocols || sourceEvidence.protocols],
        ["Policy preservation", item.policyPreservation || sourceEvidence.policyPreservation],
        ["Contract changes", item.contractChanges || sourceEvidence.contractChanges],
        ["Guard order", item.guardOrder || sourceEvidence.guardOrder],
        ["Response contract", responses],
        ["Expected revision scope", revision],
        ["Authorizer sources", item.authorizerSources || sourceEvidence.authorizerSources],
        ["Source contract", item.sourceContract || sourceEvidence.sourceContract],
        ["Primary handler", item.handler || sourceEvidence.handler],
      ]),
      "",
      "#### Request fields",
      "",
      fields.length ? table(["Field", "Type", "Required"], fields.map((field) => [field.name, field.type, field.required])) : "[Placeholder: selected request fields are not recorded.]",
      "",
      "#### HTTP routes",
      "",
      http.length ? table(["Method", "Path", "Policy"], http.map((route) => [route.method, route.path, route.policy])) : "[Placeholder: no HTTP route is recorded for this command.]",
      "",
      "#### Handler source evidence",
      "",
      handlerSources.length ? table(["Handler", "Registered", "Source"], handlerSources.map((handler) => [handler.name, handler.registered, sourceLink(output, handler.source)])) : "[Placeholder: handler source evidence is not recorded.]",
      "",
      "#### Test evidence recorded by the target contract",
      "",
      "Existing tests: " + display(item.existingTests || sourceEvidence.existingTests || "[Placeholder: existing tests not recorded]"),
      "",
      "Required tests: " + display(item.requiredTests || sourceEvidence.requiredTests || "[Placeholder: required tests not recorded]"),
      "",
    ];
  });
  write(output, [
    "# " + owner.owner + " commands",
    "",
    owner.sourceOwner && owner.sourceOwner !== owner.owner ? "In the selected source catalog these commands are grouped under " + code(owner.sourceOwner) + "; the proposed target assigns them to " + code(owner.owner) + " based on the selected business rule." : "",
    "",
    "Selected command contracts: **" + contracts.length + "**. Complete selected contracts and companion source evidence: " + link(output, data, "owner command JSON") + ".",
    "",
    rows.length ? table(["Target command", "Selected source name", "Kind", "Identity", "Fields", "HTTP routes"], rows) : "[Placeholder: no selected command contract is recorded for this owner.]",
    "",
    "## Selected command details",
    "",
    details.length ? details.join("\n") : "[Placeholder: command-level contract details are not recorded.]",
    "",
    "## Source contracts",
    "",
    audit.length ? table(["Selected source command", "Handler", "Registered", "Entry point"], audit.map((item) => [
      item.command,
      item.handler || "[Placeholder: handler not recorded]",
      item.sourceHandlers && item.sourceHandlers.map((handler) => handler.registered).join(", "),
      item.entryPoint || "[Placeholder: entry point not recorded]",
    ])) : "[Placeholder: no companion target-command source records match this owner.]",
    "",
    "[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]",
  ].join("\n"));
}

for (const owner of owners) {
  ownerPage(owner);
  commandPage(owner);
}

const eventOwners = new Set([...eventOwnerRows.keys(), ...owners.map((item) => item.owner)]);
for (const ownerName of eventOwners) {
  const id = slug(ownerName);
  const output = "docs/proposed/events/" + id + ".md";
  const records = eventOwnerRows.get(ownerName) || [];
  const full = records.map((item) => ({
    canonical: item,
    schema: eventSchemas.get(item.event) || null,
    naming: item.targetOnly
      ? { targetName: item.event, reason: item.nameReason, meaning: item.meaning }
      : eventNameDecision(item.event),
  }));
  const data = "docs/proposed/data/events/" + id + ".json";
  writeJson(data, {
    owner: ownerName,
    events: full.map(({ canonical, schema, naming }) => ({
      canonical,
      schema,
      sourceName: canonical.targetOnly ? "No selected source event type" : canonical.event,
      targetName: naming.targetName,
      nameDecision: naming.reason,
      targetMeaning: naming.meaning || canonical.meaning,
    })),
  });
  const rows = full.map(({ canonical, schema, naming }) => [
    naming.targetName,
    canonical.targetOnly ? "New proposed target fact" : canonical.event,
    canonical.emissionKind,
    canonical.targetOnly ? "No source wire selected" : canonical.adoptedTargetWire && canonical.adoptedTargetWire.discriminator,
    canonical.targetOnly ? "Not selected" : canonical.adoptedTargetWire && canonical.adoptedTargetWire.version,
    canonical.targetOnly ? "Contract required" : schema ? (schema.fields || []).length : "[Placeholder: schema absent]",
    naming.meaning || canonical.meaning,
  ]);
  const mismatches = full.filter(({ canonical, schema }) => schema && schema.discriminatorVersion !== undefined && canonical.adoptedTargetWire && schema.discriminatorVersion !== canonical.adoptedTargetWire.version);
  const details = full.flatMap(({ canonical, schema, naming }) => {
    const targetMeaning = naming.meaning || canonical.meaning;
    const metadata = [
      ["Target event name", naming.targetName],
      ["Selected source event name", canonical.targetOnly ? "No selected source event type" : canonical.event],
      ["Name decision", naming.reason],
      ["Canonical semantic key", canonical.canonicalSemanticKey],
      ["Owner selection", canonical.ownerSelection],
      ["Target concrete type", canonical.targetConcreteType],
      ["Adopted wire", canonical.targetOnly ? "No source discriminator or version selected" : canonical.adoptedTargetWire],
      ["Wire qualification", canonical.wireQualification],
      ["Schema status", canonical.schemaStatus],
      ["Source status", canonical.sourceStatus],
      ["Adopted decision IDs", canonical.adoptedDecisionIds],
      ["Superseded selected wire", canonical.supersededSelectedWire],
      ["Target meaning", targetMeaning],
      ["Selected source meaning", canonical.targetOnly ? "No source event meaning; see selected product rule." : canonical.meaning],
      ["Meaning source", canonical.meaningSource],
    ];
    const schemaMetadata = schema
      ? Object.entries(schema).filter(([key]) => !["fields", "contentSchemas", "resolvedIntentSchema"].includes(key)).map(([key, value]) => ["Schema " + key, value])
      : [];
    const contentSchemas = schema && schema.contentSchemas || [];
    const nestedSchemas = contentSchemas.flatMap((content) => {
      const parts = [
        "#### Content schema: " + (content.schema || content.field || "[Placeholder: content schema name not recorded]"),
        "",
        table(["Property", "Value"], Object.entries(content).filter(([key]) => !["fields", "variants"].includes(key))),
        "",
      ];
      if (content.fields && content.fields.length) parts.push("##### Fields", "", table(["Field", "Type", "Required"], content.fields.map((field) => [field.name, field.type, field.required])), "");
      for (const variant of content.variants || []) {
        parts.push("##### Variant: " + (variant.recordType || variant.schema || "[Placeholder: variant name not recorded]"), "");
        if (variant.fields && variant.fields.length) parts.push(table(["Field", "Type", "Required"], variant.fields.map((field) => [field.name, field.type, field.required])), "");
        else parts.push("[Placeholder: variant fields are not recorded.]", "");
      }
      return parts;
    });
    const matchingFlows = flows.map((flow) => {
      const roles = [];
      if ((flow.physicalRootEvents || []).includes(canonical.event)) roles.push("physical root");
      if ((flow.logicalPrerequisiteFacts || []).includes(canonical.event)) roles.push("logical prerequisite");
      if ((flow.emittedSemanticFacts || []).includes(canonical.event)) roles.push("emitted semantic fact");
      return roles.length ? [flow, roles.join(", ")] : null;
    }).filter(Boolean);
    const choreography = matchingFlows.length
      ? table(["Row", "Relationship", "Source owner", "Receiver command", "Target owner"], matchingFlows.map(([flow, role]) => [
        link(output, "docs/proposed/reactors/" + slug((flow.currentRegisteredRootTypes || [])[0] || flow.plannedRootRole || "unassigned-architecture-rows") + ".md#" + headingAnchor(flow.id), flow.id),
        role,
        targetOwnerName(flow.sourceOwner),
        commandName(flow.receiverCommand),
        targetOwnerName(flow.targetOwner),
      ]))
      : "[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]";
    return [
      "### " + naming.targetName,
      "",
      table(["Canonical event detail", "Value"], metadata),
      "",
      schema ? "#### Selected schema and implementation evidence" : "#### Schema",
      "",
      schema ? table(["Property", "Value"], schemaMetadata) : "[Placeholder: no selected payload schema matches this canonical event.]",
      "",
      schema && schema.fields && schema.fields.length ? "#### Event fields\n\n" + table(["Field", "Type", "Required"], schema.fields.map((field) => [field.name, field.type, field.required])) : "[Placeholder: selected top-level schema fields are not recorded.]",
      "",
      contentSchemas.length ? "#### Nested content schemas\n\n" + nestedSchemas.join("\n") : "[Placeholder: no nested content schemas are recorded.]",
      "",
      schema && schema.resolvedIntentSchema ? "#### Resolved intent schema\n\n" + (typeof schema.resolvedIntentSchema === "object" ? table(["Property", "Value"], Object.entries(schema.resolvedIntentSchema)) : display(schema.resolvedIntentSchema)) : "[Placeholder: no resolved intent schema is recorded.]",
      "",
      canonical.targetOnly ? "#### Target event contract" : "#### Choreography references",
      "",
      canonical.targetOnly
        ? "[Placeholder: define the event fields, wire discriminator, version, and emitting command before implementation.]"
        : choreography,
      "",
    ];
  });
  write(output, [
    "# " + ownerName + " events",
    "",
    "Canonical events: **" + records.length + "**. Complete canonical event and available schema records: " + link(output, data, "owner event JSON") + ".",
    "",
    rows.length ? table(["Target event", "Selected source name", "Kind", "Adopted discriminator", "Version", "Schema fields", "Meaning"], rows) : "[Placeholder: no canonical events are assigned to this owner.]",
    "",
    "## Event details",
    "",
    details.length ? details.join("\n") : "[Placeholder: event-level details are not recorded.]",
    "",
    mismatches.length ? "## Wire/schema version discrepancies\n\n" + mismatches.map(({ canonical, schema }) => "- **" + canonical.event + "** — [Placeholder: reconcile schema version " + schema.discriminatorVersion + " with adopted version " + canonical.adoptedTargetWire.version + "].").join("\n") : "",
    "",
    "[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]",
  ].join("\n"));
}

function reactorPage(name, records) {
  const id = slug(name);
  const output = "docs/proposed/reactors/" + id + ".md";
  const data = "docs/proposed/data/reactors/" + id + ".json";
  const naming = reactorNameDecision(name);
  writeJson(data, { sourceRoot: name, targetRoot: naming.targetName, businessReason: naming.businessReason, rows: records });
  const rows = records.map((item) => [
    item.id,
    item.kind,
    (item.logicalPrerequisiteFacts || []).map(eventName),
    commandName(item.receiverCommand),
    targetOwnerName(item.targetOwner),
    (item.emittedSemanticFacts || []).map(eventName),
  ]);
  const details = records.flatMap((item) => [
    "### " + item.id,
    "",
    table(["Selected choreography detail", "Value"], Object.entries(item).map(([key, value]) => {
      if (key === "receiverCommand" && value) return ["Target command (selected source name: " + value + ")", commandName(value)];
      if (key === "sourceOwner" && value) return ["Source owner (target name)", targetOwnerName(value) + " (source: " + value + ")"];
      if (key === "targetOwner" && value) return ["Target owner", targetOwnerName(value)];
      if (["logicalPrerequisiteFacts", "physicalRootEvents", "originalAdmissionKinds", "emittedSemanticFacts"].includes(key) && Array.isArray(value)) return [key + " (target names)", value.map(eventName)];
      if (key === "currentRegisteredRootTypes" && Array.isArray(value)) return [key + " (target names)", value.map(reactorName)];
      return [key, value];
    })),
    "",
  ]);
  write(output, [
    "# " + naming.targetName,
    "",
    "Selected source root name: " + code(name) + ".",
    naming.businessReason ? "## Why this reaction exists\n\n" + naming.businessReason : "",
    "",
    "Selected choreography rows: **" + records.length + "**. Required proofs, recovery, effect identity, and implementation evidence are preserved in " + link(output, data, "reactor row JSON") + ".",
    "",
    rows.length ? table(["Row", "Kind", "Prerequisite facts", "Command", "Target owner", "Emitted facts"], rows) : "[Placeholder: no selected rows are assigned to this root.]",
    "",
    "## Selected row details",
    "",
    details.length ? details.join("\n") : "[Placeholder: no row-level details are recorded.]",
    "",
    "## Root and recovery contract",
    "",
    "Per-row proof, identity, recovery, conformance, and implementation fields are shown above and preserved in the linked JSON record.",
  ].join("\n"));
}
for (const [name, records] of flowRootRows) reactorPage(name, records);
reactorPage("Unassigned architecture rows", flowUnassigned);

const processDefinitions = aggregateCatalog.processDefinitions || [];
const processRows = [];
const rolloverNarrative = "`ProgramRollover` retains the exact proposal and independent approval, then atomically claims the approved version before any successor mutation. The claim prevents revise/reject races and pins the actual approval tuple. `ProgramRolloverExecutionProcess` waits for a person to author a complete successor `TypeIIPlan` under normal source-validation rules, freezes that proposal and an exhaustive mapping of each rollover record and commitment to a plan destination or justified nonrepresentation, then waits for independent review and executive approval; `changes_requested` returns to human revision and a new complete mapping, while `reject` terminates as failed. It submits the exact `StartTypeIIObservationPeriod` command. Its 202 opening acceptance is pending. The process is complete only after it verifies the actual `ObservationPeriodStarted` chain against the immutable opening intent, exact plan proposal/review/approval facts and content hash, period/range, calendar authorization, operation root and result. Process state stores verified proof references and terminal outcome; the business aggregate does not emit `ProgramNextPeriodCreated`. Rejection/failure never implies the period started.";
for (const [index, item] of processDefinitions.entries()) {
  const name = item.process || item.name || item.owner || item.id || "Process " + (index + 1);
  const id = slug(name);
  const output = "docs/proposed/processes/" + id + ".md";
  const data = "docs/proposed/data/processes/" + id + ".json";
  writeJson(data, item);
  processRows.push([link("docs/proposed/processes/README.md", output, name), item.rootSourceEvents, item.ordered]);
  const stages = item.stages || [];
  write(output, [
    "# " + name,
    "",
    ...(name === "ProgramRolloverExecutionProcess" ? ["## Selected rollover contract", "", rolloverNarrative, ""] : []),
    "- Root source events: " + display(item.rootSourceEvents || "[Placeholder: process root events not recorded]"),
    "- Ordered stages: " + display(item.ordered),
    "",
    stages.length ? table(["Stage", "Receivers", "Rule"], stages.map((stage) => [stage.stage, stage.receivers, stage.rule || "[Placeholder: no stage-specific rule recorded]"])) : "[Placeholder: process stages are not recorded.]",
    "",
    "Abort commands: " + display(item.abort || "[Placeholder: abort behavior not recorded]"),
    "",
    "Complete selected process definition: " + link(output, data, "JSON") + ".",
    ...(name === "ProgramRolloverExecutionProcess" ? ["R000 choreography row: " + link(output, "docs/proposed/data/processes/program-rollover-execution-row.json", "JSON") + "."] : []),
    "",
    "[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]",
  ].join("\n"));
}
const rolloverRow = flows.find((item) => item.id === "R000");
writeJson("docs/proposed/data/processes/program-rollover-execution-row.json", rolloverRow || { placeholder: "R000 row missing" });

const decisions = decisionCatalog.decisions || [];
writeJson("docs/proposed/data/decisions.json", decisions);
writeJson("docs/proposed/data/event-reconciliation-snapshot.json", eventReconciliationCatalog);
writeJson("docs/proposed/data/physical-registration-plan-snapshot.json", physicalPlanCatalog);

const rulesMissingSource = aggregateCatalog.productRulesNotYetInSource || [];
const ownerAreas = new Map();
for (const owner of owners) {
  const area = ownerArea(owner);
  const rows = ownerAreas.get(area) || [];
  rows.push(owner);
  ownerAreas.set(area, rows);
}
const ownerSections = [...ownerAreas.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([area, rows]) =>
  "## " + area + "\n\n" + table(["Owner", "Class", "Commands", "Events", "Invariant"], rows.map((owner) => [
    link("docs/proposed/aggregates/README.md", "docs/proposed/aggregates/" + slug(owner.owner) + ".md", owner.owner),
    owner.classification,
    (owner.selectedCommandContracts || []).length,
    (eventOwnerRows.get(owner.owner) || []).length,
    owner.invariant,
  ])),
);
write("docs/proposed/aggregates/README.md", [
  "# Aggregate and technical-owner index",
  "",
  "Selected target: **" + businessOwners.length + " business/authority owners** and **" + technicalOwners.length + " technical/storage responsibilities**. Technical responsibilities are not business aggregates.",
  "",
  "Groups are derived from source feature paths. They are navigation labels, not bounded-context decisions.",
  "",
  ownerSections.join("\n\n"),
  "",
  "Full selected owner records: " + link("docs/proposed/aggregates/README.md", "docs/proposed/data/aggregates", "data/aggregates") + ".",
].join("\n"));

write("docs/proposed/commands/README.md", [
  "# Commands by owner",
  "",
  "Selected command contracts are grouped by their canonical owner.",
  "",
  table(["Owner", "Contracts", "Classification"], owners.map((owner) => [
    link("docs/proposed/commands/README.md", "docs/proposed/commands/" + slug(owner.owner) + ".md", owner.owner),
    (owner.selectedCommandContracts || []).length,
    owner.classification,
  ])),
  "",
  "Product command scope: " + link("docs/proposed/commands/README.md", "docs/proposed/commands/product-scope.md", "product-scope.md") + ".",
].join("\n"));

write("docs/proposed/events/README.md", [
  "# Canonical event index",
  "",
  "Selected source canonical events: **" + events.length + "**. The proposed target has **" + targetEventUniverse.length + "** business events, including **" + targetOnlyEvents.length + "** newly named facts whose event contracts are not selected yet. " + sourceEventDispositions.length + " source event is retained as compatibility evidence without a direct target event mapping.",
  "",
  table(["Owner", "Events", "Schemas"], [...eventOwnerRows.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([name, rows]) => [
    link("docs/proposed/events/README.md", "docs/proposed/events/" + slug(name) + ".md", name),
    rows.length,
    rows.filter((item) => eventSchemas.has(item.event)).length,
  ])),
  "",
  "Product event scope: " + link("docs/proposed/events/README.md", "docs/proposed/events/product-scope.md", "product-scope.md") + ".",
  "Source facts kept only for compatibility: " + link("docs/proposed/events/README.md", "docs/proposed/events/source-compatibility.md", "source-compatibility.md") + ".",
  "Historical name/disposition snapshot: " + link("docs/proposed/events/README.md", "docs/proposed/events/reconciliation.md", "reconciliation.md") + ".",
].join("\n"));

const sourceCompatibilityEventRows = sourceEventDispositions.map((item) => {
  const event = events.find((candidate) => candidate.event === item.event) || {};
  return {
    ...item,
    sourceMeaning: event.meaning,
    sourceWire: event.adoptedTargetWire,
    sourceStatus: event.sourceStatus,
  };
});
writeJson("docs/proposed/data/events/source-compatibility.json", sourceCompatibilityEventRows);
write("docs/proposed/events/source-compatibility.md", [
  "# Source events kept for compatibility",
  "",
  "These records describe facts from the selected source catalog that do not have a direct event name in the proposed target. They preserve the old source meaning and wire for later reconciliation; they are not additional target business events.",
  "",
  table(["Selected source event", "Source owner", "Proposed owner", "Related target event", "Wire", "Version", "Disposition"], sourceCompatibilityEventRows.map((item) => [
    code(item.event),
    code(item.sourceOwner),
    code(item.targetOwner),
    code(item.relatedTargetEvent),
    item.sourceWire && item.sourceWire.discriminator,
    item.sourceWire && item.sourceWire.version,
    item.reason,
  ])),
  "",
  ...sourceCompatibilityEventRows.flatMap((item) => [
    "## " + item.event,
    "",
    "**Selected source meaning:** " + display(item.sourceMeaning),
    "",
    "**Source status:** " + display(item.sourceStatus),
    "",
    "**Target disposition:** " + item.reason,
    "",
  ]),
].join("\n"));

const eventCategoryCounts = Object.entries((eventReconciliationCatalog.events || []).reduce((counts, item) => {
  const category = item.category || "[Placeholder: category not recorded]";
  counts[category] = (counts[category] || 0) + 1;
  return counts;
}, {})).sort(([a], [b]) => a.localeCompare(b));
const eventDispositionCounts = Object.entries((eventReconciliationCatalog.events || []).reduce((counts, item) => {
  const disposition = item.sourceInventoryDisposition || "[Placeholder: source disposition not recorded]";
  counts[disposition] = (counts[disposition] || 0) + 1;
  return counts;
}, {})).sort(([a], [b]) => a.localeCompare(b));
write("docs/proposed/events/reconciliation.md", [
  "# Historical event-name reconciliation snapshot",
  "",
  "This full name/disposition map is retained from an earlier reconciliation packet. Its recorded polish date is " + display(eventReconciliationCatalog.polish && eventReconciliationCatalog.polish.date) + ", but its summary still reports " + display(eventReconciliationCatalog.summary && eventReconciliationCatalog.summary.businessOwnerCount) + " owners and " + display(eventReconciliationCatalog.summary && eventReconciliationCatalog.summary.intendedEmittedCanonicalSemanticEvents) + " emitted events. The selected source catalogs report " + sourceBusinessOwners.length + " business owners and " + events.length + " source event rows; the proposed target omits " + sourceEventDispositions.length + " source-only compatibility fact and adds " + targetOnlyEvents.length + " newly named event facts. Treat the snapshot as historical evidence until it is reconciled against the current catalogs.",
  "",
  "Current selected event index: " + link("docs/proposed/events/reconciliation.md", "docs/proposed/events/README.md", "events/README.md") + ". Complete captured snapshot: " + link("docs/proposed/events/reconciliation.md", "docs/proposed/data/event-reconciliation-snapshot.json", "JSON") + ".",
  "",
  "## Recorded snapshot counts",
  "",
  table(["Field", "Value"], [
    ["Source checkpoint", eventReconciliationCatalog.sourceCheckpoint],
    ["Raw name union", eventReconciliationCatalog.summary && eventReconciliationCatalog.summary.rawNameUnion],
    ["Snapshot event rows", (eventReconciliationCatalog.events || []).length],
    ["Intended emitted canonical semantic events in snapshot", eventReconciliationCatalog.summary && eventReconciliationCatalog.summary.intendedEmittedCanonicalSemanticEvents],
    ["Selected schema names in snapshot", eventReconciliationCatalog.summary && eventReconciliationCatalog.summary.selectedSchemaNames],
    ["Runtime/schema qualification", eventReconciliationCatalog.summary && eventReconciliationCatalog.summary.schemaAndRuntimeImplementationQualified],
  ]),
  "",
  "## Name categories",
  "",
  table(["Category", "Rows"], eventCategoryCounts),
  "",
  "## Source dispositions",
  "",
  table(["Disposition", "Rows"], eventDispositionCounts),
  "",
  "[Placeholder: reconcile all historical names, aliases, and dispositions against the latest 501-row canonical event catalog.]",
].join("\n"));

write("docs/proposed/reactors/README.md", [
  "# Reactors and choreography",
  "",
  "Selected architecture rows: **" + flows.length + "**. Each row preserves source, target, trigger, required proofs, effect identity, recovery, and implementation-conformance details in JSON.",
  "",
  table(["Root", "Rows"], [...flowRootRows.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([name, rows]) => [
    link("docs/proposed/reactors/README.md", "docs/proposed/reactors/" + slug(name) + ".md", reactorName(name)),
    rows.length,
  ])),
  "",
  "Rows without a root name: " + link("docs/proposed/reactors/README.md", "docs/proposed/reactors/unassigned-architecture-rows.md", "unassigned architecture rows") + ".",
  "Program rollover: " + link("docs/proposed/reactors/README.md", "docs/proposed/processes/program-rollover-execution-process.md", "process page") + ".",
  "Physical registration snapshot: " + link("docs/proposed/reactors/README.md", "docs/proposed/reactors/registration-plan.md", "registration-plan.md") + ".",
].join("\n"));

const physicalCounts = physicalPlanCatalog.selectedCounts || {};
write("docs/proposed/reactors/registration-plan.md", [
  "# Physical registration plan snapshot",
  "",
  "This captured plan is tied to source checkpoint " + code(choreographyManifestCatalog.sourceCheckpoint || "[Placeholder: checkpoint not recorded]") + " and reports status " + code(physicalPlanCatalog.status || "[Placeholder: status not recorded]") + ". It is implementation evidence, not proof of current registration or runtime qualification.",
  "",
  "The latest choreography catalog lists **" + flows.length + "** architecture rows and **" + new Set(flows.flatMap((item) => item.currentRegisteredRootTypes || [])).size + "** registered root types. This earlier physical plan records the following counts:",
  "",
  table(["Field", "Value"], [
    ["Existing registered reactors", physicalCounts.existingRegisteredReactors],
    ["Planned Artifact root starter", physicalCounts.plannedAdditionalArtifactRootStarters],
    ["Planned ProgramRollover root starter", physicalCounts.plannedAdditionalProgramRolloverRootStarters],
    ["Selected total physical root reactors", physicalCounts.selectedTotalPhysicalRootReactorsAfterImplementation],
    ["Existing process definitions", physicalCounts.existingRegisteredProcessDefinitions],
    ["Planned process definitions", physicalCounts.plannedProcessDefinitions],
    ["Selected total process definitions", physicalCounts.selectedTotalProcessDefinitionsAfterImplementation],
    ["Additional process driver types", physicalCounts.additionalProcessDriverTypes],
    ["Additional hosted worker types", physicalCounts.additionalHostedWorkerTypes],
  ]),
  "",
  "Planned process definitions: " + display(physicalPlanCatalog.plannedProcessDefinitionTypes || "[Placeholder: planned process types not recorded]") + ".",
  "",
  "Complete reactor registrations, source hashes, host activation, and process aliases: " + link("docs/proposed/reactors/registration-plan.md", "docs/proposed/data/physical-registration-plan-snapshot.json", "physical plan JSON") + ".",
  "",
  "[Placeholder: reconcile this checkpointed registration plan with the latest selected choreography and current source registrations.]",
].join("\n"));

const existingReactorRows = physicalPlanCatalog.reactors || [];
const plannedStarters = [physicalPlanCatalog.plannedArtifactRootStarter, physicalPlanCatalog.plannedProgramRolloverRootStarter].filter(Boolean);
const plannedProcesses = physicalPlanCatalog.plannedProcessDefinitionTypes || [];
const workerSources = physicalPlanCatalog.processInfrastructure && physicalPlanCatalog.processInfrastructure.workerSource || [];
const plannedStarterStatuses = new Map((physicalPlanCatalog.plannedAdditionalRegistration || []).map((item) => [item.reactor, item.status]));
write("docs/proposed/worker-catalog.md", [
  "# Worker and process catalog",
  "",
  "This is a structured capture of selected domain reactors, durable process execution, and host activation. The physical registration plan is checkpointed evidence, not a claim that the selected target has been implemented or qualified.",
  "",
  "## Host activation",
  "",
  table(["Host mode", "Source", "Lines", "Registered work"], (physicalPlanCatalog.hostActivation || []).map((host) => [host.host, host.source, host.lines, host.effects])),
  "",
  "The selected process plan says that API-only mode does not activate processing. The worker-running API/standalone modes and worker-only branch both register Portia workers, " + code("ProcessSweep") + ", and " + code("WorkDigestDispatchBackgroundService") + ".",
  "",
  "## Existing selected reactor registrations",
  "",
  "Existing root reactors recorded by this snapshot: **" + existingReactorRows.length + "**.",
  "",
  table(["Reactor", "Workload", "Scope", "Source events", "Selected rows"], existingReactorRows.map((reactor) => [
    reactor.type,
    reactor.workloadName,
    reactor.scope,
    reactor.sourceEvents,
    (reactor.selectedEdgeIds || []).concat(reactor.supplementalEdgeIds || []),
  ])),
  "",
  "## Planned root starters",
  "",
  "The selected target adds two root starters with new workload identities; implementation remains unauthorized/unimplemented in the source plan.",
  "",
  table(["Role", "Reactor", "Workload", "Scope", "Source events", "Status"], plannedStarters.map((starter) => [
    starter.role,
    starter.plannedTypeName,
    starter.plannedWorkloadName,
    starter.scope,
    starter.sourceEvents || starter.eventPattern,
    starter.implementationStatus || plannedStarterStatuses.get(starter.plannedTypeName) || "[Placeholder: starter implementation status not recorded]",
  ])),
  "",
  "## Durable process runtime",
  "",
  table(["Component or rule", "Selected detail"], [
    ["Common components", physicalPlanCatalog.processInfrastructure && physicalPlanCatalog.processInfrastructure.common],
    ["Root admission", physicalPlanCatalog.processInfrastructure && physicalPlanCatalog.processInfrastructure.processRootRule],
    ["Driver", physicalPlanCatalog.processInfrastructure && physicalPlanCatalog.processInfrastructure.driverRule],
    ["Worker activation rule", physicalPlanCatalog.processInfrastructure && physicalPlanCatalog.processInfrastructure.workerRegistration],
    ["Existing process definitions", physicalPlanCatalog.selectedCounts && physicalPlanCatalog.selectedCounts.existingRegisteredProcessDefinitions],
    ["Planned process definitions", physicalPlanCatalog.selectedCounts && physicalPlanCatalog.selectedCounts.plannedProcessDefinitions],
    ["Selected total definitions", physicalPlanCatalog.selectedCounts && physicalPlanCatalog.selectedCounts.selectedTotalProcessDefinitionsAfterImplementation],
    ["Additional driver types", physicalPlanCatalog.selectedCounts && physicalPlanCatalog.selectedCounts.additionalProcessDriverTypes],
    ["Additional hosted worker types", physicalPlanCatalog.selectedCounts && physicalPlanCatalog.selectedCounts.additionalHostedWorkerTypes],
  ]),
  "",
  "Existing registered definitions: " + display(physicalPlanCatalog.registeredDefinitionTypes || "[Placeholder: registered process definition list not recorded]") + ".",
  "",
  "Planned definitions: " + display(plannedProcesses) + ".",
  "",
  "Process identities, ordered stages, and aliases: " + link("docs/proposed/worker-catalog.md", "docs/proposed/processes/README.md", "process catalog") + " and " + display(physicalPlanCatalog.processAliases || "[Placeholder: process aliases not recorded]") + ".",
  "",
  "## Other technical orchestrations",
  "",
  table(["Type", "Entry", "Authority", "Recovery"], (physicalPlanCatalog.otherTechnicalOrchestrations || []).map((item) => [item.type, item.entry || item.registration, item.authority, item.recovery])),
  "",
  "## Runtime evidence and open details",
  "",
  "Process infrastructure source records: " + display(workerSources.map((item) => item.path + " (" + item.sha256 + ")")) + ".",
  "",
  "Current selected process definitions: " + processDefinitions.length + "; the physical plan records " + display(physicalPlanCatalog.selectedCounts && physicalPlanCatalog.selectedCounts.existingRegisteredProcessDefinitions) + " existing registered definitions and " + display(physicalPlanCatalog.selectedCounts && physicalPlanCatalog.selectedCounts.plannedProcessDefinitions) + " planned definitions.",
  "",
  "[Placeholder: worker polling intervals, process timeout policy, cross-host singleton guarantees, operational alerting, and operator runbooks are not consolidated in the selected worker catalog.]",
  "",
  "Complete physical plan snapshot: " + link("docs/proposed/worker-catalog.md", "docs/proposed/data/physical-registration-plan-snapshot.json", "JSON") + ".",
].join("\n"));

write("docs/proposed/partner-seam.md", [
  "# Provider and external-source seams",
  "",
  "This page separates governed provider/partner facts from future integrations that observe external systems. It captures current decisions and manual source paths without proposing a connector.",
  "",
  "## Governed providers and subservice organizations",
  "",
  "A " + link("docs/proposed/partner-seam.md", "docs/proposed/aggregates/provider.md", "Provider") + " is a domain record about a service provider. Provider assurance reports, reviews, coverage gaps, and risk acceptance have their own selected owners: " + ["provider-assurance-report", "provider-assurance-review", "provider-assurance-gap", "provider-risk-acceptance"].map((id) => link("docs/proposed/partner-seam.md", "docs/proposed/aggregates/" + id + ".md", id)).join(", ") + ". These records do not imply a live connection to the provider.",
  "",
  "M0-D11 selects these rules: materiality is based on customer-data processing/storage or a critical-path role, not spend; material providers need current SOC 2 or ISO/IEC 27001 evidence, with a completed security questionnaire accepted when neither exists; material reviews recur annually; and subservice organizations default to carve-out with CSOCs represented as subservice-responsibility commitments. A carved-out subservice without a CSOC and a material provider without a current review are readiness gaps.",
  "",
  "Decision source: " + link("docs/proposed/partner-seam.md", "docs/product/decisions/m0-d11-vendors.md", "M0-D11") + ". The selected data-shape alignment to M0-D28 is recorded in that decision; exact source/catalog implementation mapping remains visible on the owner pages.",
  "",
  "## External source and integration boundary",
  "",
  "M0-D20 defers connector/import work in R1/R2 until the canonical data shape and storage internals are settled. The product remains usable through its manual path; future connectors are optional enhancements. Initial candidate sources are AWS accounts, the GitHub organization, the identity-provider tenant, and core SaaS admin access such as Slack and Jira.",
  "",
  "For technology inventory, M0-D08 makes the governed Compliance inventory authoritative; cloud-console, MDM, and repository-host data are discovery-only. Its component, information-classification, and data-flow rules are included in the proposed domain model.",
  "",
  "The manual workforce observation path is separately captured in " + link("docs/proposed/partner-seam.md", "docs/product/workforce-source-governance.md", "workforce source governance") + ". It records observed external facts, explicit correlation, authority labels, and human reconciliation. Provider observations can corroborate service-identity facts but do not assign governed owner, purpose, or review date.",
  "",
  "A future source adapter must write observations through the canonical source shape and the staged all-or-nothing acceptance boundary described by " + link("docs/proposed/partner-seam.md", "docs/architecture/decisions/0005-import-reconciliation-and-background-processing.md", "ADR 0005") + ". A callback or status endpoint is an observation, not proof of complete partner state.",
  "",
  "## Connector proposal record",
  "",
  "M0-D20 requires a proposal to answer the recorded integration questions below before connector implementation. The source heading says ten questions, but it numbers thirteen; all thirteen are preserved here pending reconciliation.",
  "",
  "1. Which business question does the source answer?",
  "2. Is it authoritative, corroborating, or discovery-only?",
  "3. What are its stable identifiers, scope boundaries, timestamps, pagination, and deletion or tombstone behavior?",
  "4. How is a complete population proven, and how is partial collection detected?",
  "5. How do records match without silently merging identities or systems?",
  "6. How do changes become reviewable proposals rather than automatic truth?",
  "7. What are the least-privilege, credential-custody, rate-limit, retry, and revocation models?",
  "8. What freshness and reconciliation cadence is required, and who owns it?",
  "9. Which raw snapshots and normalized facts must be retained for audit proof?",
  "10. How does this integration make the existing manual workflow easier to start, maintain, or operate, and how does that workflow stay usable when the integration is unavailable or disabled?",
  "11. Which partner-side writes can have an ambiguous outcome when a request or response is lost, and what idempotency key, lookup, or operator recovery path prevents an unsafe duplicate write?",
  "12. Which states depend on callbacks, partner staff, or partner communications; how are missing, late, duplicate, out-of-order, and unknown signals detected and reconciled, and who owns each manual follow-up?",
  "13. What contract version, authentication and signature rules, validation and product rules, limits, and environment differences have been verified against representative test cases? Which behaviors remain untestable or unconfirmed before launch?",
  "",
  "A connector must not blindly repeat a partner-side write after an unknown response. It needs partner-supported idempotency, reliable lookup, or visible operator recovery. Verified contract/version, authentication, callbacks, state transitions, limits, retries, test cases, and remaining launch risks stay explicit.",
  "",
  "## Source and unresolved details",
  "",
  "- Product decision: " + link("docs/proposed/partner-seam.md", "docs/product/decisions/m0-d20-integration-sources.md", "M0-D20") + ".",
  "- Provider policy: " + link("docs/proposed/partner-seam.md", "docs/product/decisions/m0-d11-vendors.md", "M0-D11") + ".",
  "- Technology inventory decisions: " + link("docs/proposed/partner-seam.md", "docs/product/decisions/m0-d08-technology-inventory.md", "M0-D08") + ".",
  "- Workforce source contract: " + link("docs/proposed/partner-seam.md", "docs/product/workforce-source-governance.md", "workforce-source-governance.md") + ".",
  "- Canonical model: " + link("docs/proposed/partner-seam.md", "docs/product/canonical-entity-model.md", "canonical-entity-model.md") + ".",
  "",
  "[Placeholder: candidate-specific source authority, verified partner contract, authentication, credentials, limits, retries, cadence, acceptance tests, and operational owner are unknown until a connector is selected.]",
].join("\n"));

write("docs/proposed/processes/README.md", [
  "# Process definitions",
  "",
  "Selected process definitions: **" + processDefinitions.length + "**.",
  "",
  table(["Process", "Root source events", "Ordered"], processRows),
  "",
  "ProgramRollover execution narrative and selected stages: " + link("docs/proposed/processes/README.md", "docs/proposed/processes/program-rollover-execution-process.md", "program-rollover-execution-process.md") + ".",
].join("\n"));

// These captured Markdown pages are maintained in docs/proposed after their former paths become pointers.
writeJson("docs/proposed/data/read-model-contracts.json", readSchemaCatalog);
write("docs/proposed/diagrams/README.md", [
  "# Domain diagrams",
  "",
  "Product journey diagrams are captured in " + link("docs/proposed/diagrams/README.md", "docs/proposed/journeys.md", "journeys.md") + ".",
  "Selected reaction flows are indexed in " + link("docs/proposed/diagrams/README.md", "docs/proposed/reactors/README.md", "reactors/README.md") + ".",
  "",
  "[Placeholder: owner lifecycle diagrams are not present in the selected catalog.]",
  "[Placeholder: a generated Mermaid rendering of the choreography graph is not part of this pass.]",
].join("\n"));

const productDecisions = fs.readdirSync(path.join(root, "docs/product/decisions")).filter((item) => item.endsWith(".md")).sort();
write("docs/proposed/decisions-log.md", [
  "# Architecture and product decisions",
  "",
  "Architecture decisions: **" + decisions.length + "**. Complete selected records: " + link("docs/proposed/decisions-log.md", "docs/proposed/data/decisions.json", "decision JSON") + ".",
  "",
  table(["ID", "Status", "Choice", "Rationale"], decisions.map((item) => [item.id, item.status, item.choice, item.rationale])),
  "",
  "The source catalog retains some older OPEN labels. Their proposed write-model disposition is recorded in " + link("docs/proposed/decisions-log.md", "docs/proposed/write-model-domain-rules.md", "Write model domain rules") + ".",
  "",
  "Product decision records remain the individual authority:",
  "",
  productDecisions.map((file) => "- " + link("docs/proposed/decisions-log.md", "docs/product/decisions/" + file, file)).join("\n"),
].join("\n"));

const domainDecisions = writeModelDomainDecisionCatalog.decisions || [];
const domainDecisionPage = "docs/proposed/write-model-domain-rules.md";
write(domainDecisionPage, [
  "# Write model domain rules",
  "",
  writeModelDomainDecisionCatalog.scope,
  "",
  writeModelDomainDecisionCatalog.authority,
  "",
  "## Selected decisions",
  "",
  table(["Decision", "Selected rule"], domainDecisions.map((item) => [item.name, item.rule])),
  "",
  ...domainDecisions.flatMap((item) => [
    "### " + item.id,
    "",
    "**Selected rule:** " + item.rule,
    "",
    "**Why:** " + item.reason,
    "",
    "**Owners:** " + namedList(item.affectedOwners, targetOwnerName) + ".",
    "",
    "**Commands:** " + namedList(item.commands, commandName) + ".",
    "",
    "**Events:** " + namedList(item.events, eventName) + ".",
    "",
    "**Reactors:** " + namedList(item.reactors, reactorName) + ".",
    "",
    "**Source evidence:** " + (item.sources || []).map((source) => sourceLink(domainDecisionPage, source)).join(", ") + ".",
    "",
  ]),
  "## Lifecycle by owner",
  "",
  table(["Owner", "Decision", "Lifecycle rule", "Evidence"], Object.entries(writeModelDomainDecisionCatalog.ownerRules || {}).map(([owner, item]) => [
    link(domainDecisionPage, "docs/proposed/aggregates/" + slug(targetOwnerName(owner)) + ".md", targetOwnerName(owner)),
    item.decisionRule,
    item.lifecycle,
    item.evidence,
  ])),
  "",
  "## Source event compatibility",
  "",
  sourceCompatibilityEventRows.length ? table(["Selected source event", "Source owner", "Target owner", "Related target event", "Reason"], sourceCompatibilityEventRows.map((item) => [
    code(item.event),
    code(item.sourceOwner),
    code(item.targetOwner),
    code(item.relatedTargetEvent),
    item.reason,
  ])) : "None listed.",
  "",
  "## Deferred to event-contract details",
  "",
  table(["Source label", "Disposition"], (writeModelDomainDecisionCatalog.deferredMechanics || []).map((item) => [item.id, item.decision])),
].join("\n"));

const inputFiles = Object.values(sources);
const counts = {
  businessOwners: businessOwners.length,
  technicalResponsibilities: technicalOwners.length,
  selectedCommandContracts: commandContracts.length,
  canonicalEvents: events.length,
  targetBusinessEvents: targetEventUniverse.length,
  proposedEventsWithoutSelectedSourceContract: targetOnlyEvents.length,
  targetEventSchemas: targetEventUniverse.filter((item) => eventSchemas.has(item.event)).length,
  choreographyRows: flows.length,
  registeredRootTypes: new Set(flows.flatMap((item) => item.currentRegisteredRootTypes || [])).size,
  namedReactorRoots: flowRootRows.size,
  targetCommandNames: new Set(commandContracts.map((item) => commandName(item.command))).size,
  targetEventNames: new Set(targetEventUniverse.map((item) => eventName(item.event))).size,
  processDefinitions: processDefinitions.length,
  architectureDecisions: decisions.length,
  productRulesNeedingImplementationEvidence: rulesMissingSource.length,
};
const commandNameChanges = commandContracts.map((item) => ({
  sourceName: item.command,
  targetName: commandName(item.command),
  reason: commandNameDecision(item.command).reason,
})).filter((item) => item.targetName !== item.sourceName);
const eventNameChanges = targetSourceEvents.map((item) => ({
  sourceName: item.event,
  targetName: eventName(item.event),
  reason: eventNameDecision(item.event).reason,
})).filter((item) => item.targetName !== item.sourceName);
const ownerNameChanges = [...ownerNameOverrides.values()].map((item) => ({
  sourceName: item.sourceOwner,
  targetName: item.targetOwner,
  reason: item.reason,
}));
const targetOnlyEventNames = targetOnlyEvents.map((item) => ({
  name: item.event,
  owner: item.owners[0],
  reason: item.nameReason,
  meaning: item.meaning,
}));
const reactorNameChanges = [...flowRootRows.keys()].map((name) => {
  const decision = reactorNameDecision(name);
  return { sourceName: name, targetName: decision.targetName, reason: decision.businessReason };
}).filter((item) => item.targetName !== item.sourceName);
const retainedEventDecisions = Object.entries(eventNameDecisions).filter(([name, decision]) => decision.targetName === name);
const meaningClarifications = Object.entries(eventNameDecisions).filter(([, decision]) => decision.meaning);
write("docs/proposed/write-model-names.md", [
  "# Write model names",
  "",
  "This is the naming target for commands → events → reactors. The complete target names appear on the generated command, event, and reactor pages; this page records the naming rules and every changed name.",
  "",
  "## Naming rules",
  "",
  ...writeModelNameCatalog.principles.map((item) => "- " + item),
  "",
  "## Coverage",
  "",
  table(["Write model element", "Target names"], [
    ["Commands", new Set(commandContracts.map((item) => commandName(item.command))).size],
    ["Events", new Set(targetEventUniverse.map((item) => eventName(item.event))).size],
    ["Named reactor roots", new Set(reactorRoots.map((item) => reactorName(item))).size],
  ]),
  "",
  "Selected command-contract entries: " + commandContracts.length + "; distinct source command names: " + new Set(commandContracts.map((item) => item.command)).size + ". Repeated source-command entries under multiple owners share their target name. Each distinct command, event, and reactor root has a unique target name. Every named reactor root has a stated business reason. Names not listed as changed retain their selected source name. Repeated authorization command/event names follow the suffix rules recorded in " + link("docs/proposed/write-model-names.md", "docs/proposed/data/write-model-names.json", "the structured naming decisions") + ".",
  "",
  "## Commands",
  "",
  table(["Selected source name", "Target command name", "Why it names the requested action"], commandNameChanges.map((item) => [code(item.sourceName), code(item.targetName), item.reason])),
  "",
  "### Command names retained\n\n" + table(["Name retained", "Why it is the right action"], Object.entries(writeModelNameCatalog.retainedCommands || {}).map(([name, decision]) => [code(name), decision.reason])),
  "",
  "## Events",
  "",
  table(["Selected source name", "Target event name", "Why it names the completed fact"], eventNameChanges.map((item) => [code(item.sourceName), code(item.targetName), item.reason])),
  "",
  retainedEventDecisions.length ? "### Event names retained for exact meaning\n\n" + table(["Name retained", "Reason"], retainedEventDecisions.map(([name, decision]) => [code(name), decision.reason])) : "",
  meaningClarifications.length ? "### Meaning clarified from the choreography\n\n" + table(["Target event name", "Meaning"], meaningClarifications.map(([name, decision]) => [code(decision.targetName), decision.meaning])) : "",
  "",
  "### New target event names\n\n" + table(["Target event", "Owner", "Why this fact is recorded", "Meaning"], targetOnlyEventNames.map((item) => [code(item.name), code(item.owner), item.reason, item.meaning])),
  "",
  "Event discriminator strings and versions remain exactly as recorded in the source contracts. The target type name is a separate decision; implementation must preserve wire compatibility while adopting the target name.",
  "",
  "## Reactors",
  "",
  table(["Selected root name", "Target reactor name", "Business reason"], reactorNameChanges.map((item) => [code(item.sourceName), code(item.targetName), item.reason])),
  "",
  "## Owner names\n\n" + table(["Selected source owner", "Target owner", "Why the target name fits"], ownerNameChanges.map((item) => [code(item.sourceName), code(item.targetName), item.reason])),
  "",
  "## Implementation boundary",
  "",
  "These target names change the proposed model only. Selected source names, routes, command identities, event discriminator strings, and discriminator versions remain compatibility evidence. This page makes no claim that the renamed contracts are already implemented.",
].filter((line) => line !== undefined).join("\n"));
const manifest = {
  generatedOn: new Date().toISOString().slice(0, 10),
  provenance: "Rendered from selected repository sources plus the explicit proposed write-model naming decisions.",
  counts,
  sourceHashes: Object.fromEntries(inputFiles.map((file) => [
    file,
    crypto.createHash("sha256").update(fs.readFileSync(path.join(root, file))).digest("hex"),
  ])),
};
writeJson("docs/proposed/data/manifest.json", manifest);
write("docs/proposed/sources.md", [
  "# Source map",
  "",
  "The reorganized domain catalog lives in docs/proposed. Structured JSON catalogs remain renderer inputs. Superseded duplicate indexes, briefs, and rollups are short forwarding notes; approved product/architecture decisions and delivery/review evidence remain at their original paths as authorities. Per-owner JSON records preserve complete selected contracts, and external temporary paths are explicit placeholders.",
  "",
  table(["Source", "Captured as"], [
    [link("docs/proposed/sources.md", sources.aggregates), "Aggregate, command, and owner records."],
    [link("docs/proposed/sources.md", sources.events), "Canonical event records and adopted wire identities."],
    [link("docs/proposed/sources.md", sources.choreography), "Reactor and process-stage records."],
    [link("docs/proposed/sources.md", sources.decisions), "Architecture decision log."],
    [link("docs/proposed/sources.md", sources.eventReconciliation), "Historical event-name/disposition snapshot; reconciliation with the latest event catalog remains open."],
    [link("docs/proposed/sources.md", sources.physicalPlan), "Checkpointed physical registration plan; current-source qualification remains open."],
    [link("docs/proposed/sources.md", sources.choreographyManifest), "Historical manifest for the physical plan and its 186-row baseline."],
    [link("docs/proposed/sources.md", sources.commandSchemas), "Companion command/handler source evidence."],
    [link("docs/proposed/sources.md", sources.eventSchemas), "Companion event payload schemas."],
    [link("docs/proposed/sources.md", sources.writeModelNames), "Proposed target names and business reasons for commands, events, and reactors."],
    [link("docs/proposed/sources.md", sources.readSchemas), "Operation-query contracts."],
    [link("docs/proposed/sources.md", "docs/product/canonical-entity-model.md"), "Canonical entity families, standards use, source authority, and manual-first constraint are integrated in domain-model.md; the approved conceptual source remains available for exact mapping."],
    [link("docs/proposed/sources.md", "docs/product/decisions/m0-d08-technology-inventory.md"), "External-source authority and no-connector decision are summarized in partner-seam.md."],
    [link("docs/proposed/sources.md", "docs/product/decisions/m0-d11-vendors.md"), "Provider materiality, assurance, review, and subservice rules are summarized in partner-seam.md."],
    [link("docs/proposed/sources.md", "docs/product/decisions/m0-d20-integration-sources.md"), "Manual-first connector boundary and all recorded proposal questions are captured in partner-seam.md."],
    [link("docs/proposed/sources.md", "docs/product/workforce-source-governance.md"), "Existing manual external-source observation and reconciliation path is distinguished from connectors in partner-seam.md."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/README.md"), "Unique architecture rules captured in architecture-overview.md."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/aggregate-details.md"), "Owner records, boundary notes, technical responsibilities, and historical OPEN labels captured."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/aggregates.md"), "Owner summaries captured in the aggregate index and per-owner pages."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/events.md"), "Canonical emissions captured in per-owner event pages; historical names captured separately."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/choreography.md"), "Architecture rows captured under reactors; rollover prose is on the selected process page."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/product-rule-traceability.md"), "Captured in product-rule-traceability.md."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/aggregate-backlog.md"), "Kept as delivery evidence."],
    [link("docs/proposed/sources.md", "docs/product/definitive-aggregate-choreography/aggregate-status.md"), "Kept as implementation evidence."],
  ]),
  "",
  "A " + code("[Placeholder: ...]") + " marks missing or conflicting detail. It is not a domain rule.",
].join("\n"));

write("docs/proposed/README.md", [
  "# Proposed domain model",
  "",
  "This is the working target for Compliance Core implementation. The current refinement scope is the write model: commands → events → reactors. The naming decisions below are intended to become the implementation names after the affected contracts are updated.",
  "",
  "The proposed pages explain business outcomes and preserve enough source names and wire details to map the target back to current contracts. Unknown or conflicting details use " + code("[Placeholder: ...]") + ".",
  "",
  "## Current target: commands → events → reactors",
  "",
  "- " + link("docs/proposed/README.md", "docs/proposed/write-model-names.md", "Write model names") + " records target names and the reason for each changed name.",
  "- " + link("docs/proposed/README.md", "docs/proposed/write-model-domain-rules.md", "Write model domain rules") + " records the selected business rules for commands, events, and reactors.",
  "- " + link("docs/proposed/README.md", "docs/proposed/commands/README.md", "Commands") + " state the requested actions; " + link("docs/proposed/README.md", "docs/proposed/events/README.md", "events") + " state completed facts; " + link("docs/proposed/README.md", "docs/proposed/reactors/README.md", "reactors") + " state why each reaction exists.",
  "- " + link("docs/proposed/README.md", "docs/proposed/domain-model.md", "Domain model") + ", " + link("docs/proposed/README.md", "docs/proposed/scope.md", "product scope") + ", and " + link("docs/proposed/README.md", "docs/proposed/journeys.md", "journeys") + " provide broader product context.",
  "- " + link("docs/proposed/README.md", "docs/proposed/aggregates/README.md", "Aggregates") + " — one page per business owner and technical responsibility.",
  "",
  "## Later reference material",
  "",
  "- " + link("docs/proposed/README.md", "docs/proposed/read-models.md", "Read models") + " and " + link("docs/proposed/README.md", "docs/proposed/processes/README.md", "processes") + " remain available for later refinement.",
  "- " + link("docs/proposed/README.md", "docs/proposed/worker-catalog.md", "Worker catalog") + " and " + link("docs/proposed/README.md", "docs/proposed/reactors/registration-plan.md", "registration plan") + " record implementation context.",
  "- " + link("docs/proposed/README.md", "docs/proposed/partner-seam.md", "Provider and external-source seams") + " records governed provider facts, the manual-first connector boundary, and placeholders for future partner contracts.",
  "- " + link("docs/proposed/README.md", "docs/proposed/architecture-overview.md", "Architecture notes") + ", " + link("docs/proposed/README.md", "docs/proposed/aggregate-boundary-notes.md", "boundary notes") + ", and " + link("docs/proposed/README.md", "docs/proposed/product-rule-traceability.md", "product-rule traceability") + ".",
  "- " + link("docs/proposed/README.md", "docs/proposed/decisions-log.md", "decisions") + ", " + link("docs/proposed/README.md", "docs/proposed/open-gaps.md", "open details") + ", and " + link("docs/proposed/README.md", "docs/proposed/sources.md", "source map") + ".",
  "",
  "## Inventory",
  "",
  table(["Catalog", "Rows"], Object.entries(counts).map(([name, value]) => [name, value])),
  "",
  "## Authority and qualification",
  "",
  "The proposed target names are recorded in " + link("docs/proposed/README.md", "docs/proposed/data/write-model-names.json", "write-model-names.json") + ". The definitive catalog and current code provide the source names, business rules, route contracts, and event wires used to reconcile that target. Renamed event types keep their selected discriminator strings and versions.",
  "",
  "Implementation evidence and delivery sequencing remain in " + link("docs/proposed/README.md", "docs/product/definitive-aggregate-choreography/aggregate-status.md", "aggregate-status.md") + " and " + link("docs/proposed/README.md", "docs/product/definitive-aggregate-choreography/aggregate-backlog.md", "aggregate-backlog.md") + ".",
  "",
  "## Regeneration",
  "",
  "Run " + code("node docs/proposed/generate.mjs") + " from the repository root after updating structured JSON catalogs. It refreshes catalog-derived pages and snapshots; captured product prose, boundary notes, and open-gap explanations are maintained in docs/proposed. The renderer does not delete generated files missing from a later source snapshot.",
  "",
  "Source hashes: " + link("docs/proposed/README.md", "docs/proposed/data/manifest.json", "data/manifest.json") + ".",
].join("\n"));
