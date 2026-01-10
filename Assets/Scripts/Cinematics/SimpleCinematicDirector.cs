using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using Unity.Cinemachine;
using DG.Tweening;
using EasyTransition;

namespace Game.Cinematics
{
    public enum CinematicActionType
    {
        Wait,
        CameraShot,
        MoveCharacter,
        PlayAnimation,
        PlaySound,
        InstantiateObject,
        Transition,
        Dialogue,
        CustomEvent
    }

    [System.Serializable]
    public class CinematicStep
    {
        public string name = "Step";
        public CinematicActionType actionType;
        public float duration = 0f; // Para Wait o duración forzada

        [Header("Camera")]
        public CinemachineCamera vcam; // Arrastra la cámara virtual aquí
        public float blendTime = 1f;

        [Header("Character")]
        public Transform character;
        public Transform targetPosition; // O usa Vector3 si prefieres
        public float moveSpeed = 3f;
        
        [Header("Animation")]
        public Animator animator;
        public AnimationClip clip;
        public string triggerName;
        public bool useTrigger = true; // true=Trigger, false=Play(clip.name)

        [Header("Audio")]
        public AudioClip audioClip;
        public float volume = 1f;

        [Header("Instantiate")]
        public GameObject prefab;
        public Transform spawnPoint;

        [Header("Transition")]
        public TransitionSettings transitionSettings;

        [Header("Dialogue")]
        public DialogueAsset dialogue;

        [Header("Custom")]
        public UnityEvent onExecute;
    }

    public class SimpleCinematicDirector : MonoBehaviour
    {
        [Header("Configuración")]
        public bool playOnStart = false;
        public bool hideHUD = true;
        public bool lockPlayer = true;
        
        [Header("Secuencia")]
        public List<CinematicStep> steps = new List<CinematicStep>();

        [Header("Eventos")]
        public UnityEvent onCinematicStart;
        public UnityEvent onCinematicEnd;

        private void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // 1. Setup inicial
            onCinematicStart?.Invoke();
            if (hideHUD && Sendero.UI.PlayerHUDV2.Instance) Sendero.UI.PlayerHUDV2.Instance.HideHUD();
            
            // Bloquear input si es necesario (usando tu sistema existente)
            if (lockPlayer) 
            {
                // Aquí deberías llamar a tu PlayerActionManager o similar
                // PlayerActionManager.Instance.PushMode(ActionMode.Cinematic);
            }

            // 2. Ejecutar pasos
            foreach (var step in steps)
            {
                // Debug.Log($"[Cinematic] Ejecutando paso: {step.name} ({step.actionType})");
                
                switch (step.actionType)
                {
                    case CinematicActionType.Wait:
                        yield return new WaitForSeconds(step.duration);
                        break;

                    case CinematicActionType.CameraShot:
                        if (step.vcam != null)
                        {
                            // Desactivar otras cámaras (simplificado)
                            // Lo ideal es tener un manager, pero para hacerlo autocontenido:
                            var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
                            foreach(var c in allCams) c.Priority.Value = 0;
                            
                            step.vcam.Priority.Value = 100;
                            
                            // Esperar el blend si se desea
                            if (step.duration > 0) yield return new WaitForSeconds(step.duration);
                        }
                        break;

                    case CinematicActionType.MoveCharacter:
                        if (step.character && step.targetPosition)
                        {
                            float dist = Vector3.Distance(step.character.position, step.targetPosition.position);
                            float time = dist / Mathf.Max(0.1f, step.moveSpeed);
                            
                            // Usar DOTween para mover (simple y efectivo)
                            step.character.DOMove(step.targetPosition.position, time).SetEase(Ease.Linear);
                            step.character.DOLookAt(step.targetPosition.position, 0.2f);
                            
                            // Si hay animador, poner animación de andar (opcional, requeriría saber el param)
                            
                            if (step.duration > 0) yield return new WaitForSeconds(step.duration);
                            else yield return new WaitForSeconds(time);
                        }
                        break;

                    case CinematicActionType.PlayAnimation:
                        if (step.animator)
                        {
                            if (step.useTrigger && !string.IsNullOrEmpty(step.triggerName))
                                step.animator.SetTrigger(step.triggerName);
                            else if (step.clip)
                                step.animator.Play(step.clip.name);
                                
                            if (step.duration > 0) yield return new WaitForSeconds(step.duration);
                        }
                        break;

                    case CinematicActionType.PlaySound:
                        if (step.audioClip)
                        {
                            // Usar tu AudioService o AudioSource.PlayClipAtPoint
                            AudioSource.PlayClipAtPoint(step.audioClip, Camera.main.transform.position, step.volume);
                        }
                        break;

                    case CinematicActionType.InstantiateObject:
                        if (step.prefab)
                        {
                            Vector3 pos = step.spawnPoint ? step.spawnPoint.position : Vector3.zero;
                            Quaternion rot = step.spawnPoint ? step.spawnPoint.rotation : Quaternion.identity;
                            Instantiate(step.prefab, pos, rot);
                        }
                        break;
                        
                    case CinematicActionType.Transition:
                        if (step.transitionSettings && TransitionManager.Instance())
                        {
                            TransitionManager.Instance().Transition(step.transitionSettings, 0);
                            yield return new WaitForSeconds(step.transitionSettings.transitionTime);
                        }
                        break;

                    case CinematicActionType.Dialogue:
                        if (step.dialogue && DialogueManager.Instance)
                        {
                            bool finished = false;
                            DialogueManager.Instance.StartDialogue(step.dialogue, () => finished = true);
                            while (!finished) yield return null;
                        }
                        break;

                    case CinematicActionType.CustomEvent:
                        step.onExecute?.Invoke();
                        if (step.duration > 0) yield return new WaitForSeconds(step.duration);
                        break;
                }
            }

            // 3. Finalizar
            if (hideHUD && Sendero.UI.PlayerHUDV2.Instance) Sendero.UI.PlayerHUDV2.Instance.ShowHUD();
            
            // Desbloquear input
            if (lockPlayer)
            {
                // PlayerActionManager.Instance.PopMode(ActionMode.Cinematic);
            }
            
            onCinematicEnd?.Invoke();
        }
    }
}
