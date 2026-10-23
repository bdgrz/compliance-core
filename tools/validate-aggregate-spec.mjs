#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';

const root = path.resolve(import.meta.dirname, '..');
const base = path.join(root, 'docs/product/audits/2026-10-23');
const failures = [];
const assert = (value, message) => { if (!value) failures.push(message); };
const read = name => JSON.parse(fs.readFileSync(path.join(base, name), 'utf8'));
const snake = value => value.replace(/(?<!^)(?=[A-Z])/g, '_').toLowerCase();
const files = fs.readdirSync(base);
for (const name of files.filter(name => name.endsWith('.json'))) read(name);
const split = read('aggregate-split-targets.json');
const source = read('command-event-contracts.json');
const current = read('aggregate-contract-catalog.json');
// Source existence checks are pinned to the catalog head so later legacy deletions do not break the design validator.
const treeAt = head => {
    const list = () => new Set(execFileSync('git', ['ls-tree', '-r', '--name-only', head], { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).split('\n').filter(Boolean));
    try { return list(); } catch { /* commit absent, e.g. shallow checkout */ }
    try { execFileSync('git', ['fetch', '--quiet', '--depth=1', 'origin', head], { cwd: root, stdio: 'ignore' }); return list(); } catch { /* fall through */ }
    console.error(`warning: commit ${head} unavailable; checking source paths against the working tree`);
    return null;
};
const trees = new Map();
const existsAt = (head, file) => { const tree = trees.get(head) ?? (trees.set(head, treeAt(head)), trees.get(head)); return tree ? tree.has(file) : fs.existsSync(path.join(root, file)); };
const commandsHead = read('target-command-contracts.json').sourceHead;
const commands = read('target-command-contracts.json').commands;
const admissions = read('source-admission-contracts.json').contracts;
const remaining = read('remaining-command-identities.json').contracts;
const reactors = read('target-reactor-contracts.json').reactors;
const events = read('target-event-contracts.json').events;
const eventByName = new Map(events.map(event => [event.event, event]));
const commandByName = new Map(commands.map(command => [command.command, command]));
const allSelected = [...commands, ...admissions, ...remaining];
const selectedNames = new Set(allSelected.map(command => command.command));
const expectedNames = source.requests.filter(request => ['command', 'internal_reaction', 'confirmed_command_gap'].includes(request.classification));
assert(current.aggregates.length === 103, 'Current aggregate count must remain 103.');
assert(split.splitOwners.length === 29, 'Split root count must remain 29.');
for (const request of expectedNames) assert(selectedNames.has(request.request), `No selected identity/entry contract: ${request.request}`);
for (const owner of current.aggregates) assert(existsAt(current.generatedAtHead, owner.source), `Missing owner source at ${current.generatedAtHead.slice(0, 8)}: ${owner.source}`);
let allocated = 0;
let externalized = 0;
let pruned = 0;
for (const row of split.splitOwners) {
    const facts = row.targetOwners.flatMap(owner => owner.existingDomainEvents);
    const removed = [...row.technicalFactsToExternalize, ...row.migrationFactsToPrune];
    const partition = [...facts, ...removed];
    assert(new Set(partition).size === partition.length, `Duplicate event allocation: ${row.currentAggregate}`);
    assert(partition.length === row.currentEventsEmitted.length && row.currentEventsEmitted.every(event => partition.includes(event)), `Incomplete event partition: ${row.currentAggregate}`);
    assert(row.currentCommands.every(command => row.commandMigrationMap.some(mapping => mapping.command === command)), `Missing command migration: ${row.currentAggregate}`);
    for (const mapping of row.commandMigrationMap) {
        if (mapping.target !== 'REMOVE' && mapping.command !== 'GenerateAuditPopulationExport') assert(row.targetOwners.some(owner => owner.proposedAggregate === mapping.target), `Unknown split destination: ${mapping.command} -> ${mapping.target}`);
        assert(commandByName.get(mapping.command)?.owner === mapping.target, `Command destination differs between catalogs: ${mapping.command}`);
    }
    for (const owner of row.targetOwners) for (const fact of owner.existingDomainEvents) assert(eventByName.get(fact)?.owner === owner.proposedAggregate, `Native event owner differs: ${fact}`);
    allocated += facts.length;
    externalized += row.technicalFactsToExternalize.length;
    pruned += row.migrationFactsToPrune.length;
}
const routes = new Map();
for (const command of allSelected) {
    const fields = command.fields ?? [];
    const fieldNames = fields.map(field => field.name);
    assert(new Set(fieldNames).size === fieldNames.length, `Duplicate command field: ${command.command}`);
    const http = command.http ? (Array.isArray(command.http) ? command.http : [command.http]) : [];
    for (const route of http) {
        const placeholders = [...route.path.matchAll(/\{(\w+)\}/g)].map(match => match[1]);
        const bySnake = new Map(fields.map(field => [snake(field.name), field]));
        for (const placeholder of placeholders) assert(bySnake.get(placeholder)?.required === true, `Unbound/optional route field: ${command.command}.${placeholder}`);
        const logical = typeof command.logicalIdentity === 'string' ? command.logicalIdentity : command.logicalIdentity?.field;
        assert(logical && placeholders.includes(snake(logical)), `Identity absent from route: ${command.command}`);
        const shape = `${route.method} ${route.path.replace(/\{[^}]+\}/g, '{}')}`;
        assert(!routes.has(shape) || routes.get(shape) === command.command, `Duplicate route shape: ${routes.get(shape)} / ${command.command}`);
        routes.set(shape, command.command);
    }
    for (const event of command.events ?? []) assert(eventByName.has(event), `Undefined target event: ${command.command} -> ${event}`);
    if (command.sourceContract) assert(existsAt(commandsHead, command.sourceContract), `Missing command source at ${commandsHead.slice(0, 8)}: ${command.sourceContract}`);
}
const receiverNames = new Set();
for (const reactor of reactors) {
    assert(!receiverNames.has(reactor.command), `Duplicate receiver command contract: ${reactor.command}`);
    receiverNames.add(reactor.command);
    const names = reactor.fields.map(field => field.name);
    assert(new Set(names).size === names.length, `Duplicate receiver field: ${reactor.command}`);
    assert(!reactor.http && !reactor.mcp, `Trusted continuation exposed publicly: ${reactor.command}`);
    assert(!reactor.fields.some(field => field.type.includes('TSourceEvent')), `Unresolved receiver proof type: ${reactor.command}`);
    for (const sourceEvent of reactor.sourceProofVariants) assert(eventByName.has(sourceEvent), `Undefined concrete source event: ${reactor.reactor}.${sourceEvent}`);
    for (const event of reactor.eventsEmitted) assert(eventByName.get(event)?.owner === reactor.targetOwner, `Receiver event/owner mismatch: ${reactor.command} -> ${event}`);
    const common = commandByName.get(reactor.command);
    if (common) assert(JSON.stringify(common.fields) === JSON.stringify(reactor.fields), `Two signatures for internal command: ${reactor.command}`);
}
const discriminators = new Set();
for (const event of events) {
    const key = `${event.discriminator}/${event.discriminatorVersion}`;
    assert(!discriminators.has(key), `Duplicate event discriminator: ${key}`);
    discriminators.add(key);
    const names = event.fields.map(field => field.name);
    assert(new Set(names).size === names.length, `Duplicate event field: ${event.event}`);
    assert(names.includes('Operation'), `Missing native provenance: ${event.event}`);
    assert(!names.includes('State'), `Whole parent snapshot remains: ${event.event}`);
    if (!event.existingContract) assert(event.contentSchemas.some(schema => schema.field === 'Intent' && schema.variants?.length > 0), `Unspecified new event intent: ${event.event}`);
    for (const content of event.contentSchemas) if (content.field === 'Change') assert(content.sourceCommandVariants.length > 0, `Unspecified native change: ${event.event}`);
}
for (const vector of read('target-contract-types.json').identityVectors) {
    const chunks = [];
    for (const item of vector.tuple) {
        const bytes = item.type === 'uuid' ? Buffer.from(item.value.replaceAll('-', ''), 'hex') : Buffer.from(item.value, 'utf8');
        if (item.type === 'string') { const length = Buffer.alloc(4); length.writeUInt32BE(bytes.length); chunks.push(length); }
        chunks.push(bytes);
    }
    const encoded = Buffer.concat(chunks);
    assert(encoded.toString('hex') === vector.encodedHex, 'Identity vector encoding changed.');
    const digest = crypto.createHash('sha1').update(Buffer.from(vector.namespace.replaceAll('-', ''), 'hex')).update(encoded).digest().subarray(0, 16);
    digest[6] = (digest[6] & 15) | 80;
    digest[8] = (digest[8] & 63) | 128;
    const text = digest.toString('hex').replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/, '$1-$2-$3-$4-$5');
    assert(text === vector.uuid, 'Identity vector UUID changed.');
}
for (const name of files.filter(name => name.endsWith('.md'))) {
    const filename = path.join(base, name);
    const text = fs.readFileSync(filename, 'utf8');
    for (const match of text.matchAll(/\]\(([^)#]+)(?:#[^)]*)?\)/g)) {
        const href = match[1];
        if (!href.includes('://')) assert(fs.existsSync(path.resolve(base, href)), `Broken Markdown link: ${name} -> ${href}`);
    }
}
const diagrams = (fs.readFileSync(path.join(base, 'choreography-flows.md'), 'utf8').match(/```mermaid/g) ?? []).length;
assert(diagrams === 20, `Expected 20 choreography diagrams, got ${diagrams}.`);
assert(split.eventAllocationCount === allocated && split.externalizeCount === externalized && split.pruneCount === pruned, 'Split allocation totals differ from contents.');
// Cross-document consistency: the Markdown tables must agree with the normative JSON.
const splitMd = fs.readFileSync(path.join(base, "aggregate-split-targets.md"), "utf8");
for (const row of split.splitOwners) for (const owner of row.targetOwners) assert(splitMd.includes(`**${owner.proposedAggregate}** \`${owner.identity}\``), `Split owner identity differs from Markdown: ${owner.proposedAggregate}`);
const admissionMd = fs.readFileSync(path.join(base, "source-admission-contracts.md"), "utf8");
for (const contract of admissions) for (const field of contract.fields) assert(admissionMd.includes(`\`${field.name}: ${field.type}\``), `Admission field differs from Markdown: ${contract.command}.${field.name}`);
// Shipped typed-identity contracts (#1033, #1035, #1037) already carry ulong ExpectedRevision; they are the only permitted exception until a deliberate contract change.
const shippedUnsignedRevision = new Set(['AssignRolePermission', 'RemoveRolePermission', 'AssignTeamRole', 'RemoveTeamRole', 'GrantPlatformOperator', 'RevokePlatformOperator']);
for (const command of allSelected) for (const field of command.fields ?? []) if (/^Expected.*(Revision|Generation|Epoch|Version)$/.test(field.name) || field.name === "Generation") assert(field.type === "long" || field.type === "long?" || (shippedUnsignedRevision.has(command.command) && field.type === 'ulong'), `Authored expected value must be signed long: ${command.command}.${field.name}`);
const removedTypes = new Set(split.splitOwners.flatMap(row => [...row.technicalFactsToExternalize, ...row.migrationFactsToPrune]));
for (const event of events) for (const field of event.fields) assert(!removedTypes.has(field.type.replace(/\?$/, "")), `Event payload nests a removed fact type: ${event.event}.${field.name}`);
const processes = read("target-reactor-contracts.json").processes ?? [];
const processNames = new Set(processes.map(process => process.process));
const inProcess = new Map();
for (const process of processes) {
    for (const stage of [...process.stages, ...(process.abortStages ?? [])]) for (const command of stage.receivers) { assert(receiverNames.has(command), `Process names unknown receiver: ${process.process} -> ${command}`); const list = inProcess.get(command) ?? []; if (!list.includes(process.process)) list.push(process.process); inProcess.set(command, list); }
    for (const command of process.abort) assert(admissions.some(contract => contract.command === command) || commandByName.has(command), `Process abort command undefined: ${process.process} -> ${command}`);
    for (const event of process.rootSourceEvents) assert(eventByName.has(event), `Process root event undefined: ${process.process} -> ${event}`);
}
for (const reactor of reactors) { assert(Array.isArray(reactor.processes) && reactor.processes.length > 0 && reactor.processes.every(name => processNames.has(name)), `Receiver without ordered process: ${reactor.command}`); assert(JSON.stringify(inProcess.get(reactor.command) ?? []) === JSON.stringify(reactor.processes), `Receiver process field disagrees with process stages: ${reactor.command}`); }
if (failures.length) {
    for (const failure of failures.slice(0, 40)) console.error(failure);
    if (failures.length > 40) console.error(`${failures.length - 40} further errors omitted.`);
    process.exitCode = 1;
} else {
    console.log(JSON.stringify({ currentOwners: current.aggregates.length, sourceMutationEntriesAccounted: expectedNames.length, splitRoots: split.splitOwners.length, splitTargetOwners: split.splitOwners.reduce((sum, row) => sum + row.targetOwners.length, 0), nativeSplitFacts: allocated, technicalFactsExternalized: externalized, factsPruned: pruned, targetEffects: reactors.length, targetEventSchemas: events.length, diagrams, result: 'valid' }));
}
