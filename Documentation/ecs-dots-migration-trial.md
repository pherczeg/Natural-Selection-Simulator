# ECS/DOTS Migration Trial

Status of the hybrid MonoBehaviour → ECS/DOTS migration of the natural-selection
simulation core, including what this trial pass added, how to toggle each piece, how to
benchmark the modes, and what is intentionally left for later.

Unity 2022.3.62f3 · Entities 1.4.6 · Unity Physics 1.4.6 (installed, not yet used by the
simulation — see *Sensing* below).

## Architecture overview

The simulation is a hybrid: creatures and food remain pooled GameObjects (visuals,
colliders, selection, UI), while simulation logic incrementally moves into ECS systems.
`ECSMirrorBridge` (MonoBehaviour singleton, `LateUpdate`) mirrors active creatures/food
into entities keyed by GameObject instance id, syncs data both ways, and hosts the
bridged execution that still needs managed objects (predation resolution, reproduction
coroutines, and creating the spawn/despawn *request* entities that the Phase 2 ECS systems
drain — see *Phase 2* below).

| Area | Where it runs | Gating flag | Notes |
|---|---|---|---|
| Sensing / observation | `ECSObservationSystem` (Burst, spatial hash) | `useEcsObservation` | Legacy fallback: `Scheduler.cs` jobs + O(n²) mate scan |
| AI context mirroring | bridge sync | `useEcsAIContext` | |
| Movement decision (utility AI scoring) | `ECSUtilityScoringSystem` (Burst) | `useUtilityAI` + `useEcsUtilityScoring` | Mono fallback: `CreatureUtilityBrain.Evaluate` |
| Creature energy/aging/maturity | `ECSCreatureLifecycleSystem` (Burst, `CreatureLifecycleCalculator`) | `useEcsCreatureLifecycle` | Mono fallback: `UpdateSlowSimulation` in the behaviours |
| Food growth/aging | `ECSFoodLifecycleSystem` (Burst, `FoodLifecycleCalculator`) | `useEcsFoodLifecycle` | |
| Action phases (search/approach for food, mate, wander) | `ECSFoodSearchExecutionSystem`, `ECSMateSearchExecutionSystem`, `ECSWanderExecutionSystem` | — (always ECS, see below) | Managed `SystemAPI.Query` loops resolving creatures via the bridge |
| Eating / food locking / herbivore competition | `ECSEatingExecutionSystem` | — (always ECS, see below) | Main-thread snapshot iteration; lock arbitration |
| **Movement execution** | **`ECSMovementExecutionSystem` (Burst `IJobParallelForTransform`)** — *new in this pass* | **`useEcsMovementExecution`** | Off → per-creature `MovementManager.MoveTowards` exactly as before |
| **Statistics aggregation** | **`ECSStatisticsSystem` (Burst `IJob`)** — *new in this pass* | **`useEcsStatistics`** (requires `useEcsCreatureLifecycle`) | Covers a documented subset; rest stays polled |
| Predation execution (success roll, energy transfer) | `ECSMirrorBridge` | — | Bridged, managed |
| Reproduction / genetics | `ReproductionManager` + bridge coroutines | — | Mono + bridged; see *Reproduction* |
| Flee-from-predator | `HerbivoreBehaviour.FixedUpdate` | — | Intentionally untouched (physics probes, per-creature threat memory) |
| Spawning/pooling, UI, camera, ground | MonoBehaviour | — | Visualization/adapter layer |

## Phase 2 — ECS-native spawn/despawn + genes on entities (full-DOTS migration)

Beyond this trial pass, the simulator is executing a 10-phase full-DOTS migration (target: full creature
behaviour in ECS, 10k+ creatures). **Phase 2** moved spawn/despawn *request processing* out of the bridge
into dedicated ECS systems and put the first genome/RNG/cooldown data directly on entities.

