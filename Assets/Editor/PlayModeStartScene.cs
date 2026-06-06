using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Bu script Unity Editor açıldığında veya kod derlendiğinde otomatik çalışır.
[InitializeOnLoad]
public static class PlayModeStartScene
{
    static PlayModeStartScene()
    {
        // Editor'de Play tuşuna basıldığında her zaman Build Settings'teki 0. sahneden başlamasını sağlar.
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        
        SetPlayModeStartScene();
    }

    static void SetPlayModeStartScene()
    {
        // Eğer Build Settings'te hiç sahne yoksa işlem yapma
        if (EditorBuildSettings.scenes.Length == 0) return;

        // Build Settings'teki en üstteki (0 numaralı) sahnenin yolunu al
        string firstScenePath = EditorBuildSettings.scenes[0].path;
        
        // Bu sahneyi bir obje olarak yükle
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(firstScenePath);
        
        if (sceneAsset != null)
        {
            // Unity Editor'e "Oyun başlarken hep bu sahneyi kullan" talimatını ver
            EditorSceneManager.playModeStartScene = sceneAsset;
        }
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Sahneler build settings'te değişmiş olabilir diye play'e basmadan hemen önce tekrar kontrol et
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SetPlayModeStartScene();
        }
    }
}
