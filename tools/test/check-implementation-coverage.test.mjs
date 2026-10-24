import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

const tool = path.resolve(import.meta.dirname, '../check-implementation-coverage.mjs');

function checkFixture(t, source, trace = false, receiverFields = [], options = {}) {
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
    catalog('aggregate-split-targets', { splitOwners: options.splitOwners ?? [] });
    catalog('target-command-contracts', { sourceHead: 'fixture', commands: [{ command: 'BeginExample', owner: 'Example', fields: [{ name: 'Id', type: 'Uuid' }], http: [{}], events: ['ExampleBegun'] }] });
    catalog('source-admission-contracts', { contracts: [] });
    catalog('remaining-command-identities', { contracts: [] });
    catalog('target-event-contracts', { events: options.events ?? [] });
    catalog('design-review-traces', { traces: trace ? [{ id: 'example', test: 'ShouldCompleteGivenExample' }] : [] });
    catalog('target-reactor-contracts', options.reactorCatalog ?? {
        reactors: [{ command: 'FinishExample', handler: 'FinishExampleHandler', fields: receiverFields, reactor: 'ExampleReactor', http: false, mcp: false }],
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

test('ShouldPermitRegisteredProcessGivenReceiverInheritsRequestThroughInterfaces', t => {
    const result = checkFixture(t, `
        public class Example : Aggregate { }
        public record BeginExample(Uuid Id) : IRequest<Result>;
        public record FinishExample() : ISecondReceiver;
        public interface ISecondReceiver : IFirstReceiver { }
        public interface IFirstReceiver : IRequest<Result>, IIdentifiedRequest { }
        services.AddRequestHandler<BeginExampleHandler>();
        services.AddRequestHandler<FinishExampleHandler>();
        app.MapPortiaPost<BeginExample, Result>("/examples");
        const string Process = "ExampleProcess";
    `);
    assert.equal(result.status, 0);
    assert.equal(result.report.receiversImplemented, '1/1');
});

test('ShouldRejectExposedRootGivenInheritedRequestAndIncompleteProcess', t => {
    const result = checkFixture(t, `
        public record BeginExample(Uuid Id) : IRootCommand;
        public interface IRootCommand : IRequest<Result> { }
        app.MapPortiaPost<BeginExample, Result>("/examples");
    `);
    assert.equal(result.status, 1);
});

test('ShouldKeepWrapperAsLegacyGivenRequestTypeArgumentWithoutRequestInheritance', t => {
    const result = checkFixture(t, `
        public record BeginExample(Uuid Id) : IEnvelope<IRequest<Result>>;
        public interface IEnvelope<T> { }
        app.MapPortiaPost<BeginExample, Result>("/examples");
    `);
    assert.equal(result.status, 0);
});

const internalUnsignedReceiver = `
    public class Example : Aggregate { }
    public record BeginExample(Uuid Id) : IRequest<Result>;
    public record FinishExample(ulong ExpectedRevision) : IRequest<Result>;
    services.AddRequestHandler<BeginExampleHandler>();
    services.AddRequestHandler<FinishExampleHandler>();
    app.MapPortiaPost<BeginExample, Result>("/examples");
    const string Process = "ExampleProcess";
`;

test('ShouldPermitCatalogUnsignedRevisionGivenUnexposedInternalReceiver', t => {
    const result = checkFixture(t, internalUnsignedReceiver, false, [{ name: 'ExpectedRevision', type: 'ulong' }]);
    assert.equal(result.status, 0);
});

for (const surface of ['http', 'mcp']) {
    test(`ShouldRejectCatalogUnsignedRevisionGivenReceiverExposedOver${surface}`, t => {
        const expose = surface === 'http'
            ? 'app.MapPortiaPost<FinishExample, Result>("/receivers");'
            : 'services.AddMcpTool<FinishExample>();';
        const result = checkFixture(t, internalUnsignedReceiver + expose, false,
            [{ name: 'ExpectedRevision', type: 'ulong' }]);
        assert.equal(result.status, 1);
    });
}

const enumReceiver = converted => `
    public class Example : Aggregate { }
    public record BeginExample(Uuid Id) : IRequest<Result>;
    public record FinishExample(SubjectKind SubjectKind) : IRequest<Result>;
    ${converted ? '[JsonConverter(typeof(JsonStringEnumConverter<SubjectKind>))]' : ''}
    public enum SubjectKind { Staff, CanonicalUser }
    services.AddRequestHandler<BeginExampleHandler>();
    services.AddRequestHandler<FinishExampleHandler>();
    app.MapPortiaPost<BeginExample, Result>("/examples");
    const string Process = "ExampleProcess";
`;

test('ShouldPermitStringWireShapeGivenUnexposedReceiverWithJsonStringEnum', t => {
    const result = checkFixture(t, enumReceiver(true), false, [{ name: 'SubjectKind', type: 'string' }]);
    assert.equal(result.status, 0);
});

test('ShouldRejectStringWireShapeGivenReceiverWithNumericEnum', t => {
    const result = checkFixture(t, enumReceiver(false), false, [{ name: 'SubjectKind', type: 'string' }]);
    assert.equal(result.status, 1);
});

for (const surface of ['http', 'mcp']) {
    test(`ShouldRequireSelectedPublicTypeGivenEnumReceiverExposedOver${surface}`, t => {
        const expose = surface === 'http'
            ? 'app.MapPortiaPost<FinishExample, Result>("/receivers");'
            : 'services.AddMcpTool<FinishExample>();';
        const result = checkFixture(t, enumReceiver(true) + expose, false, [{ name: 'SubjectKind', type: 'string' }]);
        assert.equal(result.status, 1);
    });
}

const sharedChildCatalog = ordered => ({
    reactors: [
        { command: 'FinishExample', handler: 'FinishExampleHandler', fields: [], reactor: 'ExampleReactor', sourceEvents: ['ExampleBegun'] },
        { command: 'FinishOther', handler: 'FinishOtherHandler', fields: [], reactor: 'OtherReactor', sourceEvents: ['OtherBegun'] }
    ],
    processes: [{ process: 'ExampleProcess', ordered, rootSourceEvents: ['ExampleBegun', 'OtherBegun'], abort: [],
        stages: [{ receivers: ['FinishExample', 'FinishOther'] }] }]
});
const shippedChildVariant = `
    public class Example : Aggregate { }
    public record BeginExample(Uuid Id) : IRequest<Result>;
    public record ExampleBegun() : DomainEvent;
    public record FinishExample() : IRequest<Result>;
    services.AddRequestHandler<BeginExampleHandler>();
    services.AddRequestHandler<FinishExampleHandler>();
    app.MapPortiaPost<BeginExample, Result>("/examples");
    const string Process = "ExampleProcess";
`;

test('ShouldPermitShippedIndependentRootGivenUnshippedUnrelatedChildVariant', t => {
    const result = checkFixture(t, shippedChildVariant, false, [], { reactorCatalog: sharedChildCatalog(false) });
    assert.equal(result.status, 0);
});

test('ShouldRejectRegisteredIndependentRootGivenShippedChildWithoutReceiver', t => {
    const result = checkFixture(t, shippedChildVariant + 'public record OtherBegun() : DomainEvent;', false, [],
        { reactorCatalog: sharedChildCatalog(false) });
    assert.equal(result.status, 1);
});

test('ShouldRequireAllReceiversGivenOrderedProcessWithUnshippedRoot', t => {
    const result = checkFixture(t, shippedChildVariant, false, [], { reactorCatalog: sharedChildCatalog(true) });
    assert.equal(result.status, 1);
});

test('ShouldDiscoverRegisteredDefinitionGivenCatalogProcessNameWithoutProcessSuffix', t => {
    const catalog = sharedChildCatalog(false);
    catalog.reactors = catalog.reactors.slice(0, 1);
    catalog.processes = [{ process: 'ExampleFanOut', rootSourceEvents: ['ExampleBegun'], stages: [{ receivers: ['FinishExample'] }] }];
    const result = checkFixture(t, shippedChildVariant.replace('"ExampleProcess"', '"ExampleFanOut"'), false, [],
        { reactorCatalog: catalog });
    assert.equal(result.status, 0);
});


test('ShouldDiscoverHandlersGivenFullyQualifiedRegistrations', t => {
    const source = shippedChildVariant
        .replace('AddRequestHandler<BeginExampleHandler>', 'AddRequestHandler<Example.Commands.BeginExampleHandler>')
        .replace('AddRequestHandler<FinishExampleHandler>', 'AddRequestHandler<Example.Receivers.FinishExampleHandler>');
    const result = checkFixture(t, source, false, [], { reactorCatalog: sharedChildCatalog(false) });
    assert.equal(result.status, 0);
    assert.equal(result.report.scanned.registeredHandlers, 2);
});


test('ShouldUseFinalEventCatalogGivenSupersededPreliminaryOwnerFact', t => {
    const result = checkFixture(t, `
        public class Example : Aggregate { }
        [Discriminator("example.accepted", 1)]
        public record ExampleAccepted(Uuid Id) : DomainEvent;
    `, false, [], {
        reactorCatalog: { reactors: [], processes: [] },
        splitOwners: [{ currentAggregate: 'OldExample', targetOwners: [{ proposedAggregate: 'Example',
            existingDomainEvents: [], newDomainEvents: ['PreliminaryProposedFact'], technicalProcessState: [], commands: [] }] }],
        events: [{ event: 'ExampleAccepted', owner: 'Example', discriminator: 'example.accepted',
            discriminatorVersion: 1, fields: [{ name: 'Id', type: 'Uuid' }] }]
    });
    assert.equal(result.status, 0);
    assert.equal(result.report.ownersImplemented, '1/1');
});

test('ShouldRecognizeTechnicalOwnerGivenCompleteRegisteredProcess', t => {
    const result = checkFixture(t, `
        public static class ExampleProcess { public const string Name = "ExampleProcess"; }
        public record FinishExample() : IRequest<Result>;
        services.AddRequestHandler<FinishExampleHandler>();
    `, false, [], {
        splitOwners: [{ currentAggregate: 'OldExample', targetOwners: [{ proposedAggregate: 'ExampleProcess',
            existingDomainEvents: [], newDomainEvents: [], technicalProcessState: ['attempts'], commands: [] }] }]
    });
    assert.equal(result.status, 0);
    assert.equal(result.report.ownersImplemented, '1/1');
});
