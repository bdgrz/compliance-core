import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const proposedDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(proposedDir, "../..");
const dataDir = path.join(proposedDir, "data");
const initialFile = path.join(dataDir, "naming-review-jev.json");
const followupFile = path.join(dataDir, "naming-review-jev-follow-up.json");
const reportFile = path.join(proposedDir, "naming-review.md");
const endpoint = "https://api.typesafe.ai/v1/systemone";
const model = "jev-1.14.0";
const protocolVersion = "stakeholder-name-review-follow-up-v1";
const maxBatchItems = 18;
const maxBatchDocumentCharacters = 48000;
const maxSectionCharacters = 6500;
const retryLimit = 6;
const questionKinds = ["vocabulary", "pair_fit", "familiarity", "mechanism"];

const businessVocabularyCriteria = {
  common_business_language: "The main words are familiar, ordinary business terms that a nontechnical stakeholder would use without a specialist glossary.",
  recognized_compliance_term: "The main words are established compliance, assurance, or audit terms that intended domain stakeholders recognize without knowing this codebase.",
  implementation_jargon: "The main words chiefly describe internal software, storage, concurrency, messaging, or orchestration mechanics.",
  unclear_or_misleading: "The words do not give stakeholders one reliable meaning for the documented record, action, or fact.",
};

const technicalVocabularyCriteria = {
  precise_technical_responsibility: "The name precisely identifies a technical responsibility without implying that it is a business aggregate.",
  recognized_architecture_term: "The name uses a recognized architecture or storage term that is clear to engineers and domain designers.",
  opaque_local_jargon: "The name relies on codebase-specific shorthand or unexplained internal jargon.",
  unclear_or_misleading: "The name does not reliably identify the documented technical responsibility.",
};

function sha256(value) {
  return crypto.createHash("sha256").update(value).digest("hex");
}

function writeJson(target, value) {
  const temporary = target + ".writing";
  fs.writeFileSync(temporary, JSON.stringify(value, null, 2) + "\n");
  fs.renameSync(temporary, target);
}

function sectionText(entry) {
  const [relative] = entry.document.split("#", 2);
  const contents = fs.readFileSync(path.join(repoRoot, relative), "utf8");
  if (entry.type === "aggregate" || entry.type === "technical_responsibility") return contents;
  const target = "### " + entry.name;
  const lines = contents.split(/\r?\n/);
  const start = lines.findIndex((line) => line === target);
  if (start < 0) return contents;
  let end = lines.length;
  for (let index = start + 1; index < lines.length; index++) {
    if (/^#{1,3} /.test(lines[index])) {
      end = index;
      break;
    }
  }
  return lines.slice(start, end).join("\n");
}

function excerpt(text) {
  if (text.length <= maxSectionCharacters) return text;
  const marker = "\n\n[Excerpt truncated for name review; consult the linked document for the complete contract.]\n\n";
  const headSize = Math.floor((maxSectionCharacters - marker.length) * 0.78);
  return text.slice(0, headSize) + marker + text.slice(-(maxSectionCharacters - marker.length - headSize));
}

