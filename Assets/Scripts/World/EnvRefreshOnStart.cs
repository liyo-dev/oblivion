using System.Collections;
using UnityEngine;

public class EnvRefreshOnStart : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return null; // espera 1 frame a que spawnee la cámara
        EnvironmentController.Instance?.RefreshCameraNow();
    }
}