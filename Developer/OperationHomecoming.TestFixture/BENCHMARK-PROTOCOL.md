# Operation Homecoming later benchmark protocol

This is developer-fixture documentation and is not included in the shipping
package. It records the bounded paired experiment for the recurring tracking
path discovered in `CompKidnapContactTracker.CompTick` and
`WorldComponent_OperationHomecoming.WorldComponentTick`.

## Workload and arms

Use two fresh harness processes with the same 1.5 game manifest, seed, save,
mod order, settings, map state, and instrumentation. The candidate arm uses
the final Operation Homecoming package. The baseline arm uses the approved
parent-controlled baseline package for the same fixture; do not synthesize a
different dependency or alter shared tooling hashes.

The fixture should create the same fixed set of 100 kidnapped, spawned pawns
and run the same 60,000-tick workload. Set the contact delay beyond the
measurement window so no contact, quest, site, raid, or random outcome can
diverge during measurement. Use 10,000 warm-up ticks, then collect 30
measurement blocks of 2,000 ticks. Randomize arm order in blocked pairs
(`B,C` / `C,B`) over at least 10 pairs, with a fresh process and isolated
runtime evidence for every trial.

Record TPS, milliseconds per tick, process CPU, private memory, managed
allocations/GC collections, and the raw harness timing/evidence files. Reject
the wave if the save, pawn count, settings, seed, mod order, or workload
fingerprint differs, if warm-up/isolation gates fail, or if observable state
diverges between arms. Report median and paired deltas; retain every raw
trial, not only aggregates.

## Current disposition

The source has recurring per-tick checks, so performance is not inherently
NOT APPLICABLE. This protocol is staged for the later clean benchmark wave.
The current release lane is `UNAVAILABLE` for a controlled benchmark because
the authoritative tooling metadata reports that no separate benchmark fixture
is registered; do not substitute an uncontrolled live run.
