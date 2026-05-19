using System;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;

public class GameClient : MonoBehaviour
{
    public Transform player;

    private SocketIOUnity socket;
    private Vector3 targetPosition;

    void Start()
    {
        var uri = new Uri("http://localhost:3000?name=UnityPlayer");

        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        socket.JsonSerializer = new NewtonsoftJsonSerializer();

        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("Connected to game server: " + socket.Id);
        };

        socket.On("pong", response =>
        {
            Debug.Log("Pong from server: " + response);
        });

        socket.On("worldUpdate", response =>
        {
            var state = response.GetValue<WorldState>();

            foreach (var serverPlayer in state.players)
            {
                if (serverPlayer.id == socket.Id)
                {
                    targetPosition = new Vector3(
                        serverPlayer.position.x / 50f,
                        0,
                        serverPlayer.position.y / 50f
                    );
                }
            }
        });

        socket.Connect();
    }

    void Update()
    {
        if (socket == null || !socket.Connected)
        {
            return;
        }

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

        if (player != null)
        {
            player.position = Vector3.Lerp(player.position, targetPosition, Time.deltaTime * 12f);
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
public class WorldState
{
    public int tick;
    public PlayerState[] players;
}

[Serializable]
public class PlayerState
{
    public string id;
    public string name;
    public PositionState position;
}

[Serializable]
public class PositionState
{
    public float x;
    public float y;
}