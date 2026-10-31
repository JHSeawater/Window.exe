using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    public sealed partial class DesktopSceneInteractionTests
    {
        [UnityTest]
        public IEnumerator WindowDesktopCaptureIncludesInternetSearchText()
        {
            SetResolution(1920,1080);
            for (var frame=0;frame<5;frame++) yield return null;
            var manager=WindowManager.Instance;
            var internet=(InternetWindow)manager.Open("internet");
            internet.SimulatePasscodeInput("1971");
            WifiConnection.Instance.SetConnected(true);
            Assert.That(internet.CurrentStep,Is.EqualTo(InternetWindow.InternetStep.SearchHistory));
            internet.MoveTo(Vector2.zero);
            internet.SetDropdownOpen(true);
            Assert.That(internet.GetFrontRowText(2),Is.Not.EqualTo("◷  총"));
            var names=new[]{"IE_BackHeading","IE_BackSearchLabel","IE_BackArrowLabel",
                "IE_BackRowText_0","IE_BackRowText_1","IE_BackRowText_2"};
            var labels=new TextMeshPro[names.Length];
            var renderers=new Renderer[names.Length];
            for (var i=0;i<names.Length;i++)
            {
                labels[i]=internet.transform.Find("Secondary/IE_BackContent/"+names[i]).GetComponent<TextMeshPro>();
                labels[i].ForceMeshUpdate();
                renderers[i]=labels[i].GetComponent<Renderer>();
            }
            Assert.That(labels[0].text,Is.EqualTo("검색"));
            Assert.That(labels[1].text,Is.EqualTo("검색 기록을 선택하세요"));
            Assert.That(labels[2].text,Is.EqualTo("▲"));
            Assert.That(labels[3].text,Is.EqualTo(internet.GetFrontRowText(0)));
            Assert.That(labels[4].text,Is.EqualTo(internet.GetFrontRowText(1)));
            Assert.That(labels[5].text,Is.EqualTo("◷  총"));
            var window=manager.Open("window");
            window.Maximize();
            var capture=window.GetComponent<WindowDesktopCapture>();
            for (var frame=0;frame<5;frame++) yield return null;
            Assert.That(internet.IsDecoded,Is.True);
            var rendered=ReadSearchCapture(capture);
            Texture2D withoutText=null;
            try
            {
                // Compare actual render-target pixels with the same text renderers disabled.
                // Checking TMP.text alone would pass even if the capture ignored every label.
                foreach (var renderer in renderers) renderer.enabled=false;
                for (var frame=0;frame<3;frame++) yield return null;
                withoutText=ReadSearchCapture(capture);
                foreach (var label in labels)
                    AssertSearchLabelPixelsChanged(label,rendered,withoutText);
            }
            finally
            {
                foreach (var renderer in renderers) if (renderer!=null) renderer.enabled=true;
                Object.Destroy(rendered);
                if (withoutText!=null) Object.Destroy(withoutText);
            }
            for (var frame=0;frame<3;frame++) yield return null;
            Directory.CreateDirectory("Builds/InternetTextFix_20261101");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Builds/InternetTextFix_20261101/search-{crtQuality}.png"));
            for (var frame=0;frame<3;frame++) yield return null;
            internet.MoveTo(new Vector2(.5f,.5f));
            internet.Resize(new Vector2(6,4));
            for (var frame=0;frame<3;frame++) yield return null;
            Assert.That(internet.GetBackRowText(0),Is.EqualTo(internet.GetFrontRowText(0)));
            Assert.That(internet.GetBackRowText(1),Is.EqualTo(internet.GetFrontRowText(1)));
            Assert.That(internet.GetBackRowText(2),Is.EqualTo("◷  총"));
            manager.Minimize("internet");
            for (var frame=0;frame<3;frame++) yield return null;
            var minimized=ReadSearchCapture(capture);
            try
            {
                manager.Enshow("internet");
                manager.FocusWindow("window");
                for (var frame=0;frame<3;frame++) yield return null;
                var restored=ReadSearchCapture(capture);
                try { AssertSearchLabelPixelsChanged(labels[5],restored,minimized); }
                finally { Object.Destroy(restored); }
            }
            finally { Object.Destroy(minimized); }
        }

        private static Texture2D ReadSearchCapture(WindowDesktopCapture capture)
        {
            Assert.That(capture.CaptureTexture,Is.Not.Null);
            var previous=RenderTexture.active;
            var result=new Texture2D(capture.CaptureTexture.width,capture.CaptureTexture.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=capture.CaptureTexture;
                result.ReadPixels(new Rect(0,0,result.width,result.height),0,0);
                result.Apply();
                return result;
            }
            finally { RenderTexture.active=previous; }
        }

        private static void AssertSearchLabelPixelsChanged(TextMeshPro label,Texture2D shown,Texture2D hidden)
        {
            var corners=new Vector3[4];
            label.rectTransform.GetWorldCorners(corners);
            var camera=DesktopWindowSpace.Instance.Camera;
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach (var corner in corners)
            {
                var uv=camera.WorldToViewportPoint(corner);
                var pixel=new Vector2(uv.x*shown.width,uv.y*shown.height);
                min=Vector2.Min(min,pixel); max=Vector2.Max(max,pixel);
            }
            int changed=0;
            for (var y=Mathf.Max(0,Mathf.FloorToInt(min.y));y<Mathf.Min(shown.height,Mathf.CeilToInt(max.y));y++)
                for (var x=Mathf.Max(0,Mathf.FloorToInt(min.x));x<Mathf.Min(shown.width,Mathf.CeilToInt(max.x));x++)
                {
                    var a=shown.GetPixel(x,y); var b=hidden.GetPixel(x,y);
                    if (Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.15f) changed++;
                }
            Assert.That(changed,Is.GreaterThan(8),label.name+" must produce visible pixels in the actual WINDOW.exe capture.");
        }

        [UnityTest]
        public IEnumerator WindowDesktopBackdropIsScreenAlignedAndKeepsSecondaryContent()
        {
            foreach (var resolution in new[] { new Vector2Int(1280,720), new Vector2Int(1920,1080) })
            {
                SetResolution(resolution.x,resolution.y);
                for (var frame=0;frame<5;frame++) yield return null;
                var manager=WindowManager.Instance;
                var window=manager.Open("window");
                Assert.That(window,Is.Not.Null);
                window.MoveTo(Vector2.zero);
                var capture=window.GetComponent<WindowDesktopCapture>();
                Assert.That(capture,Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(capture.DesktopBackground),
                    Is.EqualTo("Assets/Art/Screen/Window_Desktop.png"));
                for (var frame=0;frame<5;frame++) yield return null;
                Assert.That(capture.CaptureTexture,Is.Not.Null);
                Assert.That(capture.CaptureTexture.width,Is.EqualTo(Screen.width));
                Assert.That(capture.CaptureTexture.height,Is.EqualTo(Screen.height));
                var originalTexture=capture.CaptureTexture;
                var desktopPixel=ReadReveal(capture,new Vector2(.2f,.8f));
                Assert.That(desktopPixel.r,Is.GreaterThan(desktopPixel.b+.15f),desktopPixel.ToString());
                Directory.CreateDirectory("Builds/WindowDesktopReveal");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Builds/WindowDesktopReveal/background-{crtQuality}-{resolution.x}x{resolution.y}.png"));
                for (var frame=0;frame<4;frame++) yield return null;

                var source=manager.Open("memo");
                source.MoveTo(new Vector2(1,1));
                source.SetSecondarySprite(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/WindowWhite.png"));
                source.transform.Find("Secondary").GetComponent<SpriteRenderer>().color=Color.green;
                manager.FocusWindow("window");
                window.MoveTo(new Vector2(1,1));
                window.Resize(new Vector2(6,4));
                for (var frame=0;frame<5;frame++) yield return null;
                Assert.That(capture.CaptureTexture,Is.SameAs(originalTexture),"Moving/resizing reuses the screen capture.");
                var uv=DesktopWindowSpace.Instance.Camera.WorldToViewportPoint(source.transform.position);
                var secondary=ReadReveal(capture,uv);
                Assert.That(secondary.g,Is.GreaterThan(.8f),secondary.ToString());
                Assert.That(secondary.r,Is.LessThan(.2f),"Primary text/background must not enter the private capture.");
                var stable=ReadReveal(capture,new Vector2(.2f,.8f));
                Assert.That(Vector4.Distance(desktopPixel,stable),Is.LessThan(.015f),"The backdrop must stay fixed to the screen.");

                var folder="Builds/WindowDesktopReveal";
                Directory.CreateDirectory(folder);
                var path=Path.GetFullPath($"{folder}/reveal-{crtQuality}-{resolution.x}x{resolution.y}.png");
                ScreenCapture.CaptureScreenshot(path);
                for (var frame=0;frame<5;frame++) yield return null;
                var screen=new Texture2D(2,2);
                try
                {
                    screen.LoadImage(File.ReadAllBytes(path));
                    var actual=screen.GetPixel(Mathf.RoundToInt(uv.x*(screen.width-1)),Mathf.RoundToInt(uv.y*(screen.height-1)));
                    Assert.That(actual.g,Is.GreaterThan(actual.r+.3f),"WINDOW.exe must display the captured Secondary at its screen position: "+actual);
                    var outside=screen.GetPixel(Mathf.RoundToInt(screen.width*.92f),Mathf.RoundToInt(screen.height*.5f));
                    Assert.That(outside.b,Is.GreaterThan(outside.r+.3f),"The normal desktop must stay blue outside WINDOW.exe: "+outside);
                }
                finally { Object.Destroy(screen); }
                manager.Minimize("memo");
                for (var frame=0;frame<3;frame++) yield return null;
                var revealedBackground=ReadReveal(capture,uv);
                Assert.That(revealedBackground.r,Is.GreaterThan(revealedBackground.g+.15f),"Minimized Secondary must disappear.");
                manager.Minimize("window");
                Assert.That(capture.CaptureTexture==null,Is.True,"Minimizing must release the capture.");
                manager.Enshow("window");
                for (var frame=0;frame<3;frame++) yield return null;
                Assert.That(capture.CaptureTexture,Is.Not.Null);
                manager.ResetWindows();
                for (var frame=0;frame<3;frame++) yield return null;
                foreach (var item in Resources.FindObjectsOfTypeAll<RenderTexture>())
                    Assert.That(item.name,Is.Not.EqualTo("WINDOW.exe Desktop Capture"),"Closing must release owned render textures.");
                foreach (var item in Resources.FindObjectsOfTypeAll<Material>())
                    Assert.That(item.name,Is.Not.EqualTo("WINDOW.exe Secondary Capture"),"Closing must release owned materials.");
                foreach (var item in Resources.FindObjectsOfTypeAll<Mesh>())
                    Assert.That(item.name,Is.Not.EqualTo("WINDOW.exe Secondary Sprite"),"Closing must release owned sprite meshes.");
            }
        }

        private static Color ReadReveal(WindowDesktopCapture capture, Vector2 uv)
        {
            var previous=RenderTexture.active;
            var pixel=new Texture2D(1,1,TextureFormat.RGBA32,false);
            try
            {
                var texture=capture.CaptureTexture;
                RenderTexture.active=texture;
                pixel.ReadPixels(new Rect(Mathf.Clamp((int)(uv.x*texture.width),0,texture.width-1),
                    Mathf.Clamp((int)(uv.y*texture.height),0,texture.height-1),1,1),0,0);
                pixel.Apply();
                return pixel.GetPixel(0,0);
            }
            finally { RenderTexture.active=previous; Object.DestroyImmediate(pixel); }
        }
    }
}
