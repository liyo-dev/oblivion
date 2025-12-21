using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Reproduce un slideshow de imágenes "recuerdos" con fade in/out automático.
/// Detecta automáticamente todos los hijos con componente Image y los cicla.
/// </summary>
public class MemorySlideshow : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Tiempo que cada imagen permanece visible")]
    [Min(0.5f)]
    public float displayDuration = 4f;
    
    [Tooltip("Duración del fade in")]
    [Min(0.1f)]
    public float fadeInDuration = 1f;
    
    [Tooltip("Duración del fade out")]
    [Min(0.1f)]
    public float fadeOutDuration = 1f;
    
    [Tooltip("Si está activo, vuelve a empezar desde la primera imagen al terminar")]
    public bool loop = true;
    
    [Tooltip("Si está activo, las imágenes aparecen en orden aleatorio")]
    public bool randomOrder = false;
    
    [Tooltip("Retraso inicial antes de empezar el slideshow")]
    [Min(0f)]
    public float initialDelay = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    private List<Image> _memoryImages = new List<Image>();
    private int _currentIndex = 0;
    private Coroutine _slideshowCoroutine;
    private List<int> _shuffledIndices = new List<int>();
    
    void Awake()
    {
        // Encontrar todas las imágenes hijas
        CollectMemoryImages();
        
        // Ocultar todas inicialmente
        HideAllImages();
    }
    
    void OnEnable()
    {
        // Iniciar slideshow cuando se activa
        if (_memoryImages.Count > 0)
        {
            StartSlideshow();
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning($"[MemorySlideshow:{name}] No hay imágenes hijas para mostrar");
        }
    }
    
    void OnDisable()
    {
        StopSlideshow();
    }
    
    void CollectMemoryImages()
    {
        _memoryImages.Clear();
        
        // Buscar todos los componentes Image en los hijos directos
        foreach (Transform child in transform)
        {
            var image = child.GetComponent<Image>();
            if (image != null)
            {
                _memoryImages.Add(image);
                if (showDebugLogs)
                {
                    Debug.Log($"[MemorySlideshow:{name}] Imagen encontrada: {child.name}");
                }
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[MemorySlideshow:{name}] Total de imágenes encontradas: {_memoryImages.Count}");
        }
    }
    
    void HideAllImages()
    {
        foreach (var img in _memoryImages)
        {
            if (img != null)
            {
                var cg = img.GetComponent<CanvasGroup>();
                if (cg == null) cg = img.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }
        }
    }
    
    public void StartSlideshow()
    {
        if (_slideshowCoroutine != null)
        {
            StopCoroutine(_slideshowCoroutine);
        }
        
        _currentIndex = 0;
        
        // Preparar orden aleatorio si está activado
        if (randomOrder)
        {
            PrepareShuffledOrder();
        }
        
        _slideshowCoroutine = StartCoroutine(SlideshowRoutine());
    }
    
    public void StopSlideshow()
    {
        if (_slideshowCoroutine != null)
        {
            StopCoroutine(_slideshowCoroutine);
            _slideshowCoroutine = null;
        }
        
        // Detener todas las animaciones DOTween en las imágenes
        foreach (var img in _memoryImages)
        {
            if (img != null)
            {
                var cg = img.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.DOKill();
                }
            }
        }
    }
    
    void PrepareShuffledOrder()
    {
        _shuffledIndices.Clear();
        for (int i = 0; i < _memoryImages.Count; i++)
        {
            _shuffledIndices.Add(i);
        }
        
        // Shuffle Fisher-Yates
        for (int i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            int temp = _shuffledIndices[i];
            _shuffledIndices[i] = _shuffledIndices[randomIndex];
            _shuffledIndices[randomIndex] = temp;
        }
    }
    
    IEnumerator SlideshowRoutine()
    {
        // Retraso inicial
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }
        
        do
        {
            for (int i = 0; i < _memoryImages.Count; i++)
            {
                int imageIndex = randomOrder ? _shuffledIndices[i] : i;
                var image = _memoryImages[imageIndex];
                
                if (image == null) continue;
                
                var cg = image.GetComponent<CanvasGroup>();
                if (cg == null) cg = image.gameObject.AddComponent<CanvasGroup>();
                
                if (showDebugLogs)
                {
                    Debug.Log($"[MemorySlideshow:{name}] Mostrando imagen {imageIndex}: {image.name}");
                }
                
                // Fade in
                cg.alpha = 0f;
                cg.DOFade(1f, fadeInDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
                
                yield return new WaitForSeconds(fadeInDuration);
                
                // Mantener visible
                yield return new WaitForSeconds(displayDuration);
                
                // Fade out
                cg.DOFade(0f, fadeOutDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
                
                yield return new WaitForSeconds(fadeOutDuration);
            }
            
            // Preparar nuevo orden aleatorio para el siguiente ciclo si está activado
            if (loop && randomOrder)
            {
                PrepareShuffledOrder();
            }
            
        } while (loop);
        
        if (showDebugLogs)
        {
            Debug.Log($"[MemorySlideshow:{name}] Slideshow finalizado");
        }
        
        _slideshowCoroutine = null;
    }
    
    /// <summary>
    /// Salta a la siguiente imagen inmediatamente
    /// </summary>
    public void SkipToNext()
    {
        if (_memoryImages.Count == 0) return;
        
        // Detener coroutine actual
        if (_slideshowCoroutine != null)
        {
            StopCoroutine(_slideshowCoroutine);
        }
        
        // Ocultar imagen actual
        if (_currentIndex >= 0 && _currentIndex < _memoryImages.Count)
        {
            var currentImage = _memoryImages[_currentIndex];
            if (currentImage != null)
            {
                var cg = currentImage.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.DOKill();
                    cg.DOFade(0f, 0.2f);
                }
            }
        }
        
        // Avanzar índice
        _currentIndex = (_currentIndex + 1) % _memoryImages.Count;
        
        // Reiniciar slideshow desde nueva posición
        _slideshowCoroutine = StartCoroutine(SlideshowRoutine());
    }
}