function makePayload(batch) {
  const stateItems = {};
  const questions = {};
  const questionRefs = {};
  for (const [index, entry] of batch.entries()) {
    const ref = "i" + index;
    const documentText = excerpt(sectionText(entry));
    stateItems[ref] = {
      type: entry.type,
      name: entry.name,
      owner: entry.owner,
      document: entry.document,
      meaning: entry.meaning,
      context: entry.context,
      documentExcerpt: documentText,
    };
    questionRefs[entry.id] = Object.fromEntries(questionKinds.map((kind) => [kind, ref + "_" + kind]));
    const technical = entry.type === "technical_responsibility";
    const vocabularyCriteria = technical ? technicalVocabularyCriteria : businessVocabularyCriteria;
    questions[ref + "_vocabulary"] = {
      type: "choice",
      instructions: "Which kind of vocabulary does the name `" + entry.name + "` rely on in this documented context? Classify the phrase itself, not whether its invariant is valid.",
      criteria: vocabularyCriteria,
    };
    questions[ref + "_pair_fit"] = {
      type: "noul",
      instructions: technical
        ? "Taken together, does the name `" + entry.name + "` and its short documented description give engineers and domain designers one coherent understanding of the technical responsibility, without implying a business aggregate?"
        : "Taken together, do the name `" + entry.name + "` and its short documented description communicate one coherent compliance concept to program owners, client operators, auditors, product staff, and engineers?",
      criteria: {
        true: technical
          ? "The name and description fit together, and the name distinguishes the documented responsibility from neighboring responsibilities."
          : "The name and description fit together, and the name remains meaningful in stakeholder conversation without explaining implementation mechanics.",
        false: technical
          ? "The name and description conflict, are too broad, or depend on unexplained local jargon."
          : "The phrase is confusing or misleading to one or more intended stakeholders, or its apparent meaning depends on internal implementation details.",
      },
    };
    questions[ref + "_familiarity"] = {
      type: "noul",
      instructions: technical
        ? "Would an implementing engineer or domain architect recognize the name `" + entry.name + "` as a clear technical responsibility without needing this repository's local terminology?"
        : "Would an experienced compliance or assurance practitioner recognize the name `" + entry.name + "` as an established business or professional term without knowing this repository?",
      criteria: {
        true: technical
          ? "The term is an established, precise technical or architecture term that engineers/domain designers can use consistently."
          : "The term is familiar ordinary business or recognized compliance/assurance language for the intended audience.",
        false: technical
          ? "The term is local shorthand, opaque, or too vague to communicate a specific technical responsibility."
          : "The term requires implementation knowledge, uncommon local shorthand, or a codebase-specific glossary.",
      },
    };
    questions[ref + "_mechanism"] = {
      type: "noul",
      instructions: technical
        ? "Does the name `" + entry.name + "` use opaque or unnecessary architecture jargon that hides the responsibility, beyond the technical wording needed to distinguish it?"
        : "Does the name `" + entry.name + "` foreground an implementation mechanism when a familiar business or compliance term could name the same record, action, or fact more directly?",
      criteria: {
        true: technical
          ? "The label includes unexplained or unnecessary internal technical shorthand that makes the responsibility hard to identify."
          : "The label emphasizes software execution, storage, transport, or coordination mechanics that are not part of the stakeholders' business concept.",
        false: technical
          ? "The architecture terminology is established, necessary, and clearly identifies the responsibility."
          : "The phrase uses business or compliance language; any technical-sounding word is a necessary, recognizable part of the domain concept.",
      },
    };
  }
  return {
    payload: {
      model,
      state: {
        purpose: "Second independent wording pass on names: classify vocabulary, check the name/definition pair, and judge professional familiarity and unnecessary mechanism language.",
        audiences: ["Compliance program owners", "Client administrators and operators", "External auditors and assessors", "Product/domain analysts", "Backend engineers"],
        items: stateItems,
      },
      questions,
    },
    questionRefs,
  };
}

function makeBatches(entries) {
  const batches = [];
  let batch = [];
  let chars = 0;
  for (const entry of entries) {
    const size = Math.min(sectionText(entry).length, maxSectionCharacters);
    if (batch.length && (batch.length >= maxBatchItems || chars + size > maxBatchDocumentCharacters)) {
      batches.push(batch);
      batch = [];
      chars = 0;
    }
    batch.push(entry);
    chars += size;
  }
  if (batch.length) batches.push(batch);
  return batches;
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function ask(payload) {
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
      const waitMs = Math.min(30000, 1000 * 2 ** attempt);
      console.log("Network retry in " + waitMs + "ms after " + String(error));
      await delay(waitMs);
      continue;
    }
    const body = await response.text();
    if (response.ok) return JSON.parse(body);
    if ([429, 529].includes(response.status) || response.status >= 500) {
      if (attempt === retryLimit) throw new Error("Jev HTTP " + response.status + ": " + body);
      const retryAfter = Number(response.headers.get("retry-after"));
      const waitMs = Math.min(60000, retryAfter > 0 ? retryAfter * 1000 : 1000 * 2 ** attempt);
      console.log("Jev HTTP " + response.status + "; retrying in " + waitMs + "ms.");
      await delay(waitMs);
      continue;
    }
    throw new Error("Jev HTTP " + response.status + ": " + body);
  }
  throw new Error("Jev retry limit reached.");
}

