using System;
using ChatCSS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Redirect($"/{Guid.NewGuid()}"));
app.MapGet("/{roomId}", async (ChatRoom room, HttpResponse res) =>
{
	await using var client = new ChatClient(room, res);
	await client.WriteResponse();
});

app.MapGet("/key-down/{roomId}/{senderId}/{key}/{nonce}", async (ChatRoom room, Guid senderId, string key) =>
{
	var message = new KeyDownAction(senderId, key);
	room.Write(message);
    await room.DisposeAsync();
	return Results.NoContent();
});
app.MapGet("/key-up/{roomId}/{senderId}/{key}/{nonce}", async (ChatRoom room, Guid senderId, string key) =>
{
    var message = new KeyUpAction(senderId, key);
    room.Write(message);
    await room.DisposeAsync();
    return Results.NoContent();
});
app.MapGet("/send-down/{roomId}/{senderId}/{nonce}", async (ChatRoom room, Guid senderId) =>
{
    var message = new SendDownAction(senderId);
    room.Write(message);
    await room.DisposeAsync();
    return Results.NoContent();
});
app.MapGet("/send-up/{roomId}/{senderId}/{nonce}", async (ChatRoom room, Guid senderId) =>
{
    var message = new SendUpAction(senderId);
    room.Write(message);
    await room.DisposeAsync();
    return Results.NoContent();
});

app.MapGet("/health", async (HttpResponse res) =>
{
    await res.WriteAsync($"Rooms: ({ChatRoom.Rooms.Count})\n");
    foreach (var (id, room) in ChatRoom.Rooms)
    {
        await res.WriteAsync($"\t{id}: {room.MemberCount}\n");
    }
});

app.Run();
