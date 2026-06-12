# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A natural-selection simulator built in **Unity 2022.3.62f3** (C#). Herbivores, predators, and food objects live on a flat ground plane; creatures sense, move, eat, flee, reproduce (with gene inheritance + mutation), age, and die, while the simulation records population statistics. It is a programming/simulation experiment, not a biologically exact model. The README is in Hungarian; this file and `Documentation/` are in English.

The defining architectural fact: the project is a **hybrid MonoBehaviour ↔ ECS/DOTS** system, mid-migration. Read `Documentation/ecs-dots-migration-trial.md` before touching simulation logic — it is the authoritative map of what runs where, which flags gate what, and what was deliberately left un-migrated. Key points summarized below.

## Commands

This is a Unity project with no CLI build script — develop by opening the project in the Unity Editor (version `2022.3.62f3`, see `ProjectSettings/ProjectVersion.txt`).

**Run EditMode tests** (NUnit) in batch mode — close the Unity editor first, and do **not** combine `-quit` with `-runTests`:

```
"C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode `
  -projectPath "C:\Users\pherc\source\repos\Natural-Selection-Simulator" -runTests -testPlatform EditMode `
  -testResults ".\TestResults\editmode-results.xml" -logFile ".\TestResults\editmode.log"
```

Run a single test by adding `-testFilter "Namespace.Class.Method"` (or a wildcard). Tests also run interactively via the Editor's **Test Runner** window.

Tests are **EditMode only** — there are no PlayMode tests. ECS tests exercise pure structs/jobs and calculators directly (no `World`). Runtime behavior must be eyeballed in the editor.

⚠️ `Assets/Tests/` is currently in `.gitignore` ("Temporarily ignore Unity test folders"), so test files are **not version-controlled** — a pre-existing project quirk. Don't assume tests are committed.

## Architecture

### Hybrid GameObject + ECS model
Creatures and food remain **pooled GameObjects** (visuals, colliders, selection, UI). Simulation *logic* is incrementally moving into **ECS systems**. `ECSMirrorBridge` (`Assets/Scripts/ECS/Systems/ECSMirrorBridge.cs`) is the heart of the hybrid: a MonoBehaviour singleton that, in `LateUpdate`, mirrors active creatures/food into entities keyed by GameObject instance id, syncs data both ways, and hosts the bridged execution that still needs managed objects (predation resolution, reproduction coroutines, spawn/despawn through pools). It also owns `CreatureMovementBatch` (the `TransformAccessArray` registry for jobified movement).

### Feature flags drive everything
All ECS-vs-Mono behavior is gated by flags on **`GameConfig`** (`Assets/Resources/GameConfig.cs`, a `ScriptableObject` loaded via `GameConfig.Instance` from `Resources/GameConfig.asset`). Flags are read **every frame**, so they can be toggled live in play mode for A/B testing. Code defaults are `false`; the committed `.asset` turns most ECS flags **on**. Flag → system map (see the migration doc for the full table):

| Flag | ECS path | Mono fallback |
|---|---|---|
| `useEcsObservation` | `ECSObservationSystem` (Burst, spatial hash) | `Scheduler.cs` jobs + O(n²) mate scan |
| `useUtilityAI` + `useEcsUtilityScoring` | `ECSUtilityScoringSystem` (Burst) | `CreatureUtilityBrain.Evaluate` |
| `useEcsCreatureLifecycle` | `ECSCreatureLifecycleSystem` + `CreatureLifecycleCalculator` | per-creature `UpdateSlowSimulation` |
| `useEcsFoodLifecycle` | `ECSFoodLifecycleSystem` + `FoodLifecycleCalculator` | — |
| `useEcsMovementExecution` | `ECSMovementExecutionSystem` (Burst `IJobParallelForTransform`) | direct `MovementManager.MoveTowards` |
| `useEcsStatistics` (requires `useEcsCreatureLifecycle`) | `ECSStatisticsSystem` (Burst `IJob`) → `ECSStatisticsMirror` | list polling |

**Permanently ECS-coordinated (no flag, no mono fallback):** the action execution pipeline (`CreatureActionExecutor.Execute` always routes through `ECSMirrorBridge.TryRequestCreatureAction`), the search/approach systems (`ECSFoodSearchExecutionSystem`, `ECSMateSearchExecutionSystem`, `ECSWanderExecutionSystem`), and eating (`ECSEatingExecutionSystem`). The old state-machine `FixedUpdate` is commented out in `HerbivoreBehaviour.cs`. Do not add default-off flags around these — creatures would be unable to act.

**Still MonoBehaviour/bridge (intentionally not migrated):** reproduction execution (acceptance rolls, cooldowns, gene inheritance in `ReproductionManager.Reproduct`, offspring spawning), reproduction-cooldown ticking, and flee-from-predator (`HerbivoreBehaviour.FixedUpdate` — physics probes + threat memory).

