import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const proposedDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(proposedDir, "../..");
const dataDir = path.join(proposedDir, "data");
const resultFile = path.join(dataDir, "naming-review-jev.json");
const reportFile = path.join(proposedDir, "naming-review.md");
const endpoint = "https://api.typesafe.ai/v1/systemone";
const model = "jev-1.14.0";
const protocolVersion = "stakeholder-name-review-v2";
const maxBatchItems = 18;
const maxBatchDocumentCharacters = 48000;
const maxSectionCharacters = 6500;
const retryLimit = 6;

const audiences = [
  "Compliance program owners and practitioners",
  "Client administrators and operational members",
  "External auditors and assessors",
  "Product and domain analysts",
  "Backend engineers implementing the authority and contract",
];

const scoreCriteria = {
  aggregate: [
    "The name is an implementation or storage mechanism, an unclear artifact label, or does not identify the business record that owns the stated invariant.",
    "A stakeholder would need to learn repository-specific jargon before recognizing the business record; the noun or qualifier obscures its purpose.",
    "The name is a recognizable domain noun and broadly matches the record, but it is specialized, ambiguous, or unnecessarily technical for some stakeholders.",
    "The name is a plain, precise business noun that matches the stated invariant and is recognizable across the listed stakeholder groups.",
  ],
  technical_responsibility: [
    "The name does not identify the recorded technical responsibility.",
    "The name is opaque shorthand or depends on unexplained local jargon.",
    "An engineer can infer the responsibility, but the name is broad or uses a term that needs nearby explanation.",
    "The name clearly and concisely identifies the technical responsibility without implying a business aggregate.",
  ],
  command: [
    "The name describes storage, orchestration, transport, or another internal mechanism instead of a business action.",
    "The action or object is vague, specialized, or obscured by implementation language.",
    "The name identifies a plausible domain action, but some stakeholders need the definition to understand its object or outcome.",
    "The name is a clear, specific action in familiar domain language and accurately describes what the owner is asked to do.",
  ],
  event: [
    "The name is chiefly an implementation signal and does not identify a durable business fact.",
    "The name signals a change but uses vague or specialized language that hides what happened.",
    "The name identifies a domain fact, but a stakeholder may need the definition to understand the outcome or affected record.",
    "The name is a clear, specific business fact in familiar language and matches the recorded meaning and authority.",
  ],
};

const questionKinds = ["clarity", "business_user", "auditor", "engineering", "jargon", "ambiguity"];

function sha256(value) {
  return crypto.createHash("sha256").update(value).digest("hex");
}

function readJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(repoRoot, relative), "utf8"));
}

function compact(value, max = 1800) {
  const text = Array.isArray(value) ? value.map(compact).join("; ") : typeof value === "object" && value !== null
    ? JSON.stringify(value)
    : value === undefined || value === null || value === ""
      ? "[Placeholder: not recorded]"
      : String(value);
  const flat = text.replace(/\s+/g, " ").trim();
  return flat.length > max ? flat.slice(0, max - 15) + " [truncated]" : flat;
}

function safeToken(value) {
  return String(value).toLowerCase().replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
}

function headingAnchor(value) {
  return String(value).toLowerCase().replace(/[^a-z0-9 -]/g, "").trim().replace(/\s+/g, "-");
}

function sectionText(file, heading, level = 3) {
  const content = fs.readFileSync(path.join(repoRoot, file), "utf8");
  const lines = content.split(/\r?\n/);
  const target = "#".repeat(level) + " " + heading;
  const start = lines.findIndex((line) => line === target);
  if (start < 0) return content;
  let end = lines.length;
  const stopPattern = new RegExp("^#{1," + level + "} ");
  for (let index = start + 1; index < lines.length; index++) {
    if (stopPattern.test(lines[index])) {
      end = index;
      break;
    }
  }
  return lines.slice(start, end).join("\n");
}

