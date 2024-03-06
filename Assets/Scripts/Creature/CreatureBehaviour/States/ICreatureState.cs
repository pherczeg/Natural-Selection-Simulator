using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICreatureState
{
    CreatureStateType StateType { get; }
    // Az állapotba való belépéskor végrehajtandó logika
    void EnterState();

    // Az állapot frissítésekor végrehajtandó logika
    void UpdateState();

    // Az állapotból való kilépéskor végrehajtandó logika
    void ExitState();
}
