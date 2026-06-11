using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles asynchronous loading between scenes with smooth progress bar interpolation
/// and a "press any key to start" prompt. Once loading is complete, progress elements 
/// are hidden, showing only the prompt.
/// Designed to be attached to a UI Panel inside an existing Canvas.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    private static LoadingScreenManager _instance;

    public static LoadingScreenManager Instance
    {
        get
        {
            if (_instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<LoadingScreenManager>(FindObjectsInactive.Include);
#else
                _instance = FindObjectOfType<LoadingScreenManager>();
#endif
            }
            return _instance;
        }
    }

    [Header("UI References")]
    [Tooltip("The main CanvasGroup of this Panel to handle fading in/out.")]
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Tooltip("Slider displaying the loading progress (0.0 to 1.0).")]
    [SerializeField] private Slider progressBar;
    
    [Tooltip("Text element displaying percentage (e.g., '100%').")]
    [SerializeField] private TMP_Text progressText;

    [Tooltip("Optional group GameObject wrapping progress elements to hide them when loaded.")]
    [SerializeField] private GameObject loadingProgressGroup;
    
    [Tooltip("Text element displaying 'Press Any Key to Start' prompt.")]
    [SerializeField] private TMP_Text pressAnyKeyText;

    [Header("Settings")]
    [Tooltip("Speed of the canvas fade in/out animations.")]
    [SerializeField] private float fadeSpeed = 2f;

    [Tooltip("How fast the slider/progress value smoothly catches up to the real progress.")]
    [SerializeField] private float smoothProgressSpeed = 1.2f;

    private bool _isLoading = false;

    private void Awake()
    {
        // Setup Singleton
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Initialize UI State so it starts invisible
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Starts asynchronous loading of the specified scene.
    /// </summary>
    /// <param name="sceneName">Name of the target gameplay scene.</param>
    public void LoadSceneAsync(string sceneName)
    {
        if (_isLoading) return;
        StartCoroutine(LoadCoroutine(sceneName));
    }

    private IEnumerator LoadCoroutine(string sceneName)
    {
        _isLoading = true;

        // If we are currently a child of another Canvas, wrap ourselves in a temporary DontDestroyOnLoad Canvas
        // so we can survive the scene transition and fade out smoothly in the new scene.
        Canvas originalCanvas = GetComponentInParent<Canvas>();
        GameObject tempCanvasGO = null;
        
        if (originalCanvas != null)
        {
            tempCanvasGO = new GameObject("TempLoadingCanvas");
            Canvas tempCanvas = tempCanvasGO.AddComponent<Canvas>();
            tempCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tempCanvas.sortingOrder = 999; // Render on top of everything

            // Match UI scaling of original canvas
            CanvasScaler originalScaler = originalCanvas.GetComponent<CanvasScaler>();
            CanvasScaler tempScaler = tempCanvasGO.AddComponent<CanvasScaler>();
            if (originalScaler != null)
            {
                tempScaler.uiScaleMode = originalScaler.uiScaleMode;
                tempScaler.referenceResolution = originalScaler.referenceResolution;
                tempScaler.screenMatchMode = originalScaler.screenMatchMode;
                tempScaler.matchWidthOrHeight = originalScaler.matchWidthOrHeight;
                tempScaler.referencePixelsPerUnit = originalScaler.referencePixelsPerUnit;
            }

            // Reparent the panel to the new temporary canvas
            transform.SetParent(tempCanvasGO.transform, false);

            // Make the temporary canvas persistent
            DontDestroyOnLoad(tempCanvasGO);
        }
        else
        {
            // Fallback: If not child of a canvas, make this object persistent directly
            DontDestroyOnLoad(gameObject);
        }

        // Show loading screen panel
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;

            // Fade in loading panel
            while (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha += Time.unscaledDeltaTime * fadeSpeed;
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // Initialize progress UI elements
        if (loadingProgressGroup != null)
        {
            loadingProgressGroup.SetActive(true);
        }
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.value = 0f;
        }
        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = "0%";
        }
        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(false);
        }

        // Start scene load asynchronously
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        
        // Prevent automatic activation when loaded (stays at 0.9 progress)
        asyncOp.allowSceneActivation = false;

        float displayedProgress = 0f;

        // Loop until asyncOp progress reaches 0.9 AND displayed progress reaches 1.0 (smooth catchup)
        while (asyncOp.progress < 0.9f || displayedProgress < 1f)
        {
            // Normalize progress from 0.0-0.9 to 0.0-1.0
            float targetProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            // Smoothly move the displayed value toward the target value
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * smoothProgressSpeed);

            if (progressBar != null) progressBar.value = displayedProgress;
            if (progressText != null) progressText.text = Mathf.RoundToInt(displayedProgress * 100f) + "%";

            yield return null;
        }

        // Ensure values are locked at 100%
        if (progressBar != null) progressBar.value = 1f;
        if (progressText != null) progressText.text = "100%";
        
        // Short pause at 100% for smooth visual transition
        yield return new WaitForSecondsRealtime(0.15f);

        // Hide progress UI so ONLY the press-any-key prompt is visible on the loading screen
        if (loadingProgressGroup != null)
        {
            loadingProgressGroup.SetActive(false);
        }
        else
        {
            if (progressBar != null) progressBar.gameObject.SetActive(false);
            if (progressText != null) progressText.text = "";
        }

        // Show input prompt
        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(true);
        }

        // Wait for player to press any key or click mouse
        bool waitingForInput = true;
        float pulseTimer = 0f;

        while (waitingForInput)
        {
            // Pulse animation on the prompt text using unscaled time
            if (pressAnyKeyText != null)
            {
                pulseTimer += Time.unscaledDeltaTime * 4f; // pulse frequency
                float pulseAlpha = (Mathf.Sin(pulseTimer) + 1f) / 2f;
                Color textColor = pressAnyKeyText.color;
                textColor.a = Mathf.Lerp(0.2f, 1.0f, pulseAlpha);
                pressAnyKeyText.color = textColor;
            }

            // Check input (Keyboard, Controller, or Mouse click)
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
            {
                waitingForInput = false;
            }

            yield return null;
        }

        // Trigger scene activation
        asyncOp.allowSceneActivation = true;

        // Wait until scene loads completely
        while (!asyncOp.isDone)
        {
            yield return null;
        }

        // Wait a frame to let scripts in the new scene initialize (e.g. SaveManager's OnSceneLoaded)
        yield return null;

        // Hide input prompt
        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(false);
        }

        // Fade out loading panel
        if (canvasGroup != null)
        {
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= Time.unscaledDeltaTime * fadeSpeed;
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        _isLoading = false;

        // Clean up temporary canvas container and destroy loading panel
        if (tempCanvasGO != null)
        {
            Destroy(tempCanvasGO);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