function number(answer, key) {
  return Number(answer && answer.probabilities && answer.probabilities[key] || 0);
}

function classify(entry, answers) {
  const vocabulary = answers.vocabulary;
  const decisionConfidence = Number(vocabulary && vocabulary.confidence || 0);
  const pairFit = Number(answers.pair_fit && answers.pair_fit.noul || 0);
  const familiarity = Number(answers.familiarity && answers.familiarity.noul || 0);
  const mechanism = Number(answers.mechanism && answers.mechanism.noul || 0);
  const technical = entry.type === "technical_responsibility";
  const acceptableVocabulary = technical
    ? number(vocabulary, "precise_technical_responsibility") + number(vocabulary, "recognized_architecture_term")
    : number(vocabulary, "common_business_language") + number(vocabulary, "recognized_compliance_term");
  const concernVocabulary = technical
    ? number(vocabulary, "opaque_local_jargon") + number(vocabulary, "unclear_or_misleading")
    : number(vocabulary, "implementation_jargon") + number(vocabulary, "unclear_or_misleading");
  if (acceptableVocabulary >= 0.8 && decisionConfidence >= 0.75 && pairFit >= 0.8 && familiarity >= 0.8 && mechanism <= 0.2) {
    return "high-confidence domain fit";
  }
  if ((concernVocabulary >= 0.8 && decisionConfidence >= 0.75) || (pairFit <= 0.2 && mechanism >= 0.7)) {
    return "high-confidence naming concern";
  }
  return "unresolved; review with stakeholders";
}

function percent(value) {
  return value === undefined || value === null ? "[Placeholder]" : Number(value).toFixed(2);
}

