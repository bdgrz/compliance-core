import fs from 'node:fs';
import path from 'node:path';

function files(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? files(file) : [file];
  });
}

function anchors(text) {
  const seen = new Map();
  return new Set([...text.matchAll(/^#{1,6} (.+)$/gm)].map(match => {
    const slug = match[1].toLowerCase().replace(/[^\p{L}\p{N}_\- ]/gu, '').replace(/ /g, '-');
    const count = seen.get(slug) ?? 0;
    seen.set(slug, count + 1);
    return count ? `${slug}-${count}` : slug;
  }));
}

// This validates the hand-authored proposal. It makes no implementation-completion claim.
export function validateDocuments(root) {
  const failures = [];
  const documents = files(root).filter(file => file.endsWith('.md'));
  const readme = fs.readFileSync(path.join(root, 'README.md'), 'utf8');
  for (const file of documents) {
    const text = fs.readFileSync(file, 'utf8');
    const relative = path.relative(root, file);
    const fail = message => failures.push(`${relative}: ${message}`);
    for (const [, raw] of text.matchAll(/(?<!!)\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)/g)) {
      if (/^[a-z][a-z\d+.-]*:/i.test(raw)) continue;
      const [link, fragment] = raw.split('#');
      const target = path.resolve(path.dirname(file), decodeURIComponent(link || path.basename(file)));
      if (!fs.existsSync(target)) fail(`missing link ${raw}`);
      else if (fragment && fs.statSync(target).isFile() && target.endsWith('.md') &&
          !anchors(fs.readFileSync(target, 'utf8')).has(decodeURIComponent(fragment))) fail(`missing anchor ${raw}`);
    }
    if (path.dirname(file) !== path.join(root, 'contexts') || path.basename(file) === 'conventions.md') continue;
    if (!readme.includes(`contexts/${path.basename(file)}`)) fail('context absent from README');
    for (const heading of ['Purpose', 'Language', 'Aggregates', 'Flows', 'Decisions', 'Open questions'])
      if (!text.includes(`## ${heading}\n`)) fail(`missing ${heading} section`);
    const aggregateSection = text.split('## Aggregates\n')[1]?.split('\n## ')[0] ?? '';
    for (const owner of aggregateSection.split(/^### /m).slice(1))
      for (const field of ['Identity', 'Invariant'])
        if (!owner.includes(`**${field}:**`)) fail(`${owner.split('\n')[0]} missing ${field}`);
    const commands = new Set();
    const events = new Set();
    const emits = new Set();
    let table = null;
    for (const line of aggregateSection.split('\n')) {
      if (!line.startsWith('|')) { table = null; continue; }
      const cells = line.split('|').slice(1, -1).map(cell => cell.trim());
      if (cells[0] === 'Command') { table = 'command'; continue; }
      if (cells[0] === 'Event') { table = 'event'; continue; }
      if (/^:?-+/.test(cells[0])) continue;
      if (!table) { fail(`row outside command/event table: ${cells[0]}`); continue; }
      const required = table === 'command' ? 4 : 2;
      if (cells.length !== required || cells.some(cell => !cell))
        fail(`${table} row requires ${required} nonempty cells: ${cells[0]}`);
      if (table === 'command') {
        if (commands.has(cells[0])) fail(`duplicate command ${cells[0]}`);
        commands.add(cells[0]);
        for (const event of (cells[3] ?? '').match(/\b[A-Z][A-Za-z0-9]+\b/g) ?? []) emits.add(event);
      } else {
        if (events.has(cells[0])) fail(`duplicate event ${cells[0]}`);
        events.add(cells[0]);
      }
    }
    for (const event of emits) if (!events.has(event)) fail(`emitted event has no payload table: ${event}`);
  }
  return failures;
}

// Reflection/Roslyn unit tests enforce source coverage; this gate checks fixture integrity.
export function validateInventory(inventory, exists) {
  const failures = [];
  if (inventory.schemaVersion !== 1) failures.push('unsupported inventory schemaVersion');
  const dispositions = new Set(['aggregate_fact', 'audit_only', 'legacy_replay', 'confirmed_gap',
    'migration_fact', 'superseded_by_decision', 'superseded_vocabulary']);
  for (const [collection, key, count] of [['facts', 'fact', 'domainEventContracts'],
    ['aggregates', 'aggregate', 'aggregateTypes'], ['requests', 'request', 'requestContracts']]) {
    const rows = inventory[collection] ?? [];
    if (inventory.counts?.[count] !== rows.length) failures.push(`stale count ${count}`);
    const names = new Set();
    for (const row of rows) {
      if (!row[key] || names.has(row[key])) failures.push(`duplicate or missing ${key}: ${row[key]}`);
      names.add(row[key]);
      if (collection === 'facts' && !dispositions.has(row.disposition)) failures.push(`invalid disposition: ${row[key]}`);
    }
  }
  function visit(value) {
    if (typeof value === 'string' && /^(src|test)\/.*\.cs$/.test(value) && !exists(value)) failures.push(`missing source path ${value}`);
    else if (Array.isArray(value)) value.forEach(visit);
    else if (value && typeof value === 'object') Object.values(value).forEach(visit);
  }
  visit(inventory);
  return [...new Set(failures)];
}