function boundedDocumentText(text) {
  if (text.length <= maxSectionCharacters) return text;
  const marker = "\n\n[Excerpt truncated for name review; consult the linked document for the complete contract.]\n\n";
  const headSize = Math.floor((maxSectionCharacters - marker.length) * 0.78);
  const tailSize = maxSectionCharacters - marker.length - headSize;
  return text.slice(0, headSize) + marker + text.slice(-tailSize);
}

function itemRecord(type, name, owner, file, meaning, details, documentText, sourceHash) {
  const id = [type, safeToken(owner), safeToken(name), sha256(file).slice(0, 10)].join("__");
  const excerpt = boundedDocumentText(documentText);
  return {
    id,
    type,
    name,
    owner,
    document: file,
    meaning: compact(meaning),
    context: details,
    documentSectionSha256: sha256(documentText),
    documentSectionCharacters: documentText.length,
    sourceRecordSha256: sourceHash,
    requestExcerpt: excerpt,
  };
}

function collectItems() {
  const items = [];
  const ownerRecords = new Map();
  const ownerDir = path.join(dataDir, "aggregates");
  for (const fileName of fs.readdirSync(ownerDir).filter((name) => name.endsWith(".json")).sort()) {
    const relative = "docs/proposed/data/aggregates/" + fileName;
    const record = readJson(relative);
    const owner = record.owner || path.basename(fileName, ".json");
    const page = "docs/proposed/aggregates/" + path.basename(fileName, ".json") + ".md";
    const type = record.businessAggregate === true ? "aggregate" : "technical_responsibility";
    const meaning = [record.invariant, record.selectedIdentitySummary, record.decides, record.mustPreserve]
      .filter(Boolean).join(" ");
    const details = {
      classification: record.classification,
      businessAggregate: record.businessAggregate,
      selectedIdentity: compact(record.selectedIdentitySummary || record.selectedIdentityClarification, 1100),
      invariant: compact(record.invariant, 1100),
      decisionRule: compact(record.decides, 1100),
      implementationStatus: record.implementationStatus,
    };
    const documentText = fs.readFileSync(path.join(repoRoot, page), "utf8");
    items.push(itemRecord(type, owner, owner, page, meaning, details, documentText, sha256(fs.readFileSync(path.join(repoRoot, relative)))));
    ownerRecords.set(owner, record);
  }

  const commandDir = path.join(dataDir, "commands");
  for (const fileName of fs.readdirSync(commandDir).filter((name) => name.endsWith(".json")).sort()) {
    const relative = "docs/proposed/data/commands/" + fileName;
    const record = readJson(relative);
    const owner = record.owner || path.basename(fileName, ".json");
    const page = "docs/proposed/commands/" + path.basename(fileName, ".json") + ".md";
    const ownerRecord = ownerRecords.get(owner) || {};
    for (const command of record.selectedContracts || []) {
      const source = (record.sourceContractEvidence || []).find((candidate) => candidate.command === command.command) || {};
      const details = {
        owner,
        ownerInvariant: compact(ownerRecord.invariant, 950),
        kind: command.kind,
        logicalIdentity: compact(command.logicalIdentity, 900),
        operationProtocol: compact(source.protocols || command.protocols, 900),
        resultFacts: compact(source.events || command.events, 600),
        entryPoint: source.entryPoint || command.entryPoint || "[Placeholder: caller/channel not recorded]",
        mcp: source.mcp === undefined ? "[Placeholder: MCP availability not recorded]" : source.mcp,
      };
      const meaning = [ownerRecord.invariant, source.protocols, command.logicalIdentity && command.logicalIdentity.rule,
        source.events || command.events, source.responseContract || command.responseContract].filter(Boolean).join(" ");
      const documentText = sectionText(page, command.command, 3);
      const hash = sha256(JSON.stringify(command) + JSON.stringify(source));
      items.push(itemRecord("command", command.command, owner, page + "#" + headingAnchor(command.command), meaning, details, documentText, hash));
    }
  }

  const eventDir = path.join(dataDir, "events");
  for (const fileName of fs.readdirSync(eventDir).filter((name) => name.endsWith(".json")).sort()) {
    const relative = "docs/proposed/data/events/" + fileName;
    const record = readJson(relative);
    const page = "docs/proposed/events/" + path.basename(fileName, ".json") + ".md";
    for (const pair of record.events || []) {
      const canonical = pair.canonical || {};
      const schema = pair.schema || {};
      const owner = (canonical.owners || [record.owner]).join(", ");
      const eventName = canonical.event || "[Placeholder: event name not recorded]";
      const fields = (schema.fields || []).map((field) => field.name + ": " + field.type).join(", ");
      const nested = (schema.contentSchemas || []).map((content) => content.schema || content.field).filter(Boolean).join(", ");
      const details = {
        authorityOwners: canonical.owners,
        emissionKind: canonical.emissionKind,
        adoptedWire: canonical.adoptedTargetWire,
        eventMeaning: compact(canonical.meaning, 1300),
        schemaStatus: canonical.schemaStatus,
        selectedSchemaStatus: schema.status,
        selectedFields: fields || "[Placeholder: schema fields not recorded]",
        nestedContentSchemas: nested || "[Placeholder: nested schemas not recorded]",
      };
      const meaning = [canonical.meaning, schema.reducerRule, fields, nested].filter(Boolean).join(" ");
      const documentText = sectionText(page, eventName, 3);
      const hash = sha256(JSON.stringify(pair));
      items.push(itemRecord("event", eventName, owner, page + "#" + headingAnchor(eventName), meaning, details, documentText, hash));
    }
  }
  return items;
}

