using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MazeGenerator : MonoBehaviour
{
    [Header("Minimap")]
    public RawImage MinimapImage;
    public Color WallColor = Color.black;
    public Color PathColor = Color.white;
    public Color PlayerColor = Color.red;
    public Color GoalColor = Color.green;

    [Header("UI Text")]
    public TMP_Text TimeText;
    public TMP_Text SeedText;
    public TMP_Text PositionText;
    public TMP_Text SizeText;
    public GameObject EscapeUI;

    [Header("Player")]
    public Transform Player;

    [Header("Maze Size")]
    public int WIDTH = 21;
    public int HEIGHT = 21;

    [Header("Random Maze Range")]
    public int MinMazeSize = 20;
    public int MaxMazeSize = 50;

    [Header("Minimap Scaling")]
    public float MaxMinimapSize = 350f; // 50x50 maze
    public float MinMinimapSize = 140f; // 20x20 maze

    [Header("Prefabs")]
    public GameObject WallPrefab;
    public GameObject FloorPrefab;
    public GameObject GoalPrefab;

    [Header("Settings")]
    public float CellSize = 2f;
    public int SEED;

    private bool[,] maze;
    private HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

    private Texture2D minimapTexture;

    private Vector2Int goalCell;
    private GameObject goalObject;

    private static readonly Vector2Int NORTH = new Vector2Int(0, -2);
    private static readonly Vector2Int SOUTH = new Vector2Int(0, 2);
    private static readonly Vector2Int EAST  = new Vector2Int(2, 0);
    private static readonly Vector2Int WEST  = new Vector2Int(-2, 0);

    private bool hasWon = false;
    private float runTime;

    void Start()
    {
        runTime = 0f;

        EscapeUI.SetActive(false);

        // RANDOM SIZE BETWEEN 20 AND 50
        int randomSize = Random.Range(MinMazeSize, MaxMazeSize + 1);

        // Force odd number for proper maze generation
        if (randomSize % 2 == 0)
            randomSize++;

        WIDTH = randomSize;
        HEIGHT = randomSize;

        if (SEED == 0)
            SEED = Random.Range(1, 32768);

        Random.InitState(SEED);

        GenerateMaze();
        BuildMaze();
        SpawnGoal();

        minimapTexture = new Texture2D(WIDTH, HEIGHT);
        minimapTexture.filterMode = FilterMode.Point;
        MinimapImage.texture = minimapTexture;

        // SCALE MINIMAP SIZE PROPORTIONALLY
        ScaleMinimap();

        GenerateMinimap();
    }

    void ScaleMinimap()
    {
        RectTransform rt = MinimapImage.rectTransform;

        // Convert maze size into 0-1 range
        float t = Mathf.InverseLerp(MinMazeSize, MaxMazeSize, WIDTH);

        // Scale from small -> large
        float size = Mathf.Lerp(MinMinimapSize, MaxMinimapSize, t);

        rt.sizeDelta = new Vector2(size, size);

        // TOP LEFT ANCHOR
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);

        // Optional padding from screen edges
        rt.anchoredPosition = new Vector2(20f, -120f);
    }

    void Update()
    {
        if (!hasWon)
        {
            runTime += Time.deltaTime;
        }

        GenerateMinimap();
        UpdateUI();

        CheckWin();

        HandleWinInput();
    }

    void HandleWinInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;

            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            Application.Quit();
        }
    }

    void UpdateUI()
    {
        TimeText.text = "Time: " + runTime.ToString("0.0");
        SeedText.text = "Seed: " + SEED;

        int playerX = Mathf.RoundToInt(Player.position.x / CellSize);
        int playerZ = Mathf.RoundToInt(Player.position.z / CellSize);

        PositionText.text = "Position: (" + playerX + ", " + playerZ + ")";
        SizeText.text = "Size: " + WIDTH + " x " + HEIGHT;
    }

    void GenerateMinimap()
    {
        int playerX = Mathf.RoundToInt(Player.position.x / CellSize);
        int playerZ = Mathf.RoundToInt(Player.position.z / CellSize);

        for (int x = 0; x < WIDTH; x++)
        {
            for (int z = 0; z < HEIGHT; z++)
            {
                if (x == playerX && z == playerZ)
                {
                    minimapTexture.SetPixel(x, z, PlayerColor);
                }
                else if (x == goalCell.x && z == goalCell.y)
                {
                    minimapTexture.SetPixel(x, z, GoalColor);
                }
                else if (maze[x, z])
                {
                    minimapTexture.SetPixel(x, z, WallColor);
                }
                else
                {
                    minimapTexture.SetPixel(x, z, PathColor);
                }
            }
        }

        minimapTexture.Apply();
    }

    void CheckWin()
    {
        if (hasWon) return;

        int playerX = Mathf.RoundToInt(Player.position.x / CellSize);
        int playerZ = Mathf.RoundToInt(Player.position.z / CellSize);

        if (playerX == goalCell.x && playerZ == goalCell.y)
        {
            Debug.Log("YOU WIN!");

            Time.timeScale = 0f;

            EscapeUI.SetActive(true);
            hasWon = true;
        }
    }

    void SpawnGoal()
    {
        goalCell = FindFurthestCell();

        Vector3 pos = new Vector3(
            goalCell.x * CellSize,
            2f,
            goalCell.y * CellSize
        );

        goalObject = Instantiate(GoalPrefab, pos, Quaternion.identity);
    }

    Vector2Int FindFurthestCell()
    {
        Vector2Int start = new Vector2Int(1, 1);

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int>();

        queue.Enqueue(start);
        dist[start] = 0;

        Vector2Int furthest = start;

        Vector2Int[] dirs =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            furthest = current;

            foreach (var d in dirs)
            {
                Vector2Int next = current + d;

                if (next.x >= 0 && next.x < WIDTH &&
                    next.y >= 0 && next.y < HEIGHT &&
                    !maze[next.x, next.y] &&
                    !dist.ContainsKey(next))
                {
                    dist[next] = dist[current] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return furthest;
    }

    void GenerateMaze()
    {
        maze = new bool[WIDTH, HEIGHT];

        for (int x = 0; x < WIDTH; x++)
            for (int z = 0; z < HEIGHT; z++)
                maze[x, z] = true;

        visited.Clear();
        visited.Add(new Vector2Int(1, 1));

        Visit(1, 1);
    }

    void Visit(int x, int z)
    {
        maze[x, z] = false;

        while (true)
        {
            List<Vector2Int> options = new List<Vector2Int>();

            if (z > 1 && !visited.Contains(new Vector2Int(x, z - 2)))
                options.Add(NORTH);

            if (z < HEIGHT - 2 && !visited.Contains(new Vector2Int(x, z + 2)))
                options.Add(SOUTH);

            if (x > 1 && !visited.Contains(new Vector2Int(x - 2, z)))
                options.Add(WEST);

            if (x < WIDTH - 2 && !visited.Contains(new Vector2Int(x + 2, z)))
                options.Add(EAST);

            if (options.Count == 0)
                return;

            Vector2Int dir = options[Random.Range(0, options.Count)];

            int nx = x + dir.x;
            int nz = z + dir.y;

            maze[x + dir.x / 2, z + dir.y / 2] = false;

            visited.Add(new Vector2Int(nx, nz));

            Visit(nx, nz);
        }
    }

    void BuildMaze()
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int z = 0; z < HEIGHT; z++)
            {
                Vector3 pos = new Vector3(x * CellSize, 0, z * CellSize);

                if (maze[x, z])
                    Instantiate(WallPrefab, pos, Quaternion.identity, transform);
                else if (FloorPrefab != null)
                    Instantiate(FloorPrefab, pos, Quaternion.identity, transform);
            }
        }
    }
}