function reportMarkdown(initial, followup) {
  const entries = initial.entries.map((entry) => ({ ...entry, followUpAnswers: followup.entries[entry.id].answers }));
  const types = ["aggregate", "technical_responsibility", "command", "event"];
  const getScore = (entry) => entry.answers.clarity;
  const getNoul = (entry, round, kind) => round === 1 ? entry.answers[kind]?.noul : entry.followUpAnswers[kind]?.noul;
  const getVocabularyProbability = (entry, options) => options.reduce((sum, option) => sum + number(entry.followUpAnswers.vocabulary, option), 0);
  const groups = types.map((type) => {
    const rows = entries.filter((entry) => entry.type === type).map((entry) => {
      const label = entry.name.replaceAll("|", "\\|");
      const target = path.relative(proposedDir, path.join(repoRoot, entry.document)).split(path.sep).join("/");
      const link = "[" + label + "](" + target + ")";
      const score = getScore(entry);
      const businessVocabulary = type === "technical_responsibility"
        ? getVocabularyProbability(entry, ["precise_technical_responsibility", "recognized_architecture_term"])
        : getVocabularyProbability(entry, ["common_business_language", "recognized_compliance_term"]);
      const conf = entry.followUpAnswers.vocabulary.confidence;
      return "| " + link + " | " + entry.owner.replaceAll("|", "\\|") + " | " + percent(score.probabilities["3"]) + " | " +
        percent(entry.answers.business_user.noul) + " | " + percent(entry.answers.auditor.noul) + " | " + percent(entry.answers.engineering.noul) + " | " +
        percent(entry.answers.jargon.noul) + " | " + percent(entry.answers.ambiguity.noul) + " | " + percent(businessVocabulary) + " | " +
        percent(conf) + " | " + percent(entry.followUpAnswers.pair_fit.noul) + " | " + percent(entry.followUpAnswers.familiarity.noul) + " | " +
        percent(entry.followUpAnswers.mechanism.noul) + " | " + entry.followUpVerdict + " |";
    });
    const title = type === "technical_responsibility" ? "Technical responsibility labels" : type[0].toUpperCase() + type.slice(1) + " names";
    return ["## " + title, "", "| Name | Owner | R1 P(clear) | R1 business | R1 auditor | R1 engineering | R1 jargon | R1 ambiguity | R2 domain/technical terms | R2 vocabulary confidence | R2 name/definition fit | R2 audience familiarity | R2 mechanism jargon | Follow-up result |", "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |", ...rows, ""].join("\n");
  });
  const counts = types.map((type) => {
    const subset = entries.filter((entry) => entry.type === type);
    return "| " + type + " | " + subset.length + " | " + subset.filter((entry) => entry.followUpVerdict === "high-confidence domain fit").length +
      " | " + subset.filter((entry) => entry.followUpVerdict === "high-confidence naming concern").length +
      " | " + subset.filter((entry) => entry.followUpVerdict === "unresolved; review with stakeholders").length + " |";
  });
  const models = [...new Set(followup.batches.map((batch) => batch.responseModel).filter(Boolean))];
  const inputTokens = followup.batches.reduce((sum, batch) => sum + Number(batch.usage && batch.usage.input_tokens || 0), 0);
  return [
    "# Domain naming review — Jev results",
    "",
    "This review covers every business aggregate, technical-responsibility label, selected command, and canonical event entry in docs/proposed. Jev's judgments are advisory; names remain unchanged until the domain review accepts a term. Each table row links to the exact proposed page and section that was sent.",
    "",
    "- Initial pass: " + initial.entries.length + " entries, six typed judgments per entry, " + initial.batches.length + " requests.",
    "- Follow-up pass: " + followup.entries.length + " entries, four differently framed judgments per entry, " + followup.batches.length + " requests.",
    "- Source commit: `" + initial.sourceCommit + "`.",
    "- Model: `" + initial.requestedModel + "`; follow-up response model(s): " + models.map((name) => "`" + name + "`").join(", ") + ".",
    "- Follow-up run: " + followup.startedAt + " to " + followup.completedAt + ".",
    "- Follow-up input tokens: " + inputTokens + ".",
    "- Full raw answers: [initial Jev responses](data/naming-review-jev.json) and [follow-up Jev responses](data/naming-review-jev-follow-up.json).",
    "- Reproducible runners: [initial review](review-names-jev.mjs) and [follow-up review](review-names-jev-followup.mjs).",
    "",
    "## How to read the answers",
    "",
    "R1 reports a 0–3 clarity score and separate probabilities for business-user, auditor, engineering, jargon, and ambiguity judgments. R2 independently classifies vocabulary as ordinary business language, recognized compliance language, implementation language, or unclear/misleading; for technical responsibility labels, it uses technical-label categories. It then checks whether the name/definition pair is coherent, whether the intended audience recognizes the term, and whether the name foregrounds unnecessary implementation mechanics.",
    "",
    "A high-confidence domain fit requires R2 acceptable-vocabulary probability at least 0.80, Choice confidence at least 0.75, name/definition fit and audience familiarity at least 0.80, and unnecessary-mechanism probability no greater than 0.20. A high-confidence concern requires a concentrated implementation/unclear vocabulary judgment, or a very low name/definition fit with high mechanism-jargon risk. Everything else is explicitly unresolved for stakeholder review. These thresholds are filters for review; they do not authorize automatic renaming.",
    "",
    "## Follow-up coverage summary",
    "",
    "| Name type | Reviewed | High-confidence domain fit | High-confidence naming concern | Unresolved |",
    "| --- | ---: | ---: | ---: | ---: |",
    ...counts,
    "",
    ...groups,
    "## Evidence binding",
    "",
    "Each initial result stores the exact source record hash and Markdown section hash. Follow-up batches store state and payload hashes, response model, input usage, and item IDs. The two response files keep the raw probabilities, score legends, and confidence values for every question.",
    "",
  ].join("\n");
}

