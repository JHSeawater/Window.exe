using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WindowExe.BossFight
{
    /// <summary>Owns the CRT-free desktop snapshot and temporarily hides the live taskbar.</summary>
    public sealed class BossEntryBackdrop : IDisposable
    {
        private readonly Camera camera;
        private readonly Canvas taskbar;
        private readonly bool taskbarWasEnabled;
        private Texture2D texture;
        private Sprite sprite;
        private SpriteRenderer view;
        private bool disposed;
        public bool IsDisposed => disposed;

        public BossEntryBackdrop(Texture2D snapshot, Camera camera, Canvas taskbar, Material material)
        {
            texture = snapshot; this.camera = camera; this.taskbar = taskbar;
            taskbarWasEnabled = taskbar != null && taskbar.enabled;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            view = new GameObject("Frozen Unlock desktop", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            view.sprite = sprite; view.sharedMaterial = material;
            view.sortingLayerName = "WindowFront"; view.sortingOrder = 32760;
            if (taskbar != null) taskbar.enabled = false;
            FitCamera();
        }

        public void ShowBehindEntry()
        {
            if (view == null) return;
            view.sortingLayerName = "Background"; view.sortingOrder = 3;
            FitCamera();
        }

        public void FitCamera()
        {
            if (camera == null || view == null) return;
            var depth = -camera.transform.position.z;
            var lower = camera.ViewportToWorldPoint(new Vector3(0, 0, depth));
            var upper = camera.ViewportToWorldPoint(new Vector3(1, 1, depth));
            view.transform.position = (lower + upper) * .5f;
            view.transform.localScale = new Vector3((upper.x - lower.x) / sprite.bounds.size.x,
                (upper.y - lower.y) / sprite.bounds.size.y, 1);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (view != null) { view.gameObject.SetActive(false); Object.Destroy(view.gameObject); view = null; }
            if (taskbar != null) taskbar.enabled = taskbarWasEnabled;
            Object.Destroy(sprite); Object.Destroy(texture); sprite = null; texture = null;
        }
    }
}
