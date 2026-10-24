#!/usr/bin/env node
// Direct TypeSafe HTTP review of each implementation file. Credentials are environment-only.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const argv = process.argv.slice(2);
const option = (name, fallback) => argv.includes(name) ? argv[argv.indexOf(name) + 1] : fallback;
const root = path.resolve(import.meta.dirname, '..');
const output = path.resolve(option('--out', path.join(root, 'artifacts/jev-file-reviews')));
const concurrency = Number(option('--concurrency', '4'));
const apiKey = process.env.TYPESAFE_API_KEY;
if (!apiKey) throw new Error('TYPESAFE_API_KEY must be provided in the command environment.');
if (!Number.isInteger(concurrency) || concurrency < 1 || concurrency > 8) throw new Error('Concurrency must be 1-8.');
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const walk = dir => fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    if (['bin', 'obj', 'node_modules'].includes(entry.name) || entry.name.startsWith('.')) return [];
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : entry.name.endsWith('.cs') ? [full] : [];
});
const sources = [...walk(path.join(root, 'src/Compliance.Core')), ...walk(path.join(root, 'src/Compliance.Common')),
    ...walk(path.join(root, 'src/Compliance.App'))]
    .map(file => ({ path: path.relative(root, file), text: fs.readFileSync(file, 'utf8') }));
