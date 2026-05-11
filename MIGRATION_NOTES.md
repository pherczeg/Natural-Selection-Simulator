# Migration Notes - UtilityAI + ECS hibrid

Ez a dokumentum a jelenlegi migralt rendszer uzemeltetesi es ellenorzesi forrasa.

## Aktiv architektura

A projekt jelenlegi ajanlott futasi modja a UtilityAI + ECS hibrid:
- UtilityAI adja a magas szintu dontest.
- ECS observation/context/scoring/action execution adja az adatot es a vegrehajtast.
- A Mono komponensek tovabbra is a GameObject, pooling, vizualis sync es bridge reteg tulajdonosai.
- A regi StateMachine osztaly es a konkret state osztalyok ki vannak vezetve. A `CreatureStateType` kompatibilitasi, diagnosztikai es lockout vetulet, nem onallo StateMachine.
- Az ECS action execution kotelezo runtime backend. Nincs hozza Inspector kapcsolo, mert kikapcsolva nem maradna mukodo execution layer.

A `useUtilityAI=false` ma mar nem a torolt StateMachine visszakapcsolasa. Ez a legacy priority decision rules mod: a regi sorrendben ellenorzi az ehesseg/reprodukcio/wander dontest, de az action vegrehajtasa tovabbra is az ECS action execution backend feladata.

## Stabil hibrid beallitas

A jelenlegi First scena es `Assets/Resources/GameConfig.asset` stabil hibrid modra van allitva:
- useUtilityAI = true
- useEcsObservation = true
- useEcsAIContext = true
- useEcsUtilityScoring = true
- useEcsFoodLifecycle = true
- useEcsCreatureLifecycle = true
- logUtilityAIScores = false

## Flag-ek

- useUtilityAI: UtilityAI alapu dontesi ciklus be/ki. False eseten legacy priority decision rules futnak.
- useEcsObservation: megfigyelesek forrasa ECS. False eseten a regi Scheduler/Job fallback tolti az ObservationManager listakat.
- useEcsAIContext: UtilityAI context ECS mirrorbol jon. False eseten Mono manager adatokbol epul.
- useEcsUtilityScoring: Utility pontozas ECS oldalon tortenik. False eseten Mono `CreatureUtilityBrain` pontoz.
- useEcsFoodLifecycle: food age/growth/nutrition/despawn ECS lifecycle szerint megy.
- useEcsCreatureLifecycle: creature age/energy/maturity/starvation/old-age ECS lifecycle szerint megy.
- logUtilityAIScores: Utility pontozas debug log. False mellett nincs per-decision log spam.
- utilityDecisionInterval: UtilityAI dontesi periodus masodpercben.

Fontos: a torolt StateMachine miatt az action execution nem optional feature flag. A jatek mukodesenek resze, ugyanugy mint a GameObject pooling vagy a prefab bridge.

## A/B teszteles

Javasolt ugyanazzal a scenaval (`Assets/Scenes/First.unity`), ugyanazzal a kezdo konfiguracioval, legalabb 3-5 futassal modonkent.

1. A - stabil hibrid
- Minden ECS/Utility flag true, `logUtilityAIScores=false`.

2. B1 - Mono scoring kontroll
- useUtilityAI = true
- useEcsObservation = true
- useEcsAIContext = true
- useEcsUtilityScoring = false
- useEcsFoodLifecycle = true
- useEcsCreatureLifecycle = true

3. B2 - observation/context kontroll
- useUtilityAI = true
- useEcsObservation = false
- useEcsAIContext = false
- useEcsUtilityScoring = false
- useEcsFoodLifecycle = true
- useEcsCreatureLifecycle = true

4. B3 - legacy priority decision kontroll
- useUtilityAI = false
- A tobbi ECS observation/lifecycle flag tetszes szerint parosithato az aktualis vizsgalathoz.

Meresi pontok:
- `SimulationDiagnostics` log: populacio, food count, energia, reproduction-ready count, state megoszlas.
- CSV export: populacio, halalozas okok, predacio, reprodukcio.
- Hitch/exception figyeles futas kozben.
- Azonos `restartTime`, `numberOfSimulations`, kezdo populacio es food beallitas hasznalata.

## State mapping

| Regi state | Jelenlegi megfelelo | Execution tulajdonos | Megjegyzes |
| --- | --- | --- | --- |
| Idle | `CreatureAction.None` + `CreatureActionPhase.Idle` | ECS action state data | Debug/compat allapot. |
| Wandering | `CreatureAction.Wander` + `Wandering` | `ECSWanderExecutionSystem` | Kotelezo execution backend. |
| SearchingForFood | `CreatureAction.SearchFood` vagy `Hunt` + `Searching` | `ECSFoodSearchExecutionSystem` | Herbivore food, predator prey keresesi fazis. |
| MovingToFood | `SearchFood` vagy `Hunt` + `MovingToTarget` | `ECSFoodSearchExecutionSystem` | Celvesztes es mozgas target action alatt. |
| Eating | `SearchFood` + `Executing` | `ECSEatingExecutionSystem` + Mono `EatingManager` bridge | Food vizualis/lifecycle sync Mono oldalon marad. |
| SearchingForMate | `CreatureAction.SearchMate` + `Searching` | `ECSMateSearchExecutionSystem` | Mate observation ECS vagy Scheduler forrasbol. |
| MovingToMate | `SearchMate` + `MovingToTarget` | `ECSMateSearchExecutionSystem` | Celvesztes/mozgas target action alatt. |
| Reproducting | `SearchMate` + `Executing` | `ECSMirrorBridge` coroutine bridge + `ReproductionManager` | Pool/prefab spawn Mono bridge oldalon. |
| Predation | `Hunt` + `Executing` | `ECSMirrorBridge` predation bridge | Capture/escape/energy gain Mono komponensekkel. |

## Ellenorizheto blokkok

- Baseline diagnosztika: `SimulationDiagnostics` + `Statistics.BuildDiagnosticsSnapshot`.
- UtilityAI scoring: `UtilityAIScoreRules`, `UtilityAIDefaultScorer`, EditMode tesztek.
- ECS observation: `ECSObservationSystem` es `ECSObservationTargetSearchJobTests`.
- ECS lifecycle: `ECSFoodLifecycleSystem`, `ECSCreatureLifecycleSystem`, lifecycle EditMode tesztek.
- ECS action execution: `ECSWanderExecutionSystem`, `ECSFoodSearchExecutionSystem`, `ECSMateSearchExecutionSystem`, `ECSEatingExecutionSystem`, `ECSMirrorBridge`.

## DisabledScriptsTemp ECS prototype-ok

A workspace-ben van `DisabledScriptsTemp/unused-ecs-prototypes-2026-05-09`. Ez archival/parkolt prototype terulet, nincs aktiv asmdef-ben es nem resze a runtime buildnek.

Javasolt kezeles:
1. Ha van aktiv, flagelt replacement es legalabb egy validalo teszt, torles vagy kulon archival branch.
2. Ha nincs replacement, maradjon kulon experimental asmdef alatt, es ne legyen scene-ben referalva.
3. 2 sprintnel regebbi, nem hasznalt proto menjen kulon archival branchbe.
4. Minden protohoz legyen tulajdonos + hatarido, kulonben torolheto.
