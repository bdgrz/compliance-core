# Delivery and snapshot boundary review

2026-11-05. Scope: populations, selections, packages, baseline, Type II plan,
period close and client export. Design review only; these documents are reopened,
not implementation authorization or evidence that the required infrastructure
exists. The delivery file formats in M0-D17 remain unchanged.

## Boundary and correctness

### Local decisions refinement

The earlier proposals' all-row and all-outcome write guards are not acceptable
under the no-all-children rule. A collection of individually owned rows plus an
aggregate that must settle them all is still an all-child decision. Nor does a
chain of single-operation coordinators eliminate its logical ledger.

- A candidate population version uses an authoritative source-order marker to
  define which individually accepted row edits belong to the immutable version.
  A late edit cannot be acknowledged as included if its ordered outcome is after
  the marker. Streaming generates the required population CSV and hash after
  version definition. Source routing, ordering, retention, duplicate requests,
  marker behavior and truthful outcomes are not yet demonstrated. The marker
  cannot contain its own commit-assigned source position or future count/hash;
  an independently attributable materialization can cite the position after
  a qualified read. Even correct source order cannot prove a person's consent:
  an edit proposed before the marker can settle after the person reviewed the
  displayed rows, including an edit made by the same actor. Exactly which view
  the completeness attestation covers and how delayed edits affect it are
  unresolved product and authority questions.
- Each selected sample row checks its own exact row identity and population
  version. A selection version must be immutable; an ordered marker and
  streamed CSV are candidates, not proof that selection admission is correct.
- A technical builder may process a qualified immutable selection incrementally
  and return a single result bound to the archive and selection. A package or
  export can record that result without owning each child. This only moves I/O
  and verification: it is not evidence that an unqualified source selection
  is complete, that participant decisions were authoritative, or that a signed
  worker attestation was honest. Coverage and result authority remain open.
- Reporting independently delivered attachments and independently completed
  responses would differ from the current whole-response and request-close
  guarantees. Likewise a signer attesting to a specific statement plus separate
  item statuses is not equivalent to an all-material-item close rule. These are
  alternatives for product decision, not silently adopted behavior.
- Preserve the actual M0-D17 output: the recipient gets a single ZIP with its
  `manifest.json` and named files. Streaming or internal segmentation must not
  alter that format. A download must be gated against the authoritative state of
  every artifact whose bytes it sends, including compressed segments and the
  manifest; an earlier package validation or shareability read cannot by itself
  prevent a subsequent quarantine/disposal race. A partial transfer is not a
  successful delivery, and revocation cannot recall already delivered bytes.

These candidates do not demonstrate a correct current implementation. In
particular, the cross-context read source, the byte-transfer fence, and the
evidence retention and crash-recovery contracts remain unqualified. Whole-set
business policies remain accepted until explicitly changed.

- Rows, selected rows, package entries, plan items, baseline source outcomes,
  close and late-item outcomes, export outcomes, and deliveries are independently
  keyed facts. Parent state does not contain a roster, manifest, inventory or
  all acknowledgements. A bounded parent fact references a specific settled cut.
- A cut is useful only if it is complete, retained, gap-detecting and sourced
  from authoritative participants. A bounded series of admission windows covers
  its admitted writers only. It does not discover unrelated source producers.
  A chain of single-operation coordinators still grows and may be a disguised
  ledger. Small current state does not bound replay: Portia aggregate hydration
  reads its full stream. Bounded historical reads and a defensible coordination
  boundary remain open. The current repository has not demonstrated a complete, consistently ordered
  tenant-wide cross-context enumeration and historical retention. Baseline,
  close and export must remain pending when that proof or any outcome is missing.
- An as-of business time is distinct from commit position. Backdated writes need
  explicit durable catch-up and correction or amendment. A late-item reaction
  must be independently idempotent and fenced against amendment completion.
- Population successors need an exclusive predecessor claim and fenced release.
  A read of a projection cannot enforce the one-unfrozen-successor invariant.
- Package artifact permission must be granted by the artifact owner and protect
  the actual transfer against quarantine/disposal races. The required reservation,
  transfer outcome, revocation ordering, retries and recovery do not yet exist in
  the Evidence proposal. Delivery is blocked until specified and verified.
- Population and sample CSVs and the ZIP with `manifest.json` are generated as
  streamed deliverables. Bounded internal storage segments preserve exact bytes;
  they are not a change to the accepted recipient-facing file format. Counts and
  digests verify what was traversed, never the absence of undiscovered sources.
  Individual source content and segments must themselves obey bounded storage
  contracts; no particular arbitrary product size is asserted here.

## Live Jev review

`delivery-snapshot-boundaries-jev-request.json` and
`delivery-snapshot-boundaries-jev-response.json` preserve the exact request and
response. Model: `jev-1.14.0`, 2026-11-05. Noul probabilities for the concrete
scenarios were: stale projection proves completeness 0.04; a bypassable admission
chain proves all sources admitted 0.02; hash proves no source omissions 0.03;
streamed traversal can compute a digest without whole-dataset memory 0.94;
quarantine can race delivery after a shareability read 0.74; independent item
outcomes required before completion 0.95; an independent late-item reaction can
leave amendment required 0.90; a delivered ZIP needs to own entries as an
aggregate 0.37. The last answer is indecisive and does not settle ownership.

These numbers are review signals, not a proof of distributed consistency,
completeness, throughput, or failure recovery. The source-cut and transfer
protocols are unresolved, and the required guarantees need implementation-level
qualification and adversarial tests before this design can be called complete.

### Additional local-decision review

The synthetic cases and exact Jev 1.14.0 request and response are retained in
[request](delivery-snapshot-local-decisions-jev-request.json) and
[response](delivery-snapshot-local-decisions-jev-response.json). Model answers
were: builder's signature proves an omitted source included 0.05; post-marker
proposal belongs to the frozen version 0.03; acknowledging success before its
late append is truthful 0.06; receiving a prior chunk can be undone 0.05;
text delivery proves a pending attachment delivered 0.03; lagging projection
proves no draft response 0.04; aggregate needs a list of entries to record the
specified build result 0.09. The builder scenario explicitly assumes a proven
complete immutable selection (0.89), so that answer does not establish that the
selection or trustworthy result exists. The unfenced post-quarantine transfer
case received 0.55, an inconclusive signal; the stated trace still permits it.
These are scenario judgments, not proof of source or provider behavior.
