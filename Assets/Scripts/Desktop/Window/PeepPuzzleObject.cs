using System;
using UnityEngine;
using UnityEngine.Events;

namespace WindowExe.Desktop.Desktop.Window
{
    public enum PeepObjectType
    {
        Clue,
        Danger,
        Watcher,
        Escape
    }

    [DisallowMultipleComponent]
    public class PeepPuzzleObject : MonoBehaviour
    {
        [Header("Object Type")]
        [SerializeField] private PeepObjectType objectType = PeepObjectType.Clue;

        [Header("Sprites (선택적 수동 할당)")]
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite completedSprite;

        [Header("Audio (선택적 수동 할당 - 없으면 미재생)")]
        [SerializeField] private AudioClip clickSound;

        [Header("Bounds Settings")]
        [SerializeField] private Vector2 fallbackSize = new Vector2(0.8f, 0.8f);

        [Header("Events")]
        public UnityEvent onClicked;

        private SpriteRenderer spriteRenderer;
        private Collider2D objectCollider;

        public PeepObjectType ObjectType => objectType;
        public bool IsCollected { get; private set; }
        public bool IsUnlocked { get; private set; }
        public SpriteRenderer Renderer => spriteRenderer != null ? spriteRenderer : (spriteRenderer = GetComponent<SpriteRenderer>());
        public Collider2D Collider => objectCollider != null ? objectCollider : (objectCollider = GetComponent<Collider2D>());

        public Bounds WorldBounds
        {
            get
            {
                if (Collider != null && Collider.enabled)
                {
                    return Collider.bounds;
                }
                if (Renderer != null && Renderer.sprite != null)
                {
                    return Renderer.bounds;
                }
                return new Bounds(transform.position, new Vector3(fallbackSize.x, fallbackSize.y, 1f));
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            objectCollider = GetComponent<Collider2D>();
            UpdateVisuals();
        }

        public void Setup(PeepObjectType type, Vector2 size)
        {
            objectType = type;
            fallbackSize = size;
            UpdateVisuals();
        }

        public void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;
            UpdateVisuals();
        }

        public void SetCollected(bool collected)
        {
            IsCollected = collected;
            UpdateVisuals();
        }

        public bool ContainsPoint(Vector2 worldPoint)
        {
            if (!gameObject.activeInHierarchy) return false;
            if (Collider != null && Collider.enabled)
            {
                return Collider.OverlapPoint(worldPoint);
            }
            return WorldBounds.Contains(new Vector3(worldPoint.x, worldPoint.y, transform.position.z));
        }

        public bool Click()
        {
            if (!gameObject.activeInHierarchy) return false;

            if (objectType == PeepObjectType.Clue)
            {
                if (IsCollected) return false;
                IsCollected = true;
                UpdateVisuals();
                onClicked?.Invoke();
                return true;
            }

            if (objectType == PeepObjectType.Escape)
            {
                if (!IsUnlocked) return false;
                onClicked?.Invoke();
                return true;
            }

            onClicked?.Invoke();
            return true;
        }

        public void ResetState()
        {
            IsCollected = false;
            IsUnlocked = (objectType != PeepObjectType.Escape);
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (Renderer == null) return;

            Renderer.sortingLayerName = Window.SecondarySortingLayerName;

            if (objectType == PeepObjectType.Clue)
            {
                if (IsCollected && completedSprite != null)
                {
                    Renderer.sprite = completedSprite;
                }
                else if (defaultSprite != null)
                {
                    Renderer.sprite = defaultSprite;
                }
            }
            else if (objectType == PeepObjectType.Escape)
            {
                if (IsUnlocked && activeSprite != null)
                {
                    Renderer.sprite = activeSprite;
                }
                else if (defaultSprite != null)
                {
                    Renderer.sprite = defaultSprite;
                }
            }
            else
            {
                if (defaultSprite != null)
                {
                    Renderer.sprite = defaultSprite;
                }
            }
        }

        public AudioClip GetClickSound() => clickSound;
    }
}

