using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private bool gameOver;
    private bool gameStarted;
    private bool paused;
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
        { 'G', new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" } },
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
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            QueueDirection(Vector2Int.up);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            QueueDirection(Vector2Int.down);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            QueueDirection(Vector2Int.left);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            QueueDirection(Vector2Int.right);
    }

    private void QueueDirection(Vector2Int newDirection)
    {
        Vector2Int lastDirection = pendingDirections.Count > 0
            ? pendingDirections.ToArray()[pendingDirections.Count - 1]
            : direction;

        if (newDirection != lastDirection && newDirection != -lastDirection && pendingDirections.Count < 2)
        {
            pendingDirections.Enqueue(newDirection);
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
            DrawMenuOverlay("SNAKE", "ESPACIO PARA JUGAR", "RECORD " + bestScore.ToString("D3"));
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

        if (paused)
        {
            DrawMenuOverlay("PAUSA", "P O ESC PARA SEGUIR", "SCORE " + score.ToString("D3"));
            return;
        }

        if (!gameOver)
        {
            return;
        }

        int panelWidth = Mathf.Min(900, Screen.width - 32);
        int panelHeight = 270;
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

        DrawCenteredPixelText("GAME OVER", panelY + 55, 10, new Color(0.01f, 0.02f, 0.04f), 6);
        DrawCenteredPixelText("GAME OVER", panelY + 49, 10, new Color(1f, 0.9f, 0.3f), 0);
        DrawPixelRect(new Rect(panelX + 90, panelY + 142, panelWidth - 180, 4), new Color(0.1f, 0.7f, 0.25f));
        DrawCenteredPixelText("SCORE " + score.ToString("D3"), panelY + 164, 4, Color.white, 0);
        DrawCenteredPixelText("PULSA R O ESPACIO PARA REINICIAR", panelY + 218, 3, new Color(0.7f, 0.95f, 0.76f), 0);
    }

    private static void DrawMenuOverlay(string title, string action, string footer)
    {
        DrawPixelRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.03f, 0.06f, 0.72f));

        int panelWidth = Mathf.Min(700, Screen.width - 32);
        int panelHeight = Mathf.Min(280, Screen.height - 32);
        int panelX = (Screen.width - panelWidth) / 2;
        int panelY = (Screen.height - panelHeight) / 2;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);

        DrawPixelRect(new Rect(panelX + 10, panelY + 10, panelWidth, panelHeight), new Color(0f, 0f, 0f, 0.55f));
        DrawPixelRect(panel, new Color(0.04f, 0.09f, 0.15f, 0.98f));
        DrawPixelBorder(panel, 8, new Color(0.12f, 0.78f, 0.28f));
        DrawPixelBorder(new Rect(panelX + 15, panelY + 15, panelWidth - 30, panelHeight - 30), 3, new Color(0.28f, 0.43f, 0.5f));

        int titleScale = Mathf.Min(10, Mathf.Max(5, (panelWidth - 80) / Mathf.Max(1, title.Length * 6)));
        DrawCenteredPixelText(title, panelY + 48, titleScale, new Color(0f, 0f, 0f, 0.8f), 5);
        DrawCenteredPixelText(title, panelY + 43, titleScale, new Color(1f, 0.9f, 0.25f), 0);
        DrawPixelRect(new Rect(panelX + 70, panelY + 135, panelWidth - 140, 4), new Color(0.12f, 0.78f, 0.28f));
        DrawCenteredPixelText(action, panelY + 165, 3, Color.white, 0);
        DrawCenteredPixelText(footer, panelY + 220, 3, new Color(0.65f, 0.95f, 0.72f), 0);
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
