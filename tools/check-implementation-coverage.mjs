#!/usr/bin/env node
// Compares the selected aggregate/reactor design (docs/product/audits/2026-10-23) with the source tree.
// Reports implemented | partial | pending | blocked | removed-ok per owner, command, receiver, process and
// trace, and exits non-zero on drift: a legacy type that outlives its implemented replacement, a process
// registered without every receiver, a reserved route mapped early, or an unsigned public expected value
// outside the shipped allowlist. Usage: node tools/check-implementation-coverage.mjs [--list] [--json]
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const base = path.join(root, 'docs/product/audits/2026-10-23');
const args = new Set(process.argv.slice(2));
const read = file => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const catalog = name => JSON.parse(fs.readFileSync(path.join(base, name), 'utf8'));

const split = catalog('aggregate-split-targets.json');
const commands = catalog('target-command-contracts.json').commands;
const admissions = catalog('source-admission-contracts.json').contracts;
const reactorCatalog = catalog('target-reactor-contracts.json');
const events = catalog('target-event-contracts.json').events;
const traces = catalog('design-review-traces.json').traces;
const remaining = catalog('remaining-command-identities.json').contracts;
const allowlist = read('tools/implementation-coverage-allowlist.json');

const failures = [];
const fail = message => failures.push(message);

// ---------- source scan ----------
const walk = (dir, out = []) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        if (entry.name === 'bin' || entry.name === 'obj' || entry.name.startsWith('.')) continue;
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) walk(full, out);
        else if (entry.name.endsWith('.cs')) out.push(full);
    }
    return out;
};
const sources = walk(path.join(root, 'src'));
const tests = walk(path.join(root, 'test'));
const rel = file => path.relative(root, file).split(path.sep).join('/');

// Parses `record|class|interface Name[(params)] : Base1, Base2 ...` declarations with balanced parameter lists.
const declarations = content => {
    const found = [];
    const pattern = /\b(record|class|interface)\s+([A-Z]\w*)\b/g;
    let match;
    while ((match = pattern.exec(content)) !== null) {
        let index = pattern.lastIndex;
        const skipSpace = () => { while (index < content.length && /\s/.test(content[index])) index++; };
        skipSpace();
        let params = null;
        if (content[index] === '<') { // generic type parameters
            while (index < content.length && content[index] !== '>') index++;
            index++;
            skipSpace();
        }
        if (content[index] === '(') {
            let depth = 0;
            const start = index;
            do {
                if (content[index] === '(') depth++;
                else if (content[index] === ')') depth--;
                index++;
            } while (depth > 0 && index < content.length);
            params = content.slice(start + 1, index - 1);
            skipSpace();
        }
        let bases = '';
        if (content[index] === ':') {
            const end = content.slice(index).search(/[;{]|\bwhere\b/);
            bases = content.slice(index + 1, end < 0 ? content.length : index + end);
        }
        const attributes = content.slice(Math.max(0, match.index - 400), match.index);
        const discriminator = /\[Discriminator\("([^"]+)",\s*(\d+)\)\]\s*(?:\[[^\]]*\]\s*)*(?:public|internal)?\s*(?:sealed\s+)?(?:partial\s+)?$/.exec(attributes);
        found.push({ kind: match[1], name: match[2], params, bases, discriminator: discriminator ? { name: discriminator[1], version: Number(discriminator[2]) } : null });
    }
    return found;
};
const paramNames = params => params === null ? new Set() : new Set([...params.matchAll(/(?:^|,)\s*(?:\[[^\]]*\]\s*)*(?:params\s+)?([\w<>?\[\],.\s]+?)\s+(\w+)\s*(?:=[^,]*)?(?=,|$)/gs)].map(m => m[2]));
const paramTypes = params => {
    const map = new Map();
    if (params === null) return map;
    for (const m of params.matchAll(/(?:^|,)\s*(?:\[[^\]]*\]\s*)*(?:params\s+)?([\w<>?\[\],.\s]+?)\s+(\w+)\s*(?:=[^,]*)?(?=,|$)/gs)) map.set(m[2], m[1].replace(/\s+/g, ''));
    return map;
};