function makeQuestions(items) {
  const questions = {};
  const questionRefs = new Map();
  for (const [index, item] of items.entries()) {
    const ref = "i" + index;
    const label = item.type === "technical_responsibility" ? "technical responsibility" : item.type;
    const base = "Rate the name in `state.items." + ref + ".name` for the documented " + label + ". Use `state.items." + ref + ".requestExcerpt` and `state.items." + ref + ".meaning` as context. Ignore PascalCase and programming-language naming style. Judge whether the words resonate across compliance program owners, client administrators, auditors, product/domain staff, and implementing engineers.";
    questions[ref + "_clarity"] = { type: "score", instructions: base, criteria: scoreCriteria[item.type] };

    const userSubject = item.type === "command" ? "requested action" : item.type === "event" ? "recorded fact" : "business record";
    questions[ref + "_business_user"] = {
      type: "noul",
      instructions: "Would a nontechnical compliance program owner or client administrator understand the name `" + item.name + "` as a " + userSubject + " from the name itself? Use its context only to know which concept the name is intended to express; do not let the definition supply missing words.",
      criteria: {
        true: "The name alone uses familiar domain language and gives this stakeholder a reliable understanding of the record, action, or fact.",
        false: "The name relies on internal implementation jargon, specialist shorthand, or an unclear phrase that this stakeholder would need explained.",
      },
    };
    questions[ref + "_auditor"] = {
      type: "noul",
      instructions: "Would an external auditor or assessor understand what the name `" + item.name + "` refers to without knowing this repository's internal vocabulary?",
      criteria: {
        true: "The name uses recognizable compliance or ordinary business language and points to the documented record, action, or fact.",
        false: "The name requires internal code, architecture, persistence, or orchestration knowledge to understand.",
      },
    };
    questions[ref + "_engineering"] = {
      type: "noul",
      instructions: "Would the name `" + item.name + "` help an implementing engineer identify the documented owner, action, or fact without confusing it with a different business responsibility?",
      criteria: {
        true: "The name distinguishes the documented concept and its authority from neighboring concepts.",
        false: "The name is too broad, misleading, or mechanically phrased to identify the documented concept reliably.",
      },
    };
    questions[ref + "_jargon"] = {
      type: "noul",
      instructions: "Does the name `" + item.name + "` depend primarily on technical implementation, storage, concurrency, transport, or orchestration jargon rather than the documented domain concept?",
      criteria: {
        true: "The name foregrounds an internal mechanism or codebase-specific term that is not meaningful domain language for the listed stakeholders.",
        false: "The name uses ordinary or established domain terms; any technical qualifier is necessary and recognizable for this concept.",
      },
    };
    questions[ref + "_ambiguity"] = {
      type: "noul",
      instructions: "Could stakeholders who see the name `" + item.name + "` reasonably infer materially different records, actions, or facts from it?",
      criteria: {
        true: "The words support multiple plausible interpretations that would change how stakeholders understand the concept.",
        false: "The name has one likely interpretation that fits the documented concept across the listed stakeholders.",
      },
    };
    questionRefs.set(item.id, { ref, index });
  }
  return { questions, questionRefs };
}

