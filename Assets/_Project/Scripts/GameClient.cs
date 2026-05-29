using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;

public class GameClient : MonoBehaviour
{
    [Header("Connection")]
    public string serverUrl = "http://localhost:3000";
    public string playerName = "UnityPlayer";
    public string roomId = "arena-1";

    [Header("World")]
    public GameObject playerPrefab;
    public GameObject spawnPointPrefab;
    public float positionScale = 1f;
    public float smoothingSpeed = 12f;

    [Header("Camera")]
    public TopDownCameraFollow cameraFollow;

    [Header("Mobile Input")]
    public Joystick movementJoystick;
    public float joystickDeadZone = 0.35f;
    public float moveRepeatInterval = 0.05f;
    public bool createRuntimeJoystick = true;

    private SocketIOUnity socket;

    private readonly Dictionary<string, PlayerView> players = new Dictionary<string, PlayerView>();
    private readonly Dictionary<string, GameObject> spawnPointObjects = new Dictionary<string, GameObject>();

    private WorldState latestState;
    private readonly object stateLock = new object();
    private float nextMoveInputTime;

    void Start()
    {
        if (movementJoystick == null)
        {
            movementJoystick = FindObjectOfType<Joystick>();
        }

        if (movementJoystick == null && createRuntimeJoystick)
        {
            movementJoystick = CreateRuntimeJoystick();
        }

        if (cameraFollow == null)
        {
            cameraFollow = Camera.main?.GetComponent<TopDownCameraFollow>();
        }

        var uri = new Uri(serverUrl + "?name=" + playerName);

        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        socket.JsonSerializer = new NewtonsoftJsonSerializer();

        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("Connected to game server: " + socket.Id);

            socket.Emit("joinRoom", new JoinRoomInput
            {
                roomId = roomId,
                name = playerName
            });
        };

        socket.On("roomJoined", response =>
        {
            var data = response.GetValue<RoomJoinedResponse>();
            Debug.Log("Joined room: " + data.roomId);
        });

        socket.On("worldUpdate", response =>
        {
            var state = response.GetValue<WorldState>();

            lock (stateLock)
            {
                latestState = state;
            }
        });

        socket.On("pong", response =>
        {
            Debug.Log("Pong from server: " + response);
        });

        socket.OnDisconnected += (sender, reason) =>
        {
            Debug.Log("Disconnected from server: " + reason);
        };

