using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.BossFight
{
    /// <summary>A passive window view; its active state never owns combat or download time.</summary>
    public sealed class BossFightWindow : Window
    {
        public const string ContentId = "boss-fight-window";
        private SpriteRenderer body;
        private float resizeInside=9,resizeOutside=24,worldScale;
        public SpriteRenderer Body => body;
        public bool IsBound { get; private set; }
        protected override void InitializePrimary()
        {
            var primary=transform.Find("Primary");
            if(primary==null) { primary=new GameObject("Primary").transform; primary.SetParent(transform,false); }
            body=primary.GetComponent<SpriteRenderer>();
            if(body==null) body=primary.gameObject.AddComponent<SpriteRenderer>();
            body.sprite=WindowTitleBar.WhiteSprite; body.drawMode=SpriteDrawMode.Sliced;
            body.color=new Color(.055f,.075f,.09f,.96f); body.size=size;
        }
        public void Bind(float worldPerPixel)
        {
            worldScale=worldPerPixel;
            minSize=new Vector2(90,90)*worldPerPixel; maxSize=new Vector2(600,510)*worldPerPixel;
            GetComponentInChildren<WindowTitleBar>(true).ConfigureCompact(36*worldPerPixel,26*worldPerPixel,resizeInside*worldPerPixel);
            GetComponentInChildren<WindowResizeHandle>(true).ConfigureSize(2*resizeInside*worldPerPixel,2*resizeInside*worldPerPixel,(resizeOutside-resizeInside)*worldPerPixel);
            SetWindowTitle("WINDOW(1).exe"); IsBound=true;
        }
        public void ConfigureResizePadding(double inside,double outside)
        { resizeInside=(float)inside;resizeOutside=(float)outside;if(IsBound)Bind(worldScale); }
        protected override void ApplySize(Vector2 value)
        { base.ApplySize(value); if(body!=null) body.size=value; }
        public override void Maximize() { }
        public override void ToggleMaximize() { }
    }
}
