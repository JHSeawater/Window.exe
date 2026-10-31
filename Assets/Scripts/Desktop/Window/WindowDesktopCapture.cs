using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>A screen-aligned backdrop and visible Secondary sprites/text, for WINDOW.exe only.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(WindowExeWindow))]
    public sealed class WindowDesktopCapture : MonoBehaviour
    {
        private static readonly int CaptureId = Shader.PropertyToID("_WindowDesktopTexture");
        [SerializeField] private Texture2D desktopBackground;
        [SerializeField] private Shader secondaryCaptureShader;
        private WindowExeWindow owner;
        private RenderTexture texture;
        private Material spriteMaterial;
        private CommandBuffer commands;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock spriteProperties;
        private DesktopController desktop;
        private readonly Dictionary<Sprite, Mesh> meshes = new Dictionary<Sprite, Mesh>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Renderer> sortedRenderers = new List<Renderer>();
        private readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        private readonly List<Material> textMaterials = new List<Material>();
        public Texture2D DesktopBackground => desktopBackground;
        public RenderTexture CaptureTexture => texture;

        public void Configure(Texture2D background, Shader captureShader)
        {
            ReleaseResources();
            desktopBackground = background;
            secondaryCaptureShader = captureShader;
        }

        private void OnEnable()
        {
            owner = GetComponent<WindowExeWindow>();
            desktop = FindAnyObjectByType<DesktopController>();
            if (properties == null) properties = new MaterialPropertyBlock();
            if (spriteProperties == null) spriteProperties = new MaterialPropertyBlock();
            RenderPipelineManager.beginCameraRendering -= Render;
            RenderPipelineManager.beginCameraRendering += Render;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            ReleaseResources();
        }

        private void Render(ScriptableRenderContext context, Camera camera)
        {
            var space = DesktopWindowSpace.Instance;
            if (space == null || camera != space.Camera || !space.Valid || owner == null
                || owner.ViewportRenderer == null || !owner.ViewportRenderer.enabled
                || desktopBackground == null || secondaryCaptureShader == null
                || !secondaryCaptureShader.isSupported || camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
                return;
            if (owner.ViewportRenderer.sharedMaterial == null
                || !owner.ViewportRenderer.sharedMaterial.HasProperty(CaptureId)) return;

            if (texture == null || texture.width != camera.pixelWidth || texture.height != camera.pixelHeight)
            {
                ReleaseTexture();
                texture = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 0, RenderTextureFormat.ARGB32)
                { name = "WINDOW.exe Desktop Capture", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                texture.Create();
            }
            if (spriteMaterial == null)
                spriteMaterial = new Material(secondaryCaptureShader)
                { name = "WINDOW.exe Secondary Capture", hideFlags = HideFlags.HideAndDontSave };
            if (commands == null) commands = new CommandBuffer { name = "WINDOW.exe Desktop Reveal" };

            sortedRenderers.Clear();
            var manager = WindowManager.Instance;
            if (manager != null)
                foreach (var process in manager.DisplayOrder)
                {
                    var window = process.Instance;
                    if (window == null || !window.gameObject.activeInHierarchy) continue;
                    var secondary = window.transform.Find("Secondary");
                    if (secondary == null) continue;
                    // TMP normally rebuilds before camera rendering. Update changed labels here too,
                    // since this private capture can run before TMP's normal render callback.
                    labels.Clear();
                    secondary.GetComponentsInChildren(false, labels);
                    foreach (var label in labels)
                        if (label.isActiveAndEnabled && label.havePropertiesChanged)
                            label.ForceMeshUpdate();
                    renderers.Clear();
                    secondary.GetComponentsInChildren(false, renderers);
                    foreach (var renderer in renderers)
                    {
                        if (!renderer.enabled || renderer.forceRenderingOff
                            || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                        if (renderer is SpriteRenderer sprite)
                        {
                            if (sprite.sprite != null) sortedRenderers.Add(sprite);
                        }
                        else if (renderer is MeshRenderer
                            && (renderer.GetComponent<TextMeshPro>() != null
                                || renderer.GetComponent<TMP_SubMesh>() != null))
                            sortedRenderers.Add(renderer);
                    }
                }
            sortedRenderers.Sort(CompareRenderers);

            var previous = RenderTexture.active;
            try
            {
                // The backdrop covers the camera viewport; moving the window reveals a different crop.
                Graphics.Blit(desktopBackground, texture);
                commands.Clear();
                commands.SetRenderTarget(texture);
                commands.SetViewport(new Rect(0, 0, texture.width, texture.height));
                DrawDesktopIcons(camera);
                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,
                    // The consumer uses bottom-left screen UVs, just like the backdrop blit.
                    GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
                foreach (var renderer in sortedRenderers)
                {
                    if (renderer is SpriteRenderer sprite) DrawSprite(sprite);
                    else DrawText(renderer);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { RenderTexture.active = previous; }

            owner.ViewportRenderer.GetPropertyBlock(properties);
            properties.SetTexture(CaptureId, texture);
            owner.ViewportRenderer.SetPropertyBlock(properties);
        }

        private static int CompareRenderers(Renderer left, Renderer right)
        {
            var layer = SortingLayer.GetLayerValueFromID(left.sortingLayerID)
                .CompareTo(SortingLayer.GetLayerValueFromID(right.sortingLayerID));
            return layer != 0 ? layer : left.sortingOrder.CompareTo(right.sortingOrder);
        }

        private void DrawText(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) return;
            textMaterials.Clear();
            renderer.GetSharedMaterials(textMaterials);
            for (var submesh = 0; submesh < filter.sharedMesh.subMeshCount && submesh < textMaterials.Count; submesh++)
                if (textMaterials[submesh] != null)
                    commands.DrawRenderer(renderer, textMaterials[submesh], submesh, 0);
        }

        private void DrawSprite(SpriteRenderer renderer)
        {
            var sprite = renderer.sprite;
            var mesh = GetSpriteMesh(sprite);
            var scale = Vector3.one;
            if (renderer.drawMode != SpriteDrawMode.Simple)
            {
                scale.x = renderer.size.x / sprite.bounds.size.x;
                scale.y = renderer.size.y / sprite.bounds.size.y;
            }
            if (renderer.flipX) scale.x = -scale.x;
            if (renderer.flipY) scale.y = -scale.y;
            spriteProperties.Clear();
            spriteProperties.SetTexture("_MainTex", sprite.texture);
            spriteProperties.SetColor("_RendererColor", renderer.color);
            spriteProperties.SetVector("_Flip", Vector4.one);
            commands.DrawMesh(mesh, renderer.localToWorldMatrix * Matrix4x4.Scale(scale), spriteMaterial, 0, 0, spriteProperties);
        }

        private Mesh GetSpriteMesh(Sprite sprite)
        {
            if (!meshes.TryGetValue(sprite, out var mesh))
            {
                var source = sprite.vertices;
                var vertices = new Vector3[source.Length];
                var colors = new Color32[source.Length];
                for (var i = 0; i < source.Length; i++) { vertices[i] = source[i]; colors[i] = Color.white; }
                var sourceTriangles = sprite.triangles;
                var triangles = new int[sourceTriangles.Length];
                for (var i = 0; i < triangles.Length; i++) triangles[i] = sourceTriangles[i];
                mesh = new Mesh { name = "WINDOW.exe Secondary Sprite", hideFlags = HideFlags.HideAndDontSave };
                mesh.vertices = vertices; mesh.uv = sprite.uv; mesh.colors32 = colors; mesh.triangles = triangles;
                meshes.Add(sprite, mesh);
            }
            return mesh;
        }

        private void DrawDesktopIcons(Camera camera)
        {
            if (desktop == null) desktop = FindAnyObjectByType<DesktopController>();
            if (desktop == null || !desktop.gameObject.activeInHierarchy) return;
            commands.SetViewProjectionMatrices(Matrix4x4.identity,
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, 1, 0, 1, -1, 1), false));
            foreach (var icon in desktop.Icons)
            {
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                var image = icon.IconImage;
                if (image != null && image.isActiveAndEnabled && !image.canvasRenderer.cull)
                {
                    var sprite = image.overrideSprite;
                    if (sprite != null)
                    {
                        if (icon.Item != null && sprite == icon.Item.Icon && icon.Item.CorruptedIcon != null)
                            sprite = icon.Item.CorruptedIcon;
                        var rect = image.GetPixelAdjustedRect();
                        if (image.preserveAspect)
                        {
                            var ratio = sprite.rect.width / sprite.rect.height;
                            var size = rect.size;
                            if (size.x / size.y > ratio) size.x = size.y * ratio;
                            else size.y = size.x / ratio;
                            rect = new Rect(rect.center - size / 2, size);
                        }
                        var bounds = sprite.bounds;
                        var fit = Matrix4x4.TRS(rect.center, Quaternion.identity,
                            new Vector3(rect.width / bounds.size.x, rect.height / bounds.size.y, 1))
                            * Matrix4x4.Translate(-bounds.center);
                        spriteProperties.Clear();
                        spriteProperties.SetTexture("_MainTex", sprite.texture);
                        var color = image.color; color.a *= image.canvasRenderer.GetInheritedAlpha();
                        spriteProperties.SetColor("_RendererColor", color);
                        spriteProperties.SetVector("_Flip", Vector4.one);
                        commands.DrawMesh(GetSpriteMesh(sprite), ScreenMatrix(image, camera) * fit,
                            spriteMaterial, 0, 0, spriteProperties);
                    }
                }
            }
        }

        private static Matrix4x4 ScreenMatrix(Graphic graphic, Camera camera)
        {
            var canvas = graphic.canvas;
            var sourceCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector3 Project(Vector3 local)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, graphic.rectTransform.TransformPoint(local));
                return new Vector3((screen.x - camera.pixelRect.x) / camera.pixelWidth,
                    (screen.y - camera.pixelRect.y) / camera.pixelHeight, 0);
            }
            var origin = Project(Vector3.zero);
            var x = Project(Vector3.right) - origin;
            var y = Project(Vector3.up) - origin;
            var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, new Vector4(x.x, x.y, 0, 0));
            matrix.SetColumn(1, new Vector4(y.x, y.y, 0, 0));
            matrix.SetColumn(3, new Vector4(origin.x, origin.y, 0, 1));
            return matrix;
        }

        private void ReleaseTexture()
        {
            if (owner != null && owner.ViewportRenderer != null && properties != null)
            {
                owner.ViewportRenderer.GetPropertyBlock(properties);
                properties.SetTexture(CaptureId, Texture2D.blackTexture);
                owner.ViewportRenderer.SetPropertyBlock(properties);
            }
            if (texture == null) return;
            texture.Release();
            DestroyOwned(texture);
            texture = null;
        }

        private void ReleaseResources()
        {
            ReleaseTexture();
            if (spriteMaterial != null) DestroyOwned(spriteMaterial);
            spriteMaterial = null;
            commands?.Release();
            commands = null;
            foreach (var mesh in meshes.Values) if (mesh != null) DestroyOwned(mesh);
            meshes.Clear();
            renderers.Clear();
            sortedRenderers.Clear();
            labels.Clear();
            textMaterials.Clear();
        }

        private static void DestroyOwned(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
