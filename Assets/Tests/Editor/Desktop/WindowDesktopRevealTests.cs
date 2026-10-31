using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    public sealed partial class DesktopSceneInteractionTests
    {
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
