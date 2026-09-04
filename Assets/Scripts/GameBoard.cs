using UnityEngine;

public static class GameBoard
{
    private const float WallThickness = 0.08f;
    private const float ScreenInset = 0.25f;
    private static Sprite squareSprite;
    private static Sprite checkerSprite;

    public static float LeftBoundary { get; private set; }
    public static float RightBoundary { get; private set; }
    public static float BottomBoundary { get; private set; }
    public static float TopBoundary { get; private set; }
    public static bool IsReady { get; private set; }
    public static int CellCount => !IsReady ? 0 :
        Mathf.RoundToInt(RightBoundary - LeftBoundary) * Mathf.RoundToInt(TopBoundary - BottomBoundary);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateBoard()
    {
        Food food = Object.FindFirstObjectByType<Food>();
        if (food == null)
        {
            GameObject foodObject = new GameObject("Food");
            food = foodObject.AddComponent<Food>();
        }

        if (GameObject.Find("Board Walls") != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        float halfHeight = mainCamera != null && mainCamera.orthographic ? mainCamera.orthographicSize : 5f;
        float halfWidth = mainCamera != null && mainCamera.orthographic
            ? halfHeight * mainCamera.aspect
            : 8f;

        food.minX = Mathf.CeilToInt(-halfWidth + ScreenInset + 0.5f);
        food.maxX = Mathf.FloorToInt(halfWidth - ScreenInset - 0.5f);
        food.minY = Mathf.CeilToInt(-halfHeight + ScreenInset + 0.5f);
        food.maxY = Mathf.FloorToInt(halfHeight - ScreenInset - 0.5f);

        // Las líneas caen en el borde exacto de las casillas exteriores.
        LeftBoundary = food.minX - 0.5f;
        RightBoundary = food.maxX + 0.5f;
        BottomBoundary = food.minY - 0.5f;
        TopBoundary = food.maxY + 0.5f;
        IsReady = true;

        GameObject walls = new GameObject("Board Walls");
        CreateCheckerboard(walls.transform);
        CreateWall(walls.transform, "Top", new Vector2(LeftBoundary, TopBoundary), new Vector2(RightBoundary, TopBoundary));
        CreateWall(walls.transform, "Bottom", new Vector2(LeftBoundary, BottomBoundary), new Vector2(RightBoundary, BottomBoundary));
        CreateWall(walls.transform, "Left", new Vector2(LeftBoundary, BottomBoundary), new Vector2(LeftBoundary, TopBoundary));
        CreateWall(walls.transform, "Right", new Vector2(RightBoundary, BottomBoundary), new Vector2(RightBoundary, TopBoundary));
    }

    private static void CreateCheckerboard(Transform parent)
    {
        GameObject background = new GameObject("Checkerboard");
        background.transform.SetParent(parent);
        background.transform.position = new Vector3(
            (LeftBoundary + RightBoundary) * 0.5f,
            (BottomBoundary + TopBoundary) * 0.5f,
            1f
        );

        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = GetCheckerSprite();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = new Vector2(RightBoundary - LeftBoundary, TopBoundary - BottomBoundary);
        renderer.sortingOrder = -10;
    }

    public static bool WouldCrossWall(Vector3 centerPosition)
    {
        if (!IsReady)
        {
            return false;
        }

        const float halfSegment = 0.5f;
        return centerPosition.x - halfSegment < LeftBoundary
            || centerPosition.x + halfSegment > RightBoundary
            || centerPosition.y - halfSegment < BottomBoundary
            || centerPosition.y + halfSegment > TopBoundary;
    }

    public static Vector3 WrapPosition(Vector3 centerPosition)
    {
        if (!IsReady) return centerPosition;
        if (centerPosition.x - 0.5f < LeftBoundary) centerPosition.x = RightBoundary - 0.5f;
        else if (centerPosition.x + 0.5f > RightBoundary) centerPosition.x = LeftBoundary + 0.5f;
        if (centerPosition.y - 0.5f < BottomBoundary) centerPosition.y = TopBoundary - 0.5f;
        else if (centerPosition.y + 0.5f > TopBoundary) centerPosition.y = BottomBoundary + 0.5f;
        return centerPosition;
    }

    private static void CreateWall(Transform parent, string name, Vector2 start, Vector2 end)
    {
        GameObject wall = new GameObject(name);
        wall.tag = "Wall";
        wall.transform.SetParent(parent);

        Vector2 midpoint = (start + end) * 0.5f;
        wall.transform.position = midpoint;

        bool horizontal = Mathf.Abs(end.x - start.x) > Mathf.Abs(end.y - start.y);
        float length = Vector2.Distance(start, end);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(wall.transform);
        visual.transform.localPosition = Vector3.zero;

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = new Color(0.08f, 0.12f, 0.2f);
        visual.transform.localScale = horizontal
            ? new Vector3(length, WallThickness, 1f)
            : new Vector3(WallThickness, length, 1f);

        EdgeCollider2D collider = wall.AddComponent<EdgeCollider2D>();
        collider.isTrigger = true;
        collider.points = new[] { start - midpoint, end - midpoint };
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.name = "Generated Board Square";
        texture.filterMode = FilterMode.Point;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }

    private static Sprite GetCheckerSprite()
    {
        if (checkerSprite != null)
        {
            return checkerSprite;
        }

        Color lightSquare = new Color(0.20f, 0.34f, 0.52f);
        Color darkSquare = new Color(0.16f, 0.29f, 0.46f);
        Texture2D texture = new Texture2D(2, 2);
        texture.name = "Generated Checkerboard";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.SetPixel(0, 0, lightSquare);
        texture.SetPixel(1, 0, darkSquare);
        texture.SetPixel(0, 1, darkSquare);
        texture.SetPixel(1, 1, lightSquare);
        texture.Apply();

        checkerSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        checkerSprite.name = "Generated Checkerboard";
        return checkerSprite;
    }
}
