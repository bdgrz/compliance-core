import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { validateDocuments, validateInventory } from '../lib/contracts.mjs';

function fixture(run) {
  const root = mkdtempSync(path.join(tmpdir(), 'compliance-contracts-'));
  try {
    mkdirSync(path.join(root, 'contexts'));
    const write = (file, text) => writeFileSync(path.join(root, file), text);
    run(root, write);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

const context = '# Example\n\n## Purpose\n\n## Language\n\n## Aggregates\n\n### Owner\n\n**Identity:** tenant + id.\n\n**Invariant:** one decision.\n\n| Command | Who | Accepts when | Emits |\n| --- | --- | --- | --- |\n| DecideThing | Member | New | ThingDecided |\n\n| Event | Carries |\n| --- | --- |\n| ThingDecided | tenant, id |\n\n## Flows\n\n## Decisions\n\n## Open questions\n';

test('documents accept linked context and declared emitted events', () => fixture((root, write) => {
  write('README.md', '[Example](contexts/example.md#owner)');
  write('contexts/example.md', context);
  assert.deepEqual(validateDocuments(root), []);
}));

test('documents reject missing links and anchors', () => fixture((root, write) => {
  write('README.md', '[Missing](missing.md) [Anchor](contexts/example.md#absent)');
  write('contexts/example.md', context);
  const failures = validateDocuments(root);
  assert.ok(failures.some(item => item.includes('missing.md')));
  assert.ok(failures.some(item => item.includes('#absent')));
}));

test('documents reject unlisted contexts, missing owner rules and undeclared facts', () => fixture((root, write) => {
  write('README.md', '# Index');
  write('contexts/example.md', context.replace('**Invariant:**', '**Rule:**').replace('| ThingDecided | tenant, id |', '| OtherFact | tenant, id |'));
  const failures = validateDocuments(root);
  assert.ok(failures.some(item => item.includes('README')));
  assert.ok(failures.some(item => item.includes('Invariant')));
  assert.ok(failures.some(item => item.includes('ThingDecided')));
}));

test('documents reject duplicate commands and malformed event tables', () => fixture((root, write) => {
  write('README.md', '[Example](contexts/example.md)');
  write('contexts/example.md', context.replace('| DecideThing | Member | New | ThingDecided |', '| DecideThing | Member | New | ThingDecided |\n| DecideThing | Member | New | ThingDecided |').replace('| ThingDecided | tenant, id |', '\nAn interrupted table.\n| ThingDecided | tenant, id |'));
  const failures = validateDocuments(root);
  assert.ok(failures.some(item => item.includes('duplicate command')));
  assert.ok(failures.some(item => item.includes('table')));
}));

test('inventory rejects missing paths, duplicate rows and stale counts', () => {
  const inventory = { schemaVersion: 1, counts: { domainEventContracts: 3, aggregateTypes: 0, requestContracts: 0 },
    facts: [{ fact: 'Fact', contract: 'src/missing.cs', disposition: 'aggregate_fact', reducers: [], emitters: [] },
      { fact: 'Fact', contract: 'src/missing.cs', disposition: 'invented', reducers: [], emitters: [] }], aggregates: [], requests: [] };
  const failures = validateInventory(inventory, () => false);
  assert.ok(failures.some(item => item.includes('duplicate')));
  assert.ok(failures.some(item => item.includes('domainEventContracts')));
  assert.ok(failures.some(item => item.includes('missing.cs')));
  assert.ok(failures.some(item => item.includes('disposition')));
});

test('documents reject missing command and event payload cells', () => fixture((root, write) => {
  write('README.md', '[Example](contexts/example.md)');
  write('contexts/example.md', context.replace('| DecideThing | Member | New | ThingDecided |', '| DecideThing | Member | New |').replace('| ThingDecided | tenant, id |', '| ThingDecided | |'));
  const failures = validateDocuments(root);
  assert.ok(failures.some(item => item.includes('command row requires')));
  assert.ok(failures.some(item => item.includes('event row requires')));
}));
