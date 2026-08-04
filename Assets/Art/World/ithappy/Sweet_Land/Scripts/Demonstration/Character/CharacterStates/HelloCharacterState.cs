using System.Collections;
using UnityEngine;

namespace ithappy
{
    public class HelloCharacterState : CharacterStateBase
    {
        private MovementBase _movement;
        private float _noticeRad = 5f;
        private float _reloadTime = 30f;
        private bool _isReloaded = true;
        private EditorLikeCameraControllerBase _player;
        
        public HelloCharacterState(CharacterBase context, MovementBase movement) : base(context)
        {
            _movement = movement;
            _player = Object.FindAnyObjectByType<EditorLikeCameraControllerBase>();
        }

        public override void Enter()
        {
            base.Enter();

            if (Vector3.Distance(_player.transform.position, _movement.MoveParent.position) < _noticeRad)
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

            return Vector3.Distance(_player.transform.position, _movement.MoveParent.position) < _noticeRad;
        }

        private IEnumerator HelloScenario()
        {
            _isReloaded = false;
            bool result;
            bool inProcess = true;
            
            _movement.RotateToTarget(_player.transform.position, (isComplete) =>
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
