using System.Collections;
using Game.NPC.Common;
using UnityEngine;

namespace ithappy
{
    public class HelloCharacterState : CharacterStateBase
    {
        private MovementBase _movement;
        private float _noticeRad = 5f;
        private float _reloadTime = 30f;
        private bool _isReloaded = true;
        private Transform _player;

        public HelloCharacterState(CharacterBase context, MovementBase movement) : base(context)
        {
            _movement = movement;
            // El pack de demo original buscaba EditorLikeCameraControllerBase (la camara de
            // SU escena de demo, que no existe en el juego real). Se sustituye por el
            // PlayerLocator del proyecto (Assets/Scripts/Behaviour NPC/Common/PlayerLocator.cs,
            // ya usado por otros sistemas de NPC) para que estos personajes reaccionen al
            // jugador real (Will/Invector) en vez de no encontrar nunca a nadie.
            // No se resuelve aqui directamente: si este estado se crea antes de que el jugador
            // exista en la escena (orden de inicializacion), EnsurePlayer() lo reintenta mas
            // adelante en vez de quedarse con null para siempre.
            EnsurePlayer();
        }

        private void EnsurePlayer()
        {
            if (_player == null)
            {
                _player = PlayerLocator.ResolvePlayer();
            }
        }

        public override void Enter()
        {
            base.Enter();
            EnsurePlayer();

            // Guard defensivo: PlayerLocator.ResolvePlayer() ya deberia encontrar siempre al
            // jugador real, pero si algun dia se usa este estado antes de que exista (p.ej. un
            // Awake muy temprano) o en una escena de pruebas sin jugador, esto evita el
            // NullReferenceException en bucle que tenia antes (buscaba EditorLikeCameraControllerBase,
            // la camara de la demo de ithappy, que nunca existe en el juego real).
            if (_player == null)
            {
                CharacterBase.NextState();
                return;
            }

            if (Vector3.Distance(_player.position, _movement.MoveParent.position) < _noticeRad)
            {
                CharacterBase.StartCoroutine(HelloScenario());
            }
            else
            {
                CharacterBase.NextState();
            }
        }

        public override void Update()
        {
        }

        public override void Exit()
        {
        }

        public override bool ShouldEnter()
        {
            return NoticePlayer();
        }
        
        private bool NoticePlayer()
        {
            if (!_isReloaded)
            {
                return false;
            }

            EnsurePlayer();

            // Ver comentario en Enter(): guard defensivo por si _player no se resolvio.
            if (_player == null)
            {
                return false;
            }

            return Vector3.Distance(_player.position, _movement.MoveParent.position) < _noticeRad;
        }

        private IEnumerator HelloScenario()
        {
            _isReloaded = false;
            CharacterBase.StartCoroutine(Reload()); // faltaba en el script original: sin esto,
                                                     // _isReloaded se quedaba en false para siempre
                                                     // y el personaje no volvia a saludar nunca mas.
            bool result;
            bool inProcess = true;
            
            _movement.RotateToTarget(_player.position, (isComplete) =>
            {
                result = isComplete;
                inProcess = false;
            });
                
            yield return new WaitUntil(() => !inProcess);

            float helloTime = CharacterBase.CharacterAnimator.Hello().length;
            
            yield return new WaitForSeconds(helloTime);
            
            CharacterBase.NextState();
        }

        private IEnumerator Reload()
        {
            yield return new WaitForSeconds(_reloadTime);
            _isReloaded = true;
        }
    }
}
