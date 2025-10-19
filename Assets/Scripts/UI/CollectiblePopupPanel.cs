using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class CollectiblePopupPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Image iconImage;

    [Header("Animation")]
    [SerializeField] private float scaleInDuration = 0.12f;
    [SerializeField] private float fadeOutDuration = 0.2f;

    [Header("Slide")]
    [SerializeField] private float slideDistance = 24f;
    [SerializeField] private float slideInDuration = 0.18f;

    [Header("Layout")]
    [SerializeField] private float minPreferredHeight = 32f;

    private CanvasGroup _cg;

    // Runtime state for aggregation
    private string _itemId;
    private int _currentAmount;
    private float _displayDuration;
    private float _expiryTime;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
    }

    public string ItemId => _itemId;

    public void Init(ItemData item, int amount, float displayDuration)
    {
        Debug.Log($"[CollectiblePopupPanel] Init called for {item?.displayName} amount={amount} displayDuration={displayDuration}");

        _itemId = item != null ? (item.itemId ?? item.displayName) : "__null__";
        _currentAmount = amount;
        _displayDuration = displayDuration;

        // Ensure visible on top of siblings
        var rt = transform as RectTransform;
        if (rt != null)
        {
            transform.SetAsLastSibling();
        }

        if (item != null)
        {
            if (nameText == null) Debug.LogWarning("[CollectiblePopupPanel] nameText is not assigned on prefab.");
            else nameText.text = item.displayName;

            if (iconImage == null) Debug.LogWarning("[CollectiblePopupPanel] iconImage is not assigned on prefab.");
            else iconImage.sprite = item.icon;
        }
        else
        {
            if (nameText != null) nameText.text = "";
            if (iconImage != null) iconImage.sprite = null;
        }

        if (amountText == null)
        {
            Debug.LogWarning("[CollectiblePopupPanel] amountText is not assigned on prefab.");
        }
        else
        {
            amountText.text = _currentAmount > 0 ? $"+{_currentAmount}" : "";
        }

        // Ensure LayoutElement exists and provides a preferred height so the VerticalLayoutGroup can size children correctly
        LayoutElement le = GetComponent<LayoutElement>();
        if (le == null)
        {
            le = gameObject.AddComponent<LayoutElement>();
        }

        // Try to infer a reasonable preferred height from the rect; if not available use minPreferredHeight
        float inferredHeight = minPreferredHeight;
        if (rt != null)
        {
            // sizeDelta might be zero before layout; use rect height if present
            if (rt.rect.height > 0f) inferredHeight = rt.rect.height;
        }

        le.preferredHeight = Mathf.Max(le.preferredHeight, inferredHeight, minPreferredHeight);

        // Force rebuild of parent layout so the layout system accounts for the preferredHeight immediately
        if (rt != null && rt.parent is RectTransform parentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        // Make sure it's active and visible
        if (!gameObject.activeInHierarchy)
        {
            Debug.Log("[CollectiblePopupPanel] GameObject not activeInHierarchy - activating");
            gameObject.SetActive(true);
        }

        if (_cg != null) _cg.alpha = 0f;

        // set expiry
        _expiryTime = Time.realtimeSinceStartup + _displayDuration;

        StartCoroutine(PlayLifecycle());
    }

    /// <summary>
    /// Adds amount to the popup (called when we want to aggregate multiple quick pickups).
    /// Extends the visible time.
    /// </summary>
    public void AddAmount(int extra)
    {
        if (extra <= 0) return;
        _currentAmount += extra;
        if (amountText != null) amountText.text = _currentAmount > 0 ? $"+{_currentAmount}" : "";
        // extend expiry
        _expiryTime = Time.realtimeSinceStartup + _displayDuration;
        Debug.Log($"[CollectiblePopupPanel] AddAmount called for {_itemId}, newAmount={_currentAmount}, extended expiry by {_displayDuration}s");
    }

    private IEnumerator PlayLifecycle()
    {
        // Wait a frame so layout groups can place this element correctly
        yield return null;
        Canvas.ForceUpdateCanvases();

        var rt = transform as RectTransform;
        Vector2 targetAnchored = Vector2.zero;
        if (rt != null)
        {
            targetAnchored = rt.anchoredPosition;
            // start below
            rt.anchoredPosition = targetAnchored - new Vector2(0f, slideDistance);
            rt.localScale = Vector3.one * 0.85f;
        }

        float t = 0f;
        // animate slide + scale + fade in
        while (t < slideInDuration)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / slideInDuration);
            float sf = Mathf.SmoothStep(0f, 1f, f);
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.Lerp(targetAnchored - new Vector2(0f, slideDistance), targetAnchored, sf);
                rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, sf);
            }
            if (_cg != null) _cg.alpha = Mathf.Lerp(0f, 1f, f);
            yield return null;
        }

        // Wait visible until expiry time (which may be extended by AddAmount calls)
        while (Time.realtimeSinceStartup < _expiryTime)
        {
            yield return null;
        }

        // fade out
        t = 0f;
        float startAlpha = _cg != null ? _cg.alpha : 1f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / fadeOutDuration);
            if (_cg != null) _cg.alpha = Mathf.Lerp(startAlpha, 0f, f);
            yield return null;
        }

        Destroy(gameObject);
    }
}