- **Request draining → ECS systems (the project's first `EntityCommandBuffer` usage).**
  `ECSCreatureSpawnSystem`, `ECSFoodSpawnSystem`, `ECSDespawnSystem` (`SystemBase`, `SimulationSystemGroup`)
  drain the existing `SpawnCreatureRequest` / `SpawnFoodRequest` / `DespawnCreatureRequest` /
  `DespawnFoodRequest` entities via `SystemAPI.Query<RefRO<…>>().WithEntityAccess()` + an ECB that destroys
  each request entity. They are gated by `RequireForUpdate`/`RequireAnyForUpdate`, so the idle path allocates
  nothing. This removes the bridge's **eight per-frame `CreateEntityQuery(...).ToEntityArray()` allocations**
  and takes the work off the hot `LateUpdate`. The systems only call the managed spawner/pool to make/teardown
  the GameObject — they never create or destroy a creature/food **entity**; the bridge remains the single
  GameObject↔entity linker keyed by instance id (true through Phase 7). Behavioral delta: a one-frame queue
  latency (a request created in `LateUpdate` is drained on the next `Update`), bounded and safe — despawn-queued
  creatures are skipped by sync/AI/population-count alike, and the species cap is re-checked at spawn time.
- **Genes / RNG / cooldown buffers on the entity** (`Assets/Scripts/ECS/Components/Genome.cs`): `Genome`
  (write-once gene snapshot), `RandomState` (`Unity.Mathematics.Random`, deterministically seeded non-zero),
  and `[InternalBufferCapacity(4)]` cooldown buffers `RejectedMateCooldown`, `FoodBlacklistCooldown`
  (herbivore), `PreyBlacklistCooldown` (predator). `ECSMirrorBridge.EnsureCreatureComponents` attaches them;
  the bridge populates `Genome`/`RandomState` once at entity creation via `GenomeFactory.FromCreature` /
  `GenomeFactory.SeedRandomState`. `GenomeFactory.FromSpawnRequest` is the pure mapper, pinned by
  `GenomeFactoryTests` (EditMode). The cooldown buffers have no consumer yet — scaffolding for Phases 4–6.
- **Flat-ground spawn placement.** `CreatureSpawner`/`FoodSpawner` no longer use `Physics.OverlapSphere`
  occupancy rejection or `Physics.RaycastAll` ground probing; placement is random-in-bounds XZ
  (`UnityEngine.Random.Range`, RNG stream unchanged) + flat-ground Y via `GroundSnapUtils.TryGetGroundY`. The
  occupancy-rejection loop is intentionally dropped (cosmetic) — this kills the spawn-time spikes.
- **Deletions.** Bridge (2545→2408 lines): `ProcessECSSpawnDespawnRequests` + both `LateUpdate` call sites,
  `ProcessDespawn{Creature,Food}Requests`, `ProcessSpawn{Creature,Food}Requests`, `ProcessSpawnCreatureRequest`,
  `GetCreaturePrefab`, and the now-unused `ToVector3`. `ReproductionManager`: the managed direct-spawn fallback
  + `InitializeOffspringFromRequest` + `GetOffspringPrefab` (offspring now spawn only via the request→system
  path). The bridge keeps the `TryRequest*` request *creators*.
- **Verification.** Compiles; EditMode suite **107/107** (101 prior + 6 new `GenomeFactoryTests`). The runtime/CSV
  gate (population curves within the Phase-1 baseline band) and the in-editor eyeball are manual editor steps.

## Phase 3 — ECS threat sensing + flee + movement effects (full-DOTS migration)

Phase 3 ran as two committed, separately-verified increments (each compiled + passed the EditMode batch).

### 3a — Threat sensing + flee (commit 2a509df)
- Herbivore threat detection moved into the existing Burst spatial-hash observation job: a predator cell map +
  `FindClosestThreat` write `closestThreatInstanceId`/`closestThreatDistanceSq` on `CreatureObservationResultData`
  (closest predator within sense radius). This **deletes the per-herbivore O(H×P) predator scan** that ran every
  `HerbivoreBehaviour.FixedUpdate` — the headline current-scale FPS win.
- New managed `ECSFleeSystem` (`SimulationSystemGroup`, `[UpdateAfter(ECSMovementExecutionSystem)]`): resolves the
  herbivore + sensed threat via the bridge and calls `HerbivoreBehaviour.UpdateEcsFlee(sensedThreat, config)`. Because it
  runs after the movement batch job and `UpdateEcsFlee` writes the transform immediately via `MovementManager.MoveTowards`,
  flee overrides the active action (legacy "flee overrides" behavior preserved). Captured herbivores are skipped.
- `FleeSteeringCalculator` (pure, tested) extracted from `MovementManager.SteerDirectionInsideBounds`, which now delegates
  to it (parity-exact, single source — same pattern as `CreatureMovementCalculator`).
- Deleted from `HerbivoreBehaviour`: `GetNearestPredatorThreat` (the scan), `IsValidThreat`, `GetThreatScanInterval`,
  `GetObstacleAwareFleeDirection`, `IsBlockedInDirection`, the `cachedThreat`/`nextThreatScanTime` fields + scan-interval
  consts. `FixedUpdate` now suppresses normal transitions while threatened via `if (IsThreatened) return;`.
- **Behavioral note — obstacle-aware flee dropped.** The plan asserted the scene had no obstacles; in fact `First.unity`
  has 4 `Obstacle`-tagged wall cubes (a 10×10 box at origin in the 150×150 ground). Creatures use **trigger** colliders and
  pass through those walls in *all* movement modes, so the obstacle-aware flee redirect was the only place they were ever
  considered. Dropping it makes flee consistent with every other movement; re-adding it would put a per-frame
  `Physics.Raycast` back in the flee path for non-functional decorative walls.

### 3b — Movement effects / sprint (commit 8b0c71d)
- Per-creature sprint/temporary-effect **ticking moved off the managed `FixedUpdate`** into a Burst `ECSMovementEffectsSystem`
  over a new `MovementState` component (sprint timers + profile + base speed + currentMoveSpeed + pending trigger fields).
  The entity is authoritative for the ticked sprint state and `currentMoveSpeed`.
- `SprintEffectsCalculator` made **Burst-compatible** (`Mathf.*` → `Unity.Mathematics.math.*`, float-identical) so the Burst
  job calls the shared calculator. The system ticks by real frame `DeltaTime` (wall-clock, matching the old per-FixedUpdate
  decay) and reads the age multiplier from `CreatureLifecycleData.speedAgeMultiplier`.
- `MovementManager` sprint triggers (`TryStartSprint`/`ApplyTemporarySpeedMultiplier`, call sites unchanged) now **record
  pending requests**; the bridge ferries pending → entity and `currentMoveSpeed` ← entity each slow-sync. Deleted
  `UpdateTemporaryEffects` + `RecalculateCurrentSpeed` + the managed `sprintState`. Removed the per-FixedUpdate
  `UpdateTemporaryEffects` call from both behaviours.
- **Behavioral delta — sprint latency.** A sprint/debuff now takes ~one slow-sync interval to affect the queued speed
  (trigger → push → tick → read). Sprint speed is a step function, so this is expected within tolerance; validate against
  the predation/ecology CSV gate. **Assumes `useEcsCreatureLifecycle` is ON** (the age multiplier comes from
  `CreatureLifecycleData`); true in the committed asset and the full-DOTS target. In an all-mono A/B config the age-speed
  decay would not reach `currentMoveSpeed`.

**Verification:** both increments compile; EditMode suite **115/115** (107 prior + 8 new `FleeSteeringCalculatorTests`; the
`ECSObservationTargetSearchJob` test extended for `closestThreat`; `SprintEffectsCalculator` parity tests pass after the
Burst-compat change). Runtime eyeball (herbivores scatter/sprint/post-escape-flee; predators still hunt) + benchmark/CSV
(main-thread ms drop at 1–2k; predation/ecology within band) are manual editor steps.

## Feature flags (`Assets/Resources/GameConfig.cs`, "AI Migration" header)

| Flag | Code default | Value in `GameConfig.asset` | Added |
|---|---|---|---|
| `useUtilityAI` | false | **on** | pre-existing |
| `useEcsObservation` | false | **on** | pre-existing |
| `useEcsAIContext` | false | **on** | pre-existing |
| `useEcsUtilityScoring` | false | **on** | pre-existing |
| `useEcsFoodLifecycle` | false | **on** | pre-existing |
| `useEcsCreatureLifecycle` | false | **on** | pre-existing |
| `useEcsMovementExecution` | false | off (new field, deserializes to default) | **this pass** |
| `useEcsStatistics` | false | off (new field) | **this pass** |

The project's active configuration (the `.asset`) runs ECS mode for everything
pre-existing; that default was kept. The two new flags default **off** so this pass
changes no behavior until enabled.

**Enabling ECS mode:** select `Assets/Resources/GameConfig.asset` and tick the flags.
All flags are read every frame, so they can be toggled during play mode for A/B testing.

**Returning to the old mode:** untick the flags. With every `useEcs*` flag off, sensing
falls back to `Scheduler.cs`, decisions to the legacy threshold state machine
(`useUtilityAI` off) or mono utility brain, lifecycle to the per-creature
`UpdateSlowSimulation`, and movement to direct `MoveTowards` calls.
`useEcsStatistics` requires `useEcsCreatureLifecycle` (the aggregation reads
`CreatureLifecycleData`, which is only synced in that mode); the statistics system
self-disables and readers fall back to list polling otherwise.

### Mapping to the trial brief's flag names

| Brief name | Project flag |
|---|---|
| UseEcsSensing | `useEcsObservation` |
| UseEcsMovementDecision | `useUtilityAI` + `useEcsUtilityScoring` (+ `useEcsAIContext`) |
| UseEcsMovementExecution | `useEcsMovementExecution` |
| UseEcsEnergyLifecycle | `useEcsCreatureLifecycle` |
| UseEcsFoodInteraction | `useEcsFoodLifecycle` (lifecycle); eating execution is ECS-only, see below |
| UseEcsReproduction | *not added* — see *Reproduction* |
| UseEcsStatistics | `useEcsStatistics` |

### Why there is no `useEcsFoodInteraction` / `useEcsReproduction` flag

A previous migration pass already removed the pure-MonoBehaviour action pipeline:
`CreatureActionExecutor.Execute` (`Assets/Scripts/Creature/CreatureBehaviour/UtilityAI/CreatureActionExecutor.cs`)
unconditionally routes every action through `ECSMirrorBridge.TryRequestCreatureAction`,
and the old state-machine `FixedUpdate` is commented out in `HerbivoreBehaviour.cs`.
`ECSEatingExecutionSystem` is therefore the **only** eating implementation — gating it
behind a default-off flag would leave creatures unable to eat in "legacy" mode. Rather
than ship a dead or dangerous flag, this is documented: food *lifecycle* is toggleable
(`useEcsFoodLifecycle`), food *interaction* is permanently ECS-coordinated.
The same applies to reproduction execution, which runs as bridge coroutines triggered
from `ECSMateSearchExecutionSystem`.

## Movement execution design (this pass)

Before: the three ECS execution systems called `creature.MovementManager.MoveTowards()`
per creature per frame on the main thread (managed call + transform write each).

Now, behind `useEcsMovementExecution`:

1. The five call sites in `ECSFoodSearchExecutionSystem`, `ECSMateSearchExecutionSystem`
   and `ECSWanderExecutionSystem` call `MovementManager.MoveTowardsOrQueue(target, flag)`.
   Flag off → identical direct `MoveTowards`. Flag on → the move intent (target, current
   speed incl. sprint/age multipliers, half height) is queued into `CreatureMovementBatch`
   (a `TransformAccessArray` + `NativeList<CreatureMoveIntent>` registry owned by the
   bridge, keyed by instance id, surviving pooling).
2. `ECSMovementExecutionSystem` (after the three execution systems) runs one
   Burst-compiled `IJobParallelForTransform` (`CreatureMoveStepJob`) that computes the
   step via `CreatureMovementCalculator.ComputeStep` and writes position + rotation on
   worker threads. The job completes inside the system update so positions are final
   before the bridge's `LateUpdate` sync and the next `FixedUpdate`.
3. `CreatureMovementCalculator` is an exact port of the legacy `MoveTowards` math
   (XZ `Vector3.MoveTowards` semantics, bounds clamp with `max(0.25, halfHeight·0.5)`
   inset and min>max center collapse, rotation toward the full remaining delta with the
   `lengthsq > 1e-4` threshold). The legacy `MovementManager.MoveTowards` now delegates
   to the same calculator, so both paths share one source of truth; parity is pinned by
   `CreatureMovementCalculatorTests`.

Ground Y: `GroundSnapUtils.TryGetGroundY` has a flat-ground fast path — for positions
inside `GroundManager.GroundBounds` it returns the cached `GroundSurfaceY` without any
raycast, and the bounds clamp guarantees in-bounds positions. The ECS job therefore uses
`groundSurfaceY + halfHeight` directly. **Limitation:** if genuinely uneven terrain is
ever added, both paths need a real height solution (e.g. a baked height grid); today the
mono path is equally flat-ground in practice.

Accepted behavioral divergence (flag on): cross-creature position reads inside the
execution systems (predator chasing prey, `IsTargetReached(mate.position)`) see
positions from the start of the system group instead of values partially updated by
earlier creatures in the same frame. Per-creature step math is parity-exact; targets
shift by at most one frame of movement. The flee path still calls `MoveTowards`
immediately from `FixedUpdate` (including its deliberate "tug-of-war" with an active
ECS action), so flee behavior is unchanged in both modes.

Pre-existing quirk kept intentionally: the execution systems tick once per rendered
frame but step movement by `Time.fixedDeltaTime`. The ECS job replicates this exactly.

## Statistics (this pass)

`ECSStatisticsSystem` (gated `useEcsStatistics && useEcsCreatureLifecycle`, throttled at
`updateInterval`) aggregates per species from mirrored ECS data in one Burst job:
count, female/male counts, avg/min/max of weight, base speed, energy, current max
energy, age, base sense radius, plus the mirrored food count. Results land in the
static `ECSStatisticsMirror` (with a 2 s staleness guard); `Statistics.BuildSnapshot`,
`Statistics.BuildDiagnosticsSnapshot` and `RealtimeSimulationStatsUI` overlay exactly
those fields and keep polling for everything not represented in ECS components:
current speed/sense (sprint and temporary multipliers), desirability, sprint profile,
dove/hawk strategy, agility/strength, utility weights, reproduction readiness, and the
state distribution. Cumulative counters (spawns, deaths, reproduction, predation)
remain event-driven on the `Statistics` singleton in both modes.

## Reproduction — intentionally not migrated

Current state: mate *search and approach* run in `ECSMateSearchExecutionSystem`; the
*execution* (mutual acceptance rolls, rejected-mate cooldown pairs, reproduction timer
coroutine, gene inheritance + mutation in `ReproductionManager.Reproduct`, offspring
spawning through `SpawnCreatureRequest` → pool) is MonoBehaviour/bridge. Reproduction
cooldown ticking also still happens in the creature `FixedUpdate` even in ECS lifecycle
mode.

Migrating this in the same pass was judged unsafe: it interleaves coroutines, per-pair
managed dictionaries, population-cap checks and statistics events. Forcing it would
have violated the "no broken rewrite" rule.

**Recommended next step:** move the scalar reproduction cooldown (and the rejected-mate
cooldown timers) into `CreatureLifecycleData`, ticked by `CreatureLifecycleCalculator.Step`
and applied back through the existing `ApplyECSLifecycle` seam — mechanical, unit-testable,
and it removes the last per-creature mono ticking in ECS mode. After that: replace the
reproduction coroutine with an ECS timer component processed by a small system, keeping
gene math in a pure calculator (same pattern as lifecycle).

## Sensing notes

Sensing was already centralized (`ECSObservationSystem`, spatial hash, Burst, throttled
by `updateInterval`). This pass added query counting: `SimulationPerfCounters.TotalSensorQueries`
increments per creature per observation pass in both the ECS system and the legacy
`Scheduler` path, and feeds the benchmark. DOTS Physics (`com.unity.physics`) is
installed but **not** used for sensing: the mirrored data are plain positions and the
search is distance-based (matching the legacy jobs), so no collision world is needed.
The gene fields `numberOfRaycasts` / `angleBetweenRaycasts` exist on
`BaseCreatureBehaviour` but are not used by any active sensing path (pre-existing).

## Benchmark

Files: `Assets/Scripts/Benchmark/SimulationBenchmark.cs` (+ `SimulationPerfCounters`).

How to run:
1. Optionally set the flags you want to compare in `GameConfig.asset`.
2. Either tick `benchmarkEnabled` (auto-runs on play) or enter play mode and press **F9**.
3. Scenario counts/durations come from `benchmarkCreatureCounts` (default
   500/1000/2000/5000), `benchmarkWarmupSeconds`, `benchmarkMeasureSeconds`,
   `benchmarkSpawnBatchPerFrame`.

Per scenario the benchmark raises the herbivore population cap to the target (the
original cap is restored when the run ends, including on quit), spawns randomized
herbivores in per-frame batches via `CreatureSpawner.SpawnRandomCreature`, warms up,
then measures: avg FPS and frame time (`Time.unscaledDeltaTime`), main-thread time
(`ProfilerRecorder` "Main Thread"), GC allocated per frame (`ProfilerRecorder`
"GC Allocated In Frame"), sensor query count delta, and spawn/death/reproduction/
predation deltas from `Statistics`. Population dynamics are **not** frozen — live
counts are reported start→end next to the target, so results are honest about drift.

Output: one console line per scenario plus `BenchmarkResults/benchmark_<timestamp>.csv`
and `.md` at the repository root. Compare runs with identical flags; toggle one flag set
per run (e.g. `useEcsMovementExecution` on/off) for A/B numbers.

Notes/limits: scenarios run in one play session without scene reload, so later scenarios
inherit the evolved population state of earlier ones (monotonic spawn-up by design).
At 5000 creatures, spawn placement (`IsPlaceOccupied` overlap checks) may fail on a
crowded map; the benchmark logs and proceeds with the actual population. In the editor,
ScriptableObject changes persist — the cap override is restored in `OnDestroy`/
`OnApplicationQuit`, but a hard editor crash mid-run could leave a raised
`maxHerbivoreCount` in the asset (visible in the inspector, easy to revert).

## Validation performed

- EditMode tests (NUnit) via Unity batch mode, including the new
  `CreatureMovementCalculatorTests` and `ECSStatisticsAggregationJobTests`
  (pure struct/job tests, no World — same pattern as the existing ECS tests).
- Compilation verified by the same batch run.
- Repaired a pre-existing broken test: `ECSObservationTargetSearchJobTests` predated
  both the spatial-hash fields of `ECSObservationTargetSearchJob` (it scheduled the job
  with unassigned containers → `InvalidOperationException`) and the sense-radius limit
  on prey/mate search (its reference helpers ignored the radius). The test now builds
  the hash maps like `ECSObservationSystem` and uses radius-aware references.
  Note: `Assets/Tests/` is currently in `.gitignore` ("Temporarily ignore Unity test
  folders"), so test files are not version-controlled — pre-existing project quirk.
- **No play-mode/runtime validation was performed** — there are no PlayMode tests in
  the project, and runtime behavior of `useEcsMovementExecution` / `useEcsStatistics`
  should be eyeballed in the editor before enabling them by default (creatures move,
  flee still works, stats panel matches between modes).

Command (close the Unity editor first; do not combine `-quit` with `-runTests`):

```
"C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode ^
  -projectPath "<repo>" -runTests -testPlatform EditMode ^
  -testResults "<repo>\TestResults\editmode-results.xml" -logFile "<repo>\TestResults\editmode.log"
```

## Known limitations and risky areas

- **Flat-ground assumption** in ECS movement (shared de facto with the mono path); see
  *Movement execution design*.
- **`ECSEatingExecutionSystem`** iterates managed snapshots on the main thread and is
  not flag-gated (no mono fallback exists). Jobifying its lock arbitration is future
  work and was deliberately not touched — it is correctness-critical for multi-creature
  food conflicts.
- **Bridge sync cost**: GameObject↔entity mirroring in `LateUpdate` is itself O(n) on
  the main thread each `updateInterval`; at very high creature counts it becomes the
  next bottleneck after movement (visible as main-thread time in the benchmark).
- **Execution systems remain managed loops** (they resolve `BaseCreatureBehaviour` via
  the bridge for validity checks, blacklists, locks). Movement *math* moved off the
  main thread; the per-creature decision bookkeeping did not yet.
- **Statistics divergence**: with `useEcsStatistics` on, covered values come from the
  ECS mirror (refreshed each `updateInterval`) while uncovered values are polled live —
  the two can be up to one interval apart. Snapshot counts may differ by despawns
  in-flight within that window.
- **Reproduction cooldown** still ticks in mono `FixedUpdate` even in full ECS mode
  (pre-existing; see *Reproduction* for the recommended fix).
- The movement batch keeps one `TransformAccessArray` slot per ever-seen pooled
  creature (compacted on despawn via the bridge's stale-entity sweep); memory is
  bounded by peak population.

## Files added/changed in this pass

Added: `Assets/Scripts/SimulationPerfCounters.cs`,
`Assets/Scripts/Creature/CreatureBehaviour/Movement/CreatureMovementCalculator.cs`,
`Assets/Scripts/ECS/Systems/CreatureMovementBatch.cs`,
`Assets/Scripts/ECS/Systems/ECSMovementExecutionSystem.cs`,
`Assets/Scripts/ECS/Systems/ECSStatisticsSystem.cs`,
`Assets/Scripts/Benchmark/SimulationBenchmark.cs`,
`Assets/Tests/EditMode/CreatureMovementCalculatorTests.cs`,
`Assets/Tests/EditMode/ECSStatisticsAggregationJobTests.cs`, this document.

Changed: `GameConfig.cs` (flags + benchmark fields), `MovementManager.cs` (delegates to
calculator; queue entry points), `ECSMirrorBridge.cs` (owns `CreatureMovementBatch`,
unregisters on despawn), the three execution systems (flag-gated `MoveTowardsOrQueue`),
`ECSObservationSystem.cs` + `Scheduler.cs` (query counting), `Statistics.cs` +
`RealtimeSimulationStatsUI.cs` (ECS aggregate overlay), `CreatureSpawner.cs`
(`SpawnRandomCreature` extraction). No scenes, prefabs, assets or packages were modified.
