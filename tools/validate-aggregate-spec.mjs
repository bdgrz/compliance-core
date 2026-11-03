#!/usr/bin/env node
import path from 'node:path';
import { validateDocuments } from './lib/contracts.mjs';

const failures = validateDocuments(path.resolve(import.meta.dirname, '../docs/proposed'));
if (failures.length) {
  console.error(failures.join('\n'));
  process.exitCode = 1;
} else console.log('Proposed context structure, links and event payload declarations are consistent.');
