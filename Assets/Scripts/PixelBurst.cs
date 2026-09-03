using System.Collections.Generic;
using UnityEngine;

public class PixelBurst : MonoBehaviour
{
    private static Sprite pixelSprite;
    private readonly List<Transform> pixels = new List<Transform>();
    private readonly List<Vector2> velocities = new List<Vector2>();
    private float elapsed;

    public static void Create(Vector3 position, Color color)
    {
        GameObject burstObject = new GameObject("Pixel Burst");
        burstObject.transform.position = position;
        PixelBurst burst = burstObject.AddComponent<PixelBurst>();
        burst.Build(color);
    }

    private void Build(Color color)
    {
        for (int i = 0; i < 12; i++)
        {
            GameObject pixel = new GameObject("Pixel");
            pixel.transform.SetParent(transform);
            pixel.transform.localPosition = Vector3.zero;
            pixel.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

            SpriteRenderer renderer = pixel.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            renderer.color = i % 3 == 0 ? new Color(1f, 0.85f, 0.2f) : color;
            renderer.sortingOrder = 10;

            pixels.Add(pixel.transform);
            velocities.Add(Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.8f));
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        for (int i = 0; i < pixels.Count; i++)
        {
            pixels[i].position += (Vector3)(velocities[i] * Time.deltaTime);
            pixels[i].localScale = Vector3.one * Mathf.Max(0f, (1f - elapsed / 0.35f) * 0.14f);
        }

        if (elapsed >= 0.35f)
        {
            Destroy(gameObject);
        }
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
        {
            return pixelSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.filterMode = FilterMode.Point;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return pixelSprite;
    }
}
