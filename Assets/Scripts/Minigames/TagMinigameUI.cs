using UnityEngine;
using TMPro;
public class TagMinigameUI : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject containerPanel;
    [SerializeField] private Color normalTimerColor = Color.white;
    [SerializeField] private Color urgentTimerColor = Color.red;
    [SerializeField] private float urgentThreshold = 10f;
    private TagMinigameController controller;
    void Awake()
    {
        if (!canvas) canvas = GetComponent<Canvas>();
        controller = GetComponentInParent<TagMinigameController>();
        Hide();
    }
    void Update()
    {
        if (controller != null && controller.IsRunning)
            UpdateTimer(controller.RemainingTime);
    }
    public void Show()
    {
        if (containerPanel) containerPanel.SetActive(true);
        if (canvas) canvas.enabled = true;
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        if (containerPanel) containerPanel.SetActive(false);
    }
    public void UpdateTimer(float time)
    {
        if (!timerText) return;
        time = Mathf.Max(0, time);
        int mins = Mathf.FloorToInt(time / 60);
        int secs = Mathf.FloorToInt(time % 60);
        timerText.text = string.Format("{0:00}:{1:00}", mins, secs);
        timerText.color = time <= urgentThreshold ? urgentTimerColor : normalTimerColor;
    }
    public void ShowCountdown(int seconds)
    {
        if (countdownText)
            countdownText.text = seconds > 0 ? seconds.ToString() : "";
    }
    public void ShowMessage(string message)
    {
        if (messageText) messageText.text = message;
    }
    public void ClearMessage()
    {
        if (messageText) messageText.text = "";
    }
    public void SetController(TagMinigameController ctrl) { controller = ctrl; }
}
