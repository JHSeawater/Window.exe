using UnityEngine;
namespace WindowExe.Desktop.Desktop.Window
{
    public enum ResizeDirection { Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }
    public class WindowResizeHandle : MonoBehaviour
    {
        [SerializeField] private float borderThickness = .12f, cornerSize = .22f;
        private Window owner;
        private SingleResizeHandle[] handles;
        private float exteriorPadding;
        public void ConfigureSize(float border, float corner, float extraOutside=0)
        { borderThickness=Mathf.Max(.001f,border); cornerSize=Mathf.Max(.001f,corner); exteriorPadding=Mathf.Max(0,extraOutside); if(owner!=null) UpdateLayout(owner.Size); }
        public void Initialize(Window window)
        {
            owner = window;
            handles = new SingleResizeHandle[8];
            for (var i=0;i<8;i++)
            {
                var name = "Handle_"+(ResizeDirection)i;
                var child = transform.Find(name);
                if (child == null) { child = new GameObject(name).transform; child.SetParent(transform,false); }
                var h = child.GetComponent<SingleResizeHandle>();
                if (h == null) h = child.gameObject.AddComponent<SingleResizeHandle>();
                h.Init(owner,this,(ResizeDirection)i); handles[i]=h;
            }
        }
        public float GetBarHeight() => owner != null ? owner.BarHeight : .5f;
        public void UpdateLayout(Vector2 size)
        {
            if (handles == null) return;
            // Enlarge only outside the frame, preserving content and title-bar hit areas.
            var shift=exteriorPadding/2;
            var l=-size.x/2-shift; var r=size.x/2+shift; var b=-size.y/2-shift; var t=size.y/2+GetBarHeight()+shift;
            var border=borderThickness+exteriorPadding; var corner=cornerSize+exteriorPadding;
            for(var i=0;i<8;i++)
            {
                var h=handles[i];
                var x=i==0||i==4||i==6?l:i==1||i==5||i==7?r:0;
                var y=i==2||i==4||i==5?t:i==3||i==6||i==7?b:(b+t)/2;
                h.transform.localPosition=new Vector3(x,y,0);
                h.Collider.size=i>=4?new Vector2(corner,corner):i<2?new Vector2(border,Mathf.Max(.1f,t-b-corner)):new Vector2(Mathf.Max(.1f,r-l-corner),border);
            }
        }
        public ResizeDirection? DirectionAt(Vector2 point)
        {
            if (owner == null || owner.IsMaximized || owner.ButtonAt(point)>=0 || handles==null) return null;
            for(var i=7;i>=0;i--) if(handles[i]!=null && handles[i].Collider.OverlapPoint(point)) return handles[i].Direction;
            return null;
        }
        public bool IsPointerOverAnyHandle(Vector2 point) => DirectionAt(point).HasValue;
    }
}
