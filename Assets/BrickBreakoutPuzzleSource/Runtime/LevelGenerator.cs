using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Generates arrow-block levels and handles taps that remove blocks with a clear exit path.</summary>
public sealed class LevelGenerator : MonoBehaviour
{
    [Header("Progression")]
    [SerializeField, Min(1)] private int startingLevel = 1;
    [SerializeField] private bool loadHighestUnlockedLevel = true;

    [Header("Puzzle")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField, Min(0.5f)] private float cellSize = 0.92f;
    [SerializeField, Range(0.02f, 0.2f)] private float blockGap = 0.07f;
    [SerializeField, Min(1f)] private float exitDistance = 2.5f;

    [Header("Materials")]
    [SerializeField] private Material litMaterialTemplate;
    [SerializeField] private Material unlitMaterialTemplate;

    [Header("Timer Challenge")]
    [SerializeField] private bool useSolveTimer = true;
    [SerializeField, Min(0f)] private float baseSolveTimeSeconds = 45f;
    [SerializeField, Min(0f)] private float extraSecondsPerBlock = 2f;

    [Header("Lives")]
    [SerializeField, Min(1)] private int startingLives = 3;

    public event Action<int> LevelLoaded;
    public event Action<int> LevelCompleted;
    public event Action<int> LevelTimerExpired;
    public event Action<int> LevelFailed;

    public int CurrentLevel => levelIndex + 1;
    public int RemainingBlocks => remaining;
    public int SuccessfulMoves => moves;
    public bool IsLevelComplete => completed;
    public bool IsTimerExpired => timerExpired;
    public int LivesRemaining => lifeManager != null ? lifeManager.LivesRemaining : 0;
    public bool IsOutOfLives => lifeManager != null && lifeManager.IsOutOfLives;
    public int HintsUsed => hintManager != null ? hintManager.HintsUsed : 0;
    public LifeManager Lives => lifeManager;
    public HintManager Hints => hintManager;
    public bool TimerEnabled => useSolveTimer;
    public float TimeLimitSeconds { get; private set; }
    public float TimeRemainingSeconds { get; private set; }
    public float ElapsedTimeSeconds { get; private set; }
    public string TapStatus { get; private set; } = "TAP STATUS: WAITING";

    [Header("Block Colors")]
    [SerializeField] private Color[] blockColors =
    {
        new Color(.05f, .72f, .57f), new Color(.96f, .24f, .19f), new Color(.98f, .66f, .08f),
        new Color(.14f, .43f, .95f), new Color(.65f, .22f, .91f), new Color(.96f, .20f, .58f), new Color(.03f, .72f, .83f)
    };
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    private BrickBlock[,] blocks;
    private Transform levelRoot;
    [SerializeField] private Camera gameCamera;
    [SerializeField] private CameraHandler cameraHandler;
    private LifeManager lifeManager;
    private HintManager hintManager;
    private Material arrowMaterial;
    private Material[] blockMaterials;
    private int levelIndex;
    private static int? pendingLevelToLoad;
    private int remaining;
    private int moves;
    private float boardCellSize;
    private bool completed;
    private bool timerExpired;
    private bool failureNotified;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(4);

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Min(
            60,
            Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
        lifeManager = GetComponent<LifeManager>();
        if (lifeManager == null) lifeManager = gameObject.AddComponent<LifeManager>();
        hintManager = GetComponent<HintManager>();
        if (hintManager == null) hintManager = gameObject.AddComponent<HintManager>();
        lifeManager.ResetLives(startingLives);
        hintManager.ResetForLevel();
    }

    private void Start()
    {
        arrowMaterial = NewMaterial(Color.white, true);
        Color[] colors = blockColors != null && blockColors.Length > 0 ? blockColors : new[] { Color.white };
        blockMaterials = new Material[colors.Length];
        for (int i = 0; i < colors.Length; i++) blockMaterials[i] = NewMaterial(colors[i], false);

        int levelToLoad = pendingLevelToLoad.HasValue
            ? pendingLevelToLoad.Value
            : loadHighestUnlockedLevel ? GetSavedLevelNumber() : startingLevel;
        pendingLevelToLoad = null;
        LoadLevel(levelToLoad);
        TapStatus = "TAP STATUS: READY";
    }

