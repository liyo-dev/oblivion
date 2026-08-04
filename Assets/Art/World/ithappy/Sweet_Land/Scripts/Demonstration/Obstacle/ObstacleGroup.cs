using UnityEngine;
using UnityEngine.AI;

namespace ithappy
{
    public class ObstacleGroup : MonoBehaviour
    {
        [SerializeField] private ObstaclePath[] _obstaclePaths;

        private int _currentFreeJumpPointCount = 0;

        private void Start()
        {
            foreach (ObstaclePath jumpPoint in _obstaclePaths)
            {
                jumpPoint.OnJumpPointStateChange += JumpPointOnOnJumpPointStateChange;
            }

            _currentFreeJumpPointCount = _obstaclePaths.Length;
        }

        // NOTA: OffMeshLink está obsoleto en Unity 6 (usar NavMeshLink), pero LadderPoint.prefab
        // (usado en CandyLand.unity) tiene un componente OffMeshLink ya serializado. Migrar el tipo
        // aquí sin también migrar el componente en el prefab dejaría el jump point roto en el juego real.
        // Se mantiene OffMeshLink y se suprime el warning hasta que se haga la migración de datos en el Editor.
#pragma warning disable 618
        private void JumpPointOnOnJumpPointStateChange(OffMeshLink offMeshLink, bool status)
        {
            if (status)
            {
                _currentFreeJumpPointCount--;

                if (_currentFreeJumpPointCount != 0)
                {
                    offMeshLink.activated = false;
                }
                else
                {
                    offMeshLink.costOverride = 1000;
                }
            }
            else
            {
                _currentFreeJumpPointCount++;
                offMeshLink.activated = true;
                offMeshLink.costOverride = -1;
            }
        }
#pragma warning restore 618
    }
}
