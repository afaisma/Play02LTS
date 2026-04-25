using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ReadingBuddy.UI
{
    public enum PuzzleDisplayMode
    {
        Unpuzzled,
        Puzzled
    }

    public enum PuzzleModePolicy
    {
        Manual,
        AutoBySolvedState
    }

    [Serializable]
    public class PuzzlePiecePlacedUnityEvent : UnityEvent<int, int, bool> { }   // pieceIndex, slotIndex, isCorrect

    [Serializable]
    public class PuzzlePiecePickedUpUnityEvent : UnityEvent<int, int, bool> { } // pieceIndex, slotIndex, wasCorrect

    [Serializable]
    public class PuzzleSolvedUnityEvent : UnityEvent { }

    [Serializable]
    public class PuzzleStateChangedUnityEvent : UnityEvent<bool> { } // solved

    /// <summary>
    /// Semantic subclass of UnityEngine.UI.Image.
    /// Behaves like Image and remains compatible with existing Image-based code.
    /// </summary>
    public class StoryImage : Image
    {
        // Intentionally empty.
    }

    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(CanvasGroup))]
    public class PuzzlePieceUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private PuzzleImage _owner;
        private RectTransform _rectTransform;
        private Image _image;
        private CanvasGroup _canvasGroup;

        public int CorrectSlotIndex { get; private set; } = -1;
        public int CurrentSlotIndex { get; private set; } = -1;
        public int DragStartSlotIndex { get; private set; } = -1;
        public bool IsDragging { get; private set; }

        public RectTransform RectTransform => _rectTransform;
        public Image Image => _image;

        public void Initialize(PuzzleImage owner, int correctSlotIndex)
        {
            _owner = owner;
            CorrectSlotIndex = correctSlotIndex;

            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;
        }

        internal void SetCurrentSlotIndex(int slotIndex)
        {
            CurrentSlotIndex = slotIndex;
        }

        internal void SnapTo(Vector2 anchoredPosition, Vector2 size)
        {
            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.sizeDelta = size;
            _rectTransform.anchoredPosition = anchoredPosition;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.localRotation = Quaternion.identity;
        }

        internal void ApplyVisualStyle(Color color, Material material, bool maskable)
        {
            _image.color = color;
            _image.material = material;
            _image.maskable = maskable;
            _image.raycastTarget = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_owner == null || !_owner.CanDragPieces)
                return;

            IsDragging = true;
            DragStartSlotIndex = CurrentSlotIndex;

            _canvasGroup.blocksRaycasts = false;
            transform.SetAsLastSibling();

            _owner.OnPieceBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_owner == null || !IsDragging)
                return;

            _owner.OnPieceDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_owner == null || !IsDragging)
                return;

            IsDragging = false;
            _canvasGroup.blocksRaycasts = true;

            _owner.OnPieceEndDrag(this, eventData);
        }
    }

    /// <summary>
    /// StoryImage + puzzle functionality.
    /// Unpuzzled = normal Image rendering.
    /// Puzzled = draggable tile puzzle.
    /// </summary>
    public class PuzzleImage : StoryImage
    {
        [Header("Mode")]
        [SerializeField] private bool _isPuzzled = false;
        [SerializeField] private PuzzleModePolicy _modePolicy = PuzzleModePolicy.Manual;
        [SerializeField] private bool _isSolved = false;

        [Header("Puzzle Grid")]
        [SerializeField, Min(1)] private int _rows = 3;
        [SerializeField, Min(1)] private int _columns = 3;
        [SerializeField, Min(0f)] private float _gap = 4f;

        [Header("Puzzle Behavior")]
        [SerializeField] private bool _shuffleWhenEnteringPuzzled = true;
        [SerializeField] private int _shuffleSeed = 0; // 0 = random each time
        [SerializeField, Range(0.1f, 2f)] private float _snapDistanceFactor = 0.85f;

        [Header("Slot Visuals")]
        [SerializeField] private bool _showSlotFrames = true;
        [SerializeField] private Color _slotFrameColor = new Color(1f, 1f, 1f, 0.18f);

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
        [SerializeField] private AudioClip _pickupClip;
        [SerializeField] private AudioClip _placeClip;
        [SerializeField] private AudioClip _correctPlaceClip;
        [SerializeField] private AudioClip _wrongPlaceClip;
        [SerializeField, Range(1, 5)] private int _solvedSoundCount = 2;

        private AudioClip[] _solvedSoundPool;

        [Header("Events")]
        [SerializeField] private PuzzlePiecePlacedUnityEvent _onPiecePlaced = new PuzzlePiecePlacedUnityEvent();
        [SerializeField] private PuzzlePiecePickedUpUnityEvent _onPiecePickedUp = new PuzzlePiecePickedUpUnityEvent();
        [SerializeField] private PuzzleSolvedUnityEvent _onPuzzleSolved = new PuzzleSolvedUnityEvent();
        [SerializeField] private PuzzleStateChangedUnityEvent _onPuzzleStateChanged = new PuzzleStateChangedUnityEvent();

        // Optional C# callbacks
        public event Action<PuzzleImage, int, int, bool> PiecePlacedEvent;      // owner, pieceIndex, slotIndex, isCorrect
        public event Action<PuzzleImage, int, int, bool> PiecePickedUpEvent;     // owner, pieceIndex, slotIndex, wasCorrect
        public event Action<PuzzleImage> PuzzleSolvedEvent;                      // owner
        public event Action<PuzzleImage, bool> PuzzleStateChangedEvent;          // owner, solved

        [SerializeField, HideInInspector] private RectTransform _slotsRoot;
        [SerializeField, HideInInspector] private RectTransform _piecesRoot;

        private readonly List<RectTransform> _slotRects = new List<RectTransform>();
        private readonly List<Image> _slotImages = new List<Image>();
        private readonly List<PuzzlePieceUI> _pieces = new List<PuzzlePieceUI>();
        private readonly List<Sprite> _generatedPieceSprites = new List<Sprite>();

        private PuzzlePieceUI[] _pieceBySlot = Array.Empty<PuzzlePieceUI>();
        private Vector2[] _slotPositions = Array.Empty<Vector2>();
        private Vector2 _cellSize;

        private Sprite _lastSourceSprite;
        private Vector2 _lastRectSize;
        private Color _lastColor;

        private bool _cachedBaseRaycastTargetValid;
        private bool _cachedBaseRaycastTarget;
        private bool _pendingRebuild;

        public PuzzleDisplayMode Mode
        {
            get => _isPuzzled ? PuzzleDisplayMode.Puzzled : PuzzleDisplayMode.Unpuzzled;
            set
            {
                bool newIsPuzzled = value == PuzzleDisplayMode.Puzzled;
                if (_isPuzzled == newIsPuzzled) return;
                _isPuzzled = newIsPuzzled;
                RefreshVisuals(forceRebuild: false);
            }
        }

        public PuzzleModePolicy ModePolicy
        {
            get => _modePolicy;
            set
            {
                if (_modePolicy == value) return;
                _modePolicy = value;
                RefreshVisuals(forceRebuild: false);
            }
        }

        public bool IsSolved
        {
            get => _isSolved;
            private set
            {
                if (_isSolved == value) return;
                _isSolved = value;
            }
        }

        public bool IsPuzzled
        {
            get => EffectiveMode == PuzzleDisplayMode.Puzzled;
            set
            {
                if (value) SetPuzzled();
                else SetUnpuzzled();
            }
        }

        public int Rows
        {
            get => _rows;
            set
            {
                int clamped = Mathf.Max(1, value);
                if (_rows == clamped) return;
                _rows = clamped;
                RefreshVisuals(forceRebuild: true);
            }
        }

        public int Columns
        {
            get => _columns;
            set
            {
                int clamped = Mathf.Max(1, value);
                if (_columns == clamped) return;
                _columns = clamped;
                RefreshVisuals(forceRebuild: true);
            }
        }

        public float Gap
        {
            get => _gap;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(_gap, clamped)) return;
                _gap = clamped;
                RefreshVisuals(forceRebuild: true);
            }
        }

        public bool CanDragPieces => EffectiveMode == PuzzleDisplayMode.Puzzled;

        private PuzzleDisplayMode EffectiveMode =>
            _modePolicy == PuzzleModePolicy.AutoBySolvedState
                ? (_isSolved ? PuzzleDisplayMode.Unpuzzled : PuzzleDisplayMode.Puzzled)
                : (_isPuzzled ? PuzzleDisplayMode.Puzzled : PuzzleDisplayMode.Unpuzzled);

        public void SetPuzzled()
        {
            SetSolvedState(false, raiseEvents: true, playSolvedSfx: false);

            bool wasUnpuzzled = EffectiveMode != PuzzleDisplayMode.Puzzled;
            Mode = PuzzleDisplayMode.Puzzled;

            // RefreshVisuals skips AssignInitialPieceSlots when pieces are already built,
            // so explicitly reshuffle when transitioning from unpuzzled.
            if (wasUnpuzzled && _pieces.Count > 0)
            {
                AssignInitialPieceSlots();
                LayoutSlotsAndPieces();
            }
        }

        public void SetUnpuzzled()
        {
            Mode = PuzzleDisplayMode.Unpuzzled;
        }

        public void ResetPuzzle(bool reshuffle = true)
        {
            SetSolvedState(false, raiseEvents: true, playSolvedSfx: false);

            if (EffectiveMode != PuzzleDisplayMode.Puzzled)
                return;

            if (reshuffle)
                AssignInitialPieceSlots();

            LayoutSlotsAndPieces();
        }

        public void ShufflePieces()
        {
            if (EffectiveMode != PuzzleDisplayMode.Puzzled)
                return;

            SetSolvedState(false, raiseEvents: true, playSolvedSfx: false);
            AssignInitialPieceSlots();
            LayoutSlotsAndPieces();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_solvedSoundPool == null)
                _solvedSoundPool = Resources.LoadAll<AudioClip>("PuzzleSounds");
            EnsureAudioSource();
            EnsureRoots();
            RefreshVisuals(forceRebuild: true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetBaseVisible(true);

            if (_slotsRoot != null) _slotsRoot.gameObject.SetActive(false);
            if (_piecesRoot != null) _piecesRoot.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ClearGeneratedContent();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _rows    = Mathf.Max(1, _rows);
            _columns = Mathf.Max(1, _columns);
            _gap     = Mathf.Max(0f, _gap);

            // Defer scene manipulation out of OnValidate to avoid re-entrancy:
            // creating/destroying GameObjects inside OnValidate can re-trigger
            // OnValidate before the first call finishes, causing children to pile up.
            UnityEditor.EditorApplication.delayCall -= EditorDelayedRefresh;
            UnityEditor.EditorApplication.delayCall += EditorDelayedRefresh;
        }

        private void EditorDelayedRefresh()
        {
            UnityEditor.EditorApplication.delayCall -= EditorDelayedRefresh;
            if (this == null || !isActiveAndEnabled) return;

            if (EffectiveMode == PuzzleDisplayMode.Puzzled)
                EnsureTextureReadable(GetSourceSprite());

            EnsureRoots();
            RefreshVisuals(forceRebuild: true);
        }

        private static void EnsureTextureReadable(Sprite src)
        {
            if (src == null || src.texture.isReadable) return;

            string path = UnityEditor.AssetDatabase.GetAssetPath(src.texture);
            if (string.IsNullOrEmpty(path)) return;

            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (importer == null || importer.isReadable) return;

            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"[PuzzleImage] Auto-enabled Read/Write on '{src.texture.name}' for puzzle mode.");
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (!isActiveAndEnabled)
                return;

            if (EffectiveMode == PuzzleDisplayMode.Puzzled)
                LayoutSlotsAndPieces();
        }

        private void LateUpdate()
        {
            var src = GetSourceSprite();
            var currentSize = rectTransform.rect.size;

            if (src != _lastSourceSprite)
            {
                // Defer rebuild if a drag is in progress — destroying pieces mid-drag
                // causes MissingReferenceExceptions from the EventSystem.
                if (IsAnyPieceDragging())
                {
                    _pendingRebuild = true;
                }
                else
                {
                    _pendingRebuild = false;
                    RefreshVisuals(forceRebuild: true);
                }
                return;
            }

            if (_pendingRebuild && !IsAnyPieceDragging())
            {
                _pendingRebuild = false;
                RefreshVisuals(forceRebuild: true);
                return;
            }

            if (currentSize != _lastRectSize)
            {
                _lastRectSize = currentSize;
                if (EffectiveMode == PuzzleDisplayMode.Puzzled)
                    LayoutSlotsAndPieces();
            }

            // If layout hasn't completed yet (zero-size container at init), retry each
            // frame until it succeeds so _cellSize is valid before any drag begins.
            if (EffectiveMode == PuzzleDisplayMode.Puzzled
                && _cellSize == Vector2.zero
                && _pieces.Count > 0)
            {
                LayoutSlotsAndPieces();
            }

            if (color != _lastColor)
            {
                _lastColor = color;
                if (EffectiveMode == PuzzleDisplayMode.Puzzled)
                    SyncPieceStyle();
            }
        }

        private bool IsAnyPieceDragging()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i] != null && _pieces[i].IsDragging)
                    return true;
            }
            return false;
        }

        private void EnsureAudioSource()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            if (_audioSource == null) return;

            _audioSource.PlayOneShot(clip, _sfxVolume);
        }

        private IEnumerator PlayRandomSolvedSounds()
        {
            if (_audioSource == null || _solvedSoundPool == null || _solvedSoundPool.Length == 0)
                yield break;

            var pool = new List<AudioClip>(_solvedSoundPool);
            var rng = new System.Random();
            int count = Mathf.Min(_solvedSoundCount, pool.Count);

            for (int i = 0; i < count; i++)
            {
                int j = rng.Next(i, pool.Count);
                AudioClip tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;

                _audioSource.PlayOneShot(pool[i], _sfxVolume * 0.7f);
                yield return new WaitForSeconds(pool[i].length);
            }
        }

        private Sprite GetSourceSprite()
        {
            return overrideSprite != null ? overrideSprite : sprite;
        }

        private void RefreshVisuals(bool forceRebuild)
        {
            EnsureRoots();

            var src = GetSourceSprite();
            _lastSourceSprite = src;
            _lastRectSize = rectTransform.rect.size;
            _lastColor = color;

            if (src == null)
            {
                SetBaseVisible(true);
                _slotsRoot.gameObject.SetActive(false);
                _piecesRoot.gameObject.SetActive(false);
                return;
            }

            if (EffectiveMode == PuzzleDisplayMode.Unpuzzled)
            {
                ClearGeneratedContent();
                SetBaseVisible(true);
                _slotsRoot.gameObject.SetActive(false);
                _piecesRoot.gameObject.SetActive(false);
                return;
            }

            if (!src.texture.isReadable)
            {
                Debug.LogWarning(
                    $"[PuzzleImage] Texture '{src.texture.name}' is not readable — " +
                    "showing as normal image. Enable Read/Write in the texture import settings to use puzzle mode.", this);
                SetBaseVisible(true);
                _slotsRoot.gameObject.SetActive(false);
                _piecesRoot.gameObject.SetActive(false);
                return;
            }

            SetBaseVisible(false);

            bool needsRebuild =
                forceRebuild ||
                _pieces.Count != _rows * _columns ||
                _slotRects.Count != _rows * _columns;

            if (needsRebuild)
            {
                BuildPuzzleObjects(src);
                AssignInitialPieceSlots();
            }

            SyncPieceStyle();
            SyncSlotStyle();
            LayoutSlotsAndPieces();

            _slotsRoot.gameObject.SetActive(true);
            _piecesRoot.gameObject.SetActive(true);
        }

        private void EnsureRoots()
        {
            if (_slotsRoot == null)
            {
                var slots = transform.Find("__PuzzleSlots");
                if (slots == null)
                {
                    var slotsGo = new GameObject("__PuzzleSlots", typeof(RectTransform));
                    _slotsRoot = slotsGo.GetComponent<RectTransform>();
                    _slotsRoot.SetParent(transform, false);
                }
                else
                {
                    _slotsRoot = (RectTransform)slots;
                }

                StretchToParent(_slotsRoot);
            }

            if (_piecesRoot == null)
            {
                var pieces = transform.Find("__PuzzlePieces");
                if (pieces == null)
                {
                    var piecesGo = new GameObject("__PuzzlePieces", typeof(RectTransform));
                    _piecesRoot = piecesGo.GetComponent<RectTransform>();
                    _piecesRoot.SetParent(transform, false);
                }
                else
                {
                    _piecesRoot = (RectTransform)pieces;
                }

                StretchToParent(_piecesRoot);
                _piecesRoot.SetAsLastSibling();
            }

            // Destroy any duplicate roots that accumulated from previous reloads.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == _slotsRoot || child == _piecesRoot) continue;
                if (child.name == "__PuzzleSlots" || child.name == "__PuzzlePieces")
                    SafeDestroy(child.gameObject);
            }
        }

        private void BuildPuzzleObjects(Sprite src)
        {
            ClearGeneratedContent();
            EnsureRoots();

            int count = _rows * _columns;
            _pieceBySlot = new PuzzlePieceUI[count];
            _slotPositions = new Vector2[count];

            // Create slot visuals
            for (int i = 0; i < count; i++)
            {
                var slotGo = new GameObject("Slot_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var slotRt = slotGo.GetComponent<RectTransform>();
                slotRt.SetParent(_slotsRoot, false);

                var slotImg = slotGo.GetComponent<Image>();
                slotImg.raycastTarget = false;
                slotImg.color = _slotFrameColor;
                slotImg.type = Type.Simple;

                _slotRects.Add(slotRt);
                _slotImages.Add(slotImg);
            }

            // Create puzzle pieces by slicing the source sprite texture rect
            Rect texRect = src.textureRect;
            float tileW = texRect.width / _columns;
            float tileH = texRect.height / _rows;

            int pieceIndex = 0;
            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _columns; col++)
                {
                    Rect subRect = new Rect(
                        texRect.x + col * tileW,
                        texRect.y + ((_rows - 1 - row) * tileH), // top row visually at top
                        tileW,
                        tileH
                    );

                    Sprite pieceSprite = Sprite.Create(
                        src.texture,
                        subRect,
                        new Vector2(0.5f, 0.5f),
                        src.pixelsPerUnit
                    );

                    _generatedPieceSprites.Add(pieceSprite);

                    var pieceGo = new GameObject("Piece_" + pieceIndex,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image),
                        typeof(CanvasGroup),
                        typeof(PuzzlePieceUI));

                    var pieceRt = pieceGo.GetComponent<RectTransform>();
                    pieceRt.SetParent(_piecesRoot, false);

                    var pieceImg = pieceGo.GetComponent<Image>();
                    pieceImg.sprite = pieceSprite;
                    pieceImg.type = Type.Simple;
                    pieceImg.preserveAspect = false;
                    pieceImg.raycastTarget = true;

                    var pieceComp = pieceGo.GetComponent<PuzzlePieceUI>();
                    pieceComp.Initialize(this, pieceIndex);

                    _pieces.Add(pieceComp);
                    pieceIndex++;
                }
            }
        }

        private void AssignInitialPieceSlots()
        {
            int count = _rows * _columns;
            _pieceBySlot = new PuzzlePieceUI[count];

            var slots = new List<int>(count);
            for (int i = 0; i < count; i++) slots.Add(i);

            Shuffle(slots, Environment.TickCount);

            for (int i = 0; i < _pieces.Count; i++)
            {
                PlacePieceInSlot(_pieces[i], slots[i], snapImmediately: false);
            }
        }

        private void LayoutSlotsAndPieces()
        {
            if (_slotRects.Count == 0 || _pieces.Count == 0)
                return;

            Rect rect = rectTransform.rect;
            float totalGapX = _gap * (_columns - 1);
            float totalGapY = _gap * (_rows - 1);

            float cellW = (rect.width - totalGapX) / _columns;
            float cellH = (rect.height - totalGapY) / _rows;

            if (cellW <= 0f || cellH <= 0f)
                return;

            _cellSize = new Vector2(cellW, cellH);

            float startX = -rect.width * 0.5f + (cellW * 0.5f);
            float startY = rect.height * 0.5f - (cellH * 0.5f);

            for (int index = 0; index < _slotRects.Count; index++)
            {
                int row = index / _columns;
                int col = index % _columns;

                float x = startX + col * (cellW + _gap);
                float y = startY - row * (cellH + _gap);

                _slotPositions[index] = new Vector2(x, y);

                RectTransform slotRt = _slotRects[index];
                slotRt.anchorMin = new Vector2(0.5f, 0.5f);
                slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.sizeDelta = _cellSize;
                slotRt.anchoredPosition = _slotPositions[index];
                slotRt.localScale = Vector3.one;
                slotRt.localRotation = Quaternion.identity;
            }

            for (int i = 0; i < _pieces.Count; i++)
            {
                PuzzlePieceUI piece = _pieces[i];
                if (piece == null || piece.IsDragging) continue;

                int slotIndex = piece.CurrentSlotIndex;
                if (slotIndex < 0 || slotIndex >= _slotPositions.Length) continue;

                piece.SnapTo(_slotPositions[slotIndex], _cellSize);
            }
        }

        private void PlacePieceInSlot(PuzzlePieceUI piece, int slotIndex, bool snapImmediately)
        {
            if (piece == null) return;
            if (slotIndex < 0 || slotIndex >= _pieceBySlot.Length) return;

            int previousSlot = piece.CurrentSlotIndex;
            if (previousSlot >= 0 && previousSlot < _pieceBySlot.Length && _pieceBySlot[previousSlot] == piece)
            {
                _pieceBySlot[previousSlot] = null;
            }

            _pieceBySlot[slotIndex] = piece;
            piece.SetCurrentSlotIndex(slotIndex);

            if (snapImmediately && slotIndex < _slotPositions.Length)
            {
                piece.SnapTo(_slotPositions[slotIndex], _cellSize);
            }
        }

        private void SwapPieces(PuzzlePieceUI a, PuzzlePieceUI b)
        {
            if (a == null || b == null) return;

            int slotA = a.CurrentSlotIndex;
            int slotB = b.CurrentSlotIndex;

            PlacePieceInSlot(a, slotB, snapImmediately: true);
            PlacePieceInSlot(b, slotA, snapImmediately: true);
        }

        private void SyncPieceStyle()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                if (piece == null) continue;
                piece.ApplyVisualStyle(color, material, maskable);
            }
        }

        private void SyncSlotStyle()
        {
            for (int i = 0; i < _slotImages.Count; i++)
            {
                var img = _slotImages[i];
                if (img == null) continue;

                img.enabled = _showSlotFrames;
                img.color = _slotFrameColor;
                img.maskable = maskable;
                img.raycastTarget = false;
            }
        }

        private void SetBaseVisible(bool visible)
        {
            if (!_cachedBaseRaycastTargetValid)
            {
                _cachedBaseRaycastTarget = raycastTarget;
                _cachedBaseRaycastTargetValid = true;
            }

            bool changed = canvasRenderer.cull == visible; // cull is opposite of visible
            canvasRenderer.cull = !visible;
            raycastTarget = visible ? _cachedBaseRaycastTarget : false;

            // Mirror what MaskableGraphic.UpdateCull does internally: after changing cull,
            // mark vertices dirty so the canvas repopulates the mesh. Without this, un-culling
            // after the first hide leaves the renderer with empty geometry and the image stays blank.
            if (changed)
                SetVerticesDirty();
        }

        private void ClearGeneratedContent()
        {
            if (_slotsRoot != null)
                DestroyAllChildren(_slotsRoot);

            if (_piecesRoot != null)
                DestroyAllChildren(_piecesRoot);

            _slotRects.Clear();
            _slotImages.Clear();
            _pieces.Clear();

            for (int i = 0; i < _generatedPieceSprites.Count; i++)
            {
                if (_generatedPieceSprites[i] != null)
                    SafeDestroy(_generatedPieceSprites[i]);
            }
            _generatedPieceSprites.Clear();

            _pieceBySlot = Array.Empty<PuzzlePieceUI>();
            _slotPositions = Array.Empty<Vector2>();
            _cellSize = Vector2.zero;
        }

        private static void DestroyAllChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                SafeDestroy(parent.GetChild(i).gameObject);
            }
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(obj);
            else
                UnityEngine.Object.Destroy(obj);
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }

        private static void Shuffle<T>(IList<T> list, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private void NotifyPiecePickedUp(PuzzlePieceUI piece)
        {
            if (piece == null) return;

            int pieceIndex = piece.CorrectSlotIndex; // stable identifier
            int slotIndex = piece.CurrentSlotIndex;
            bool wasCorrect = slotIndex == piece.CorrectSlotIndex;

            _onPiecePickedUp?.Invoke(pieceIndex, slotIndex, wasCorrect);
            PiecePickedUpEvent?.Invoke(this, pieceIndex, slotIndex, wasCorrect);

            PlaySfx(_pickupClip);
        }

        private void NotifyPiecePlaced(PuzzlePieceUI piece)
        {
            if (piece == null) return;

            int pieceIndex = piece.CorrectSlotIndex; // stable identifier
            int slotIndex = piece.CurrentSlotIndex;
            bool isCorrect = slotIndex == piece.CorrectSlotIndex;

            _onPiecePlaced?.Invoke(pieceIndex, slotIndex, isCorrect);
            PiecePlacedEvent?.Invoke(this, pieceIndex, slotIndex, isCorrect);

            PlaySfx(_placeClip);
            if (isCorrect)
                PlaySfx(_correctPlaceClip);
            else
                PlaySfx(_wrongPlaceClip);
        }

        private bool SetSolvedState(bool solvedNow, bool raiseEvents, bool playSolvedSfx)
        {
            bool changed = solvedNow != IsSolved;
            IsSolved = solvedNow;

            if (!changed || !raiseEvents)
                return changed;

            _onPuzzleStateChanged?.Invoke(solvedNow);
            PuzzleStateChangedEvent?.Invoke(this, solvedNow);

            if (solvedNow)
            {
                _onPuzzleSolved?.Invoke();
                PuzzleSolvedEvent?.Invoke(this);

                if (playSolvedSfx)
                    StartCoroutine(PlayRandomSolvedSounds());
            }

            return changed;
        }

        // ------------------------------
        // Drag API (called by PuzzlePieceUI)
        // ------------------------------

        internal void OnPieceBeginDrag(PuzzlePieceUI piece, PointerEventData eventData)
        {
            NotifyPiecePickedUp(piece);
        }

        internal void OnPieceDrag(PuzzlePieceUI piece, PointerEventData eventData)
        {
            if (piece == null || _piecesRoot == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _piecesRoot,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                piece.RectTransform.anchoredPosition = localPoint;
            }
        }

        internal void OnPieceEndDrag(PuzzlePieceUI piece, PointerEventData eventData)
        {
            if (piece == null)
                return;

            // Ensure layout is valid — _cellSize may be zero if the container hadn't
            // finished layout when the puzzle was first built.
            if (_cellSize == Vector2.zero)
                LayoutSlotsAndPieces();

            int originalSlot = piece.DragStartSlotIndex;
            int nearestSlot = FindNearestSlot(piece.RectTransform.anchoredPosition, out float distance);

            float snapThreshold = Mathf.Min(_cellSize.x, _cellSize.y) * _snapDistanceFactor;
            if (nearestSlot < 0 || distance > snapThreshold)
            {
                nearestSlot = originalSlot; // too far -> snap back
            }

            PuzzlePieceUI occupant = (nearestSlot >= 0 && nearestSlot < _pieceBySlot.Length)
                ? _pieceBySlot[nearestSlot]
                : null;

            if (occupant == null || occupant == piece)
            {
                PlacePieceInSlot(piece, nearestSlot, snapImmediately: true);
                NotifyPiecePlaced(piece);
            }
            else
            {
                // Swap and notify both
                SwapPieces(piece, occupant);
                NotifyPiecePlaced(piece);
                NotifyPiecePlaced(occupant);
            }

            EvaluateSolvedState();
        }

        private int FindNearestSlot(Vector2 position, out float distance)
        {
            int bestIndex = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < _slotPositions.Length; i++)
            {
                float d = (_slotPositions[i] - position).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    bestIndex = i;
                }
            }

            distance = bestIndex >= 0 ? Mathf.Sqrt(bestDistSq) : float.MaxValue;
            return bestIndex;
        }

        private void EvaluateSolvedState()
        {
            bool solvedNow = true;

            for (int i = 0; i < _pieces.Count; i++)
            {
                PuzzlePieceUI piece = _pieces[i];
                if (piece == null || piece.CurrentSlotIndex != piece.CorrectSlotIndex)
                {
                    solvedNow = false;
                    break;
                }
            }

            bool changed = SetSolvedState(solvedNow, raiseEvents: true, playSolvedSfx: true);
            if (!changed)
                return;

            if (_modePolicy == PuzzleModePolicy.AutoBySolvedState)
            {
                RefreshVisuals(forceRebuild: false);
            }
        }
    }
}
