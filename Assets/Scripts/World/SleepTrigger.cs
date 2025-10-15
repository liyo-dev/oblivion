using UnityEngine;

public class SleepTrigger : MonoBehaviour
{
    [Header("Referencia al jugador")]
    public GameObject player;
    [Header("Nombre del estado de animación de dormir")]
    public string sleepAnimationState = "Sleeping_NoWeapon";
    [Header("Posición de la cama (opcional)")]
    public Transform bedPosition;

    private bool isSleeping = false;
    private Animator playerAnimator;
    private CharacterController playerController;
    private bool wasControllerEnabled;

    void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeInHierarchy) return;
        if (isSleeping) return;
        if (!other.CompareTag("Player")) return;
        // Mover al jugador a la cama si se indica
        if (bedPosition != null)
            other.transform.position = bedPosition.position;

        playerAnimator = other.GetComponent<Animator>();
        playerController = other.GetComponent<CharacterController>();

        if (playerAnimator != null)
            playerAnimator.Play(sleepAnimationState); // Animación en bucle

        // Opcional: desactivar el movimiento del jugador mientras duerme
        if (playerController != null)
        {
            wasControllerEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        isSleeping = true;
        player = other.gameObject;
    }

    void Update()
    {
        if (!isSleeping) return;
        // Detectar cualquier input (tecla, botón, joystick, etc.)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        if (Input.anyKeyDown || Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            WakeUp();
        }
    }

    void WakeUp()
    {
        isSleeping = false;
        // Vuelve al estado idle (Locomotion)
        if (playerAnimator != null)
            playerAnimator.Play("Locomotion");
        // Reactivar el movimiento del jugador
        if (playerController != null)
            playerController.enabled = wasControllerEnabled;
    }
}
