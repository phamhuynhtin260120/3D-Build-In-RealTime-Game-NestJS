using System;
using UnityEngine;

public class GameClient : MonoBehaviour
{
    public SocketIOUnity socket;

    void Start()
    {
        // 1. Cấu hình URL server (Nhớ dùng http thay vì ws, thư viện sẽ tự chuyển)
        var uri = new Uri("http://localhost:3000");
        socket = new SocketIOUnity(uri);

        // 2. Gửi dữ liệu lúc "bắt tay" (handshake) như chúng ta đã học
        socket.Options.Query = new System.Collections.Generic.Dictionary<string, string>
        {
            { "name", "UnityPlayer_Zen" }
        };

        // 3. Kết nối
        socket.Connect();

        // 4. Lắng nghe dữ liệu từ Server (worldUpdate)
        socket.OnUnityThread("worldUpdate", (data) => {
            // data ở đây chính là chuỗi JSON chứa tick và players
            Debug.Log("Nhận dữ liệu thế giới: " + data);
            // Tại đây bạn sẽ viết code để cập nhật vị trí nhân vật trên màn hình 3D
        });
    }

    void Update()
    {
        // Ví dụ: Bấm phím D để gửi lệnh sang phải
        if (Input.GetKeyDown(KeyCode.D))
        {
            // Lưu ý: Gửi đúng định dạng JSON mà Server của bạn đang chờ
            socket.Emit("move", "{\"direction\":\"right\"}");
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            socket.Emit("move", "{\"direction\":\"left\"}");
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            socket.Emit("move", "{\"direction\":\"up\"}");
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            socket.Emit("move", "{\"direction\":\"down\"}");
        }
    }
}