function batchesOf(items) {
  const batches = [];
  let batch = [];
  let documentCharacters = 0;
  for (const item of items) {
    const size = item.requestExcerpt.length;
    if (batch.length && (batch.length >= maxBatchItems || documentCharacters + size > maxBatchDocumentCharacters)) {
      batches.push(batch);
      batch = [];
      documentCharacters = 0;
    }
    batch.push(item);
    documentCharacters += size;
  }
  if (batch.length) batches.push(batch);
  return batches;
}

function makePayload(items) {
  const { questions, questionRefs } = makeQuestions(items);
  const questionIds = {};
  const stateItems = {};
  for (const item of items) {
    const { ref, index } = questionRefs.get(item.id);
    stateItems[ref] = {
      type: item.type,
      name: item.name,
      owner: item.owner,
      document: item.document,
      meaning: item.meaning,
      context: item.context,
      requestExcerpt: item.requestExcerpt,
    };
    questionIds[item.id] = Object.fromEntries(questionKinds.map((kind) => [kind, ref + "_" + kind]));
  }
  return {
    payload: {
      model,
      state: { purpose: "Assess proposed domain names for clarity, stakeholder language, semantic fit, jargon, and ambiguity.", audiences, items: stateItems },
      questions,
    },
    questionIds,
  };
}

