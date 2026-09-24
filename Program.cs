using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

var rooms = new ConcurrentDictionary<string, Room>();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        success = true,
        app = "My Summer Car Online",
        status = "running"
    });
});

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        success = true,
        status = "ok",
        rooms = rooms.Count
    });
});

app.MapPost("/room/create", (CreateRoomRequest data) =>
{
    string code;

    do
    {
        code = Random.Shared.Next(100000, 1000000).ToString();
    }
    while (rooms.ContainsKey(code));

    var room = new Room
    {
        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };

    room.Players["1"] = new Player
    {
        Name = string.IsNullOrWhiteSpace(data.PlayerName)
            ? "Player1"
            : data.PlayerName,

        LastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };

    rooms[code] = room;

    return Results.Ok(new
    {
        success = true,
        code,
        player_id = "1"
    });
});

app.MapPost("/room/join", (JoinRoomRequest data) =>
{
    var code = data.Code?.Trim() ?? "";

    if (!rooms.TryGetValue(code, out var room))
    {
        return Results.NotFound(new
        {
            detail = "Room not found"
        });
    }

    lock (room.Lock)
    {
        if (room.Players.ContainsKey("2"))
        {
            return Results.Conflict(new
            {
                detail = "Room is full"
            });
        }

        room.Players["2"] = new Player
        {
            Name = string.IsNullOrWhiteSpace(data.PlayerName)
                ? "Player2"
                : data.PlayerName,

            LastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    return Results.Ok(new
    {
        success = true,
        code,
        player_id = "2"
    });
});

app.MapGet("/room/{code}", (string code) =>
{
    if (!rooms.TryGetValue(code, out var room))
    {
        return Results.NotFound(new
        {
            detail = "Room not found"
        });
    }

    return Results.Ok(new
    {
        success = true,
        code,
        players = room.Players
    });
});

app.MapPost("/room/{code}/player/{playerId}/state",
    (string code, string playerId, PlayerState state) =>
{
    if (!rooms.TryGetValue(code, out var room))
    {
        return Results.NotFound(new
        {
            detail = "Room not found"
        });
    }

    if (!room.Players.TryGetValue(playerId, out var player))
    {
        return Results.NotFound(new
        {
            detail = "Player not found"
        });
    }

    lock (room.Lock)
    {
        player.X = state.X;
        player.Y = state.Y;
        player.Z = state.Z;

        player.RotX = state.RotX;
        player.RotY = state.RotY;
        player.RotZ = state.RotZ;

        player.LastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    return Results.Ok(new
    {
        success = true
    });
});

app.MapDelete("/room/{code}/player/{playerId}",
    (string code, string playerId) =>
{
    if (!rooms.TryGetValue(code, out var room))
    {
        return Results.NotFound(new
        {
            detail = "Room not found"
        });
    }

    lock (room.Lock)
    {
        room.Players.TryRemove(playerId, out _);

        if (room.Players.IsEmpty)
        {
            rooms.TryRemove(code, out _);
        }
    }

    return Results.Ok(new
    {
        success = true
    });
});

app.Run();

public class Room
{
    public long Created { get; set; }

    public ConcurrentDictionary<string, Player> Players { get; } = new();

    public object Lock { get; } = new();
}

public class Player
{
    public string Name { get; set; } = "";

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public double RotX { get; set; }
    public double RotY { get; set; }
    public double RotZ { get; set; }

    public long LastSeen { get; set; }
}

public class CreateRoomRequest
{
    public string PlayerName { get; set; } = "Player1";
}

public class JoinRoomRequest
{
    public string PlayerName { get; set; } = "Player2";

    public string Code { get; set; } = "";
}

public class PlayerState
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public double RotX { get; set; }
    public double RotY { get; set; }
    public double RotZ { get; set; }
}