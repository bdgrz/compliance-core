#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { validateInventory } from './lib/contracts.mjs';

const root = path.resolve(import.meta.dirname, '..');
const inventory = JSON.parse(fs.readFileSync(path.join(root, 'test/Compliance.Tests/Fixtures/aggregate-command-coverage.json'), 'utf8'));
const failures = validateInventory(inventory, file => fs.existsSync(path.join(root, file)));
if (failures.length) {
  console.error(failures.join('\n'));
  process.exitCode = 1;
} else console.log('Runtime coverage fixture is consistent; mandatory backend tests verify reflected contracts, source paths and transport rules.');
