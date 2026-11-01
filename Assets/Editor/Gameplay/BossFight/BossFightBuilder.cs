using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace WindowExe.BossFight.Editor
{
    public static class BossFightBuilder
    {
        [MenuItem("WINDOW.exe/Boss Fight/Connect Desktop Lock Warning")]
        public static void ConnectDesktopLockWarning()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Desktop.unity") throw new System.InvalidOperationException("Open Desktop.unity first.");
            var rules=AssetDatabase.LoadAssetAtPath<BossFightSettings>("Assets/Data/Gameplay/BossFight/BossFightSettings.asset");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/BossFight/BossFightWindow.prefab");
            if(rules==null || prefab==null) throw new System.InvalidOperationException("Boss settings/prefab missing.");
            var bridge=Object.FindFirstObjectByType<LockWarningBossEntry>();
            var root=bridge!=null?bridge.gameObject:new GameObject("Lock warning boss entry");
            var controller=root.GetComponent<BossFightController>()??root.AddComponent<BossFightController>();
            controller.ConfigureFullFight(rules,prefab.GetComponent<BossFightWindow>());
            if(bridge==null)bridge=root.AddComponent<LockWarningBossEntry>();
            bridge.Configure(controller);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        [MenuItem("WINDOW.exe/Boss Fight/Create Full Fight Validation Scene")]
        public static void CreateFullValidation()
        {
            CreatePhase2Validation();
            var rules=AssetDatabase.LoadAssetAtPath<BossFightSettings>("Assets/Data/Gameplay/BossFight/BossFightSettings.asset");
            const string phasePath="Assets/Data/Gameplay/BossFight/Phase1.asset";
            var phase=AssetDatabase.LoadAssetAtPath<BossPhaseDefinition>(phasePath);
            if(phase==null){phase=ScriptableObject.CreateInstance<BossPhaseDefinition>();AssetDatabase.CreateAsset(phase,phasePath);}
            rules.phase1=phase;rules.hiddenViewShader=Shader.Find("WINDOW.exe/BossHiddenView");
            rules.phase1EyeSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/눈깔풀.png");
            rules.phase1PupilSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/빨강이.png");
            rules.phase1HiddenBackgroundSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/Phase_1.png");
            rules.phase1ErrorSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Warning/Cannot_Move.png");
            rules.phase1IntroSprite=System.Array.Find(AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Warning/One_more.png"),asset=>asset is Sprite sprite && sprite.name=="One_more_0") as Sprite;
            rules.phase1DownloadSprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Warning/Downloading.png").OfType<Sprite>().OrderByDescending(sprite=>sprite.rect.width*sprite.rect.height).FirstOrDefault();
            rules.phase1EntryEyeFrames=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Screen/BossFight/Boss_Start_Eyes_frames.png").OfType<Sprite>().OrderBy(sprite=>sprite.name).ToArray();
            rules.phase1EntryFrameSeconds=new double[]{.8,.1,.1,.1,.24,.1,.12,.1,.6,.26,.32,.32,1.2};
            rules.phase1EntryErrorSprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Warning/Error.png").OfType<Sprite>().OrderByDescending(sprite=>sprite.rect.width*sprite.rect.height).FirstOrDefault();
            rules.entryDownloadSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/삑.mp3");
            rules.entryEyeSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/띵에러.mp3");
            rules.entryErrorSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/에러(띵).mp3");
            rules.phaseTransitionDosSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/Dos.png");
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");int layer=LayerMask.NameToLayer("BossHidden");
            if(layer<0)for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {layer=i;layers.GetArrayElementAtIndex(i).stringValue="BossHidden";tags.ApplyModifiedPropertiesWithoutUndo();break;}
            if(layer<0)throw new System.InvalidOperationException("No free layer for the isolated boss world.");rules.hiddenLayer=layer;
            const string rendererPath="Assets/Data/Gameplay/BossFight/BossHiddenRenderer.asset";
            var renderer=AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererPath);
            if(renderer==null)
            {renderer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Renderer2D.asset"));renderer.name="BossHiddenRenderer";AssetDatabase.CreateAsset(renderer,rendererPath);}
            var rendererData=new SerializedObject(renderer);rendererData.FindProperty("m_UseCameraSortingLayersTexture").boolValue=false;
            rendererData.FindProperty("m_LayerMask").intValue=1<<layer;rendererData.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(renderer);
            var pipeline=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/UniversalRP.asset");var pipelineData=new SerializedObject(pipeline);
            var renderers=pipelineData.FindProperty("m_RendererDataList");int rendererIndex=-1;
            for(int i=0;i<renderers.arraySize;i++)if(renderers.GetArrayElementAtIndex(i).objectReferenceValue==renderer)rendererIndex=i;
            if(rendererIndex<0){rendererIndex=renderers.arraySize;renderers.arraySize++;renderers.GetArrayElementAtIndex(rendererIndex).objectReferenceValue=renderer;pipelineData.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(pipeline);}
            rules.hiddenRendererIndex=rendererIndex;EditorUtility.SetDirty(rules);AssetDatabase.SaveAssetIfDirty(rules);
            var scene=SceneManager.GetActiveScene();EditorSceneManager.SaveScene(scene,"Assets/Scenes/BossFightFullValidation.unity",true);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/BossFightFullValidation.unity");
            var controller=Object.FindFirstObjectByType<BossFightController>();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/BossFight/BossFightWindow.prefab").GetComponent<BossFightWindow>();
            controller.ConfigureFullFight(rules,prefab,true);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            new GameObject("Boss Validation Diagnostics").AddComponent<BossValidationDiagnostics>();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("WINDOW.exe/Boss Fight/Create Phase 2 Validation Scene")]
        public static void CreatePhase2Validation()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save existing scene changes before creating validation assets.");
            System.IO.Directory.CreateDirectory("Assets/Data/Gameplay/BossFight");
            System.IO.Directory.CreateDirectory("Assets/Prefabs/Gameplay/BossFight");
            AssetDatabase.Refresh();
            const string rulesPath="Assets/Data/Gameplay/BossFight/BossFightSettings.asset";
            var rules=AssetDatabase.LoadAssetAtPath<BossFightSettings>(rulesPath);
            if(rules==null)
            {
                rules=ScriptableObject.CreateInstance<BossFightSettings>();
                rules.trashSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icon/bin.png");
                rules.bossSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icon/wndow.png");
                AssetDatabase.CreateAsset(rules,rulesPath);
            }
            rules.eyeSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/creature/Iris.png");
            rules.pupilSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/creature/Red_Pupil_2.png");
            rules.phase2BackgroundSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/Phase_1.png");
            rules.normalEndingVideo=AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>("Assets/Art/Screen/Normal_Ending.mp4");
            rules.badEndingVideo=AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>("Assets/Art/Screen/Bad_Ending.mp4");
            rules.phase2SuccessSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/깨진삐.mp3");
            rules.phase1DropSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/노이즈(깜놀).mp3");
            rules.phase1EyeContactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/삐.mp3");
            rules.phase2EyeContactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/깨진삐.mp3");
            rules.phase1Music=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/1페이즈 보스.mp3");
            rules.phase2Music=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/2페이즈 보스.mp3");
            rules.phase2SpacebarSprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Warning/Spacebar.png").OfType<Sprite>().OrderByDescending(sprite=>sprite.rect.width*sprite.rect.height).FirstOrDefault();
            if(!(rules.phase2FloatingEyes??System.Array.Empty<BossStaticEye>()).Any(eye=>eye.center.x==960 && eye.center.y==530))
                rules.phase2FloatingEyes=(rules.phase2FloatingEyes??System.Array.Empty<BossStaticEye>()).Concat(new[]{new BossStaticEye(960,530,70)}).ToArray();
            EditorUtility.SetDirty(rules);
            const string prefabPath="Assets/Prefabs/Gameplay/BossFight/BossFightWindow.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(prefab==null)
            {
                var template=new GameObject("BossFightWindow");template.SetActive(false);template.AddComponent<BossFightWindow>();
                prefab=PrefabUtility.SaveAsPrefabAsset(template,prefabPath);Object.DestroyImmediate(template);
            }
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/BossFightValidation.unity",true);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/BossFightValidation.unity");
            // Desktop now carries a real entry controller; validation owns a separate auto-start session.
            foreach(var entry in Object.FindObjectsByType<LockWarningBossEntry>(FindObjectsInactive.Include))
            {
                var desktopFight=entry.GetComponent<BossFightController>();
                if(desktopFight!=null)Object.DestroyImmediate(desktopFight);
                Object.DestroyImmediate(entry);
            }
            new GameObject("Boss Fight Session").AddComponent<BossFightController>().Configure(rules,prefab.GetComponent<BossFightWindow>(),true);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssetIfDirty(rules);
        }
        [MenuItem("WINDOW.exe/Boss Fight/Create Input Validation Scene")]
        public static void CreateInputValidation()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save existing scene changes before creating the validation scene.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Validation camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
            new GameObject("Boss Input Probe", typeof(BossInputProbe));
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BossInputValidation.unity");
        }
    }
}
