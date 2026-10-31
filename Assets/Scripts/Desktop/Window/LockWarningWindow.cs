using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>Image-authored permission warning. Only the pictured padlock accepts a key.</summary>
    public sealed class LockWarningWindow : RepairWindow
    {
        public const string ContentId = "lock-warning";
        [SerializeField] private Rect keyTarget = new Rect(.095f, .42f, .105f, .34f);
        [SerializeField] private Rect lockedClose = new Rect(.86f, .84f, .06f, .11f);
        [SerializeField] private Rect lockedConfirm = new Rect(.375f, .21f, .235f, .205f);
        [SerializeField] private Rect unlockedClose = new Rect(.855f, .74f, .06f, .13f);
        [SerializeField] private Rect unlockedConfirm = new Rect(.335f, .12f, .26f, .22f);

        public override float BarHeight => 0;
        public event System.Action<LockWarningWindow> BossEntryRequested;

        public override void RequestClose()
        {
            if (CurrentStage == 0) { base.RequestClose(); return; }
            var manager = WindowManager.Instance;
            if (manager != null && !manager.CanRequest(this, WindowAction.Close)) return;
            BossEntryRequested?.Invoke(this);
        }

        protected override void InitializePrimary()
        {
            base.InitializePrimary();
            transform.Find("TitleBar").gameObject.SetActive(false);
            transform.Find("ResizeHandle").gameObject.SetActive(false);
            SetBorder(false, 0, Color.clear);
        }

        private Vector2 ImagePoint(Vector2 world)
        {
            var renderer = FrontImageRenderer;
            if (renderer == null || renderer.sprite == null) return new Vector2(-1, -1);
            var sprite = renderer.sprite;
            var local = (Vector2)renderer.transform.InverseTransformPoint(world);
            var pixels = local * sprite.pixelsPerUnit + sprite.pivot;
            return new Vector2(pixels.x / sprite.rect.width, pixels.y / sprite.rect.height);
        }

        public Vector2 ImageToWorld(Vector2 normalizedPoint)
        {
            var sprite = FrontImageRenderer.sprite;
            var pixels = Vector2.Scale(normalizedPoint, sprite.rect.size) - sprite.pivot;
            return FrontImageRenderer.transform.TransformPoint(pixels / sprite.pixelsPerUnit);
        }

        public Vector2 KeyTargetCenter => ImageToWorld(keyTarget.center);
        public override bool CanUseKeyAt(Vector2 worldPos) => CurrentStage == 0 && keyTarget.Contains(ImagePoint(worldPos));
        public override bool Hit(Vector2 point) => FrontImageRenderer != null && FrontImageRenderer.bounds.Contains(point);
        public override bool IsPointerOverResizeHandle(Vector2 point) => false;
        public override ResizeDirection? ResizeAt(Vector2 point) => null;

        public override int ButtonAt(Vector2 point)
        {
            var uv = ImagePoint(point);
            var close = CurrentStage == 0 ? lockedClose : unlockedClose;
            var confirm = CurrentStage == 0 ? lockedConfirm : unlockedConfirm;
            return close.Contains(uv) || confirm.Contains(uv) ? 2 : -1;
        }

        public override bool TitleContains(Vector2 point)
        {
            var title = CurrentStage == 0 ? new Rect(.07f, .82f, .85f, .15f) : new Rect(.015f, .74f, .9f, .15f);
            return ButtonAt(point) < 0 && title.Contains(ImagePoint(point));
        }
    }
}