async function main() {
  if (!fs.existsSync(initialFile)) throw new Error("Run the initial Jev naming review first.");
  const initial = JSON.parse(fs.readFileSync(initialFile, "utf8"));
  if (initial.status !== "complete") throw new Error("Initial Jev review is incomplete.");
  const runnerHash = sha256(fs.readFileSync(fileURLToPath(import.meta.url)));
  const fingerprint = sha256(JSON.stringify({ initialFingerprint: initial.inputFingerprint, protocolVersion, model, runnerHash, businessVocabularyCriteria, technicalVocabularyCriteria, questionKinds }));
  let followup = null;
  if (fs.existsSync(followupFile)) {
    const existing = JSON.parse(fs.readFileSync(followupFile, "utf8"));
    if (existing.inputFingerprint !== fingerprint) throw new Error("Follow-up file has a different input fingerprint; do not mix review runs.");
    followup = existing;
  } else {
    followup = {
      protocolVersion,
      status: "in_progress",
      startedAt: new Date().toISOString(),
      completedAt: null,
      requestedModel: model,
      sourceCommit: initial.sourceCommit,
      initialInputFingerprint: initial.inputFingerprint,
      inputFingerprint: fingerprint,
      runnerSha256: runnerHash,
      batches: [],
      entries: initial.entries.map((entry) => ({ id: entry.id, type: entry.type, name: entry.name, owner: entry.owner, document: entry.document, answers: null, followUpVerdict: "[Placeholder: follow-up pending]" })),
    };
    writeJson(followupFile, followup);
  }
  const batches = makeBatches(initial.entries);
  for (const [batchIndex, originalBatch] of batches.entries()) {
    const batch = originalBatch.filter((entry) => !followup.entries.find((item) => item.id === entry.id)?.answers);
    if (!batch.length) continue;
    const { payload, questionRefs } = makePayload(batch);
    const response = await ask(payload);
    const missing = [];
    for (const entry of batch) {
      const target = followup.entries.find((item) => item.id === entry.id);
      const refs = questionRefs[entry.id];
      const answers = Object.fromEntries(questionKinds.map((kind) => [kind, response.answers && response.answers[refs[kind]] || null]));
      const missingKinds = questionKinds.filter((kind) => !answers[kind]);
      if (missingKinds.length) {
        missing.push(entry.id + ": " + missingKinds.join(", "));
        continue;
      }
      target.answers = answers;
      target.responseModel = response.model || "[Placeholder: response model not returned]";
      target.followUpVerdict = classify(entry, answers);
    }
    if (missing.length) throw new Error("Jev response omitted follow-up answers for " + missing.join("; "));
    followup.batches.push({
      index: batchIndex + 1,
      itemIds: batch.map((entry) => entry.id),
      stateSha256: sha256(JSON.stringify(payload.state)),
      payloadSha256: sha256(JSON.stringify(payload)),
      requestInputTokensEstimate: Math.ceil(JSON.stringify(payload).length / 4),
      usage: response.usage || null,
      responseModel: response.model || "[Placeholder: response model not returned]",
    });
    writeJson(followupFile, followup);
    console.log("Reviewed follow-up batch " + (batchIndex + 1) + "/" + batches.length + ": " + batch.length + " entries; " + followup.entries.filter((entry) => entry.answers).length + "/" + followup.entries.length + " complete.");
    await delay(350);
  }
  for (const entry of followup.entries) {
    if (!entry.answers) throw new Error("Follow-up incomplete; missing answers for " + entry.id);
  }
  followup.status = "complete";
  followup.completedAt = new Date().toISOString();
  fs.writeFileSync(reportFile, reportMarkdown(initial, followup));
  writeJson(followupFile, followup);
  console.log("Completed second naming pass: " + followup.entries.length + " entries across " + followup.batches.length + " Jev requests.");
}

main().catch((error) => {
  console.error(String(error));
  process.exitCode = 1;
});
