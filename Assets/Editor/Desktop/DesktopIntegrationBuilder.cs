using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WindowExe.Desktop.Desktop.Window;
using W = WindowExe.Desktop.Desktop.Window.Window;

namespace WindowExe.Desktop.Editor
{
    public static class DesktopIntegrationBuilder
    {
        private const string Data = "Assets/Data/Desktop";
        private const string Prefabs = "Assets/Prefabs/Desktop/Windows";
        [MenuItem("WINDOW.exe/Desktop/Upgrade Window Integration")]
        public static void Upgrade()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var desktop=UnityEngine.Object.FindAnyObjectByType<DesktopController>();
            if(desktop==null||desktop.gameObject.scene.isDirty) throw new InvalidOperationException("Open and save the desktop scene first.");
            Directory.CreateDirectory("Builds/DesktopValidation/WindowIntegration");
            var backup="Builds/DesktopValidation/WindowIntegration/Desktop-before-integration.unity";
            if(!File.Exists(backup)) File.Copy(desktop.gameObject.scene.path,backup);
            Install(desktop);
            EditorSceneManager.MarkSceneDirty(desktop.gameObject.scene);
            EditorSceneManager.SaveScene(desktop.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        public static void Install(DesktopController desktop)
        {
            Directory.CreateDirectory(Prefabs); AssetDatabase.Refresh();
            var cam=Camera.main;
            if(cam==null) throw new InvalidOperationException("Main Camera is missing.");
            cam.transform.SetPositionAndRotation(new Vector3(0,0,-10),Quaternion.identity);
            cam.orthographic=true; cam.orthographicSize=5.4f; cam.nearClipPlane=.1f; cam.farClipPlane=100;
            var canvas=desktop.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=cam; canvas.planeDistance=20;
            canvas.sortingLayerName="Background"; canvas.sortingOrder=0;
            var scene=desktop.gameObject.scene;
            var bar=UnityEngine.Object.FindObjectsByType<TaskbarController>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.gameObject.scene==scene);
            var overlay=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="TaskbarCanvas");
            if(overlay==null) overlay=new GameObject("TaskbarCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(DesktopFont));
            overlay.layer=5;
            overlay.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            bar.transform.SetParent(overlay.transform,false);
            var barRect=(RectTransform)bar.transform; barRect.anchorMin=Vector2.zero; barRect.anchorMax=new Vector2(1,0); barRect.pivot=new Vector2(.5f,0); barRect.anchoredPosition=Vector2.zero; barRect.sizeDelta=new Vector2(0,48);
            var scroll=bar.GetComponent<ScrollRect>();
            if(!(scroll is TaskbarScrollRect))
            {
                var viewport=scroll.viewport; var content=scroll.content;
                UnityEngine.Object.DestroyImmediate(scroll);
                scroll=bar.gameObject.AddComponent<TaskbarScrollRect>(); scroll.viewport=viewport; scroll.content=content;
                scroll.horizontal=true; scroll.vertical=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
            }
            foreach(var arrowName in new[]{"ScrollLeft","ScrollRight"})
            {
                var go=bar.transform.Find(arrowName).gameObject;
                var old=go.GetComponent<Button>();
                if(!(old is TaskbarInputButton)) { UnityEngine.Object.DestroyImmediate(old); var b=go.AddComponent<TaskbarInputButton>(); b.targetGraphic=go.GetComponent<Image>(); b.navigation=new Navigation{mode=Navigation.Mode.None}; }
            }
            var root=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="WindowWorldRoot");
            if(root==null) root=new GameObject("WindowWorldRoot");
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); root.transform.localScale=Vector3.one;
            var manager=root.GetComponent<WindowManager>(); if(manager==null) manager=root.AddComponent<WindowManager>();
            var entries=CreatePrefabs(); manager.Configure(root.transform,entries);
            var source=root.GetComponent<WindowTaskbarSource>(); if(source==null) source=root.AddComponent<WindowTaskbarSource>(); source.Configure(manager);
            var launcher=desktop.GetComponent<DesktopWindowLauncher>(); if(launcher==null) launcher=desktop.gameObject.AddComponent<DesktopWindowLauncher>(); launcher.Configure(desktop,manager);
            var demo=desktop.GetComponent<TaskbarDemoSource>(); if(demo!=null) UnityEngine.Object.DestroyImmediate(demo);
            var workArea=desktop.GetComponent<DesktopWorkArea>(); workArea.Configure(desktop,barRect);
            var space=root.GetComponent<DesktopWindowSpace>(); if(space==null) space=root.AddComponent<DesktopWindowSpace>(); space.Configure(cam,workArea,manager);
            var router=root.GetComponent<DesktopInputRouter>(); if(router==null) router=root.AddComponent<DesktopInputRouter>(); router.Configure(manager,desktop,barRect,space);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(DesktopSceneBuilder.TaskbarPrefabPath).GetComponent<TaskbarButtonView>();
            bar.Configure(source,scroll.viewport,scroll.content,prefab,bar.transform.Find("ScrollLeft").GetComponent<Button>(),bar.transform.Find("ScrollRight").GetComponent<Button>());
            TaskbarUiFactory.ApplyAppearance(bar);
            foreach(var orphan in UnityEngine.Object.FindObjectsByType<W>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(orphan.gameObject.scene==scene&&orphan.transform.parent!=root.transform) UnityEngine.Object.DestroyImmediate(orphan.gameObject);
            Canvas.ForceUpdateCanvases(); space.Refresh(); desktop.EnsureValidLayout();
            CRTOverlayBuilder.Install(scene);
        }
        private static void EnsureTypography(TMP_FontAsset font)
        {
            const string folder=Data+"/Resources";
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Data,"Resources");
            var moves=new[]{
                new[]{"Assets/TextMesh Pro/Resources/TMP Settings.asset",folder+"/TMP Settings.asset"},
                new[]{"Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset",folder+"/WindowDefaultStyle.asset"},
                new[]{"Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt",folder+"/LineBreaking Leading Characters.txt"},
                new[]{"Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt",folder+"/LineBreaking Following Characters.txt"}
            };
            foreach(var move in moves) if(AssetDatabase.LoadMainAssetAtPath(move[1])==null&&AssetDatabase.LoadMainAssetAtPath(move[0])!=null)
            { var error=AssetDatabase.MoveAsset(move[0],move[1]); if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error); }
            var config=AssetDatabase.LoadAssetAtPath<TMP_Settings>(folder+"/TMP Settings.asset");
            if(config==null) { config=ScriptableObject.CreateInstance<TMP_Settings>(); config.name="TMP Settings"; AssetDatabase.CreateAsset(config,folder+"/TMP Settings.asset"); }
            var serialized=new SerializedObject(config);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue=font;
            serialized.FindProperty("m_defaultSpriteAsset").objectReferenceValue=null;
            serialized.FindProperty("m_defaultStyleSheet").objectReferenceValue=AssetDatabase.LoadMainAssetAtPath(folder+"/WindowDefaultStyle.asset");
            serialized.FindProperty("m_leadingCharacters").objectReferenceValue=AssetDatabase.LoadMainAssetAtPath(folder+"/LineBreaking Leading Characters.txt");
            serialized.FindProperty("m_followingCharacters").objectReferenceValue=AssetDatabase.LoadMainAssetAtPath(folder+"/LineBreaking Following Characters.txt");
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(config);
            GameTypographyEditor.EnsureSettings(font);
        }
        private static WindowPrefabEntry[] CreatePrefabs()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Data+"/WindowUnlit.mat");
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material,Data+"/WindowUnlit.mat"); }
            const string whitePath="Assets/Art/Screen/WindowWhite.png";
            if(!File.Exists(whitePath))
            {
                var texture=new Texture2D(4,4,TextureFormat.RGBA32,false);
                texture.SetPixels(Enumerable.Repeat(Color.white,16).ToArray()); texture.Apply();
                File.WriteAllBytes(whitePath,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(whitePath);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(whitePath);
            importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=4;
            importer.spriteBorder=new Vector4(1,1,1,1); importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var white=AssetDatabase.LoadAssetAtPath<Sprite>(whitePath);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Data+"/WindowKoreanFont.asset");
            if(font==null)
            {
                font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/neodgm.ttf"),90,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                font.name="WindowKoreanFont"; AssetDatabase.CreateAsset(font,Data+"/WindowKoreanFont.asset");
                font.material.name="WindowKoreanFont Material"; AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var atlas in font.atlasTextures) { atlas.name="WindowKoreanFont Atlas"; AssetDatabase.AddObjectToAsset(atlas,font); }
            }
            EnsureTypography(font);
            var ids=new[]{"recycle-bin","memo","photos","internet"};
            var body=new[]{"휴지통\n검증용 창입니다. 삭제 퍼즐은 아직 연결되지 않았습니다.","메모\n창을 움직이고 크기를 조절해 보세요.\n작업 표시줄로 최소화한 창을 복원할 수 있습니다.","사진","인터넷\n검증용 안내 창입니다. 실제 웹 탐색 기능은 제공하지 않습니다."};
            var entries=new WindowPrefabEntry[ids.Length];
            for(var i=0;i<ids.Length;i++)
            {
                var definition=AssetDatabase.LoadAssetAtPath<DesktopItemDefinition>(Data+"/"+ids[i]+".asset");
                var go=new GameObject("Window_"+ids[i]); go.SetActive(false);
                W window=i==2?(W)go.AddComponent<ImageWindow>():go.AddComponent<NotepadWindow>();
                window.ConfigureAppearance(definition.DisplayName,definition.Icon,font);
                if(window is NotepadWindow note) { note.SetBackgroundSprite(white); note.SetText(body[i]); }
                if(window is ImageWindow image) image.SetPrimarySprite(definition.Icon);
                foreach(var sprite in go.GetComponentsInChildren<SpriteRenderer>(true))
                { sprite.sharedMaterial=material; if(sprite.sprite==WindowTitleBar.WhiteSprite) sprite.sprite=white; }
                window.ApplyGeometry(Vector2.zero,new Vector2(5.2f,3.4f));
                var saved=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"/"+ids[i]+".prefab");
                UnityEngine.Object.DestroyImmediate(go);
                entries[i]=new WindowPrefabEntry{id=ids[i],prefab=saved.GetComponent<W>()};
            }
            if(AssetDatabase.LoadAssetAtPath<Sprite>(Data+"/WindowWhite.asset")!=null) AssetDatabase.DeleteAsset(Data+"/WindowWhite.asset");
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
            return entries;
        }
    }
}
