using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Snake : MonoBehaviour
{
    private enum GameMode { Classic, TimeAttack, NoWalls }
    public Transform segmentPrefab;

    public Vector2Int direction = Vector2Int.right;
    public float moveTime = 0.15f;
    [Min(1)] public int initialSize = 3;

    private float timer;
    private readonly List<Transform> segments = new List<Transform>();
    private readonly Queue<Vector2Int> pendingDirections = new Queue<Vector2Int>();
    private static Sprite fallbackSprite;
    private static Sprite bodySprite;
    private static Sprite headSprite;
    private static Sprite deadHeadSprite;
    private static Sprite[] deadHeadFrames;
    private static Sprite appleUiSprite;
    private static Texture2D inputPromptsTexture;
    private static Material skinMaterial;
    private static int activeSkinIndex;
    private static readonly Color[] SkinColors =
    {
        new Color(0.52f, 0.9f, 0.3f),
        new Color(0.28f, 0.58f, 0.95f),
        new Color(0.98f, 0.72f, 0.2f),
        new Color(0.92f, 0.32f, 0.4f),
        new Color(0.62f, 0.34f, 0.88f)
    };
    private bool gameOver;
    private bool gameStarted;
    private bool paused;
    private bool settingsOpen;
    private int pendingSkinIndex;
    private float pendingVolume;
    private int score;
    private int bestScore;
    private GameMode selectedMode;
    private int level = 1;
    private float initialMoveTime;
    private float timeRemaining = 60f;
    private bool boardCompleted;
    private string achievementToast = "";
    private float achievementToastUntil;
    private bool gamepadStickLatched;
    private bool menuStickLatched;
    private int pauseSelection;
    private AudioSource audioSource;
    private AudioClip eatSound;
    private AudioClip moveSound;
    private AudioClip loseSound;
    private AudioClip recordSound;
    private Coroutine deadHeadAnimation;

    private static readonly Dictionary<char, string[]> PixelGlyphs = new Dictionary<char, string[]>
    {
        { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
        { 'B', new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" } },
        { 'C', new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" } },
        { 'D', new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" } },
        { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
        { 'F', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" } },
        { 'G', new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" } },
        { 'H', new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" } },
        { 'I', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" } },
        { 'J', new[] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" } },
        { 'K', new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" } },
        { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
        { 'M', new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" } },
        { 'N', new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" } },
        { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
        { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
        { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } },
        { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
        { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
        { 'U', new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" } },
        { 'V', new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" } },
        { 'W', new[] { "10001", "10001", "10001", "10101", "10101", "11011", "10001" } },
        { 'X', new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" } },
        { '+', new[] { "00000", "00100", "00100", "11111", "00100", "00100", "00000" } },
        { '-', new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" } },
        { '.', new[] { "00000", "00000", "00000", "00000", "00000", "00110", "00110" } },
        { '/', new[] { "00001", "00010", "00010", "00100", "01000", "01000", "10000" } },
        { '0', new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" } },
        { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
        { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
        { '3', new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" } },
        { '4', new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
        { '5', new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" } },
        { '6', new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" } },
        { '7', new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" } },
        { '8', new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" } },
        { '9', new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" } }
    };

    private void Start()
    {
        // El juego es ligero y depende de entradas rápidas. Evita que macOS deje
        // el render a una frecuencia baja, lo que se siente como teclas tardías.
        QualitySettings.vSyncCount = 0;
        QualitySettings.maxQueuedFrames = 1;
        Application.targetFrameRate = 120;
        Application.runInBackground = true;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

        bestScore = PlayerPrefs.GetInt("SnakeBestScore", 0);
        selectedMode = (GameMode)Mathf.Clamp(PlayerPrefs.GetInt("SnakeMode", 0), 0, 2);
        initialMoveTime = moveTime;
        activeSkinIndex = Mathf.Clamp(PlayerPrefs.GetInt("SnakeSkin", 0), 0, SkinColors.Length - 1);
        pendingSkinIndex = activeSkinIndex;
        pendingVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SnakeVolume", 1f));
        AudioListener.volume = pendingVolume;
        ApplySkinColor(activeSkinIndex);
        ConfigureAudio();
        ConfigureSegment(transform);

        segments.Add(transform);

        for (int i = 1; i < initialSize; i++)
        {
            Grow();
            if (segments.Count > i)
            {
                segments[i].position = transform.position - new Vector3(direction.x * i, direction.y * i, 0f);
            }
        }

        UpdateSpriteOrientations();
        ApplyModeRules();

    }

    private void Update()
    {
        HandleGamepadInput();

        if (!gameStarted)
        {
            if (settingsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelSettings();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                gameStarted = true;
            }
            else
            {
                HandleInput();
                gameStarted = pendingDirections.Count > 0;
            }

            if (!gameStarted)
            {
                return;
            }
        }

        if (gameOver)
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space))
            {
                RestartGame();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
        {
            paused = !paused;
            if (paused) pauseSelection = 0;
        }

        if (paused)
        {
            return;
        }

        if (selectedMode == GameMode.TimeAttack)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                LoseGame();
                return;
            }
        }

        HandleInput();
        HandleTouchPadInput();
        timer += Time.deltaTime;

        if (timer >= moveTime)
        {
            timer = 0f;
            Move();
        }
    }

    private void HandleInput()
    {
        if (WasPressed(Key.W, KeyCode.W) || WasPressed(Key.UpArrow, KeyCode.UpArrow))
            QueueDirection(Vector2Int.up);
        if (WasPressed(Key.S, KeyCode.S) || WasPressed(Key.DownArrow, KeyCode.DownArrow))
            QueueDirection(Vector2Int.down);
        if (WasPressed(Key.A, KeyCode.A) || WasPressed(Key.LeftArrow, KeyCode.LeftArrow))
            QueueDirection(Vector2Int.left);
        if (WasPressed(Key.D, KeyCode.D) || WasPressed(Key.RightArrow, KeyCode.RightArrow))
            QueueDirection(Vector2Int.right);
    }

    private void HandleGamepadInput()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return;

        if (!gameStarted)
        {
            if (settingsOpen)
            {
                if (pad.buttonEast.wasPressedThisFrame) CancelSettings();
                if (pad.dpad.left.wasPressedThisFrame) SelectPreviousSkin();
                if (pad.dpad.right.wasPressedThisFrame) SelectNextSkin();
                if (pad.dpad.down.wasPressedThisFrame) ChangeVolume(-.1f);
                if (pad.dpad.up.wasPressedThisFrame) ChangeVolume(.1f);
                if (pad.buttonSouth.wasPressedThisFrame) ApplySettings();
                return;
            }

            if (pad.leftShoulder.wasPressedThisFrame) ChangeMode(-1);
            if (pad.rightShoulder.wasPressedThisFrame) ChangeMode(1);
            if (pad.buttonNorth.wasPressedThisFrame)
            {
                pendingSkinIndex=activeSkinIndex;
                pendingVolume=AudioListener.volume;
                settingsOpen=true;
                return;
            }
            if (pad.buttonSouth.wasPressedThisFrame) gameStarted=true;
            return;
        }

        if (gameOver)
        {
            if (pad.buttonSouth.wasPressedThisFrame) RestartGame();
            else if (pad.buttonEast.wasPressedThisFrame) ReturnToMainMenu();
            return;
        }

        if (paused)
        {
            UpdatePauseSelection(pad);
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                if (pauseSelection==0) paused=false;
                else ReturnToMainMenu();
            }
            else if (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) paused=false;
            return;
        }

        if (pad.startButton.wasPressedThisFrame)
        {
            paused=true;
            pauseSelection=0;
            menuStickLatched=false;
            return;
        }

        Vector2Int requested=Vector2Int.zero;
        if (pad.dpad.up.wasPressedThisFrame) requested=Vector2Int.up;
        else if (pad.dpad.down.wasPressedThisFrame) requested=Vector2Int.down;
        else if (pad.dpad.left.wasPressedThisFrame) requested=Vector2Int.left;
        else if (pad.dpad.right.wasPressedThisFrame) requested=Vector2Int.right;

        Vector2 stick=pad.leftStick.ReadValue();
        if (stick.magnitude < .45f) gamepadStickLatched=false;
        else if (!gamepadStickLatched && requested==Vector2Int.zero)
        {
            requested=Mathf.Abs(stick.x)>Mathf.Abs(stick.y)
                ? (stick.x>0?Vector2Int.right:Vector2Int.left)
                : (stick.y>0?Vector2Int.up:Vector2Int.down);
            gamepadStickLatched=true;
        }

        if (requested!=Vector2Int.zero) QueueDirection(requested);
    }

    private void UpdatePauseSelection(Gamepad pad)
    {
        int change=0;
        if(pad.dpad.left.wasPressedThisFrame||pad.dpad.up.wasPressedThisFrame) change=-1;
        else if(pad.dpad.right.wasPressedThisFrame||pad.dpad.down.wasPressedThisFrame) change=1;

        Vector2 stick=pad.leftStick.ReadValue();
        if(stick.magnitude<.45f) menuStickLatched=false;
        else if(!menuStickLatched&&change==0)
        {
            change=(Mathf.Abs(stick.x)>Mathf.Abs(stick.y)?stick.x:-stick.y)>0?1:-1;
            menuStickLatched=true;
        }
        if(change!=0) pauseSelection=(pauseSelection+change+2)%2;
    }

    private void ChangeMode(int delta)
    {
        selectedMode=(GameMode)(((int)selectedMode+delta+3)%3);
        PlayerPrefs.SetInt("SnakeMode",(int)selectedMode);
        PlayerPrefs.Save();
        ApplyModeRules();
    }

    private void SelectPreviousSkin()
    {
        pendingSkinIndex=(pendingSkinIndex-1+SkinColors.Length)%SkinColors.Length;
        ApplySkinColor(pendingSkinIndex);
    }

    private void SelectNextSkin()
    {
        pendingSkinIndex=(pendingSkinIndex+1)%SkinColors.Length;
        ApplySkinColor(pendingSkinIndex);
    }

    private void ChangeVolume(float delta)
    {
        pendingVolume=Mathf.Clamp01(pendingVolume+delta);
        AudioListener.volume=pendingVolume;
    }

    private void ApplySettings()
    {
        activeSkinIndex=pendingSkinIndex;
        ApplySkinColor(activeSkinIndex);
        PlayerPrefs.SetInt("SnakeSkin",activeSkinIndex);
        PlayerPrefs.SetFloat("SnakeVolume",pendingVolume);
        PlayerPrefs.Save();
        settingsOpen=false;
    }

    private static bool WasPressed(Key key, KeyCode fallback)
    {
        return (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame)
            || Input.GetKeyDown(fallback);
    }

    private void HandleTouchPadInput()
    {
        if (!UseMobileLayout() || Touchscreen.current == null ||
            !Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return;
        }

        Vector2 screenTouch = Touchscreen.current.primaryTouch.position.ReadValue();
        Vector2 guiTouch = new Vector2(screenTouch.x, Screen.height - screenTouch.y);
        GetTouchPadRects(out Rect up, out Rect down, out Rect left, out Rect right);
        if (up.Contains(guiTouch)) QueueDirection(Vector2Int.up);
        else if (down.Contains(guiTouch)) QueueDirection(Vector2Int.down);
        else if (left.Contains(guiTouch)) QueueDirection(Vector2Int.left);
        else if (right.Contains(guiTouch)) QueueDirection(Vector2Int.right);
    }

    private void QueueDirection(Vector2Int newDirection)
    {
        Vector2Int[] queued = pendingDirections.ToArray();
        Vector2Int lastDirection = queued.Length > 0 ? queued[queued.Length - 1] : direction;

        // Si ya hay dos giros guardados, mantenemos el primero (es el próximo
        // paso seguro) y reemplazamos el segundo por la tecla más reciente.
        // De esta forma ninguna intención válida se pierde por una cola llena.
        if (pendingDirections.Count >= 2)
        {
            Vector2Int nextDirection = pendingDirections.Dequeue();
            pendingDirections.Clear();
            pendingDirections.Enqueue(nextDirection);

            if (newDirection != nextDirection && newDirection != -nextDirection)
            {
                pendingDirections.Enqueue(newDirection);
            }

            return;
        }

        if (newDirection == lastDirection || newDirection == -lastDirection)
        {
            return;
        }

        pendingDirections.Enqueue(newDirection);

        // Da feedback visual en el mismo frame. El cuerpo continúa moviéndose
        // sobre la cuadrícula, pero el control ya no se siente retrasado.
        if (pendingDirections.Count == 1)
        {
            transform.rotation = RotationForHeadDirection(newDirection);

            // Ejecuta el próximo paso en este mismo Update. La serpiente sigue
            // avanzando por celdas enteras, pero el control no espera otro tick.
            timer = moveTime;
        }
    }

    private void Move()
    {
        Vector2Int previousDirection = direction;

        if (pendingDirections.Count > 0)
        {
            direction = pendingDirections.Dequeue();
        }

        if (direction != previousDirection)
        {
            PlaySound(moveSound, 0.28f);
        }

        Vector3 nextPosition = transform.position + new Vector3(direction.x, direction.y, 0f);

        if (GameBoard.WouldCrossWall(nextPosition) && selectedMode == GameMode.NoWalls)
        {
            nextPosition = GameBoard.WrapPosition(nextPosition);
        }
        else if (GameBoard.WouldCrossWall(nextPosition))
        {
            LoseGame();
            return;
        }

        // Se excluye la última pieza porque abandonará esa casilla en este mismo paso.
        for (int i = 1; i < segments.Count - 1; i++)
        {
            if (Vector2.SqrMagnitude((Vector2)(segments[i].position - nextPosition)) < 0.01f)
            {
                LoseGame();
                return;
            }
        }

        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i].position = segments[i - 1].position;
        }

        transform.position = nextPosition;
        UpdateSpriteOrientations();
    }

    public void Grow()
    {
        if (segmentPrefab == null)
        {
            Debug.LogError("Snake necesita un Segment Prefab asignado.", this);
            return;
        }

        Transform segment = Instantiate(segmentPrefab);
        segment.position = segments[segments.Count - 1].position;
        ConfigureSegment(segment);

        segments.Add(segment);
    }

    private static void ConfigureSegment(Transform segment)
    {
        segment.localScale = Vector3.one;

        SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = segment.gameObject.AddComponent<SpriteRenderer>();
        }

        bool isHead = segment.GetComponent<Snake>() != null;

        if (isHead && headSprite == null)
        {
            headSprite = Resources.Load<Sprite>("SnakeHead");
        }

        if (!isHead && bodySprite == null)
        {
            bodySprite = Resources.Load<Sprite>("SnakeBody");
        }

        Sprite selectedSprite = isHead ? headSprite : bodySprite;
        if (selectedSprite != null)
        {
            renderer.sprite = selectedSprite;
        }
        else if (renderer.sprite == null)
        {
            renderer.sprite = GetFallbackSprite();
        }

        renderer.color = Color.white;
        renderer.sharedMaterial = GetSkinMaterial();

        if (segment.GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider = segment.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.9f, 0.9f);
        }

        if (isHead && segment.GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D body = segment.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }
    }

    private static Material GetSkinMaterial()
    {
        if (skinMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("SnakeRecolor");
            if (shader != null)
            {
                skinMaterial = new Material(shader);
                skinMaterial.name = "Snake Skin (Runtime)";
                skinMaterial.SetColor("_SkinColor", SkinColors[Mathf.Clamp(activeSkinIndex, 0, SkinColors.Length - 1)]);
            }
        }

        return skinMaterial;
    }

    private static void ApplySkinColor(int skinIndex)
    {
        int validSkinIndex = Mathf.Clamp(skinIndex, 0, SkinColors.Length - 1);
        Material material = GetSkinMaterial();
        if (material != null)
        {
            material.SetColor("_SkinColor", SkinColors[validSkinIndex]);
        }
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.name = "Generated Snake Square";
        texture.filterMode = FilterMode.Point;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        fallbackSprite.name = "Generated Snake Square";
        return fallbackSprite;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Food"))
        {
            Food food = other.GetComponent<Food>();
            int points = food != null ? food.PointValue : 1;
            bool wasGolden = food != null && food.IsGolden;
            PixelBurst.Create(other.transform.position, wasGolden ? new Color(1f, .82f, .1f) : new Color(1f, 0.18f, 0.12f));
            Grow();
            score += points;
            level = 1 + score / 10;
            moveTime = Mathf.Max(0.07f, initialMoveTime - (level - 1) * 0.008f);
            CheckAchievements(wasGolden);

            if (score > bestScore)
            {
                bestScore = score;
                PlayerPrefs.SetInt("SnakeBestScore", bestScore);
                PlayerPrefs.Save();
                PlaySound(recordSound, 0.65f);
            }
            else
            {
                PlaySound(eatSound, 0.55f);
            }

            if (food != null)
            {
                food.HandleEaten(score);
            }

            if (GameBoard.CellCount > 0 && segments.Count >= GameBoard.CellCount)
            {
                HandleBoardCompleted();
            }
        }
        else if (other.CompareTag("Wall"))
        {
            if (selectedMode != GameMode.NoWalls) LoseGame();
        }
    }

    private void LoseGame()
    {
        if (gameOver)
        {
            return;
        }

        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("SnakeBestScore", bestScore);
            PlayerPrefs.Save();
        }

        gameOver = true;
        StartDeadHeadAnimation();
        PlaySound(loseSound, 0.7f);
        StartCoroutine(ShakeCamera());
    }

    private void RestartGame()
    {
        for (int i = segments.Count - 1; i > 0; i--)
        {
            Destroy(segments[i].gameObject);
        }

        segments.RemoveRange(1, segments.Count - 1);
        transform.position = Vector3.zero;
        direction = Vector2Int.right;
        pendingDirections.Clear();
        timer = 0f;
        gameOver = false;
        boardCompleted = false;
        paused = false;
        score = 0;
        level = 1;
        moveTime = initialMoveTime;
        timeRemaining = 60f;
        Food.ResetBonusFood();
        RestoreHeadSprite();

        for (int i = 1; i < initialSize; i++)
        {
            Grow();
            if (segments.Count > i)
            {
                segments[i].position = transform.position - new Vector3(direction.x * i, direction.y * i, 0f);
            }
        }

        UpdateSpriteOrientations();
        ApplyModeRules();

    }

    private void ApplyModeRules()
    {
        GameBoard.SetWallsEnabled(selectedMode != GameMode.NoWalls);
    }

    public void HandleBoardCompleted()
    {
        if (gameOver) return;
        boardCompleted = true;
        gameOver = true;
        UnlockAchievement("board", "TABLERO");
        PlaySound(recordSound, .8f);
    }

    private void CheckAchievements(bool golden)
    {
        if (score >= 1) UnlockAchievement("first", "PRIMERA");
        if (score >= 15) UnlockAchievement("level2", "NIVEL DOS");
        if (score >= 30) UnlockAchievement("thirty", "TREINTA");
        if (golden) UnlockAchievement("golden", "DORADA");
    }

    private void UnlockAchievement(string id, string label)
    {
        string key = "SnakeAchievement_" + id;
        if (PlayerPrefs.GetInt(key, 0) != 0) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        achievementToast = "LOGRO " + label;
        achievementToastUntil = Time.unscaledTime + 3f;
    }

    private static int GetAchievementCount()
    {
        string[] ids = { "first", "level2", "thirty", "golden", "board" };
        int count = 0;
        foreach (string id in ids) count += PlayerPrefs.GetInt("SnakeAchievement_" + id, 0);
        return count;
    }

    public bool IsPositionReservedForSnake(Vector2 candidate)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (Vector2.SqrMagnitude((Vector2)segments[i].position - candidate) < 0.01f)
            {
                return true;
            }
        }

        // Reserva el recorrido inmediato que ya está comprometido por el input.
        Vector2 projectedPosition = transform.position;
        Vector2Int projectedDirection = direction;
        Vector2Int[] queuedDirections = pendingDirections.ToArray();
        const int reservedSteps = 3;
        for (int step = 0; step < reservedSteps; step++)
        {
            if (step < queuedDirections.Length)
            {
                projectedDirection = queuedDirections[step];
            }

            projectedPosition += projectedDirection;
            if (Vector2.SqrMagnitude(projectedPosition - candidate) < 0.01f)
            {
                return true;
            }
        }

        return false;
    }

    private void ReturnToMainMenu()
    {
        RestartGame();
        gameStarted = false;
        settingsOpen = false;
    }

    private void UpdateSpriteOrientations()
    {
        transform.rotation = RotationForHeadDirection(direction);

        for (int i = 1; i < segments.Count; i++)
        {
            Vector2 segmentDirection = segments[i - 1].position - segments[i].position;

            if (segmentDirection.sqrMagnitude > 0.01f)
            {
                Vector2Int cardinalDirection = new Vector2Int(
                    Mathf.RoundToInt(segmentDirection.x),
                    Mathf.RoundToInt(segmentDirection.y)
                );
                segments[i].rotation = RotationForBodyDirection(cardinalDirection);
            }
        }
    }

    private static Quaternion RotationForHeadDirection(Vector2Int lookDirection)
    {
        float angle;

        // El dibujo original de SnakeHead mira hacia la izquierda.
        if (lookDirection == Vector2Int.up)
            angle = -90f;
        else if (lookDirection == Vector2Int.right)
            angle = 180f;
        else if (lookDirection == Vector2Int.down)
            angle = 90f;
        else
            angle = 0f;

        return Quaternion.Euler(0f, 0f, angle);
    }

    private static Quaternion RotationForBodyDirection(Vector2Int lookDirection)
    {
        // El sprite nuevo está dibujado horizontalmente. Solo hace falta
        // girarlo un cuarto de vuelta cuando el segmento avanza en vertical.
        float angle = lookDirection.x == 0 ? 90f : 0f;

        return Quaternion.Euler(0f, 0f, angle);
    }

    private void SetDeadHeadSprite()
    {
        if (deadHeadSprite == null)
        {
            deadHeadSprite = Resources.Load<Sprite>("SnakeDead");
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && deadHeadSprite != null)
        {
            renderer.sprite = deadHeadSprite;
        }
    }

    private void StartDeadHeadAnimation()
    {
        if (deadHeadAnimation != null)
        {
            StopCoroutine(deadHeadAnimation);
        }

        deadHeadAnimation = StartCoroutine(AnimateDeadHead());
    }

    private IEnumerator AnimateDeadHead()
    {
        if (deadHeadFrames == null || deadHeadFrames.Length == 0)
        {
            deadHeadFrames = Resources.LoadAll<Sprite>("Dead-snake-spritesheet-4x4-256");
            System.Array.Sort(deadHeadFrames, (left, right) =>
                GetSpriteFrameIndex(left.name).CompareTo(GetSpriteFrameIndex(right.name)));
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null || deadHeadFrames.Length == 0)
        {
            SetDeadHeadSprite();
            deadHeadAnimation = null;
            yield break;
        }

        transform.rotation = RotationForDeadHeadDirection(direction);
        for (int i = 0; i < deadHeadFrames.Length; i++)
        {
            if (!gameOver)
            {
                break;
            }

            renderer.sprite = deadHeadFrames[i];
            yield return new WaitForSecondsRealtime(0.065f);
        }

        deadHeadAnimation = null;
    }

    private static int GetSpriteFrameIndex(string spriteName)
    {
        int separator = spriteName.LastIndexOf('_');
        int frameIndex;
        return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), out frameIndex)
            ? frameIndex
            : int.MaxValue;
    }

    private static Quaternion RotationForDeadHeadDirection(Vector2Int lookDirection)
    {
        // El spritesheet está dibujado mirando hacia arriba.
        float angle;
        if (lookDirection == Vector2Int.right)
            angle = -90f;
        else if (lookDirection == Vector2Int.down)
            angle = 180f;
        else if (lookDirection == Vector2Int.left)
            angle = 90f;
        else
            angle = 0f;

        return Quaternion.Euler(0f, 0f, angle);
    }

    private void RestoreHeadSprite()
    {
        if (deadHeadAnimation != null)
        {
            StopCoroutine(deadHeadAnimation);
            deadHeadAnimation = null;
        }

        if (headSprite == null)
        {
            headSprite = Resources.Load<Sprite>("SnakeHead");
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && headSprite != null)
        {
            renderer.sprite = headSprite;
        }
    }

    private void ConfigureAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        moveSound = Resources.Load<AudioClip>("Audio/Move");
        if (moveSound == null)
        {
            moveSound = CreateRetroTone("Move", 180f, 220f, 0.035f);
        }
        eatSound = CreateRetroTone("Eat", 420f, 660f, 0.09f);
        recordSound = CreateRetroTone("Record", 520f, 1100f, 0.22f);
        loseSound = Resources.Load<AudioClip>("Audio/Lose");
        if (loseSound == null)
        {
            loseSound = CreateRetroTone("Lose", 260f, 70f, 0.38f);
        }
    }

    private static AudioClip CreateRetroTone(string clipName, float startFrequency, float endFrequency, float duration)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            phase += frequency / sampleRate;
            float envelope = 1f - progress;
            samples[i] = (phase % 1f < 0.5f ? 0.22f : -0.22f) * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private IEnumerator ShakeCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            yield break;
        }

        Transform cameraTransform = mainCamera.transform;
        Vector3 originalPosition = cameraTransform.position;
        float elapsed = 0f;

        while (elapsed < 0.22f)
        {
            Vector2 offset = Random.insideUnitCircle * 0.1f;
            cameraTransform.position = originalPosition + new Vector3(offset.x, offset.y, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        cameraTransform.position = originalPosition;
    }

    private void OnGUI()
    {
        if (!gameStarted)
        {
            if (settingsOpen)
            {
                DrawSettingsOverlay();
                DrawVersionBadge();
                return;
            }

            if (DrawStartOverlay())
            {
                gameStarted = true;
            }
            DrawVersionBadge();
            return;
        }

        int hudWidth = 150;
        int hudHeight = 48;
        int hudX = 10;
        Rect hudRect = new Rect(hudX, 10, hudWidth, hudHeight);

        DrawPixelRect(new Rect(hudRect.x + 4, hudRect.y + 4, hudRect.width, hudRect.height), new Color(0.01f, 0.02f, 0.04f, 0.55f));
        DrawPixelRect(hudRect, new Color(0.04f, 0.08f, 0.14f, 0.95f));
        DrawPixelBorder(hudRect, 4, new Color(0.15f, 0.85f, 0.32f));
        DrawAppleIcon(new Rect(hudX + 10, 18, 32, 32));
        string scoreText = score.ToString("D3");
        DrawPixelText(scoreText, hudX + 60, 20, 4, new Color(1f, 0.9f, 0.3f));

        int recordHudWidth = Mathf.Min(220, Screen.width - hudWidth - 30);
        int recordHudX = Screen.width - recordHudWidth - 10;
        Rect recordHudRect = new Rect(recordHudX, 10, recordHudWidth, hudHeight);
        DrawPixelRect(new Rect(recordHudRect.x + 4, recordHudRect.y + 4, recordHudRect.width, recordHudRect.height), new Color(0.01f, 0.02f, 0.04f, 0.55f));
        DrawPixelRect(recordHudRect, new Color(0.04f, 0.08f, 0.14f, 0.95f));
        DrawPixelBorder(recordHudRect, 4, new Color(0.15f, 0.85f, 0.32f));
        string recordText = "RECORD " + bestScore.ToString("D3");
        int recordPixelSize = recordHudWidth >= 190 ? 3 : 2;
        int recordWidth = (recordText.Length * 6 - 1) * recordPixelSize;
        int recordX = recordHudX + (recordHudWidth - recordWidth) / 2;
        int recordY = 10 + (hudHeight - 7 * recordPixelSize) / 2;
        DrawPixelText(recordText, recordX + 2, recordY + 2, recordPixelSize, new Color(0.01f, 0.02f, 0.04f, 0.8f));
        DrawPixelText(recordText, recordX, recordY, recordPixelSize, new Color(1f, 0.9f, 0.3f));

        string centerHud = selectedMode == GameMode.TimeAttack
            ? "TIEMPO " + Mathf.CeilToInt(timeRemaining).ToString("D2")
            : "NIVEL " + level.ToString("D2");
        DrawCenteredPixelText(centerHud, UseMobileLayout() ? 68 : 22, UseMobileLayout() ? 2 : 3, new Color(.65f, .95f, .72f), 0);

        if (Time.unscaledTime < achievementToastUntil)
        {
            int toastWidth = Mathf.Min(430, Screen.width - 40);
            Rect toast = new Rect((Screen.width - toastWidth) / 2, 68, toastWidth, 54);
            DrawPixelRect(new Rect(toast.x + 5, toast.y + 5, toast.width, toast.height), new Color(0,0,0,.5f));
            DrawPixelRect(toast, new Color(.08f,.15f,.22f,.97f));
            DrawPixelBorder(toast, 4, new Color(1f,.84f,.2f));
            DrawCenteredPixelTextInRect(achievementToast, toast, 3, new Color(1f,.9f,.3f));
        }

        if (!paused && !gameOver && UseMobileLayout())
        {
            DrawTouchPad();
        }

        if (paused)
        {
            DrawPauseOverlay();
            DrawVersionBadge();
            return;
        }

        if (!gameOver)
        {
            DrawVersionBadge();
            return;
        }

        int panelWidth = Mathf.Min(900, Screen.width - 32);
        int panelHeight = 300;
        int panelX = (Screen.width - panelWidth) / 2;
        int panelY = (Screen.height - panelHeight) / 2;

        DrawPixelRect(new Rect(panelX + 12, panelY + 12, panelWidth, panelHeight), new Color(0.01f, 0.02f, 0.04f, 0.65f));
        DrawPixelRect(new Rect(panelX, panelY, panelWidth, panelHeight), new Color(0.04f, 0.08f, 0.14f, 0.94f));
        DrawPixelBorder(new Rect(panelX, panelY, panelWidth, panelHeight), 8, new Color(0.1f, 0.7f, 0.25f));
        DrawPixelBorder(new Rect(panelX + 14, panelY + 14, panelWidth - 28, panelHeight - 28), 3, new Color(0.25f, 0.38f, 0.48f));

        DrawPixelRect(new Rect(panelX + 26, panelY + 26, 18, 18), new Color(1f, 0.9f, 0.3f));
        DrawPixelRect(new Rect(panelX + panelWidth - 44, panelY + 26, 18, 18), new Color(1f, 0.9f, 0.3f));
        DrawPixelRect(new Rect(panelX + 26, panelY + panelHeight - 44, 18, 18), new Color(1f, 0.9f, 0.3f));
        DrawPixelRect(new Rect(panelX + panelWidth - 44, panelY + panelHeight - 44, 18, 18), new Color(1f, 0.9f, 0.3f));

        string endTitle = boardCompleted ? "TABLERO COMPLETO" : "GAME OVER";
        int endScale = boardCompleted ? 5 : 9;
        DrawCenteredPixelText(endTitle, panelY + 48, endScale, new Color(0.01f, 0.02f, 0.04f), 5);
        DrawCenteredPixelText(endTitle, panelY + 43, endScale, new Color(1f, 0.9f, 0.3f), 0);
        DrawPixelRect(new Rect(panelX + 90, panelY + 120, panelWidth - 180, 4), new Color(0.1f, 0.7f, 0.25f));
        DrawCenteredPixelText("SCORE " + score.ToString("D3"), panelY + 145, 4, Color.white, 0);
        DrawCenteredPixelText("RECORD " + bestScore.ToString("D3"), panelY + 185, 3, new Color(0.65f, 0.95f, 0.72f), 0);
        string restartPrompt=UseMobileLayout()?"A PARA REINICIAR":"R ESPACIO O A PARA REINICIAR";
        DrawCenteredPixelText(restartPrompt, panelY + 239, UseMobileLayout()?2:3, new Color(1f, 0.9f, 0.3f), 0);
        DrawVersionBadge();
    }

    private bool DrawStartOverlay()
    {
        if (UseMobileLayout()) return DrawMobileStartOverlay();
        DrawPixelRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.03f, 0.06f, 0.76f));

        int panelWidth = Mathf.Min(900, Screen.width - 40);
        int panelHeight = Mathf.Min(600, Screen.height - 40);
        int panelX = (Screen.width - panelWidth) / 2;
        int panelY = (Screen.height - panelHeight) / 2;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);

        DrawPixelRect(new Rect(panel.x + 12, panel.y + 12, panel.width, panel.height), new Color(0f, 0f, 0f, 0.55f));
        DrawPixelRect(panel, new Color(0.04f, 0.09f, 0.15f, 0.98f));
        DrawPixelBorder(panel, 8, new Color(0.24f, 0.9f, 0.46f));
        DrawPixelBorder(new Rect(panel.x + 16, panel.y + 16, panel.width - 32, panel.height - 32), 3, new Color(0.28f, 0.43f, 0.5f));
        DrawPixelRect(new Rect(panel.x + 8, panel.y + 8, panel.width - 16, 5), new Color(1f, 0.9f, 0.3f));

        int titleScale = Mathf.Clamp((panelWidth - 120) / 44, 9, 14);
        DrawCenteredPixelText("SNAKE", panelY + 58, titleScale, new Color(0f, 0f, 0f, 0.8f), 6);
        DrawCenteredPixelText("SNAKE", panelY + 52, titleScale, new Color(1f, 0.9f, 0.25f), 0);
        DrawCenteredPixelText("GAME", panelY + 164, 3, new Color(0.65f, 0.95f, 0.72f), 0);

        Rect playButton = new Rect(panelX + panelWidth / 2 - 300, panelY + 210, 280, 70);
        Rect settingsButton = new Rect(panelX + panelWidth / 2 + 20, panelY + 210, 280, 70);
        bool playClicked = DrawPixelButton(playButton, "JUGAR", true, 4);
        if (DrawPixelButton(settingsButton, "AJUSTES", false, 3))
        {
            pendingSkinIndex = activeSkinIndex;
            pendingVolume = AudioListener.volume;
            settingsOpen = true;
        }

        DrawCenteredPixelText("MODO", panelY + 316, 3, Color.white, 0);
        float modeWidth = (panelWidth - 100f) / 3f;
        string[] modeLabels = { "CLASICO", "CONTRA", "SIN MUROS" };
        for (int i = 0; i < 3; i++)
        {
            Rect modeButton = new Rect(panelX + 40 + i * (modeWidth + 10), panelY + 354, modeWidth, 60);
            if (DrawPixelButton(modeButton, modeLabels[i], (int)selectedMode == i, 2))
            {
                selectedMode = (GameMode)i;
                PlayerPrefs.SetInt("SnakeMode", i);
                PlayerPrefs.Save();
                ApplyModeRules();
            }
        }
        DrawCenteredPixelText("ESPACIO ENTER O A", panelY + 450, 2, Color.white, 0);
        DrawPixelRect(new Rect(panelX + 95, panelY + 486, panelWidth - 190, 3), new Color(0.12f, 0.68f, 0.42f));
        DrawCenteredPixelText("WASD FLECHAS O CONTROL", panelY + 510, 2, new Color(0.65f, 0.8f, 0.88f), 0);
        DrawCenteredPixelText("RECORD " + bestScore.ToString("D3"), panelY + 544, 2, new Color(1f, 0.9f, 0.3f), 0);
        DrawCenteredPixelText("LOGROS " + GetAchievementCount() + "/5", panelY + 570, 2, new Color(.65f, .95f, .72f), 0);

        return playClicked;
    }

    private static bool UseMobileLayout()
    {
#if UNITY_EDITOR
        // En el editor, una ventana angosta sirve para previsualizar el móvil.
        return Application.isMobilePlatform || Screen.width < 700;
#else
        // En builds de escritorio nunca activamos controles táctiles.
        return Application.isMobilePlatform;
#endif
    }

    private bool DrawMobileStartOverlay()
    {
        DrawPixelRect(new Rect(0, 0, Screen.width, Screen.height), new Color(.01f,.03f,.06f,.8f));
        Rect safe = Screen.safeArea;
        float top = Screen.height - safe.yMax;
        float width = Mathf.Min(Screen.width - 20f, 440f);
        float height = Mathf.Min(safe.height - 20f, 650f);
        Rect panel = new Rect((Screen.width-width)/2f, top+(safe.height-height)/2f, width, height);
        DrawPixelRect(new Rect(panel.x+7,panel.y+7,panel.width,panel.height),new Color(0,0,0,.5f));
        DrawPixelRect(panel,new Color(.04f,.09f,.15f,.98f));
        DrawPixelBorder(panel,5,new Color(.24f,.9f,.46f));
        DrawPixelBorder(new Rect(panel.x+10,panel.y+10,panel.width-20,panel.height-20),2,new Color(.28f,.43f,.5f));

        int titleScale=Mathf.Clamp(Mathf.FloorToInt(width/48f),5,8);
        DrawCenteredPixelText("SNAKE",Mathf.RoundToInt(panel.y+28),titleScale,new Color(1f,.9f,.25f),0);
        Rect play=new Rect(panel.x+24,panel.y+105,panel.width-48,54);
        Rect settings=new Rect(panel.x+24,panel.y+169,panel.width-48,50);
        bool playClicked=DrawPixelButton(play,"JUGAR",true,4);
        if(DrawPixelButton(settings,"AJUSTES",false,3))
        {
            pendingSkinIndex=activeSkinIndex; pendingVolume=AudioListener.volume; settingsOpen=true;
        }
        DrawCenteredPixelText("MODO",Mathf.RoundToInt(panel.y+244),3,Color.white,0);
        string[] labels={"CLASICO","CONTRA","SIN MUROS"};
        for(int i=0;i<labels.Length;i++)
        {
            Rect mode=new Rect(panel.x+34,panel.y+278+i*57,panel.width-68,46);
            if(DrawPixelButton(mode,labels[i],(int)selectedMode==i,2))
            {
                selectedMode=(GameMode)i; PlayerPrefs.SetInt("SnakeMode",i); PlayerPrefs.Save();
                ApplyModeRules();
            }
        }
        float footer=panel.y+panel.height-94;
        DrawCenteredPixelText("PAD O CONTROL PARA MOVER",Mathf.RoundToInt(footer),2,new Color(.65f,.8f,.88f),0);
        DrawCenteredPixelText("RECORD "+bestScore.ToString("D3"),Mathf.RoundToInt(footer+30),2,new Color(1f,.9f,.3f),0);
        DrawCenteredPixelText("LOGROS "+GetAchievementCount()+"/5",Mathf.RoundToInt(footer+58),2,new Color(.65f,.95f,.72f),0);
        return playClicked;
    }

    private void DrawMobileSettingsOverlay()
    {
        DrawPixelRect(new Rect(0,0,Screen.width,Screen.height),new Color(.01f,.03f,.06f,.84f));
        Rect safe=Screen.safeArea;
        float top=Screen.height-safe.yMax;
        Rect panel=new Rect(10,top+10,Screen.width-20,safe.height-20);
        DrawPixelRect(panel,new Color(.04f,.09f,.15f,.99f));
        DrawPixelBorder(panel,5,new Color(.48f,.9f,.58f));
        DrawCenteredPixelText("AJUSTES",Mathf.RoundToInt(panel.y+24),5,new Color(1f,.9f,.3f),0);
        DrawCenteredPixelText("COLOR",Mathf.RoundToInt(panel.y+82),3,Color.white,0);
        float gap=6f, area=panel.width-28f;
        float cardWidth=(area-gap*4f)/5f;
        for(int i=0;i<5;i++)
        {
            Rect card=new Rect(panel.x+14+i*(cardWidth+gap),panel.y+120,cardWidth,82);
            DrawPixelRect(card,new Color(.07f,.14f,.22f));
            DrawPixelBorder(card,pendingSkinIndex==i?4:2,pendingSkinIndex==i?new Color(1f,.9f,.3f):new Color(.3f,.75f,.48f));
            DrawWormPreview(card,SkinColors[i]);
            if(IsLeftClick(card)){pendingSkinIndex=i;ApplySkinColor(i);}
        }
        DrawCenteredPixelText("SONIDO",Mathf.RoundToInt(panel.y+235),3,Color.white,0);
        Rect minus=new Rect(panel.x+24,panel.y+275,54,48);
        Rect plus=new Rect(panel.xMax-78,panel.y+275,54,48);
        if(DrawPixelButton(minus,"-",false,4)) pendingVolume=Mathf.Max(0,pendingVolume-.1f);
        if(DrawPixelButton(plus,"+",false,4)) pendingVolume=Mathf.Min(1,pendingVolume+.1f);
        AudioListener.volume=pendingVolume;
        float meterX=minus.xMax+12, meterWidth=plus.x-meterX-12;
        for(int i=0;i<10;i++)
        {
            Rect block=new Rect(meterX+i*meterWidth/10f,panel.y+288,meterWidth/10f-3,22);
            DrawPixelRect(block,i<Mathf.RoundToInt(pendingVolume*10)?new Color(.48f,.9f,.58f):new Color(.11f,.2f,.27f));
        }
        DrawControlsGuide(new Rect(panel.x+20,panel.y+350,panel.width-40,145));
        Rect back=new Rect(panel.x+20,panel.yMax-66,(panel.width-50)/2f,48);
        Rect apply=new Rect(back.xMax+10,panel.yMax-66,(panel.width-50)/2f,48);
        if(DrawPixelButton(back,"VOLVER",false,2)) CancelSettings();
        if(DrawPixelButton(apply,"APLICAR",true,2))
        {
            activeSkinIndex=pendingSkinIndex; ApplySkinColor(activeSkinIndex);
            PlayerPrefs.SetInt("SnakeSkin",activeSkinIndex); PlayerPrefs.SetFloat("SnakeVolume",pendingVolume);
            PlayerPrefs.Save(); settingsOpen=false;
        }
    }

    private void DrawTouchPad()
    {
        GetTouchPadRects(out Rect up,out Rect down,out Rect leftButton,out Rect rightButton);
        if(DrawPadButton(up,Vector2Int.up)) QueueDirection(Vector2Int.up);
        if(DrawPadButton(down,Vector2Int.down)) QueueDirection(Vector2Int.down);
        if(DrawPadButton(leftButton,Vector2Int.left)) QueueDirection(Vector2Int.left);
        if(DrawPadButton(rightButton,Vector2Int.right)) QueueDirection(Vector2Int.right);
    }

    private static void GetTouchPadRects(out Rect up,out Rect down,out Rect leftButton,out Rect rightButton)
    {
        Rect safe=Screen.safeArea;
        float size=Mathf.Clamp(Screen.width*.16f,54f,76f);
        float gap=5f;
        float x=safe.x+18f;
        float bottomInset=safe.y+16f;
        float y=Screen.height-bottomInset-size*2f-gap;
        up=new Rect(x+size+gap,y,size,size);
        down=new Rect(x+size+gap,y+size+gap,size,size);
        leftButton=new Rect(x,y+size+gap,size,size);
        rightButton=new Rect(x+(size+gap)*2f,y+size+gap,size,size);
    }

    private static bool DrawPadButton(Rect rect,Vector2Int arrow)
    {
        bool pressed=rect.Contains(Event.current.mousePosition) && Event.current.type==EventType.MouseDown;
        DrawPixelRect(new Rect(rect.x+4,rect.y+4,rect.width,rect.height),new Color(0,0,0,.38f));
        DrawPixelRect(rect,pressed?new Color(.25f,.7f,.42f,.9f):new Color(.05f,.12f,.19f,.72f));
        DrawPixelBorder(rect,3,new Color(.48f,.9f,.58f,.9f));
        Vector2 c=rect.center;
        float unit=rect.width*.12f;
        if(arrow==Vector2Int.up) DrawPixelArrow(c,unit,0);
        else if(arrow==Vector2Int.right) DrawPixelArrow(c,unit,1);
        else if(arrow==Vector2Int.down) DrawPixelArrow(c,unit,2);
        else DrawPixelArrow(c,unit,3);
        return IsLeftClick(rect);
    }

    private static void DrawPixelArrow(Vector2 center,float unit,int turns)
    {
        Vector2[] cells={new Vector2(0,-2),new Vector2(-1,-1),new Vector2(0,-1),new Vector2(1,-1),new Vector2(0,0),new Vector2(0,1),new Vector2(0,2)};
        foreach(Vector2 source in cells)
        {
            Vector2 p=source;
            for(int i=0;i<turns;i++) p=new Vector2(-p.y,p.x);
            DrawPixelRect(new Rect(center.x+p.x*unit-unit*.5f,center.y+p.y*unit-unit*.5f,unit,unit),Color.white);
        }
    }

    private void DrawSettingsOverlay()
    {
        if (UseMobileLayout())
        {
            DrawMobileSettingsOverlay();
            return;
        }
        DrawPixelRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.03f, 0.06f, 0.82f));

        int panelWidth = Mathf.Min(900, Screen.width - 32);
        int panelHeight = Mathf.Min(640, Screen.height - 32);
        int panelX = (Screen.width - panelWidth) / 2;
        int panelY = (Screen.height - panelHeight) / 2;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);

        DrawPixelRect(new Rect(panel.x + 12, panel.y + 12, panel.width, panel.height), new Color(0f, 0f, 0f, 0.55f));
        DrawPixelRect(panel, new Color(0.04f, 0.09f, 0.15f, 0.99f));
        DrawPixelBorder(panel, 6, new Color(0.48f, 0.9f, 0.58f));
        DrawPixelBorder(new Rect(panel.x + 14, panel.y + 14, panel.width - 28, panel.height - 28), 2, new Color(0.25f, 0.42f, 0.5f));

        DrawCenteredPixelText("AJUSTES", panelY + 35, 7, new Color(1f, 0.9f, 0.3f), 0);
        DrawPixelRect(new Rect(panelX + 55, panelY + 92, panelWidth - 110, 3), new Color(0.3f, 0.78f, 0.5f));
        DrawCenteredPixelText("COLOR DEL GUSANO", panelY + 108, 3, Color.white, 0);

        float cardGap = 10f;
        float cardsAreaWidth = panelWidth - 90f;
        float cardWidth = (cardsAreaWidth - cardGap * (SkinColors.Length - 1)) / SkinColors.Length;
        float cardsX = panelX + 45f;
        float cardsY = panelY + 145f;
        for (int i = 0; i < SkinColors.Length; i++)
        {
            Rect card = new Rect(cardsX + i * (cardWidth + cardGap), cardsY, cardWidth, 92f);
            bool selected = pendingSkinIndex == i;
            bool hovered = card.Contains(Event.current.mousePosition);
            DrawPixelRect(new Rect(card.x + 4, card.y + 4, card.width, card.height), new Color(0f, 0f, 0f, 0.45f));
            DrawPixelRect(card, hovered ? new Color(0.11f, 0.2f, 0.29f) : new Color(0.07f, 0.14f, 0.22f));
            DrawPixelBorder(card, selected ? 4 : 2, selected ? new Color(1f, 0.9f, 0.3f) : new Color(0.3f, 0.75f, 0.48f));
            DrawWormPreview(card, SkinColors[i]);

            if (selected)
            {
                DrawPixelRect(new Rect(card.xMax - 18, card.y + 9, 5, 14), new Color(1f, 0.9f, 0.3f));
                DrawPixelRect(new Rect(card.xMax - 13, card.y + 17, 10, 5), new Color(1f, 0.9f, 0.3f));
            }

            if (IsLeftClick(card))
            {
                pendingSkinIndex = i;
                ApplySkinColor(i);
            }
        }

        DrawPixelRect(new Rect(panelX + 55, panelY + 260, panelWidth - 110, 3), new Color(0.3f, 0.78f, 0.5f));
        DrawCenteredPixelText("SONIDO", panelY + 276, 3, Color.white, 0);

        Rect minusButton = new Rect(panelX + panelWidth / 2 - 205, panelY + 312, 58, 44);
        Rect plusButton = new Rect(panelX + panelWidth / 2 + 147, panelY + 312, 58, 44);
        if (DrawPixelButton(minusButton, "-", false, 4)) pendingVolume = Mathf.Max(0f, pendingVolume - 0.1f);
        if (DrawPixelButton(plusButton, "+", false, 4)) pendingVolume = Mathf.Min(1f, pendingVolume + 0.1f);
        AudioListener.volume = pendingVolume;

        for (int i = 0; i < 10; i++)
        {
            Rect block = new Rect(panelX + panelWidth / 2 - 130 + i * 27, panelY + 323, 19, 21);
            DrawPixelRect(block, i < Mathf.RoundToInt(pendingVolume * 10f)
                ? new Color(0.48f, 0.9f, 0.58f)
                : new Color(0.11f, 0.2f, 0.27f));
            DrawPixelBorder(block, 2, new Color(0.3f, 0.64f, 0.45f));
        }

        DrawControlsGuide(new Rect(panelX + 55, panelY + 382, panelWidth - 110, 150));

        Rect backButton = new Rect(panelX + 70, panelY + panelHeight - 72, 230, 48);
        Rect applyButton = new Rect(panelX + panelWidth - 300, panelY + panelHeight - 72, 230, 48);
        if (DrawPixelButton(backButton, "VOLVER", false, 3))
        {
            CancelSettings();
        }

        if (DrawPixelButton(applyButton, "APLICAR", true, 3))
        {
            activeSkinIndex = pendingSkinIndex;
            ApplySkinColor(activeSkinIndex);
            PlayerPrefs.SetInt("SnakeSkin", activeSkinIndex);
            PlayerPrefs.SetFloat("SnakeVolume", pendingVolume);
            PlayerPrefs.Save();
            settingsOpen = false;
        }
    }

    private void CancelSettings()
    {
        pendingSkinIndex = activeSkinIndex;
        pendingVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SnakeVolume", 1f));
        AudioListener.volume = pendingVolume;
        ApplySkinColor(activeSkinIndex);
        settingsOpen = false;
    }

    private static void DrawWormPreview(Rect card, Color color)
    {
        float size = Mathf.Min(30f, (card.width - 22f) / 3f);
        float startX = card.x + (card.width - size * 3f + 4f) / 2f;
        float y = card.y + (card.height - size) / 2f;
        Color border = color * 0.55f;
        border.a = 1f;
        for (int i = 0; i < 3; i++)
        {
            Rect segment = new Rect(startX + i * (size - 2f), y, size, size);
            DrawPixelRect(segment, color);
            DrawPixelBorder(segment, 2, border);
        }

        float headX = startX + 2f * (size - 2f);
        DrawPixelRect(new Rect(headX + size * 0.62f, y + size * 0.24f, 3, 5), Color.white);
        DrawPixelRect(new Rect(headX + size * 0.62f, y + size * 0.56f, 3, 5), Color.white);
    }

    private static void DrawControlsGuide(Rect area)
    {
        bool controllerConnected=Gamepad.current!=null;
        DrawPixelRect(new Rect(area.x+4,area.y+4,area.width,area.height),new Color(0,0,0,.35f));
        DrawPixelRect(area,new Color(.055f,.11f,.17f,.96f));
        DrawPixelBorder(area,3,controllerConnected?new Color(1f,.82f,.18f):new Color(.35f,.72f,.55f));
        DrawPixelRect(new Rect(area.x+14,area.y+14,9,9),controllerConnected?new Color(1f,.85f,.2f):new Color(.45f,.8f,.6f));
        string title=controllerConnected?"CONTROL CONECTADO":"TECLADO";
        DrawPixelText(title,Mathf.RoundToInt(area.x+34),Mathf.RoundToInt(area.y+12),2,Color.white);

        float icon=controllerConnected?38f:34f;
        float baseY=area.y+52;
        if(controllerConnected)
        {
            DrawPromptTile(new Rect(area.x+20,baseY,icon,icon),1,0);
            DrawPromptTile(new Rect(area.x+63,baseY,icon,icon),6,0);
            DrawPixelText("MOVER",Mathf.RoundToInt(area.x+112),Mathf.RoundToInt(baseY+12),2,new Color(.7f,.9f,.8f));

            float actionX=area.x+area.width*.55f;
            DrawPromptTile(new Rect(actionX,baseY,icon,icon),0,4);
            DrawPixelText("ACEPTAR",Mathf.RoundToInt(actionX+46),Mathf.RoundToInt(baseY+2),2,Color.white);
            DrawPromptTile(new Rect(actionX,baseY+45,icon,icon),0,5);
            DrawPixelText("VOLVER",Mathf.RoundToInt(actionX+46),Mathf.RoundToInt(baseY+52),2,Color.white);
            DrawPixelText("START PAUSA",Mathf.RoundToInt(area.x+22),Mathf.RoundToInt(area.y+118),2,new Color(1f,.88f,.35f));
        }
        else
        {
            float clusterX=area.x+25;
            DrawPromptTile(new Rect(clusterX+icon,baseY,icon,icon),2,18);
            DrawPromptTile(new Rect(clusterX,baseY+icon+3,icon,icon),3,17);
            DrawPromptTile(new Rect(clusterX+icon+3,baseY+icon+3,icon,icon),3,18);
            DrawPromptTile(new Rect(clusterX+(icon+3)*2,baseY+icon+3,icon,icon),3,19);
            DrawPixelText("MOVER",Mathf.RoundToInt(clusterX+icon*3+18),Mathf.RoundToInt(baseY+38),2,new Color(.7f,.9f,.8f));
            float infoX=area.x+area.width*.58f;
            DrawPixelText("ESPACIO JUGAR",Mathf.RoundToInt(infoX),Mathf.RoundToInt(baseY+4),2,Color.white);
            DrawPixelText("P ESC PAUSA",Mathf.RoundToInt(infoX),Mathf.RoundToInt(baseY+35),2,Color.white);
            DrawPixelText("R REINICIAR",Mathf.RoundToInt(infoX),Mathf.RoundToInt(baseY+66),2,new Color(1f,.88f,.35f));
        }
    }

    private static void DrawPromptTile(Rect destination,int row,int column)
    {
        if(inputPromptsTexture==null)
        {
            inputPromptsTexture=Resources.Load<Texture2D>("InputPrompts");
            if(inputPromptsTexture!=null) inputPromptsTexture.filterMode=FilterMode.Point;
        }
        if(inputPromptsTexture==null) return;
        const float tile=16f, stride=17f;
        Rect uv=new Rect(
            column*stride/inputPromptsTexture.width,
            (inputPromptsTexture.height-row*stride-tile)/inputPromptsTexture.height,
            tile/inputPromptsTexture.width,
            tile/inputPromptsTexture.height);
        GUI.DrawTextureWithTexCoords(destination,inputPromptsTexture,uv,true);
    }

    private static bool DrawPixelButton(Rect rect, string label, bool primary, int pixelSize)
    {
        bool hovered = rect.Contains(Event.current.mousePosition);
        Color background = primary
            ? (hovered ? new Color(1f, 0.95f, 0.52f) : new Color(1f, 0.84f, 0.25f))
            : (hovered ? new Color(0.18f, 0.31f, 0.4f) : new Color(0.09f, 0.17f, 0.25f));
        Color foreground = primary ? new Color(0.03f, 0.08f, 0.12f) : Color.white;
        DrawPixelRect(new Rect(rect.x + 5, rect.y + 5, rect.width, rect.height), new Color(0f, 0f, 0f, 0.48f));
        DrawPixelRect(rect, background);
        DrawPixelBorder(rect, 3, primary ? new Color(1f, 0.92f, 0.4f) : new Color(0.4f, 0.78f, 0.58f));
        DrawCenteredPixelTextInRect(label, rect, pixelSize, foreground);
        return IsLeftClick(rect);
    }

    private static bool IsLeftClick(Rect rect)
    {
        if (Event.current.type != EventType.MouseDown || Event.current.button != 0 || !rect.Contains(Event.current.mousePosition))
        {
            return false;
        }

        Event.current.Use();
        return true;
    }

    private static void DrawCenteredPixelTextInRect(string message, Rect rect, int pixelSize, Color color)
    {
        int width = (message.Length * 6 - 1) * pixelSize;
        int height = 7 * pixelSize;
        DrawPixelText(message, Mathf.RoundToInt(rect.center.x - width / 2f), Mathf.RoundToInt(rect.center.y - height / 2f), pixelSize, color);
    }

    private static void DrawVersionBadge()
    {
        string versionText = "V " + Application.version.ToUpperInvariant();
        const int pixelSize = 2;
        int textWidth = (versionText.Length * 6 - 1) * pixelSize;
        int badgeWidth = textWidth + 20;
        const int badgeHeight = 30;
        float badgeX = UseMobileLayout() ? Screen.width - badgeWidth - 10 : 10;
        Rect badge = new Rect(badgeX, Screen.height - badgeHeight - 10, badgeWidth, badgeHeight);

        DrawPixelRect(new Rect(badge.x + 3, badge.y + 3, badge.width, badge.height), new Color(0f, 0f, 0f, 0.38f));
        DrawPixelRect(badge, new Color(0.03f, 0.07f, 0.12f, 0.82f));
        DrawPixelBorder(badge, 2, new Color(0.25f, 0.65f, 0.43f, 0.9f));
        DrawPixelText(versionText, Mathf.RoundToInt(badge.x + 10), Mathf.RoundToInt(badge.y + 8), pixelSize, new Color(0.68f, 0.86f, 0.76f));
    }

    private void DrawPauseOverlay()
    {
        if (UseMobileLayout())
        {
            DrawMobilePauseOverlay();
            return;
        }
        DrawPixelRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.03f, 0.06f, 0.72f));

        int panelWidth = Mathf.Min(700, Screen.width - 32);
        int panelHeight = Mathf.Min(340, Screen.height - 32);
        int panelX = (Screen.width - panelWidth) / 2;
        int panelY = (Screen.height - panelHeight) / 2;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);

        DrawPixelRect(new Rect(panelX + 10, panelY + 10, panelWidth, panelHeight), new Color(0f, 0f, 0f, 0.55f));
        DrawPixelRect(panel, new Color(0.04f, 0.09f, 0.15f, 0.98f));
        DrawPixelBorder(panel, 8, new Color(0.12f, 0.78f, 0.28f));
        DrawPixelBorder(new Rect(panelX + 15, panelY + 15, panelWidth - 30, panelHeight - 30), 3, new Color(0.28f, 0.43f, 0.5f));

        DrawCenteredPixelText("PAUSA", panelY + 42, 9, new Color(0f, 0f, 0f, 0.8f), 5);
        DrawCenteredPixelText("PAUSA", panelY + 37, 9, new Color(1f, 0.9f, 0.25f), 0);
        DrawPixelRect(new Rect(panelX + 70, panelY + 115, panelWidth - 140, 4), new Color(0.12f, 0.78f, 0.28f));
        DrawCenteredPixelText("SCORE " + score.ToString("D3"), panelY + 139, 3, new Color(0.65f, 0.95f, 0.72f), 0);

        Rect continueButton = new Rect(panelX + 70, panelY + 195, 255, 58);
        Rect homeButton = new Rect(panelX + panelWidth - 325, panelY + 195, 255, 58);
        if (DrawPixelButton(continueButton, "CONTINUAR", true, 3))
        {
            paused = false;
        }

        if (DrawPixelButton(homeButton, "INICIO", false, 3))
        {
            ReturnToMainMenu();
        }

        if(Gamepad.current!=null)
        {
            DrawControllerFocus(pauseSelection==0?continueButton:homeButton);
        }

        DrawCenteredPixelText(Gamepad.current!=null?"D PAD ELEGIR  A ACEPTAR":"P ESC PARA SEGUIR", panelY + 286, 2, new Color(0.62f, 0.76f, 0.84f), 0);
    }

    private void DrawMobilePauseOverlay()
    {
        DrawPixelRect(new Rect(0,0,Screen.width,Screen.height),new Color(.01f,.03f,.06f,.76f));
        Rect panel=new Rect(16,(Screen.height-340)/2f,Screen.width-32,340);
        DrawPixelRect(panel,new Color(.04f,.09f,.15f,.98f));
        DrawPixelBorder(panel,6,new Color(.12f,.78f,.28f));
        DrawCenteredPixelText("PAUSA",Mathf.RoundToInt(panel.y+34),6,new Color(1f,.9f,.25f),0);
        DrawCenteredPixelText("SCORE "+score.ToString("D3"),Mathf.RoundToInt(panel.y+102),3,new Color(.65f,.95f,.72f),0);
        Rect resume=new Rect(panel.x+26,panel.y+155,panel.width-52,56);
        Rect home=new Rect(panel.x+26,panel.y+230,panel.width-52,56);
        if(DrawPixelButton(resume,"CONTINUAR",true,3)) paused=false;
        if(DrawPixelButton(home,"INICIO",false,3)) ReturnToMainMenu();
        if(Gamepad.current!=null) DrawControllerFocus(pauseSelection==0?resume:home);
    }

    private static void DrawControllerFocus(Rect rect)
    {
        Color glow=new Color(1f,.9f,.22f);
        DrawPixelBorder(new Rect(rect.x-7,rect.y-7,rect.width+14,rect.height+14),4,glow);
        const float corner=10f;
        DrawPixelRect(new Rect(rect.x-10,rect.y-10,corner,corner),glow);
        DrawPixelRect(new Rect(rect.xMax,rect.y-10,corner,corner),glow);
        DrawPixelRect(new Rect(rect.x-10,rect.yMax,corner,corner),glow);
        DrawPixelRect(new Rect(rect.xMax,rect.yMax,corner,corner),glow);
    }

    private static void DrawAppleIcon(Rect destination)
    {
        if (appleUiSprite == null)
        {
            appleUiSprite = Resources.Load<Sprite>("Apple");
        }

        if (appleUiSprite == null)
        {
            return;
        }

        Texture2D texture = appleUiSprite.texture;
        Rect source = appleUiSprite.textureRect;
        Rect uv = new Rect(
            source.x / texture.width,
            source.y / texture.height,
            source.width / texture.width,
            source.height / texture.height
        );
        GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
    }

    private static void DrawCenteredPixelText(string message, int y, int pixelSize, Color color, int shadowOffset)
    {
        int width = (message.Length * 6 - 1) * pixelSize;
        DrawPixelText(message, (Screen.width - width) / 2 + shadowOffset, y + shadowOffset, pixelSize, color);
    }

    private static void DrawPixelText(string message, int x, int y, int pixelSize, Color color)
    {
        for (int characterIndex = 0; characterIndex < message.Length; characterIndex++)
        {
            string[] glyph;
            if (!PixelGlyphs.TryGetValue(message[characterIndex], out glyph))
            {
                continue;
            }

            for (int row = 0; row < glyph.Length; row++)
            {
                for (int column = 0; column < glyph[row].Length; column++)
                {
                    if (glyph[row][column] == '1')
                    {
                        DrawPixelRect(new Rect(
                            x + (characterIndex * 6 + column) * pixelSize,
                            y + row * pixelSize,
                            pixelSize,
                            pixelSize
                        ), color);
                    }
                }
            }
        }
    }

    private static void DrawPixelBorder(Rect rect, int thickness, Color color)
    {
        DrawPixelRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawPixelRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawPixelRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawPixelRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static void DrawPixelRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