    private void Update()
    {
        if (completed || timerExpired || IsOutOfLives || Time.timeScale <= 0f) return;
        ElapsedTimeSeconds += Time.deltaTime;
        if (useSolveTimer)
        {
            TimeRemainingSeconds = Mathf.Max(0f, TimeRemainingSeconds - Time.deltaTime);
            if (TimeRemainingSeconds <= 0f)
            {
                timerExpired = true;
                LevelTimerExpired?.Invoke(CurrentLevel);
                NotifyLevelFailed();
                return;
            }
        }

        bool touchRead = false;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.wasPressedThisFrame) continue;
                HandleScreenTap(touch.position.ReadValue());
                touchRead = true;
            }
        }
        if (!touchRead && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleScreenTap(Mouse.current.position.ReadValue());
            touchRead = true;
        }
#endif

    }

    private void HandleScreenTap(Vector2 screenPosition)
    {
        TapStatus = $"TOUCH READ {screenPosition.x:0},{screenPosition.y:0}";
        if (IsPointerOverInteractiveUI(screenPosition)) { TapStatus = "UI TAP"; return; }
        RaycastHit[] hits = Physics.RaycastAll(gameCamera.ScreenPointToRay(screenPosition), 100f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            BrickBlock block = hits[i].collider.GetComponentInParent<BrickBlock>();
            if (block == null) continue;
            HandleBlockTap(block);
            return;
        }
        TapStatus = "RAY MISS: NO BLOCK HIT";
    }

    private void HandleBlockTap(BrickBlock block)
    {
        if (block == null || block.IsMovingOut || IsOutOfLives || timerExpired || completed) return;
        TapStatus = "BLOCK TAP: " + block.name;
        TryRemove(block);
    }

    private bool IsPointerOverInteractiveUI(Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        var pointer = new PointerEventData(eventSystem) { position = position };
        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointer, uiRaycastResults);
        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            // EventSystem.RaycastAll includes PhysicsRaycaster hits as well as UI.
            // BrickBlock implements IPointerDownHandler, so treating every pointer
            // handler as UI incorrectly swallowed the direct tap before cube raycasting.
            if (!(uiRaycastResults[i].module is GraphicRaycaster)) continue;

            Transform target = uiRaycastResults[i].gameObject.transform;
            while (target != null)
            {
                Selectable selectable = target.GetComponent<Selectable>();
                if ((selectable != null && selectable.IsInteractable()) ||
                    target.GetComponent<IPointerClickHandler>() != null ||
                    target.GetComponent<IPointerDownHandler>() != null ||
                    target.GetComponent<IDragHandler>() != null)
                {
                    uiRaycastResults.Clear();
                    return true;
                }
                target = target.parent;
            }
        }
        uiRaycastResults.Clear();
        return false;
    }

    public static void SetLevelToLoad(int levelNumber)
    {
        pendingLevelToLoad = Mathf.Clamp(levelNumber, 1, GameManager.MaximumLevel);
    }

    /// <summary>Loads a one-based level number. Layouts are generated deterministically from the level number.</summary>
    public void LoadLevel(int levelNumber)
    {
        int safeLevel = Mathf.Clamp(levelNumber, 1, GameManager.MaximumLevel);
        GameManager.SelectLevel(safeLevel);
        levelIndex = safeLevel - 1;
        BuildLevel();
        LevelLoaded?.Invoke(CurrentLevel);
    }

    /// <summary>Reloads the currently selected level without changing saved progress.</summary>
    public void ReloadLevel() => BuildLevel();

    /// <summary>Saves the current level, or unlocks the following level after completion.</summary>
    public void SaveLevelProgress()
    {
        int levelToSave = completed ? Mathf.Min(GameManager.MaximumLevel, CurrentLevel + 1) : CurrentLevel;
        GameManager.SaveHighestUnlockedLevel(levelToSave);
    }

    /// <summary>Returns the saved highest unlocked level, falling back to the configured starting level.</summary>
    public int GetSavedLevelNumber() => Mathf.Clamp(GameManager.SelectedLevel, startingLevel, GameManager.MaximumLevel);

    /// <summary>Loads the saved highest unlocked level.</summary>
    public void LoadSavedLevel() => LoadLevel(GetSavedLevelNumber());

    /// <summary>Loads the next procedural level. Call after completion from your UI/game flow.</summary>
    public void LoadNextLevel()
    {
        LoadLevel(Mathf.Min(GameManager.MaximumLevel, CurrentLevel + 1));
    }

    /// <summary>Restarts the current level and resets its timer and move count.</summary>
    public void RestartLevel() => ReloadLevel();

    /// <summary>Highlights a block whose exit path is currently clear.</summary>
    public void ShowHint()
    {
        if (completed || timerExpired || IsOutOfLives) return;
        if (GameManager.Hints <= 0) { TapStatus = "HINT: BUY MORE HINTS IN THE SHOP"; return; }
        if (!hintManager.ShowHint(blocks)) { TapStatus = "HINT: NO CLEAR EXIT FOUND"; return; }
        GameManager.TryUseHint();
        TapStatus = "HINT: TRY THE PULSING BLOCK";
    }

    public void GrantResumeLife()
    {
        if (lifeManager == null || !IsOutOfLives || completed || timerExpired) return;
        lifeManager.AddLife();
        failureNotified = false;
        TapStatus = "RESUMED WITH ONE LIFE";
    }

    /// <summary>Marks this level complete and notifies listeners such as a UI or progression manager.</summary>
    public void CompleteLevel()
    {
        if (completed || timerExpired) return;
        completed = true;
        SaveLevelProgress();
        LevelCompleted?.Invoke(CurrentLevel);
    }

    private void BuildLevel()
    {
        if (levelRoot != null) Destroy(levelRoot.gameObject);
        string[] rows = GenerateRows(levelIndex);
        int width = rows[0].Length, height = rows.Length;
        blocks = new BrickBlock[width, height];
        cameraHandler.FitBoard(width, height, boardCellSize = cellSize);
        moves = 0;
        remaining = 0;
        completed = false;
        timerExpired = false;
        failureNotified = false;
        ElapsedTimeSeconds = 0f;
        lifeManager.ResetLives(startingLives);
        hintManager.ResetForLevel();
        levelRoot = new GameObject("Arrow Puzzle Level " + CurrentLevel).transform;

        Vector3 origin = new Vector3(-(width - 1) * boardCellSize * .5f, -(height - 1) * boardCellSize * .5f, 0f);
        int colorIndex = 0;
        Vector2Int[,] arrowDirections = CreateSolvableDirections(rows);
        for (int row = 0; row < height; row++)
        for (int x = 0; x < width; x++)
        {
            if (rows[row][x] != '#') continue;
            int y = height - 1 - row;
            Vector3 position = origin + new Vector3(x * boardCellSize, y * boardCellSize, 0f);
            GameObject cube = Instantiate(blockPrefab, position, Quaternion.identity, levelRoot);
            cube.name = "Arrow Block " + x + " " + y;
            if (cube.transform.parent != levelRoot) cube.transform.SetParent(levelRoot, true);
            float cubeSize = boardCellSize - blockGap;
            cube.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize * .75f);
            cube.transform.position = position;
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            cubeRenderer.sharedMaterial = blockMaterials[colorIndex % blockMaterials.Length];
            colorIndex++;
            BrickBlock block = cube.GetComponent<BrickBlock>();
            block.Initialize(new Vector2Int(x, y), arrowDirections[x, y], position, HandleBlockTap);
            CreateArrowIcon(block.transform, arrowDirections[x, y], cubeSize);
            block.PlaySpawnAnimation((x + y) * 0.018f);
            blocks[x, y] = block;
            remaining++;
        }

        TimeLimitSeconds = useSolveTimer ? baseSolveTimeSeconds + remaining * extraSecondsPerBlock : 0f;
        TimeRemainingSeconds = TimeLimitSeconds;
    }

    /// <summary>Procedural boards grow from 4x4 to 16x16, becoming denser while remaining seed-reproducible.</summary>
    private string[] GenerateRows(int index)
    {
        const int maxGridGrowthLevel = 2000;
        int gridGrowthIndex = Mathf.Min(index, maxGridGrowthLevel - 1);
        int size = Mathf.Min(16, 4 + Mathf.FloorToInt(Mathf.Sqrt(gridGrowthIndex / 25f)));
        float progress = 1f - Mathf.Exp(-index / 360f);
        float density = Mathf.Lerp(.58f, .96f, progress);
        System.Random random = new System.Random(unchecked(49157 + index * 7919));
        bool[,] occupied = new bool[size, size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
            bool keepCorner = !corner || random.NextDouble() > Mathf.Lerp(.5f, .12f, progress);
            occupied[x, y] = keepCorner && random.NextDouble() < density;
        }
        for (int y = 0; y < size; y++)
        {
            bool any = false; for (int x = 0; x < size; x++) any |= occupied[x, y];
            if (!any) occupied[random.Next(size), y] = true;
        }
        for (int x = 0; x < size; x++)
        {
            bool any = false; for (int y = 0; y < size; y++) any |= occupied[x, y];
            if (!any) occupied[x, random.Next(size)] = true;
        }

        string[] rows = new string[size];
        for (int row = 0; row < size; row++)
        {
            char[] chars = new char[size];
            for (int x = 0; x < size; x++) chars[x] = occupied[x, size - 1 - row] ? '#' : '.';
            rows[row] = new string(chars);
        }
        return rows;
    }

    /// <summary>Assigns arrows using a valid peel order, keeping generated layouts solvable.</summary>
    private Vector2Int[,] CreateSolvableDirections(string[] rows)
    {
        int width = rows[0].Length, height = rows.Length;
        bool[,] alive = new bool[width, height];
        Vector2Int[,] result = new Vector2Int[width, height];
        for (int row = 0; row < height; row++) for (int x = 0; x < width; x++) alive[x, height - 1 - row] = rows[row][x] == '#';
        System.Random random = new System.Random(unchecked(7231 + levelIndex * 971));
        List<Vector3Int> candidates = new List<Vector3Int>(width * height);
        List<Vector2Int> exits = new List<Vector2Int>(width * height);

        while (true)
        {
            candidates.Clear();
            exits.Clear();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                if (!alive[x, y]) continue;
                Vector2Int selectedDirection = default;
                int clearDirectionCount = 0;
                foreach (Vector2Int direction in Directions)
                {
                    int cx = x + direction.x, cy = y + direction.y;
                    bool open = true;
                    while (cx >= 0 && cy >= 0 && cx < width && cy < height)
                    {
                        if (alive[cx, cy]) { open = false; break; }
                        cx += direction.x; cy += direction.y;
                    }
                    if (open && random.Next(++clearDirectionCount) == 0) selectedDirection = direction;
                }
                if (clearDirectionCount == 0) continue;
                candidates.Add(new Vector3Int(x, y, 0));
                exits.Add(selectedDirection);
            }
            if (candidates.Count == 0) break;
            int pick = random.Next(candidates.Count);
            Vector3Int cell = candidates[pick];
            result[cell.x, cell.y] = exits[pick];
            alive[cell.x, cell.y] = false;
        }

        // Any finite board shape has a peelable edge cell; this fallback supports future hand-authored masks.
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            if (!alive[x, y]) continue;
            int[] distance = { y, width - 1 - x, height - 1 - y, x };
            int nearest = 0; for (int i = 1; i < distance.Length; i++) if (distance[i] < distance[nearest]) nearest = i;
            result[x, y] = Directions[nearest];
        }
        return result;
    }

    private void CreateArrowIcon(Transform block, Vector2Int direction, float size)
    {
        GameObject icon = new GameObject("Arrow Icon");
        icon.transform.SetParent(block, false);
        icon.transform.localPosition = new Vector3(0f, 0f, -size * .55f);
        float angle = direction == Vector2Int.up ? 0f : direction == Vector2Int.right ? -90f : direction == Vector2Int.down ? 180f : 90f;
        icon.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Arrow Shaft"; shaft.transform.SetParent(icon.transform, false);
        shaft.transform.localPosition = new Vector3(0f, -size * .07f, -size * .018f);
        shaft.transform.localScale = new Vector3(size * .13f, size * .38f, size * .035f);
        shaft.GetComponent<Renderer>().sharedMaterial = arrowMaterial;
        Destroy(shaft.GetComponent<Collider>());

        GameObject head = new GameObject("Arrow Head", typeof(MeshFilter), typeof(MeshRenderer));
        head.transform.SetParent(icon.transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, -size * .026f);
        head.GetComponent<MeshFilter>().sharedMesh = CreateArrowHeadMesh(size);
        head.GetComponent<MeshRenderer>().sharedMaterial = arrowMaterial;
    }

    private Mesh CreateArrowHeadMesh(float size)
    {
        float z = -size * .018f;
        Mesh mesh = new Mesh { name = "Arrow Head Mesh" };
        mesh.vertices = new[] { new Vector3(0, size * .39f, z), new Vector3(-size * .25f, size * .10f, z), new Vector3(size * .25f, size * .10f, z) };
        mesh.triangles = new[] { 0, 2, 1 };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
        return mesh;
    }

    private void TryRemove(BrickBlock block)
    {
        Vector2Int cell = block.Cell;
        if (blocks[cell.x, cell.y] != block) return;
        Vector2Int direction = block.Direction;
        int x = cell.x + direction.x, y = cell.y + direction.y;
        while (x >= 0 && y >= 0 && x < blocks.GetLength(0) && y < blocks.GetLength(1))
        {
            if (blocks[x, y] != null)
            {
                TapStatus = "BLOCKED: PATH OCCUPIED";
                block.PlayBlockedFeedback();
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX("Wrong");
                lifeManager.LoseLife();
                if (IsOutOfLives)
                {
                    TapStatus = "OUT OF LIVES";
                    NotifyLevelFailed();
                }
                return;
            }
            x += direction.x; y += direction.y;
        }

        blocks[cell.x, cell.y] = null;
        remaining--; moves++;
        TapStatus = "BLOCK REMOVED";
        block.FlyOut(
            new Vector3(direction.x, direction.y, 0f),
            exitDistance + Mathf.Max(blocks.GetLength(0), blocks.GetLength(1)) * boardCellSize,
            () =>
            {
                if (remaining == 0)
                    CompleteLevel();
            });
    }

    private void NotifyLevelFailed()
    {
        if (failureNotified) return;
        failureNotified = true;
        LevelFailed?.Invoke(CurrentLevel);
    }

    private Material NewMaterial(Color color, bool unlit)
    {
        Material template = unlit ? unlitMaterialTemplate : litMaterialTemplate;
        if (template != null) return new Material(template) { color = color };

        Shader shader = unlit ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = unlit ? Shader.Find("Unlit/Color") : Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return new Material(shader) { color = color };
    }

    private void OnDestroy()
    {
        if (arrowMaterial != null) Destroy(arrowMaterial);
        if (blockMaterials != null) foreach (Material material in blockMaterials) if (material != null) Destroy(material);
    }
}
