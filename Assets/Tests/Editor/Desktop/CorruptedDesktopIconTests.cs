using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    public sealed class CorruptedDesktopIconDataTests
    {
        [Test]
        public void FourDesktopItemsHaveSeparateCorruptedSprites()
        {
            var mappings=new[]{new[]{"recycle-bin","bin"},new[]{"memo","Icon_Memo"},
                new[]{"peep-window","Image"},new[]{"internet","internet"}};
            foreach(var row in mappings)
            {
                var item=AssetDatabase.LoadAssetAtPath<DesktopItemDefinition>($"Assets/Data/Desktop/{row[0]}.asset");
                Assert.That(item.Icon!=null,Is.True);
                Assert.That(item.CorruptedIcon!=null,Is.True,"The mapped variant must resolve to an imported Sprite.");
                var normal = row[0] == "peep-window"
                    ? AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Icon/Image.png").OfType<Sprite>().Single(s => s.name == "Image_1")
                    : AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Icon/{row[1]}.png");
                Assert.That(item.Icon,Is.SameAs(normal));
                Assert.That(item.CorruptedIcon,Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Icon/{row[1]}-corrupted-red.png")));
                Assert.That(item.CorruptedIcon,Is.Not.SameAs(item.Icon));
            }
        }
    }

    public sealed partial class DesktopSceneInteractionTests
    {
        [UnityTest]
        public IEnumerator CorruptedDesktopIconsTrackMovementAndStayInsideWindow()
        {
            foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)})
            {
                SetResolution(resolution.x,resolution.y);
                for(var frame=0;frame<5;frame++) yield return null;
                var desktop=Object.FindAnyObjectByType<DesktopController>();
                var icon=desktop.Icons.First(i=>i.Item.ItemId=="memo");
                var original=icon.Item; var originalName=icon.NameLabel.text;
                var selection=(Image)typeof(DesktopIconView).GetField("selection",Flags).GetValue(icon);
                var beforePosition=icon.Rect.anchoredPosition;
                var blue=new Texture2D(2,2); blue.SetPixels(Enumerable.Repeat(Color.blue,4).ToArray()); blue.Apply();
                var green=new Texture2D(2,2); green.SetPixels(Enumerable.Repeat(Color.green,4).ToArray()); green.Apply();
                var normal=Sprite.Create(blue,new Rect(0,0,2,2),Vector2.one*.5f);
                var corrupted=Sprite.Create(green,new Rect(0,0,2,2),Vector2.one*.5f);
                var changed=Sprite.Create(blue,new Rect(0,0,2,2),Vector2.one*.5f);
                var temporary=ScriptableObject.CreateInstance<DesktopItemDefinition>();
                temporary.Configure("memo","변형 테스트",normal); temporary.SetCorruptedIcon(corrupted);
                var manager=WindowManager.Instance;
                try
                {
                    icon.Configure(desktop,temporary,selection,icon.IconImage,icon.NameLabel);
                    var window=manager.Open("window"); window.Maximize();
                    var capture=window.GetComponent<WindowDesktopCapture>();
                    for(var frame=0;frame<5;frame++) yield return null;
                    var uv=GraphicUv(icon.IconImage);
                    var pixel=ReadReveal(capture,uv);
                    Assert.That(pixel.g,Is.GreaterThan(pixel.b+.5f),pixel.ToString());
                    Assert.That(icon.IconImage.sprite,Is.SameAs(normal),"The normal UI sprite must not be swapped.");
                    Assert.That(icon.NameLabel.font,Is.SameAs(GameTypography.Settings.UiFont));

                    temporary.SetCorruptedIcon(null);
                    for(var frame=0;frame<3;frame++) yield return null;
                    pixel=ReadReveal(capture,uv);
                    Assert.That(pixel.b,Is.GreaterThan(pixel.g+.5f),"No variant must fall back to the current icon: "+pixel);
                    temporary.SetCorruptedIcon(corrupted);
                    icon.SetIconSprite(changed);
                    for(var frame=0;frame<3;frame++) yield return null;
                    pixel=ReadReveal(capture,uv);
                    Assert.That(pixel.b,Is.GreaterThan(pixel.g+.5f),"A runtime-changed icon must not use a stale variant.");
                    icon.SetIconSprite(normal);
                    icon.Rect.anchoredPosition+=new Vector2(300,-100);
                    for(var frame=0;frame<3;frame++) yield return null;
                    var moved=GraphicUv(icon.IconImage);
                    pixel=ReadReveal(capture,moved);
                    Assert.That(pixel.g,Is.GreaterThan(pixel.b+.5f),"The reveal must follow the moved icon: "+pixel);
                    pixel=ReadReveal(capture,uv);
                    Assert.That(pixel.r,Is.GreaterThan(pixel.g+.15f),"The old position must return to the red backdrop.");

                    window.Restore();
                    window.MoveTo(new Vector2(DesktopWindowSpace.Instance.Bounds.xMax,0));
                    for(var frame=0;frame<5;frame++) yield return null;
                    Directory.CreateDirectory("Builds/CorruptedDesktopIcons");
                    var path=Path.GetFullPath($"Builds/CorruptedDesktopIcons/outside-{crtQuality}-{resolution.x}x{resolution.y}.png");
                    ScreenCapture.CaptureScreenshot(path);
                    for(var frame=0;frame<5;frame++) yield return null;
                    var screenshot=new Texture2D(2,2);
                    try
                    {
                        screenshot.LoadImage(File.ReadAllBytes(path));
                        pixel=screenshot.GetPixel((int)(moved.x*screenshot.width),(int)(moved.y*screenshot.height));
                        Assert.That(pixel.b,Is.GreaterThan(pixel.g+.3f),"Outside WINDOW.exe the original icon must stay blue: "+pixel);
                    }
                    finally { Object.Destroy(screenshot); }
                }
                finally
                {
                    manager.ResetWindows();
                    icon.Rect.anchoredPosition=beforePosition;
                    icon.Configure(desktop,original,selection,icon.IconImage,icon.NameLabel);
                    icon.SetDisplayName(originalName);
                    Object.Destroy(temporary); Object.Destroy(normal); Object.Destroy(corrupted); Object.Destroy(changed);
                    Object.Destroy(blue); Object.Destroy(green);
                }
                yield return null;
            }
        }

        private static Vector2 GraphicUv(Graphic graphic)
        {
            var camera=DesktopWindowSpace.Instance.Camera;
            var canvas=graphic.canvas;
            var source=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var point=RectTransformUtility.WorldToScreenPoint(source,graphic.rectTransform.TransformPoint(graphic.GetPixelAdjustedRect().center));
            return new Vector2((point.x-camera.pixelRect.x)/camera.pixelWidth,(point.y-camera.pixelRect.y)/camera.pixelHeight);
        }

    }
}
