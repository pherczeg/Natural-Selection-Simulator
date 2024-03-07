using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Overlays;

namespace Assets.Scripts.Creature.CreatureBehaviour.States
{
    internal class MovingToMateState : ICreatureState
    {
        public CreatureStateType StateType => CreatureStateType.MovingToMate;

        public void EnterState()
        {
        }

        public void ExitState()
        {
        }

        public void UpdateState()
        {
        }
    }
}
