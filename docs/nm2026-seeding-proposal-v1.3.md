# NM2026 seeding proposal v1.3

Status: proposal/specification, not yet fully implemented in `seed-startlag`.

This version builds on the current NM startlag seeding rules:

- NSF ranking is matched by `Deltaker.sa2Id == ranking.personId`.
- Seed-eligible classes are `Å`, `M`, `K`, `Jr-NM`, `Jm`, and `Jk`.
- Classes remain contiguous blocks.
- Multi-shooter seed groups stay together.
- 25m competition targets use the existing NM target ranges.

## Changes in v1.3

### 1. Spread club mates in final start lists

The final start lists should avoid placing shooters from the same club side by side when practical.

Rules:

- Treat equal `ClubShortName` as the same club.
- Avoid adjacent targets for the same club inside one startlag.
- For 25m events, also avoid same-club neighbors on adjacent competition targets when that would visibly place the shooters next to each other.
- Do not break hard constraints to satisfy club spreading:
  - target capacity
  - relay count
  - seeded group placement
  - class block order
  - explicit cross-event conflict rules below

If same-club adjacency cannot be avoided, the planner should emit a warning with event, relay, target numbers, and shooter names.

### 2. Avoid Fin-to-Grov turnaround conflict

Shooters who shoot Grovpistol after Finpistol must not be placed in the last Finpistol startlag and the first Grovpistol startlag.

Hard rule:

- If a shooter is entered in both Finpistol and Grovpistol, that shooter may not have:
  - Finpistol relay = last Finpistol relay
  - Grovpistol relay = first Grovpistol relay

Preferred resolution order:

1. Move the shooter out of the last Finpistol relay when this does not hurt seeded placement or class block structure.
2. Otherwise move the shooter out of the first Grovpistol relay.
3. If neither move is possible, keep the assignment and emit a hard validation error before `--apply`.

### 3. Protect junior finalists from last qualification relay

Junior shooters who are expected to shoot Finpistol or Silhuett finals should not shoot the last qualification relay in that exercise.

Hard rule:

- A junior finalist in Finpistol must not be in the last Finpistol qualification relay.
- A junior finalist in Silhuett must not be in the last Silhuett qualification relay.

Required input:

- A deterministic list of affected junior finalists, preferably by `Deltaker.Id` or `sa2Id`.
- The list must be event-specific because a junior finalist constraint may apply to Finpistol, Silhuett, or both.

If the finalist list is missing, `seed-startlag` can only warn that this rule was not evaluated.

## Validation output

The seeding report should clearly separate:

- hard errors that block `--apply`
- warnings where the planner made the best available deterministic assignment
- informational notes about constraints that were not evaluated because input was missing

Minimum v1.3 warnings/errors:

- same-club adjacent targets that could not be resolved
- Fin/Grov last-to-first turnaround conflicts
- junior finalist in last qualification relay
- missing junior finalist input list

## Implementation notes

The current `SeedStartLagPlanner` works event by event. Rules 2 and 3 require either a post-plan validation pass across planned events or a planner context that includes all selected NM events.

The safest implementation path is:

1. Keep the current per-event planner as the initial deterministic assignment.
2. Add a cross-event validation/planning pass over all `SeedStartLagEventPlan` values.
3. Apply local swaps inside affected class blocks when possible.
4. Re-run validation and fail before `--apply` if hard constraints remain.

This keeps the existing ranking/class behavior reviewable while adding v1.3 constraints as explicit, testable rules.
