import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

const tool = path.resolve(import.meta.dirname, '../check-implementation-coverage.mjs');

function checkFixture(t, source, trace = false) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'compliance-coverage-'));
    t.after(() => fs.rmSync(root, { recursive: true, force: true }));
    const write = (name, value) => {
        const file = path.join(root, name);
        fs.mkdirSync(path.dirname(file), { recursive: true });
        fs.writeFileSync(file, typeof value === 'string' ? value : JSON.stringify(value));
    };
    const catalog = (name, value) => write(`docs/product/audits/2026-10-23/${name}.json`, value);
    write('tools/check-implementation-coverage.mjs', fs.readFileSync(tool, 'utf8'));
    write('tools/implementation-coverage-allowlist.json', { deferred: [], ulongExpectedRevision: [], reservedRoutes: [] });
    catalog('aggregate-split-targets', { splitOwners: [] });
    catalog('target-command-contracts', { sourceHead: 'fixture', commands: [{ command: 'BeginExample', owner: 'Example', fields: [{ name: 'Id', type: 'Uuid' }], http: [{}], events: ['ExampleBegun'] }] });
    catalog('source-admission-contracts', { contracts: [] });
    catalog('remaining-command-identities', { contracts: [] });
    catalog('target-event-contracts', { events: [] });
    catalog('design-review-traces', { traces: trace ? [{ id: 'example', test: 'ShouldCompleteGivenExample' }] : [] });
    catalog('target-reactor-contracts', {
        reactors: [{ command: 'FinishExample', handler: 'FinishExampleHandler', fields: [], reactor: 'ExampleReactor' }],
        processes: [{ process: 'ExampleProcess', rootSourceEvents: ['ExampleBegun'], stages: [{ receivers: ['FinishExample'] }] }]
    });
    write('src/Compliance.Core/Example.cs', source);
    write('test/ExampleTests.cs', trace ? 'public void ShouldCompleteGivenExample() { throw new Exception(); }' : '');
    const result = spawnSync(process.execPath, [path.join(root, 'tools/check-implementation-coverage.mjs'), '--json'], { encoding: 'utf8' });
    assert.equal(result.signal, null);
    return { ...result, report: JSON.parse(result.stdout) };
}

test('ShouldRejectExposedRootGivenIncompleteOwnerAndProcess', t => {
    const result = checkFixture(t, `
        public record BeginExample(Uuid Id) : IRequest<Result>;
        services.AddRequestHandler<BeginExampleHandler>();
        app.MapPortiaPost<BeginExample, Result>("/examples");
    `);
    assert.equal(result.status, 1);
    assert.equal(result.report.result, 'drift');
});

test('ShouldRejectExposedRootGivenMissingHandlerAndIncompleteProcess', t => {
    const result = checkFixture(t, `
        public record BeginExample(Uuid Id) : IRequest<Result>;
        app.MapPortiaPost<BeginExample, Result>("/examples");
    `);
    assert.equal(result.status, 1);
});

test('ShouldPermitLegacyRootGivenReplacementContractNotYetShipped', t => {
    const result = checkFixture(t, `
        public record BeginExample(string LegacyId) : IRequest<Result>;
        app.MapPortiaPost<BeginExample, Result>("/examples");
    `);
    assert.equal(result.status, 0);
});

test('ShouldPermitUnexposedRootGivenIncompleteProcess', t => {
    const result = checkFixture(t, 'public record BeginExample(Uuid Id) : IRequest<Result>;');
    assert.equal(result.status, 0);
});

test('ShouldRejectMcpRootGivenIncompleteProcess', t => {
    const result = checkFixture(t, `
        public record BeginExample(Uuid Id) : IRequest<Result>;
        services.AddMcpTool<BeginExample>();
    `);
    assert.equal(result.status, 1);
});

test('ShouldPermitExposedRootGivenCompleteProcess', t => {
    const result = checkFixture(t, `
        public class Example : Aggregate { }
        public record BeginExample(Uuid Id) : IRequest<Result>;
        public record FinishExample() : IRequest<Result>;
        services.AddRequestHandler<BeginExampleHandler>();
        services.AddRequestHandler<FinishExampleHandler>();
        app.MapPortiaPost<BeginExample, Result>("/examples");
        const string Process = "ExampleProcess";
    `);
    assert.equal(result.status, 0);
});

test('ShouldReportTracePresenceGivenUnexecutedTest', t => {
    const result = checkFixture(t, '', true);
    assert.equal(result.report.tracesPresent, '1/1');
    assert.equal(Object.hasOwn(result.report, 'tracesPassing'), false);
});
