using UnityEngine;
namespace WindowExe.Desktop.Desktop.Window
{
    public class ImageWindow : Window
    {
        [SerializeField] private Sprite primarySprite;
        private SpriteRenderer image, background;
        public Sprite PrimarySprite { get=>primarySprite; set=>SetPrimarySprite(value); }
        public Sprite GetPrimarySprite()=>primarySprite;
        protected override void InitializePrimary()
        {
            var primary=transform.Find("Primary");
            if(primary==null) { primary=new GameObject("Primary").transform; primary.SetParent(transform,false); }
            background=Renderer(primary,"Background");
            if(background.sprite==null) background.sprite=WindowTitleBar.WhiteSprite;
            background.drawMode=SpriteDrawMode.Sliced;
            image=Renderer(primary,"Image"); image.drawMode=SpriteDrawMode.Simple;
            SetPrimarySprite(primarySprite);
        }
        private static SpriteRenderer Renderer(Transform parent,string name)
        {
            var child=parent.Find(name); if(child==null) { child=new GameObject(name).transform; child.SetParent(parent,false); }
            var renderer=child.GetComponent<SpriteRenderer>(); return renderer!=null?renderer:child.gameObject.AddComponent<SpriteRenderer>();
        }
        public void SetPrimarySprite(Sprite sprite)
        {
            primarySprite=sprite;
            if(image==null) return;
            image.sprite=sprite; image.enabled=sprite!=null;
            ApplySize(size);
        }
        protected override void ApplySize(Vector2 value)
        {
            base.ApplySize(value);
            if(background!=null) background.size=value;
            if(image!=null&&image.sprite!=null)
            {
                var b=image.sprite.bounds.size;
                var scale=Mathf.Min(value.x/b.x,value.y/b.y);
                image.transform.localScale=new Vector3(scale,scale,1);
            }
        }
    }
}
