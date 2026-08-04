using System;
using UnityEngine;
using UnityEngine.AI;

namespace ithappy
{
    public class ObstaclePath : MonoBehaviour
    {
        // NOTA: OffMeshLink está obsoleto en Unity 6 (usar NavMeshLink), pero LadderPoint.prefab
        // (usado en CandyLand.unity) tiene un componente OffMeshLink ya serializado. Migrar el tipo
        // aquí sin también migrar el componente en el prefab dejaría el jump point roto en el juego real.
        // Se mantiene OffMeshLink y se suprime el warning hasta que se haga la migración de datos en el Editor.
#pragma warning disable 618
        public event Action<OffMeshLink, bool> OnJumpPointStateChange;

        [SerializeField] private ObstacleBase[] _frontObstacles;
        [SerializeField] private ObstacleBase[] _backObstacles;

        private OffMeshLink _offMeshLink;

        private void Awake()
        {
            _offMeshLink = GetComponent<OffMeshLink>();
        }
#pragma warning restore 618

        public ObstacleBase[] GetNearestPath(Vector3 characterPos)
        {
            if (_backObstacles.Length == 0)
            {
                return _frontObstacles;
            }

            if (_frontObstacles.Length == 0)
            {
                return _frontObstacles;
            }

            if (Vector3.Distance(characterPos, _frontObstacles[0].StartPoint.position) <
                Vector3.Distance(characterPos, _backObstacles[0].StartPoint.position))
            {
                return _frontObstacles;
            }

            return _backObstacles;
        }

        public void SetIsUsedPath(bool isUsed)
        {
            OnJumpPointStateChange?.Invoke(_offMeshLink, isUsed);
        }
    }
}