const byName = new Map();
for (const source of sources) byName.set(path.basename(source.path, '.cs'), source);
const kindOf = source => {
    const kinds = [];
    if (/\bclass\s+\w+[^;{]*:\s*(?:[\w.]+\.)?Aggregate\b/s.test(source.text)) kinds.push('aggregate');
    if (source.path.endsWith('Handler.cs') || source.text.includes('IRequestHandler<')) kinds.push('handler');
    if (source.path.endsWith('Reactor.cs') || /\bclass\s+\w+Reactor\b/.test(source.text)) kinds.push('reactor');
    return kinds;
};
let inventory = sources.filter(source => source.path.startsWith('src/Compliance.Core/'))
    .map(source => ({ ...source, kinds: kindOf(source) })).filter(source => source.kinds.length);
if (argv.includes('--files')) {
    const supplied = fs.readFileSync(option('--files'), 'utf8');
    const names = new Set(supplied.trim().startsWith('[')
        ? JSON.parse(supplied).map(row => typeof row === 'string' ? row : row.path) : supplied.trim().split(/\r?\n/));
    inventory = sources.filter(source => names.has(source.path))
        .map(source => ({ ...source, kinds: kindOf(source).length ? kindOf(source) : ['dependency'] }));
}
inventory.sort((a, b) => a.path.localeCompare(b.path));
fs.mkdirSync(path.join(output, 'requests'), { recursive: true });
fs.mkdirSync(path.join(output, 'responses'), { recursive: true });
const questions = {
    authority_gap: { type: 'noul', instructions: 'Does this file contain a concrete authority, tenant/program isolation, or source-proof binding defect? Judge its actual responsibility. Do not require aggregates to repeat trusted handler authorization or queries to own mutation policy. Missing helper context alone is not evidence of a defect.' },
    invariant_gap: { type: 'noul', instructions: 'Does this file contain a concrete domain invariant or aggregate boundary defect, such as independently mutable children sharing authority without a required atomic invariant, or a live foreign read deciding authoritative state? A bounded immutable manifest or read-only projection is not by itself a defect.' },
    recovery_gap: { type: 'noul', instructions: 'Does this file contain a concrete idempotency, replay, optimistic concurrency, interruption, cancellation, or unavailable-dependency recovery defect? Read-only queries do not require mutation replay receipts. Account for the supplied shared helpers.' },
    incomplete_path: { type: 'noul', instructions: 'Does this implementation expose or execute unfinished behavior, such as a throwing stub, a missing required receiver, an admitted effect with no durable recovery, or a placeholder result? Explicitly deferred and unregistered paths are blockers to track, not silently implemented behavior.' },
};
const version = 'implementation-file-review-v1';
const results = [];
let cursor = 0;
let calls = 0;
let cached = 0;
let nextStart = 0;
let rateTail = Promise.resolve();
const throttle = () => {
    const reserved = rateTail.then(async () => {
        const delay = Math.max(0, nextStart - Date.now());
        nextStart = Math.max(nextStart, Date.now()) + 250;
        if (delay) await pause(delay);
    });
    rateTail = reserved.catch(() => {});
    return reserved;
};
const contextFor = source => {
    const requestName = path.basename(source.path, '.cs').replace(/Handler$/, '');
    const tokens = [...new Set(source.text.match(/\b[A-Z][A-Za-z0-9]+\b/g) ?? [])];
    const candidates = tokens.map(name => byName.get(name)).filter(item => item && item.path !== source.path);
    candidates.sort((a, b) => {
        const rank = item => path.basename(item.path, '.cs') === requestName ? 0 : /(?:Verifier|ReceiverSupport|Commands|Operations|Admission|Reservations|Content)\.cs$/.test(item.path) ? 1 : 2;
        return rank(a) - rank(b) || a.path.localeCompare(b.path);
    });
    let bytes = 0;
    const selected = [];
    for (const candidate of candidates) {
        if (selected.length >= 5) break;
        if (candidate.text.length + bytes > 18000) continue;
        selected.push({ path: candidate.path, sha256: hash(candidate.text), source: candidate.text });
        bytes += candidate.text.length;
    }
    return selected;
};
const review = async source => {
    const state = {
        review_version: version,
        objective: 'Complete the accepted aggregate/command/reactor design. Report semantic risk signals per source file; probabilities are advisory, and code/test evidence decides findings. Blocked items are recorded locally and other files continue. No new GitHub issues.',
        file: { path: source.path, kinds: source.kinds, sha256: hash(source.text), source: source.text },
        dependency_context: contextFor(source),
        missing_context: 'Other contracts, authorizers, and tests may be outside this excerpt. Do not invent their absence.',
    };
    const body = { model: 'jev-latest', state, questions };
    const requestText = JSON.stringify(body);
    const key = hash(requestText);
    const responsePath = path.join(output, 'responses', `${key}.json`);
    if (fs.existsSync(responsePath)) {
        cached++;
        return JSON.parse(fs.readFileSync(responsePath, 'utf8'));
    }
    fs.writeFileSync(path.join(output, 'requests', `${key}.json`), JSON.stringify(body, null, 2) + '\n');
    let failure;
    for (let attempt = 0; attempt < 4; attempt++) {
        try {
            await throttle();
            const headers = { Authorization: `Bearer ${apiKey}`, 'Content-Type': 'application/json' };
            if (requestText.length > 45000) headers['TypeSafe-Danger-Zone-Long-Context'] = 'true';
            const response = await fetch('https://api.typesafe.ai/v1/systemone', {
                method: 'POST', headers, body: requestText, signal: AbortSignal.timeout(45000),
            });
            calls++;
            if (!response.ok) {
                const detail = (await response.text()).slice(0, 1500);
                failure = `HTTP ${response.status}: ${detail}`;
                if (response.status === 429 || response.status >= 500) {
                    const retry = Number(response.headers.get('retry-after'));
                    await pause(Math.min(60000, Number.isFinite(retry) && retry > 0 ? retry * 1000 : 1000 * 2 ** attempt));
                    continue;
                }
                break;
            }
            const answer = await response.json();
            const row = {
                path: source.path, kinds: source.kinds, sha256: state.file.sha256,
                dependency_sha256: state.dependency_context.map(item => ({ path: item.path, sha256: item.sha256 })),
                review_key: key, reviewed_at: new Date().toISOString(), status: 'reviewed',
                model: answer.model, answers: answer.answers, usage: answer.usage,
            };
            fs.writeFileSync(responsePath, JSON.stringify(row, null, 2) + '\n');
            return row;
        } catch (error) {
            failure = String(error);
            await pause(1000 * 2 ** attempt);
        }
    }
    return { path: source.path, kinds: source.kinds, sha256: state.file.sha256, review_key: key, status: 'blocked', error: failure };
};
const worker = async () => {
    while (cursor < inventory.length) {
        const source = inventory[cursor++];
        results.push(await review(source));
        if (results.length % 25 === 0 || results.length === inventory.length) {
            fs.writeFileSync(path.join(output, 'progress.json'), JSON.stringify({ total: inventory.length, complete: results.length, calls, cached }, null, 2));
            process.stdout.write(JSON.stringify({ complete: results.length, total: inventory.length, calls, cached }) + '\n');
        }
    }
};
await Promise.all(Array.from({ length: concurrency }, worker));
results.sort((a, b) => a.path.localeCompare(b.path));
const summary = {
    version, total: inventory.length, reviewed: results.filter(row => row.status === 'reviewed').length,
    blocked: results.filter(row => row.status === 'blocked').length, calls, cached,
    usage: results.reduce((sum, row) => ({ input_tokens: sum.input_tokens + (row.usage?.input_tokens ?? 0), output_tokens: sum.output_tokens + (row.usage?.output_tokens ?? 0) }), { input_tokens: 0, output_tokens: 0 }),
    records: results,
};
fs.writeFileSync(path.join(output, 'manifest.json'), JSON.stringify(summary, null, 2) + '\n');
process.stdout.write(JSON.stringify({ ...summary, records: undefined }) + '\n');
