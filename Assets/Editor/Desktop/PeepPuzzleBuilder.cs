using System;
using UnityEditor;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Editor
{
    public static class PeepPuzzleBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Desktop/Windows/PeepPuzzleWindow.prefab";

        [MenuItem("WINDOW.exe/Peep Puzzle/Setup Peep Puzzle in Selected Window", false, 10)]
        public static void SetupInSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Peep Puzzle Builder", "하이어라키에서 PeepPuzzleWindow 오브젝트를 선택한 뒤 실행해 주세요.", "확인");
                return;
            }

            var puzzleWindow = selected.GetComponent<PeepPuzzleWindow>();
            if (puzzleWindow == null)
            {
                puzzleWindow = selected.AddComponent<PeepPuzzleWindow>();
            }

            SetupPeepWindowHierarchy(puzzleWindow);
            EditorUtility.SetDirty(selected);
            Debug.Log("[PeepPuzzleBuilder] PeepPuzzleWindow hierarchy setup complete for: " + selected.name);
        }

        [MenuItem("WINDOW.exe/Peep Puzzle/Create Peep Puzzle Window Prefab", false, 11)]
        public static void CreatePeepPuzzlePrefab()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");

            EnsureFolder("Assets/Prefabs/Desktop/Windows");

            var go = new GameObject("PeepPuzzleWindow");
            var puzzleWindow = go.AddComponent<PeepPuzzleWindow>();
            SetupPeepWindowHierarchy(puzzleWindow);

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = savedPrefab;
            Debug.Log("[PeepPuzzleBuilder] Created new PeepPuzzleWindow prefab at: " + PrefabPath);
        }

        public static void SetupPeepWindowHierarchy(PeepPuzzleWindow window)
        {
            window.InitializeVisuals();

            // 1. Secondary Transform 탐색/생성
            var secondary = window.transform.Find("Secondary");
            if (secondary == null)
            {
                secondary = new GameObject("Secondary").transform;
                secondary.SetParent(window.transform, false);
            }

            var secondaryRenderer = secondary.GetComponent<SpriteRenderer>();
            if (secondaryRenderer == null)
            {
                secondaryRenderer = secondary.gameObject.AddComponent<SpriteRenderer>();
            }
            secondaryRenderer.sortingLayerName = Window.SecondarySortingLayerName;
            secondaryRenderer.sortingOrder = 0;

            // 2. 4개의 단서 오브젝트 (Clues)
            var clues = new PeepPuzzleObject[4];
            var cluePositions = new[]
            {
                new Vector2(-1.5f, 0.8f),
                new Vector2(-1.0f, -0.6f),
                new Vector2(1.2f, 0.6f),
                new Vector2(0.6f, -0.7f)
            };

            for (var i = 0; i < 4; i++)
            {
                var clueObj = GetOrCreatePuzzleChild(secondary, $"Clue_{i + 1}", cluePositions[i], PeepObjectType.Clue, new Color(0.9f, 0.8f, 0.2f, 0.7f));
                clues[i] = clueObj;
            }

            // 3. 봐서는 안 되는 오브젝트 (Danger)
            var dangerObj = GetOrCreatePuzzleChild(secondary, "Danger", new Vector2(0.0f, 0.5f), PeepObjectType.Danger, new Color(0.9f, 0.2f, 0.2f, 0.7f));

            // 4. 주기적으로 봐야 하는 오브젝트 (Watcher)
            var watcherObj = GetOrCreatePuzzleChild(secondary, "Watcher", new Vector2(-1.8f, -1.0f), PeepObjectType.Watcher, new Color(0.2f, 0.6f, 0.9f, 0.7f));

            // 5. 탈출 오브젝트 (Escape) - 더 이상 사용하지 않으므로 기존 자식이 있으면 비활성화
            var existingEscape = secondary.Find("Escape");
            if (existingEscape != null) existingEscape.gameObject.SetActive(false);

            // 6. PeepPuzzleWindow에 연결
            window.SetupPuzzleObjects(clues, new[] { dangerObj }, new[] { watcherObj }, null);
            window.DangerMaxTime = 3.0f;
            window.WatcherMaxUnseenTime = 15.0f;
            window.TargetWindowId = "window";
        }

        private static PeepPuzzleObject GetOrCreatePuzzleChild(Transform parent, string name, Vector2 localPos, PeepObjectType type, Color debugColor)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            child.localPosition = new Vector3(localPos.x, localPos.y, 0f);

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = Window.SecondarySortingLayerName;
            renderer.sortingOrder = 1;
            if (renderer.sprite == null) renderer.sprite = WindowTitleBar.WhiteSprite;
            renderer.color = debugColor;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.6f, 0.6f);

            var col = child.GetComponent<BoxCollider2D>();
            if (col == null) col = child.gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.6f, 0.6f);

            var puzzleObj = child.GetComponent<PeepPuzzleObject>();
            if (puzzleObj == null) puzzleObj = child.gameObject.AddComponent<PeepPuzzleObject>();
            puzzleObj.Setup(type, new Vector2(0.6f, 0.6f));

            return puzzleObj;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

