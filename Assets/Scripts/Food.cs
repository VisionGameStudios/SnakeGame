using System.Collections.Generic;
using UnityEngine;

public class Food : MonoBehaviour
{
    const float DoubleChance=.28f, GoldenChance=.025f, GoldenLife=5f;
    const int DoubleThreshold=15, RingSegments=32;
    static Food bonusFood, goldenFood;
    public int minX=-4,maxX=4,minY=-4,maxY=4;
    bool isBonus,isGolden;
    float goldenExpiresAt;
    SpriteRenderer spriteRenderer;
    LineRenderer countdownRing;
    public int PointValue => isGolden ? 3 : 1;
    public bool IsGolden => isGolden;

    void Awake()
    {
        gameObject.tag="Food";
        spriteRenderer=GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
        if(spriteRenderer.sprite==null) spriteRenderer.sprite=Resources.Load<Sprite>("Apple");
        CircleCollider2D collider=GetComponent<CircleCollider2D>() ?? gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger=true; collider.radius=.42f;
    }

    void Start(){ RandomizePosition(); }

    void Update()
    {
        if(!isGolden) return;
        float remaining=goldenExpiresAt-Time.unscaledTime;
        UpdateRing(Mathf.Clamp01(remaining/GoldenLife));
        if(remaining<=0f) Destroy(gameObject);
    }

    public bool RandomizePosition()
    {
        Snake snake=Object.FindFirstObjectByType<Snake>();
        Food[] foods=Object.FindObjectsByType<Food>(FindObjectsSortMode.None);
        List<Vector2> available=new List<Vector2>();
        List<Vector2> visibleAvailable=new List<Vector2>();
        for(int x=minX;x<=maxX;x++) for(int y=minY;y<=maxY;y++)
        {
            Vector2 candidate=new Vector2(x,y);
            if(IsAvailable(candidate,snake,foods))
            {
                available.Add(candidate);
                if(!IsBehindScore(candidate)) visibleAvailable.Add(candidate);
            }
        }
        if(available.Count==0)
        {
            if(snake!=null) snake.HandleBoardCompleted();
            return false;
        }
        List<Vector2> pool=visibleAvailable.Count>0 ? visibleAvailable : available;
        transform.position=pool[Random.Range(0,pool.Count)];
        return true;
    }

    bool IsBehindScore(Vector2 candidate)
    {
        Camera camera=Camera.main;
        if(camera==null) return false;
        Vector3 screen=camera.WorldToScreenPoint(candidate);
        return screen.x<180f && screen.y>Screen.height-70f;
    }

    bool IsAvailable(Vector2 candidate,Snake snake,Food[] foods)
    {
        if(snake!=null && snake.IsPositionReservedForSnake(candidate)) return false;
        foreach(Food food in foods)
            if(food!=this && Vector2.SqrMagnitude((Vector2)food.transform.position-candidate)<.01f) return false;
        foreach(Collider2D hit in Physics2D.OverlapPointAll(candidate))
            if(hit.gameObject!=gameObject && (hit.CompareTag("Snake")||hit.CompareTag("Food"))) return false;
        return true;
    }

    public void HandleEaten(int score)
    {
        if(isGolden){ goldenFood=null; Destroy(gameObject); return; }
        if(isBonus){ bonusFood=null; Destroy(gameObject); return; }
        if(!RandomizePosition()) return;
        if(score>=DoubleThreshold && bonusFood==null && Random.value<DoubleChance)
        {
            bonusFood=CreateFood("Bonus Food"); bonusFood.isBonus=true;
        }
        if(goldenFood==null && Random.value<GoldenChance) SpawnGolden();
    }

    Food CreateFood(string objectName)
    {
        Food food=new GameObject(objectName).AddComponent<Food>();
        food.minX=minX; food.maxX=maxX; food.minY=minY; food.maxY=maxY;
        return food;
    }

    void SpawnGolden()
    {
        goldenFood=CreateFood("Golden Apple");
        goldenFood.isGolden=true;
        goldenFood.spriteRenderer.sprite=Resources.Load<Sprite>("GoldenApple");
        goldenFood.goldenExpiresAt=Time.unscaledTime+GoldenLife;
        goldenFood.CreateRing();
    }

    void CreateRing()
    {
        GameObject ring=new GameObject("Golden Timer");
        ring.transform.SetParent(transform,false);
        ring.transform.localPosition=new Vector3(0f,.72f,-.1f);
        countdownRing=ring.AddComponent<LineRenderer>();
        countdownRing.useWorldSpace=false; countdownRing.loop=false;
        countdownRing.startWidth=countdownRing.endWidth=.065f;
        countdownRing.startColor=countdownRing.endColor=new Color(1f,.86f,.15f);
        countdownRing.material=new Material(Shader.Find("Sprites/Default"));
        countdownRing.sortingOrder=spriteRenderer.sortingOrder+2;
        UpdateRing(1f);
    }

    void UpdateRing(float fraction)
    {
        if(countdownRing==null) return;
        int visible=Mathf.Max(1,Mathf.CeilToInt(RingSegments*fraction));
        countdownRing.positionCount=visible+1;
        for(int i=0;i<=visible;i++)
        {
            float angle=Mathf.PI*.5f-i*Mathf.PI*2f/RingSegments;
            countdownRing.SetPosition(i,new Vector3(Mathf.Cos(angle)*.23f,Mathf.Sin(angle)*.23f,0f));
        }
    }

    public static void ResetBonusFood()
    {
        if(bonusFood!=null){ Destroy(bonusFood.gameObject); bonusFood=null; }
        if(goldenFood!=null){ Destroy(goldenFood.gameObject); goldenFood=null; }
    }

    void OnDestroy()
    {
        if(bonusFood==this) bonusFood=null;
        if(goldenFood==this) goldenFood=null;
    }
}