        socket.Connect();
    }

    void Update()
    {
        if (socket != null && socket.Connected)
        {
            HandleInput();
        }

        WorldState stateToRender = null;

        lock (stateLock)
        {
            stateToRender = latestState;
            latestState = null;
        }

        if (stateToRender != null)
        {
            RenderWorldState(stateToRender);
        }

        SmoothPlayers();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            socket.Emit("ping", new { message = "hello from Unity" });
        }

        string direction = GetMoveDirection();

        if (direction == null)
        {
            return;
        }

        if (Time.time < nextMoveInputTime)
        {
            return;
        }

        socket.Emit("move", new MoveInput { direction = direction });
        nextMoveInputTime = Time.time + moveRepeatInterval;
    }

    private string GetMoveDirection()
    {
        if (movementJoystick != null)
        {
            Vector2 joystickDirection = movementJoystick.Direction;

            if (joystickDirection.magnitude >= joystickDeadZone)
            {
                return GetCardinalDirection(joystickDirection);
            }
        }

        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
        {
            return "up";
        }

        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            return "down";
        }

        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
        {
            return "left";
        }

        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
        {
            return "right";
        }

        return null;
    }

    private string GetCardinalDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x > 0 ? "right" : "left";
        }

        return direction.y > 0 ? "up" : "down";
    }

    private void RenderWorldState(WorldState state)
    {
        RenderSpawnPoints(state);
        RenderPlayers(state);
    }

    private void RenderSpawnPoints(WorldState state)
    {
        if (spawnPointPrefab == null || state.spawnPoints == null)
        {
            return;
        }

        foreach (var spawnPoint in state.spawnPoints)
        {
            Vector3 unityPosition = ToUnityPosition(spawnPoint.position);

            if (!spawnPointObjects.ContainsKey(spawnPoint.id))
            {
                var obj = Instantiate(spawnPointPrefab, unityPosition, Quaternion.identity);
                obj.name = "SpawnPoint_" + spawnPoint.id;
                spawnPointObjects.Add(spawnPoint.id, obj);
            }
            else
            {
                spawnPointObjects[spawnPoint.id].transform.position = unityPosition;
            }
        }
    }

    private void RenderPlayers(WorldState state)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player Prefab is not assigned in Inspector.");
            return;
        }

        if (state.players == null)
        {
            return;
        }

        foreach (var serverPlayer in state.players)
        {
            Vector3 unityPosition = ToUnityPosition(serverPlayer.position);

            if (!players.ContainsKey(serverPlayer.id))
            {
                Debug.Log("Spawning player: " + serverPlayer.name);

                var obj = Instantiate(playerPrefab, unityPosition, Quaternion.identity);
                obj.name = "Player_" + serverPlayer.name;

                players.Add(serverPlayer.id, new PlayerView
                {
                    obj = obj,
                    targetPosition = unityPosition
                });

                if (serverPlayer.id == socket.Id && cameraFollow != null)
                {
                    cameraFollow.SetTarget(obj.transform);
                }
            }
            else
            {
                players[serverPlayer.id].targetPosition = unityPosition;
            }
        }
    }

    private void SmoothPlayers()
    {
        foreach (var playerView in players.Values)
        {
            if (playerView.obj == null)
            {
                continue;
            }

            playerView.obj.transform.position = Vector3.Lerp(
                playerView.obj.transform.position,
                playerView.targetPosition,
                Time.deltaTime * smoothingSpeed
            );
        }
    }

    private Vector3 ToUnityPosition(PositionState position)
    {
        return new Vector3(
            position.x / positionScale,
            0,
            position.y / positionScale
        );
    }

    private Joystick CreateRuntimeJoystick()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "Mobile Controls Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        GameObject root = new GameObject(
            "Runtime Joystick",
            typeof(RectTransform),
            typeof(Image),
            typeof(FixedJoystick)
        );

        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(420f, 420f);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = Color.clear;
        rootImage.raycastTarget = true;

        RectTransform background = CreateJoystickCircle(
            "Background",
            rootRect,
            new Vector2(180f, 180f),
            280f,
            new Color(1f, 1f, 1f, 0.22f)
        );

        RectTransform handle = CreateJoystickCircle(
            "Handle",
            background,
            Vector2.zero,
            120f,
            new Color(1f, 1f, 1f, 0.65f)
        );

        Joystick joystick = root.GetComponent<Joystick>();
        joystick.Configure(background, handle);
        joystick.DeadZone = joystickDeadZone;
        return joystick;
    }

    private RectTransform CreateJoystickCircle(
        string name,
        RectTransform parent,
        Vector2 anchoredPosition,
        float size,
        Color color
    )
    {
        GameObject circle = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        circle.transform.SetParent(parent, false);

        RectTransform rect = circle.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(size, size);

        Image image = circle.GetComponent<Image>();
        image.sprite = CreateCircleSprite(128);
        image.color = color;
        image.raycastTarget = true;

        return rect;
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Joystick Circle";

        float radius = size * 0.5f;
        Vector2 center = new Vector2(radius, radius);
        Color transparent = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : transparent);
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }

    void OnDestroy()
    {
        socket?.Disconnect();
        socket?.Dispose();
    }
}

public class PlayerView
{
    public GameObject obj;
    public Vector3 targetPosition;
}

[Serializable]
public class MoveInput
{
    public string direction;
}

[Serializable]
public class JoinRoomInput
{
    public string roomId;
    public string name;
}

[Serializable]
public class WorldState
{
    public string roomId;
    public int tick;
    public PlayerState[] players;
    public SpawnPointState[] spawnPoints;
}

[Serializable]
public class PlayerState
{
    public string id;
    public string name;
    public PositionState position;
    public int hp;
    public string status;
}

[Serializable]
public class SpawnPointState
{
    public string id;
    public PositionState position;
}

[Serializable]
public class PositionState
{
    public float x;
    public float y;
}

[Serializable]
public class RoomJoinedResponse
{
    public string roomId;
    public PlayerState player;
}