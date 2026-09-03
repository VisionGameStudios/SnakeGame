using UnityEngine;

public class Food : MonoBehaviour
{
    private const float DoubleSpawnChance = 0.28f;
    private const int DoubleSpawnThreshold = 15;
    private static Food bonusFood;

    public int minX = -4;
    public int maxX = 4;

    public int minY = -4;
    public int maxY = 4;

    private bool isBonus;

    private void Awake()
    {
        gameObject.tag = "Food";

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = Resources.Load<Sprite>("Apple");
        }

        CircleCollider2D collider = GetComponent<CircleCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CircleCollider2D>();
        }

        collider.isTrigger = true;
        collider.radius = 0.42f;
    }

    private void Start()
    {
        RandomizePosition();
    }

    public void RandomizePosition()
    {
        Snake snake = Object.FindFirstObjectByType<Snake>();
        Food[] foods = Object.FindObjectsByType<Food>(FindObjectsSortMode.None);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            int x = Random.Range(minX, maxX + 1);
            int y = Random.Range(minY, maxY + 1);
            Vector2 candidate = new Vector2(x, y);

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 screenPosition = mainCamera.WorldToScreenPoint(candidate);
                bool hiddenByScore = screenPosition.x < 180f && screenPosition.y > Screen.height - 70f;
                if (hiddenByScore)
                {
                    continue;
                }
            }

            bool occupied = snake != null && snake.IsPositionReservedForSnake(candidate);
            foreach (Food food in foods)
            {
                if (food != this && Vector2.SqrMagnitude((Vector2)food.transform.position - candidate) < 0.01f)
                {
                    occupied = true;
                    break;
                }
            }

            if (occupied)
            {
                continue;
            }

            Collider2D[] hits = Physics2D.OverlapPointAll(candidate);
            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject != gameObject && (hit.CompareTag("Snake") || hit.CompareTag("Food")))
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
            {
                transform.position = candidate;
                return;
            }
        }
    }

    public void HandleEaten(int applesEaten)
    {
        if (isBonus)
        {
            if (bonusFood == this)
            {
                bonusFood = null;
            }

            Destroy(gameObject);
            return;
        }

        RandomizePosition();

        if (applesEaten >= DoubleSpawnThreshold && bonusFood == null && Random.value < DoubleSpawnChance)
        {
            SpawnBonusFood();
        }
    }

    private void SpawnBonusFood()
    {
        GameObject bonusObject = new GameObject("Bonus Food");
        Food bonus = bonusObject.AddComponent<Food>();
        bonus.minX = minX;
        bonus.maxX = maxX;
        bonus.minY = minY;
        bonus.maxY = maxY;
        bonus.isBonus = true;
        bonusFood = bonus;
    }

    public static void ResetBonusFood()
    {
        if (bonusFood != null)
        {
            Destroy(bonusFood.gameObject);
            bonusFood = null;
        }
    }

    private void OnDestroy()
    {
        if (bonusFood == this)
        {
            bonusFood = null;
        }
    }
}
