using TMPro;
using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class WindowTitleBar : MonoBehaviour
    {
        [SerializeField] private float barHeight = 0.5f;
        private Window owner;
        private SpriteRenderer background;
        private TextMeshPro title, buttons;
        private bool compact;
        private float compactButtonWidth;
        private float compactTopResizeInset;
        public void ConfigureCompact(float height, float buttonWidth, float topResizeInset=0)
        {
            compact=true; barHeight=Mathf.Max(.01f,height); compactButtonWidth=Mathf.Max(.01f,buttonWidth);
            compactTopResizeInset=Mathf.Clamp(topResizeInset,0,barHeight/2);
            if(owner!=null) { Initialize(owner); UpdateLayout(owner.Size); }
        }
        private static Sprite white;
        public float BarHeight => barHeight;
        public static Sprite WhiteSprite
        {
            get
            {
                if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(.5f,.5f), 2, 0, SpriteMeshType.FullRect);
                return white;
            }
        }
        public void Initialize(Window window)
        {
            owner = window;
            background = GetComponent<SpriteRenderer>();
            if (background == null) background = gameObject.AddComponent<SpriteRenderer>();
            if (background.sprite == null) background.sprite = WhiteSprite;
            background.color = new Color(.12f,.25f,.55f);
            background.drawMode = SpriteDrawMode.Sliced;
            title = Text("TitleText", TextAlignmentOptions.MidlineLeft);
            buttons = Text("ButtonsText", TextAlignmentOptions.MidlineRight);
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            buttons.text = "";
            for(var i=0;i<3;i++) Text("Button"+i,TextAlignmentOptions.Center).text=new[]{"_","□","×"}[i];
            if(compact) transform.Find("Button1").gameObject.SetActive(false);
            SetTitle(window.WindowTitle);
        }
        private TextMeshPro Text(string name, TextAlignmentOptions alignment)
        {
            var child = transform.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(transform, false); }
            var result = child.GetComponent<TextMeshPro>();
            if (result == null) result = child.gameObject.AddComponent<TextMeshPro>();
            GameTypography.Apply(result);
            result.fontSize = 2.5f; result.color = Color.white; result.alignment = alignment;
            result.textWrappingMode = TextWrappingModes.NoWrap;
            return result;
        }
        public void UpdateLayout(Vector2 size)
        {
            if (owner == null) return;
            transform.localPosition = new Vector3(0, size.y/2 + barHeight/2, 0);
            GetComponent<BoxCollider2D>().size = new Vector2(size.x,barHeight);
            background.size = new Vector2(size.x,barHeight);
            if(compact)
            {
                var w=Mathf.Min(compactButtonWidth,size.x/2);
                title.rectTransform.sizeDelta=new Vector2(Mathf.Max(0,size.x-w*2-.04f),barHeight);
                title.transform.localPosition=new Vector3(-w,0,0); title.fontSize=barHeight*5;
                buttons.gameObject.SetActive(false);
                for(var i=0;i<3;i+=2)
                {
                    var glyph=transform.Find("Button"+i).GetComponent<TextMeshPro>();
                    glyph.rectTransform.sizeDelta=new Vector2(w,barHeight-compactTopResizeInset);
                    glyph.transform.localPosition=new Vector3(size.x/2-w*(i==0?1.5f:.5f),-compactTopResizeInset/2,0);
                    glyph.fontSize=(barHeight-compactTopResizeInset)*5; glyph.color=i==2?new Color(1,.28f,.24f):Color.white;
                }
                return;
            }
            title.rectTransform.sizeDelta = new Vector2(Mathf.Max(.1f,size.x-1.4f),barHeight);
            title.transform.localPosition = new Vector3(-.65f,0,0);
            buttons.rectTransform.sizeDelta = new Vector2(1.2f,barHeight);
            buttons.transform.localPosition = new Vector3(size.x/2-.65f,0,0);
            for(var i=0;i<3;i++) { var glyph=transform.Find("Button"+i).GetComponent<TextMeshPro>(); glyph.rectTransform.sizeDelta=new Vector2(.4f,barHeight); glyph.transform.localPosition=new Vector3(size.x/2-1.05f+i*.4f,0,0); }
        }
        public void SetTitle(string value) { if (title != null) title.text = value; }
        public bool Contains(Vector2 point) => owner != null && GetComponent<BoxCollider2D>().OverlapPoint(point);
        public int ButtonAt(Vector2 point)
        {
            if (owner == null) return -1;
            var p = transform.InverseTransformPoint(point);
            if(compact)
            {
                if(p.y < -barHeight/2 || p.y > barHeight/2-compactTopResizeInset || p.x > owner.Size.x/2) return -1;
                var w=Mathf.Min(compactButtonWidth,owner.Size.x/2);
                if(p.x>=owner.Size.x/2-w) return 2;
                if(p.x>=owner.Size.x/2-w*2) return 0;
                return -1;
            }
            var start = owner.Size.x/2-1.25f;
            if (p.y < -barHeight/2 || p.y > barHeight/2 || p.x < start || p.x > start+1.2f) return -1;
            return Mathf.Min(2,Mathf.FloorToInt((p.x-start)/.4f));
        }
    }
}
