using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Snake : MonoBehaviour
{
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
    private static Sprite appleUiSprite;
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
    private AudioSource audioSource;
    private AudioClip eatSound;
    private AudioClip moveSound;
    private AudioClip loseSound;
    private AudioClip recordSound;

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
        bestScore = PlayerPrefs.GetInt("SnakeBestScore", 0);
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

    }

    private void Update()
    {
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

            return;
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
        }

        if (paused)
        {
            return;
        }

        HandleInput();
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

    private static bool WasPressed(Key key, KeyCode fallback)
    {
        return (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame)
            || Input.GetKeyDown(fallback);
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

            // Mantiene el movimiento sobre la cuadrícula, pero limita la espera
            // de una entrada nueva a una fracción muy corta del ciclo.
            timer = Mathf.Max(timer, Mathf.Max(0f, moveTime - 0.02f));
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

        if (GameBoard.WouldCrossWall(nextPosition))
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
            PixelBurst.Create(other.transform.position, new Color(1f, 0.18f, 0.12f));
            Grow();
            score++;

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

            Food food = other.GetComponent<Food>();

            if (food != null)
            {
                food.RandomizePosition();
            }
        }
        else if (other.CompareTag("Wall"))
        {
            LoseGame();
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
        SetDeadHeadSprite();
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
        paused = false;
        score = 0;
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

    private void RestoreHeadSprite()
    {
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
                return;
            }

            if (DrawStartOverlay())
            {
                gameStarted = true;
            }
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

        if (paused)
        {
            DrawPauseOverlay();
            return;
        }

        if (!gameOver)
        {
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

        DrawCenteredPixelText("GAME OVER", panelY + 48, 9, new Color(0.01f, 0.02f, 0.04f), 5);
        DrawCenteredPixelText("GAME OVER", panelY + 43, 9, new Color(1f, 0.9f, 0.3f), 0);
        DrawPixelRect(new Rect(panelX + 90, panelY + 120, panelWidth - 180, 4), new Color(0.1f, 0.7f, 0.25f));
        DrawCenteredPixelText("SCORE " + score.ToString("D3"), panelY + 145, 4, Color.white, 0);
        DrawCenteredPixelText("RECORD " + bestScore.ToString("D3"), panelY + 185, 3, new Color(0.65f, 0.95f, 0.72f), 0);
        DrawCenteredPixelText("R O ESPACIO PARA REINICIAR", panelY + 239, 3, new Color(1f, 0.9f, 0.3f), 0);
    }

    private bool DrawStartOverlay()
    {
        DrawPixelRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.03f, 0.06f, 0.76f));

        int panelWidth = Mathf.Min(760, Screen.width - 32);
        int panelHeight = Mathf.Min(420, Screen.height - 32);
        int panelX = (Screen.width - panelWidth) / 2;
        int panelY = (Screen.height - panelHeight) / 2;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);

        DrawPixelRect(new Rect(panel.x + 12, panel.y + 12, panel.width, panel.height), new Color(0f, 0f, 0f, 0.55f));
        DrawPixelRect(panel, new Color(0.04f, 0.09f, 0.15f, 0.98f));
        DrawPixelBorder(panel, 8, new Color(0.24f, 0.9f, 0.46f));
        DrawPixelBorder(new Rect(panel.x + 16, panel.y + 16, panel.width - 32, panel.height - 32), 3, new Color(0.28f, 0.43f, 0.5f));
        DrawPixelRect(new Rect(panel.x + 8, panel.y + 8, panel.width - 16, 5), new Color(1f, 0.9f, 0.3f));

        int titleScale = Mathf.Clamp((panelWidth - 100) / 35, 8, 14);
        DrawCenteredPixelText("SNAKE", panelY + 50, titleScale, new Color(0f, 0f, 0f, 0.8f), 6);
        DrawCenteredPixelText("SNAKE", panelY + 44, titleScale, new Color(1f, 0.9f, 0.25f), 0);
        DrawCenteredPixelText("PIXEL ARCADE", panelY + 145, 3, new Color(0.65f, 0.95f, 0.72f), 0);

        Rect playButton = new Rect(panelX + panelWidth / 2 - 260, panelY + 195, 245, 62);
        Rect settingsButton = new Rect(panelX + panelWidth / 2 + 15, panelY + 195, 245, 62);
        bool playClicked = DrawPixelButton(playButton, "JUGAR", true, 4);
        if (DrawPixelButton(settingsButton, "AJUSTES", false, 3))
        {
            pendingSkinIndex = activeSkinIndex;
            pendingVolume = AudioListener.volume;
            settingsOpen = true;
        }

        DrawCenteredPixelText("ESPACIO O ENTER", panelY + 278, 2, Color.white, 0);
        DrawPixelRect(new Rect(panelX + 70, panelY + 316, panelWidth - 140, 3), new Color(0.12f, 0.58f, 0.34f));
        DrawCenteredPixelText("WASD O FLECHAS", panelY + 337, 2, new Color(0.65f, 0.8f, 0.88f), 0);
        DrawCenteredPixelText("RECORD " + bestScore.ToString("D3"), panelY + 377, 3, new Color(1f, 0.9f, 0.3f), 0);

        return playClicked;
    }

    private void DrawSettingsOverlay()
    {
        DrawPixelRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.03f, 0.06f, 0.82f));

        int panelWidth = Mathf.Min(900, Screen.width - 32);
        int panelHeight = Mathf.Min(520, Screen.height - 32);
        int panelX = (Screen.width - panelWidth) / 2;
        int panelY = (Screen.height - panelHeight) / 2;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);

        DrawPixelRect(new Rect(panel.x + 12, panel.y + 12, panel.width, panel.height), new Color(0f, 0f, 0f, 0.55f));
        DrawPixelRect(panel, new Color(0.04f, 0.09f, 0.15f, 0.99f));
        DrawPixelBorder(panel, 6, new Color(0.48f, 0.9f, 0.58f));
        DrawPixelBorder(new Rect(panel.x + 14, panel.y + 14, panel.width - 28, panel.height - 28), 2, new Color(0.25f, 0.42f, 0.5f));

        DrawCenteredPixelText("AJUSTES", panelY + 35, 7, new Color(1f, 0.9f, 0.3f), 0);
        DrawPixelRect(new Rect(panelX + 55, panelY + 105, panelWidth - 110, 3), new Color(0.3f, 0.78f, 0.5f));
        DrawCenteredPixelText("COLOR DEL GUSANO", panelY + 124, 3, Color.white, 0);

        float cardGap = 10f;
        float cardsAreaWidth = panelWidth - 90f;
        float cardWidth = (cardsAreaWidth - cardGap * (SkinColors.Length - 1)) / SkinColors.Length;
        float cardsX = panelX + 45f;
        float cardsY = panelY + 172f;
        for (int i = 0; i < SkinColors.Length; i++)
        {
            Rect card = new Rect(cardsX + i * (cardWidth + cardGap), cardsY, cardWidth, 112f);
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

        DrawPixelRect(new Rect(panelX + 55, panelY + 313, panelWidth - 110, 3), new Color(0.3f, 0.78f, 0.5f));
        DrawCenteredPixelText("SONIDO", panelY + 332, 3, Color.white, 0);

        Rect minusButton = new Rect(panelX + panelWidth / 2 - 205, panelY + 372, 58, 48);
        Rect plusButton = new Rect(panelX + panelWidth / 2 + 147, panelY + 372, 58, 48);
        if (DrawPixelButton(minusButton, "-", false, 4)) pendingVolume = Mathf.Max(0f, pendingVolume - 0.1f);
        if (DrawPixelButton(plusButton, "+", false, 4)) pendingVolume = Mathf.Min(1f, pendingVolume + 0.1f);
        AudioListener.volume = pendingVolume;

        for (int i = 0; i < 10; i++)
        {
            Rect block = new Rect(panelX + panelWidth / 2 - 130 + i * 27, panelY + 384, 19, 23);
            DrawPixelRect(block, i < Mathf.RoundToInt(pendingVolume * 10f)
                ? new Color(0.48f, 0.9f, 0.58f)
                : new Color(0.11f, 0.2f, 0.27f));
            DrawPixelBorder(block, 2, new Color(0.3f, 0.64f, 0.45f));
        }

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

    private void DrawPauseOverlay()
    {
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

        DrawCenteredPixelText("P O ESC PARA SEGUIR", panelY + 286, 2, new Color(0.62f, 0.76f, 0.84f), 0);
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
