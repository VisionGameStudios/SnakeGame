using UnityEngine;

public class Food : MonoBehaviour
{
    public int minX = -4;
    public int maxX = 4;

    public int minY = -4;
    public int maxY = 4;

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
        for (int attempt = 0; attempt < 50; attempt++)
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

            bool occupiedBySnake = false;
            Collider2D[] hits = Physics2D.OverlapPointAll(candidate);
            foreach (Collider2D hit in hits)
            {
                if (hit.CompareTag("Snake"))
                {
                    occupiedBySnake = true;
                    break;
                }
            }

            if (!occupiedBySnake)
            {
                transform.position = candidate;
                return;
            }
        }
    }
}
