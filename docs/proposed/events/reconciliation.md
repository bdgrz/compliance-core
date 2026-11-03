# Historical event-name reconciliation snapshot

This full name/disposition map is retained from an earlier reconciliation packet. Its recorded polish date is 2026-10-29, but its summary still reports 129 owners and 492 emitted events. The selected source catalogs report 131 business owners and 501 source event rows; the proposed target omits 1 source-only compatibility fact and adds 4 newly named event facts. Treat the snapshot as historical evidence until it is reconciled against the current catalogs.

Current selected event index: [events/README.md](README.md). Complete captured snapshot: [JSON](../data/event-reconciliation-snapshot.json).

## Recorded snapshot counts

| Field | Value |
| --- | --- |
| Source checkpoint | 8074fb02e1f5137fb3c9653231e1551f5fde34f2 |
| Raw name union | 559 |
| Snapshot event rows | 570 |
| Intended emitted canonical semantic events in snapshot | 492 |
| Selected schema names in snapshot | 295 |
| Runtime/schema qualification | false |

## Name categories

| Category | Rows |
| --- | --- |
| audit_only | 2 |
| business | 1 |
| current_concrete_type_of_canonical_event | 11 |
| excluded_redundant_or_contradictory_split_name | 4 |
| intended_business | 480 |
| legacy_decoder_only | 2 |
| legacy_protocol_replaced | 6 |
| legacy_version_or_name_alias | 17 |
| migration_cutover_only | 4 |
| migration_import_fact | 1 |
| projection_only_compatibility | 1 |
| pruned_future_writer | 3 |
| retained_technical_emission | 9 |
| superseded_vocabulary_or_decision | 17 |
| technical_process_only | 12 |

## Source dispositions

| Disposition | Rows |
| --- | --- |
| [Placeholder: source disposition not recorded] | 42 |
| aggregate_fact | 404 |
| audit_only | 2 |
| confirmed_gap | 6 |
| legacy_replay | 98 |
| new_target_decision | 1 |
| superseded_by_decision | 8 |
| superseded_vocabulary | 9 |

[Placeholder: reconcile all historical names, aliases, and dispositions against the latest 501-row canonical event catalog.]
