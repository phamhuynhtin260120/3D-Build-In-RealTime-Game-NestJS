using System;
using System.Collections.Generic;
using UnityEngine;
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
    public float positionScale = 50f;

    private SocketIOUnity socket;
    private readonly Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

    private WorldState latestState;
    private readonly object stateLock = new object();

    void Start()
    {
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

            Debug.Log("Room: " + state.roomId + " | Players: " + state.players.Length);

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
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            socket.Emit("move", new MoveInput { direction = "up" });
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            socket.Emit("move", new MoveInput { direction = "down" });
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            socket.Emit("move", new MoveInput { direction = "left" });
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            socket.Emit("move", new MoveInput { direction = "right" });
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            socket.Emit("ping", new { message = "hello from Unity" });
        }
    }

    private void RenderWorldState(WorldState state)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player Prefab is not assigned in Inspector.");
            return;
        }

        foreach (var serverPlayer in state.players)
        {
            if (!players.ContainsKey(serverPlayer.id))
            {
                Debug.Log("Spawning player: " + serverPlayer.name);

                var obj = Instantiate(playerPrefab);
                obj.name = "Player_" + serverPlayer.name;
                players.Add(serverPlayer.id, obj);
            }

            var unityPosition = new Vector3(
                serverPlayer.position.x / positionScale,
                0,
                serverPlayer.position.y / positionScale
            );

            players[serverPlayer.id].transform.position = unityPosition;
        }
    }

    void OnDestroy()
    {
        socket?.Disconnect();
        socket?.Dispose();
    }
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