### Pure calculators = single source of truth
Where logic was migrated, the math lives in a **pure, Burst-friendly static calculator** that both the ECS job and the mono path call, so behavior is identical and unit-testable without a `World`:
- `CreatureMovementCalculator.ComputeStep` (movement) — `MovementManager.MoveTowards` delegates to it; parity pinned by `CreatureMovementCalculatorTests`.
- `CreatureLifecycleCalculator.Step` (energy/aging/maturity).
- `FoodLifecycleCalculator` (food growth/aging).
- Utility scoring rules in `UtilityAIScoreRules`.

When adding or changing migrated logic, keep both paths driven by one calculator and add a parity test — don't fork the math.

### ECS system ordering
Systems use `[UpdateBefore]`/`[UpdateAfter]` (default system group). Rough order: lifecycle systems → `ECSObservationSystem` → `ECSUtilityScoringSystem` → search/wander execution → `ECSMovementExecutionSystem` (after all three execution systems) → `ECSEatingExecutionSystem`. The movement job completes inside its system update so positions are final before the bridge's `LateUpdate` sync.

### Creature composition
`BaseCreatureBehaviour` (subclasses `HerbivoreBehaviour`, `PredatorBehaviour`) composes per-concern managers rather than one monolith: `EnergyManager`, `MovementManager`, `AgeManager`, `ObservationManager`, `ReproductionManager`, `EatingManager`, and `CreatureUtilityBrain`. Genes (weight, speed, sense radius, sprint profile, agility/strength, desirability, dove/hawk strategy, utility behavior weights) are fields on the behaviour, set at spawn and inherited+mutated at reproduction.

### Utility AI
Decision-making scores candidate actions (search food, search mate, wander, hunt, keep current state) and picks the highest, instead of fixed `if-else`. Lives under `Assets/Scripts/Creature/CreatureBehaviour/UtilityAI/`. A simplified Hawks-and-Doves behavioral strategy adds per-creature variation beyond raw physical stats.

### Statistics & lifecycle
`Statistics` (singleton) collects time-series + cumulative counters (spawns, deaths by cause, reproduction, predation). `RealtimeSimulationStatsUI` shows a live overlay; `SelectedCreatureStatsUI` shows the clicked creature. `GameManager` runs a **batch of N timed simulations**, exporting one CSV per run to `Application.persistentDataPath` and reloading the scene between runs. With `useEcsStatistics` on, core species aggregates come from `ECSStatisticsMirror` (refreshed each `updateInterval`) while uncovered fields stay polled — the two can be up to one interval apart.

### Assemblies
Three asmdefs: `NaturalSelectionSimulator` (main, `Assets/Scripts`), `NaturalSelectionSimulator.Resources` (`Assets/Resources`, holds `GameConfig`), and `NaturalSelectionSimulator.EditModeTests` (Editor-only). `allowUnsafeCode` is **off** in the main assembly.

## Benchmarking

`Assets/Scripts/Benchmark/SimulationBenchmark.cs` (+ `SimulationPerfCounters`). Tick `benchmarkEnabled` for auto-run on play, or press **F9** in play mode. It raises the herbivore cap per scenario (counts from `benchmarkCreatureCounts`, default 500/1000/2000/5000), warms up, then measures FPS, frame/main-thread time, GC alloc/frame, sensor query count, and population deltas. Output: console line per scenario + `BenchmarkResults/benchmark_<timestamp>.{csv,md}` at the repo root. To A/B a flag, run with identical config and toggle one flag set between runs. Note: scenarios run in one session without scene reload, so population state carries over (monotonic spawn-up by design); the cap override is restored in `OnDestroy`/`OnApplicationQuit`.

## Gotchas

- **Flat-ground assumption:** ECS movement uses `groundSurfaceY + halfHeight` directly (fast path in `GroundSnapUtils.TryGetGroundY` for in-bounds positions). Uneven terrain would need a real height solution on both paths.
- **Bridge sync is O(n) on the main thread** each `updateInterval` — the next bottleneck after movement at high creature counts.
- Execution systems are still **managed loops** (they resolve `BaseCreatureBehaviour` via the bridge for validity/blacklist/lock checks); only the movement *math* is off the main thread.
- `GameConfig.asset` is a ScriptableObject — editor changes **persist** across play sessions; watch for a stray raised `maxHerbivoreCount` left by an interrupted benchmark.
- The gene fields `numberOfRaycasts` / `angleBetweenRaycasts` exist on `BaseCreatureBehaviour` but are **unused** by any active sensing path. Unity Physics is installed but not used for sensing (distance-based search on mirrored positions).
