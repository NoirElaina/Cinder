using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinder.Runtime.World
{
    /// <summary>查看器飞行相机：WASD/方向键移动，Shift 加速，滚轮缩放。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FlyCamera : MonoBehaviour
    {
        public float MoveSpeed = 40f;
        public float MinSize = 5f;
        public float MaxSize = 80f;

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                Vector3 move = Vector3.zero;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                float speed = MoveSpeed * (kb.leftShiftKey.isPressed ? 3f : 1f);
                transform.position += move * (speed * Time.deltaTime);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    cam.orthographicSize = Mathf.Clamp(
                        cam.orthographicSize * Mathf.Pow(0.9f, scroll * 0.01f),
                        MinSize, MaxSize);
                }
            }
        }
    }
}