const aggregates = new Map(); // name -> file
const domainEvents = new Map(); // name -> { file, discriminator, params }
const requests = new Map(); // name -> { file, params, bases }
const typeNames = new Map(); // every declared record/class name -> files
const registeredHandlers = new Set();
const registeredAuthorizers = new Set();
const registeredReactors = new Map(); // class -> workload name
const mappedRoutes = new Map(); // request -> [{ verb, path }]
const mcpTools = new Set();
const literals = new Set(); // string literals in Core (process names)
const ulongExpected = []; // { request, field, file }

// Resolve marker interfaces across files and declaration order. Generic arguments do not make their
// containing interface a request: only the outer base types participate in inheritance.
const baseTypes = bases => {
    const parts = [];
    let start = 0;
    let depth = 0;
    for (let index = 0; index <= bases.length; index++) {
        const char = bases[index];
        if (char === '<' || char === '(') depth++;
        else if (char === '>' || char === ')') depth--;
        if (index === bases.length || char === ',' && depth === 0) {
            const outer = bases.slice(start, index).trim().split(/[<(]/)[0].trim();
            if (outer) parts.push(outer.split(/[.:]/).at(-1));
            start = index + 1;
        }
    }
    return parts;
};
const scannedSources = sources.map(file => {
    const content = fs.readFileSync(file, 'utf8');
    return { file, content, declared: declarations(content) };
});
const interfaces = new Map(scannedSources.flatMap(source => source.declared
    .filter(declaration => declaration.kind === 'interface')
    .map(declaration => [declaration.name, baseTypes(declaration.bases)])));
const isRequestInterface = (name, visited = new Set()) => {
    if (name === 'IRequest' || name === 'IIdentifiedRequest') return true;
    if (visited.has(name)) return false;
    visited.add(name);
    return (interfaces.get(name) ?? []).some(parent => isRequestInterface(parent, visited));
};
for (const { file, content, declared } of scannedSources) {
    for (const d of declared) {
        typeNames.set(d.name, [...(typeNames.get(d.name) ?? []), rel(file)]);
        if (/(^|[\s,])Aggregate\b/.test(d.bases)) aggregates.set(d.name, rel(file));
        if (/(^|[\s,])DomainEvent\b/.test(d.bases) && d.kind === 'record') domainEvents.set(d.name, { file: rel(file), discriminator: d.discriminator, params: d.params });
        if (d.kind === 'record' && baseTypes(d.bases).some(name => isRequestInterface(name))) {
            requests.set(d.name, { file: rel(file), params: d.params, bases: d.bases });
            for (const [name, type] of paramTypes(d.params)) if (type === 'ulong' && /^Expected\w*(Revision|Generation|Epoch|Version)$/.test(name)) ulongExpected.push({ request: d.name, field: name, file: rel(file) });
        }
    }
    for (const m of content.matchAll(/AddRequestHandler<(\w+)>/g)) registeredHandlers.add(m[1]);
    for (const m of content.matchAll(/AddRequestAuthorizer<(\w+)>/g)) registeredAuthorizers.add(m[1]);
    for (const m of content.matchAll(/AddReactor<(\w+)>\(\s*(?:"(\w+)")?/g)) registeredReactors.set(m[1], m[2] ?? null);
    for (const m of content.matchAll(/MapPortia(Put|Post|Delete)<(\w+)\s*[,>][^;]*?"([^"]*)"/gs)) mappedRoutes.set(m[2], [...(mappedRoutes.get(m[2]) ?? []), { verb: m[1].toUpperCase(), path: m[3] }]);
    for (const m of content.matchAll(/AddMcpTool<(\w+)>/g)) mcpTools.add(m[1]);
    if (file.includes(`${path.sep}Compliance.Core${path.sep}`)) for (const m of content.matchAll(/"(\w+Process)"/g)) literals.add(m[1]);
}
const testNames = new Set();
for (const file of tests) for (const m of fs.readFileSync(file, 'utf8').matchAll(/\b(?:Task|void)\s+(\w*Should\w+)\s*\(/g)) testNames.add(m[1]);

// ---------- allowlist integrity ----------
const commandNames = new Set([...commands.map(c => c.command), ...admissions.map(c => c.command), ...remaining.map(c => c.command), ...reactorCatalog.reactors.map(r => r.command), ...reactorCatalog.processes.flatMap(p => p.abort ?? [])]);
const deferred = new Map();
for (const entry of allowlist.deferred) {
    if (!commandNames.has(entry.name)) fail(`Allowlist names an unknown catalog command: ${entry.name}`);
    deferred.set(entry.name, entry);
}
for (const name of allowlist.ulongExpectedRevision) if (!requests.has(name)) fail(`Allowlist ulong exception names a missing request type: ${name}`);
const ulongAllowed = new Set(allowlist.ulongExpectedRevision);
// A catalog's trusted, unexposed receiver may pin an unsigned source position. Exposing that same
// contract over HTTP/MCP restores the public signed-revision rule; this is not a blanket marker exemption.
const internalUnsigned = new Set(reactorCatalog.reactors
    .filter(receiver => receiver.http === false && receiver.mcp === false)
    .flatMap(receiver => receiver.fields.filter(field => field.type === 'ulong')
        .map(field => `${receiver.command}.${field.name}`)));
for (const hit of ulongExpected) {
    const internal = internalUnsigned.has(`${hit.request}.${hit.field}`) &&
        !mappedRoutes.has(hit.request) && !mcpTools.has(hit.request);
    if (!ulongAllowed.has(hit.request) && !internal) fail(`Unsigned public expected value outside the shipped allowlist: ${hit.request}.${hit.field} (${hit.file})`);
}

// ---------- events ----------
const eventStatus = new Map();
for (const event of events) {
    const declared = domainEvents.get(event.event);
    if (!declared) { eventStatus.set(event.event, 'pending'); continue; }
    const names = paramNames(declared.params);
    const fieldsOk = event.fields.every(field => names.has(field.name));
    const discriminatorOk = declared.discriminator && declared.discriminator.name === event.discriminator && declared.discriminator.version === event.discriminatorVersion;
    eventStatus.set(event.event, fieldsOk && discriminatorOk ? 'implemented' : 'partial');
}
// Two live types sharing one discriminator@version is a serializer conflict. A legacy type that still holds a
// discriminator the catalog reassigns to its replacement is pending until the replacement declares it.
const declaredBy = new Map();
for (const [name, declared] of domainEvents) {
    if (!declared.discriminator) continue;
    const key = `${declared.discriminator.name}@${declared.discriminator.version}`;
    declaredBy.set(key, [...(declaredBy.get(key) ?? []), name]);
}
for (const [key, names] of declaredBy) if (names.length > 1) fail(`Discriminator ${key} is declared by more than one type: ${names.join(', ')}`);

// ---------- commands ----------
const simpleType = type => type.replace(/\s+/g, '');
const requestMatches = (name, fields) => {
    const declared = requests.get(name);
    if (!declared) return { exists: false };
    const names = paramNames(declared.params);
    const types = paramTypes(declared.params);
    const missing = fields.filter(f => !names.has(f.name)).map(f => f.name);
    const mistyped = fields.filter(f => names.has(f.name) && /^(Uuid\??|long\??|int\??|string\??|bool\??|ulong\??|DateTimeOffset\??)$/.test(f.type) && types.get(f.name) !== simpleType(f.type)).map(f => `${f.name}:${types.get(f.name)}!=${f.type}`);
    return { exists: true, missing, mistyped, ok: missing.length === 0 && mistyped.length === 0 };
};
const commandStatus = new Map();
const commandDetail = new Map();
for (const command of commands) {
    const name = command.command;
    if (command.status === 'remove') {
        commandStatus.set(name, requests.has(name) ? 'pending' : 'removed-ok');
        commandDetail.set(name, requests.has(name) ? 'legacy request still declared' : 'removed');
        continue;
    }
    const shape = requestMatches(name, command.fields);
    const handler = command.handler ?? `${name}Handler`;
    const checks = {
        owner: !command.owner || aggregates.has(command.owner),
        contract: shape.exists && shape.ok,
        handler: registeredHandlers.has(handler),
        http: (command.http ?? []).length === 0 || (mappedRoutes.get(name) ?? []).length >= command.http.length,
        mcp: !command.mcp || mcpTools.has(name)
    };
    const all = Object.values(checks).every(Boolean);
    const detail = Object.entries(checks).filter(([, ok]) => !ok).map(([k]) => k).join(',') + (shape.exists && !shape.ok ? ` [missing ${shape.missing.join('/') || '-'}; mistyped ${shape.mistyped.join('/') || '-'}]` : '');
    let status = all ? 'implemented' : checks.contract ? 'partial' : 'pending';
    if (!all && deferred.has(name)) status = 'blocked';
    commandStatus.set(name, status);
    commandDetail.set(name, status === 'blocked' ? `#${deferred.get(name).issue}` : detail);
    if (checks.contract && !checks.handler && (command.http ?? []).length > 0 && (mappedRoutes.get(name) ?? []).length > 0) fail(`Route mapped for ${name} without a registered handler ${handler}`);
}
for (const reserved of allowlist.reservedRoutes) for (const [request, routes] of mappedRoutes) for (const route of routes) if (route.path.includes(reserved.pathContains)) fail(`Reserved route ${route.path} (${request}) is mapped before #${reserved.issue}`);

// ---------- remaining identities (existing owners gaining explicit identity) ----------
const remainingStatus = new Map();
const remainingDetail = new Map();
for (const contract of remaining) {
    const shape = requestMatches(contract.command, contract.fields ?? []);
    const handler = contract.handler ?? `${contract.command}Handler`;
    const handlerOk = registeredHandlers.has(handler);
    const status = shape.exists && shape.ok && handlerOk ? 'implemented' : shape.exists ? 'partial' : 'pending';
    remainingStatus.set(contract.command, deferred.has(contract.command) && status !== 'implemented' ? 'blocked' : status);
    remainingDetail.set(contract.command, status === 'implemented' ? '' : deferred.has(contract.command) ? `#${deferred.get(contract.command).issue}` : shape.exists ? [!shape.ok ? `contract [missing ${shape.missing.join('/') || '-'}; mistyped ${shape.mistyped.join('/') || '-'}]` : null, !handlerOk ? `handler ${handler}` : null].filter(Boolean).join(',') : 'no request type');
}

// ---------- receivers ----------
const receiverStatus = new Map();
const receiverDetail = new Map();
const reactorByCommand = new Map(reactorCatalog.reactors.map(r => [r.command, r]));
for (const reactor of reactorCatalog.reactors) {
    const shape = requestMatches(reactor.command, reactor.fields);
    const handlerOk = registeredHandlers.has(reactor.handler);
    const status = shape.exists && shape.ok && handlerOk ? 'implemented' : shape.exists ? 'partial' : 'pending';
    receiverStatus.set(reactor.command, deferred.has(reactor.command) && status !== 'implemented' ? 'blocked' : status);
    receiverDetail.set(reactor.command, shape.exists ? [!shape.ok ? `contract [missing ${shape.missing.join('/') || '-'}; mistyped ${shape.mistyped.join('/') || '-'}]` : null, !handlerOk ? `handler ${reactor.handler}` : null].filter(Boolean).join(',') : 'no request type');
}
const reactorClasses = reactorCatalog.reactors.filter(r => registeredReactors.has(r.reactor)).map(r => r.reactor);

// ---------- processes ----------
const processStatus = new Map();
const processDetail = new Map();
const abortStatus = name => {
    if (receiverStatus.has(name)) return receiverStatus.get(name);
    if (commandStatus.has(name)) return commandStatus.get(name);
    const shape = requestMatches(name, []);
    return shape.exists && registeredHandlers.has(`${name}Handler`) ? 'implemented' : shape.exists ? 'partial' : 'pending';
};
for (const process of reactorCatalog.processes) {
    const receivers = [...new Set([...process.stages.flatMap(s => s.receivers), ...(process.abort ?? [])])];
    const statuses = receivers.map(r => [r, receiverStatus.get(r) ?? abortStatus(r)]);
    const registered = literals.has(process.process);
    const allDone = statuses.every(([, s]) => s === 'implemented');
    const anyDone = statuses.some(([, s]) => s !== 'pending');
    processStatus.set(process.process, allDone && registered ? 'implemented' : anyDone || registered ? 'partial' : 'pending');
    processDetail.set(process.process, statuses.filter(([, s]) => s !== 'implemented').map(([r, s]) => `${r}:${s}`).join(',') + (registered ? '' : ' [definition literal absent]'));
    if (registered && !allDone) fail(`Process ${process.process} is registered but these receivers are incomplete: ${statuses.filter(([, s]) => s !== 'implemented').map(([r]) => r).join(', ')}`);
    // A public route ships in the same PR as every receiver of every process rooted at its native facts.
    const rootCommands = commands.filter(c => c.status !== 'remove' && (c.events ?? []).some(e => process.rootSourceEvents.includes(e)));
    for (const command of rootCommands) {
        // A selected replacement contract can be exposed before its owner or handler is complete.
        // Legacy contracts with the same name remain pending until their owning cutover slice.
        const exposed = requestMatches(command.command, command.fields).ok && ((mappedRoutes.get(command.command) ?? []).length > 0 || mcpTools.has(command.command));
        if (exposed && !(allDone && registered)) fail(`${command.command} is exposed over HTTP/MCP but ${process.process}, rooted at its facts, is incomplete: ${statuses.filter(([, s]) => s !== 'implemented').map(([r]) => r).join(', ') || 'definition literal absent'}`);
    }
}

// ---------- owners and legacy remnants ----------
const ownerStatus = new Map();
const ownerDetail = new Map();
for (const row of split.splitOwners) {
    const targetNames = new Set(row.targetOwners.map(o => o.proposedAggregate));
    for (const owner of row.targetOwners) {
        const name = owner.proposedAggregate;
        const classExists = aggregates.has(name);
        const ownerEvents = [...owner.existingDomainEvents, ...(owner.newDomainEvents ?? [])];
        const eventsDone = ownerEvents.every(e => eventStatus.get(e) === 'implemented');
        const ownerCommands = (owner.commands ?? []).filter(c => commandStatus.has(c));
        const commandsDone = ownerCommands.every(c => ['implemented', 'removed-ok', 'blocked'].includes(commandStatus.get(c)));
        const status = classExists && eventsDone && commandsDone ? 'implemented' : classExists ? 'partial' : 'pending';
        ownerStatus.set(name, status);
        ownerDetail.set(name, [!classExists ? 'class' : null, !eventsDone ? `events ${ownerEvents.filter(e => eventStatus.get(e) !== 'implemented').join('/')}` : null, !commandsDone ? `commands ${ownerCommands.filter(c => !['implemented', 'removed-ok', 'blocked'].includes(commandStatus.get(c))).join('/')}` : null].filter(Boolean).join('; '));
    }
    const rootImplemented = row.targetOwners.every(o => ownerStatus.get(o.proposedAggregate) === 'implemented');
    if (!rootImplemented) continue;
    if (!targetNames.has(row.currentAggregate) && aggregates.has(row.currentAggregate)) fail(`Legacy aggregate ${row.currentAggregate} still exists although every target owner is implemented`);
    for (const fact of [...(row.technicalFactsToExternalize ?? []), ...(row.migrationFactsToPrune ?? [])]) {
        const factName = typeof fact === 'string' ? fact : fact.event ?? fact.fact ?? fact.name;
        if (factName && domainEvents.has(factName)) fail(`Externalized or pruned fact ${factName} still declared although ${row.currentAggregate} replacements are implemented`);
    }
    for (const command of (row.currentCommands ?? [])) if (commandStatus.get(command) === 'pending' && commands.find(c => c.command === command)?.status === 'remove') fail(`Removed command ${command} still declared although ${row.currentAggregate} replacements are implemented`);
}

// ---------- traces ----------
// Source presence is evidence that a trace has a test, not that the test has passed.
const traceStatus = new Map(traces.map(t => [t.id, [...testNames].some(n => n === t.test || n.endsWith(`_${t.test}`) || n.endsWith(t.test)) ? 'present' : 'pending']));

// ---------- report ----------
const count = (map, ...states) => [...map.values()].filter(v => states.includes(v)).length;
const summary = {
    sourceHead: catalog('target-command-contracts.json').sourceHead,
    ownersImplemented: `${count(ownerStatus, 'implemented')}/${ownerStatus.size}`,
    ownersPartial: count(ownerStatus, 'partial'),
    commandsImplemented: `${count(commandStatus, 'implemented', 'removed-ok')}/${commandStatus.size}`,
    commandsPartial: count(commandStatus, 'partial'),
    commandsBlocked: count(commandStatus, 'blocked'),
    remainingIdentitiesImplemented: `${count(remainingStatus, 'implemented')}/${remainingStatus.size}`,
    remainingIdentitiesPartial: count(remainingStatus, 'partial'),
    remainingIdentitiesBlocked: count(remainingStatus, 'blocked'),
    receiversImplemented: `${count(receiverStatus, 'implemented')}/${receiverStatus.size}`,
    receiversPartial: count(receiverStatus, 'partial'),
    processesImplemented: `${count(processStatus, 'implemented')}/${processStatus.size}`,
    processesPartial: count(processStatus, 'partial'),
    eventsImplemented: `${count(eventStatus, 'implemented')}/${eventStatus.size}`,
    tracesPresent: `${count(traceStatus, 'present')}/${traceStatus.size}`,
    reactorClassesPresent: reactorClasses.length,
    scanned: { sourceFiles: sources.length, testFiles: tests.length, aggregates: aggregates.size, domainEvents: domainEvents.size, requests: requests.size, registeredHandlers: registeredHandlers.size, registeredReactors: registeredReactors.size, routes: [...mappedRoutes.values()].flat().length, mcpTools: mcpTools.size, tests: testNames.size },
    failures: failures.length,
    result: failures.length === 0 ? 'ok' : 'drift'
};
if (args.has('--list')) {
    const print = (title, status, detail) => {
        console.log(`\n## ${title}`);
        for (const [name, state] of [...status].sort((a, b) => a[0].localeCompare(b[0]))) console.log(`${state.padEnd(11)} ${name}${detail?.get(name) ? `  (${detail.get(name)})` : ''}`);
    };
    print('Owners', ownerStatus, ownerDetail);
    print('Commands', commandStatus, commandDetail);
    print('Remaining identities', remainingStatus, remainingDetail);
    print('Receivers', receiverStatus, receiverDetail);
    print('Processes', processStatus, processDetail);
    print('Events', eventStatus);
    print('Traces', traceStatus);
    console.log('');
}
if (!args.has('--json')) for (const failure of failures) console.error(`FAIL ${failure}`);
console.log(JSON.stringify(summary, null, args.has('--json') ? 0 : 2));
process.exit(failures.length === 0 ? 0 : 1);