function writeJson(target, value) {
  const temporary = target + ".writing";
  fs.writeFileSync(temporary, JSON.stringify(value, null, 2) + "\n");
  fs.renameSync(temporary, target);
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function askJev(payload) {
  const apiKey = process.env.TYPESAFE_API_KEY;
  if (!apiKey) throw new Error("TYPESAFE_API_KEY is not set.");
  for (let attempt = 0; attempt <= retryLimit; attempt++) {
    let response;
    try {
      response = await fetch(endpoint, {
        method: "POST",
        headers: { Authorization: "Bearer " + apiKey, "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
    } catch (error) {
      if (attempt === retryLimit) throw error;
      const milliseconds = Math.min(30000, 1000 * 2 ** attempt);
      console.log("Network retry in " + milliseconds + "ms after " + String(error));
      await delay(milliseconds);
      continue;
    }
    const text = await response.text();
    if (response.ok) return JSON.parse(text);
    if ([429, 529].includes(response.status) || response.status >= 500) {
      if (attempt === retryLimit) throw new Error("Jev HTTP " + response.status + ": " + text);
      const retryAfter = Number(response.headers.get("retry-after"));
      const milliseconds = Math.min(60000, retryAfter > 0 ? retryAfter * 1000 : 1000 * 2 ** attempt);
      console.log("Jev HTTP " + response.status + "; retrying in " + milliseconds + "ms.");
      await delay(milliseconds);
      continue;
    }
    throw new Error("Jev HTTP " + response.status + ": " + text);
  }
  throw new Error("Jev retry limit reached.");
}

function probability(answer, key) {
  if (!answer || !answer.probabilities) return 0;
  return Number(answer.probabilities[String(key)] || 0);
}

function answerValue(answers, key) {
  const answer = answers && answers[key];
  if (!answer) return null;
  if (answer.type === "noul") return answer.noul;
  return answer;
}

function classify(item, answers) {
  const clarity = answers && answers.clarity;
  const pClear = probability(clarity, 3);
  const pUnclear = probability(clarity, 0) + probability(clarity, 1);
  const business = answerValue(answers, "business_user");
  const auditor = answerValue(answers, "auditor");
  const engineering = answerValue(answers, "engineering");
  const jargon = answerValue(answers, "jargon");
  const ambiguity = answerValue(answers, "ambiguity");
  const technical = item.type === "technical_responsibility";
  const clear = pClear >= 0.8 && Number(clarity && clarity.confidence || 0) >= 0.75 &&
    engineering >= 0.8 && jargon <= 0.2 && ambiguity <= 0.2 &&
    (technical || (business >= 0.8 && auditor >= 0.8));
  const concern = (pUnclear >= 0.8 && Number(clarity && clarity.confidence || 0) >= 0.75) ||
    engineering <= 0.2 || jargon >= 0.8 || ambiguity >= 0.8 ||
    (!technical && (business <= 0.2 || auditor <= 0.2));
  return clear ? "high-confidence fit" : concern ? "high-confidence concern" : "uncertain; review manually";
}

function reportMarkdown(result) {
  const entries = result.entries;
  const types = ["aggregate", "technical_responsibility", "command", "event"];
  const getAnswer = (entry, kind) => entry.answers && entry.answers[kind];
  const getNoul = (entry, kind) => {
    const answer = getAnswer(entry, kind);
    return answer && answer.noul !== undefined ? Number(answer.noul).toFixed(2) : "[Placeholder]";
  };
  const getP3 = (entry) => probability(getAnswer(entry, "clarity"), 3).toFixed(2);
  const groups = types.map((type) => {
    const rows = entries.filter((entry) => entry.type === type).map((entry) => {
      const label = type === "aggregate" || type === "technical_responsibility" ? entry.name : entry.name;
      const relative = path.relative(path.dirname(reportFile), path.join(repoRoot, entry.document)).split(path.sep).join("/");
      const target = relative;
      const linked = "[" + label.replaceAll("|", "\\|") + "](" + target + ")";
      const clarity = getAnswer(entry, "clarity");
      const clarityConfidence = clarity && clarity.confidence !== undefined ? Number(clarity.confidence).toFixed(2) : "[Placeholder]";
      return "| " + linked + " | " + entry.owner.replaceAll("|", "\\|") + " | " +
        getP3(entry) + " | " + clarityConfidence + " | " + getNoul(entry, "business_user") + " | " +
        getNoul(entry, "auditor") + " | " + getNoul(entry, "engineering") + " | " + getNoul(entry, "jargon") + " | " +
        getNoul(entry, "ambiguity") + " | " + entry.verdict + " |";
    });
    const title = type === "technical_responsibility" ? "Technical responsibility labels" : type[0].toUpperCase() + type.slice(1) + " names";
    return ["## " + title, "", "| Name | Owner | P(clear) | Score confidence | Business user | Auditor | Engineering | Jargon risk | Ambiguity risk | Jev result |", "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |", ...rows, ""].join("\n");
  });
  const counts = types.map((type) => {
    const subset = entries.filter((entry) => entry.type === type);
    return "| " + type + " | " + subset.length + " | " + subset.filter((entry) => entry.verdict === "high-confidence fit").length +
      " | " + subset.filter((entry) => entry.verdict === "high-confidence concern").length +
      " | " + subset.filter((entry) => entry.verdict === "uncertain; review manually").length + " |";
  });
  const usages = result.batches.reduce((sum, batch) => sum + Number(batch.usage && batch.usage.input_tokens || 0), 0);
  const models = [...new Set(result.batches.map((batch) => batch.responseModel).filter(Boolean))];
  return [
    "# Domain naming review — Jev results",
    "",
    "This is an advisory pass over each aggregate/technical-responsibility page and each selected command/event section in docs/proposed. It evaluates names against their captured meanings; it does not rename source contracts or establish domain authority. Jev returns typed judgments rather than explanations, so every result marked concern or uncertain needs human review before changing a name.",
    "",
    "- Run status: **" + result.status + "**",
    "- Started: " + result.startedAt,
    "- Completed: " + (result.completedAt || "[Placeholder: review is incomplete]") ,
    "- Source commit: `" + result.sourceCommit + "`",
    "- Review script SHA-256: `" + result.reviewScriptSha256 + "`",
    "- Pinned model: `" + result.requestedModel + "`; returned model(s): " + (models.map((name) => "`" + name + "`").join(", ") || "[Placeholder: no response model recorded]") + ".",
    "- Input fingerprint: `" + result.inputFingerprint + "`",
    "- Jev input tokens recorded: " + usages + ".",
    "- Per-item question set: clarity score plus business-user, auditor, engineering, jargon, and ambiguity judgments. Full typed answers are in [naming-review-jev.json](data/naming-review-jev.json).",
    "",
    "## High-confidence fit rule",
    "",
    "For business aggregates, a fit requires P(clear)=0.80 or higher, score confidence at least 0.75, business-user/auditor/engineering probabilities at least 0.80, and jargon/ambiguity probabilities no greater than 0.20. Technical responsibility labels are judged for engineering clarity, jargon, and ambiguity; they are not treated as business aggregates. A high-confidence concern is a review signal, not an automatic rename. All other outcomes remain uncertain.",
    "",
    "## Coverage summary",
    "",
    "| Name type | Reviewed | High-confidence fit | High-confidence concern | Uncertain |",
    "| --- | ---: | ---: | ---: | ---: |",
    ...counts,
    "",
    ...groups,
    "## Source records and method",
    "",
    "The request state included each name, its recorded owner/meaning, and the corresponding proposed Markdown section. `documentSectionSha256` and `sourceRecordSha256` on each item bind the judgment to the exact input; each batch also records its state/payload hash, model, usage, and item IDs. The generator is [review-names-jev.mjs](review-names-jev.mjs).",
    "",
    "[Placeholder: cross-stakeholder review and any agreed replacement terms are not recorded yet.]",
    "",
  ].join("\n");
}

function planReport(items) {
  const categories = [...new Set(items.map((item) => item.type))];
  console.log(JSON.stringify({
    totalItems: items.length,
    byType: Object.fromEntries(categories.map((type) => [type, items.filter((item) => item.type === type).length])),
    batchCount: batchesOf(items).length,
    maxBatchItems,
    maxBatchDocumentCharacters,
    largestDocumentSectionCharacters: Math.max(...items.map((item) => item.documentSectionCharacters)),
    requestExcerptCharacters: items.reduce((sum, item) => sum + item.requestExcerpt.length, 0),
    duplicateIds: items.length - new Set(items.map((item) => item.id)).size,
    example: items.slice(0, 3).map(({ id, type, name, owner, document, documentSectionCharacters }) => ({ id, type, name, owner, document, documentSectionCharacters })),
  }, null, 2));
}

async function main() {
  const items = collectItems();
  if (process.argv.includes("--plan")) {
    planReport(items);
    return;
  }
  const reviewScriptSha256 = sha256(fs.readFileSync(fileURLToPath(import.meta.url)));
  const inputFingerprint = sha256(JSON.stringify({ protocolVersion, model, reviewScriptSha256, audiences, scoreCriteria, questionKinds, items: items.map(({ id, type, name, owner, document, meaning, context, documentSectionSha256, sourceRecordSha256 }) => ({ id, type, name, owner, document, meaning, context, documentSectionSha256, sourceRecordSha256 })) }));
  let result = null;
  if (fs.existsSync(resultFile)) {
    const existing = JSON.parse(fs.readFileSync(resultFile, "utf8"));
    if (existing.inputFingerprint !== inputFingerprint || existing.requestedModel !== model || existing.protocolVersion !== protocolVersion) {
      if (!process.argv.includes("--restart") || existing.status === "complete") {
        throw new Error("Existing naming review has different inputs/model/protocol; pass --restart only for an incomplete review created by this script.");
      }
      console.log("Restarting the incomplete review because input identifiers changed; previous partial results will be overwritten.");
    } else {
      result = existing;
    }
    if (result && result.status === "complete") {
      for (const entry of result.entries) entry.verdict = classify(entry, entry.answers);
      fs.writeFileSync(reportFile, reportMarkdown(result));
      console.log("Existing complete review re-rendered; no Jev requests sent.");
      return;
    }
  }
  if (!result) {
    const sourceCommit = execFileSync("git", ["rev-parse", "HEAD"], { cwd: repoRoot, encoding: "utf8" }).trim();
    result = {
      protocolVersion,
      status: "in_progress",
      startedAt: new Date().toISOString(),
      completedAt: null,
      sourceCommit,
      reviewScriptSha256,
      requestedModel: model,
      inputFingerprint,
      audiences,
      scoreCriteria,
      questionKinds,
      batches: [],
      entries: items.map(({ requestExcerpt, ...entry }) => ({ ...entry, answers: null, verdict: "[Placeholder: pending Jev review]" })),
    };
    writeJson(resultFile, result);
  }

  const orderedBatches = batchesOf(items);
  for (const [batchIndex, originalBatch] of orderedBatches.entries()) {
    const batch = originalBatch.filter((item) => {
      const existing = result.entries.find((entry) => entry.id === item.id);
      return !existing || !existing.answers;
    });
    if (!batch.length) continue;
    const { payload, questionIds } = makePayload(batch);
    const response = await askJev(payload);
    const responseAnswers = response.answers || {};
    const missing = [];
    for (const item of batch) {
      const entry = result.entries.find((candidate) => candidate.id === item.id);
      const refs = questionIds[item.id];
      const answers = Object.fromEntries(questionKinds.map((kind) => [kind, responseAnswers[refs[kind]] || null]));
      const missingKinds = questionKinds.filter((kind) => !answers[kind]);
      if (missingKinds.length) {
        missing.push(item.id + ": " + missingKinds.join(", "));
        continue;
      }
      entry.answers = answers;
      entry.batchIndex = batchIndex + 1;
      entry.responseModel = response.model || "[Placeholder: response model not returned]";
      entry.verdict = classify(entry, answers);
    }
    if (missing.length) throw new Error("Jev response omitted answers for " + missing.join("; "));
    result.batches.push({
      index: batchIndex + 1,
      itemIds: batch.map((item) => item.id),
      stateSha256: sha256(JSON.stringify(payload.state)),
      payloadSha256: sha256(JSON.stringify(payload)),
      requestInputTokensEstimate: Math.ceil(JSON.stringify(payload).length / 4),
      usage: response.usage || null,
      responseModel: response.model || "[Placeholder: response model not returned]",
    });
    writeJson(resultFile, result);
    console.log("Reviewed batch " + (batchIndex + 1) + "/" + orderedBatches.length + ": " + batch.length + " items; " + result.entries.filter((entry) => entry.answers).length + "/" + result.entries.length + " complete.");
    await delay(350);
  }

  for (const entry of result.entries) {
    if (!entry.answers) throw new Error("Review incomplete; missing Jev answers for " + entry.id);
    entry.verdict = classify(entry, entry.answers);
  }
  result.status = "complete";
  result.completedAt = new Date().toISOString();
  fs.writeFileSync(reportFile, reportMarkdown(result));
  writeJson(resultFile, result);
  console.log("Completed naming review: " + result.entries.length + " names across " + result.batches.length + " Jev requests.");
}

main().catch((error) => {
  console.error(String(error));
  process.exitCode = 1;
});
