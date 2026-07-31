using UnityEngine;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 查看器引导：任意场景按 Play 自动创建世界与相机（若场景中尚未放置）。
    /// </summary>
    public static class WorldBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<WorldController>() != null) return;

            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 34f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
                camGo.AddComponent<AudioListener>();
            }

            var world = new GameObject("World");
            world.AddComponent<WorldController>();
        }
    }
}
