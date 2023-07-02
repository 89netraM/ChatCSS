using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ChatCSS;

public class ChatClient : IAsyncDisposable
{
	private readonly ChatRoom room;
	private readonly HttpResponse response;
	private readonly CancellationToken cancellationToken;

	public Guid RoomId => room.Id;
    public Guid Id;

	private StringBuilder message = new();

	public ChatClient(ChatRoom room, HttpResponse response)
	{
		this.room = room;
		this.response = response;
		cancellationToken = response.HttpContext.RequestAborted;
		Id = Guid.NewGuid();
	}

	public async Task WriteResponse()
	{
		await WriteInitialResponse();

		while (!cancellationToken.IsCancellationRequested)
		{
			var action = await room.ReadAsync(cancellationToken);
            await HandleAction(action);
        }
	}

    private Task HandleAction(Action action) =>
        action switch
        {
            KeyDownAction kda when kda.SenderId == Id => OnKeyDownAction(kda),
            KeyUpAction kua when kua.SenderId == Id => OnKeyUpAction(kua),
            SendDownAction sda when sda.SenderId == Id => OnSendDownAction(sda),
            SendUpAction sua when sua.SenderId == Id => OnSendUpAction(sua),
            MessageAction ma => OnMessageAction(ma),
            _ => Task.CompletedTask,
        };

    private Task OnKeyDownAction(KeyDownAction kda)
    {
        message.Append(kda.Key);
        return response.WriteAsync($$"""
			<style>
			    #{{kda.Key}}:not(:active) {
				    background: url("/key-up/{{RoomId}}/{{Id}}/{{kda.Key}}/{{Guid.NewGuid()}}");
				}
			</style>
			<p class="current"><strong>Message: </strong>{{message}}</p>
			""",
            cancellationToken);
    }

    private Task OnKeyUpAction(KeyUpAction kua) =>
        response.WriteAsync($$"""
			<style>
			    #{{kua.Key}}:active {
				    background: url("/key-down/{{RoomId}}/{{Id}}/{{kua.Key}}/{{Guid.NewGuid()}}");
				}
			</style>
			""",
            cancellationToken);

    private async Task OnSendDownAction(SendDownAction sa)
    {
        if (message.Length == 0)
        {
            await response.WriteAsync($$"""
			<style>
			    #send:not(:active) {
				    background: url("/send-up/{{RoomId}}/{{Id}}/{{Guid.NewGuid()}}");
				}
			</style>
			""",
                cancellationToken);
            return;
        }

        var completedMessage = message.ToString();
        message.Clear();
        await response.WriteAsync($$"""
			<style>
			    #send:not(:active) {
				    background: url("/send-up/{{RoomId}}/{{Id}}/{{Guid.NewGuid()}}");
				}
			</style>
			<p><strong>{{Id}}: </strong>{{completedMessage}}</p>
			<p class="current"><strong>Message: </strong></p>
			""",
            cancellationToken);
        room.Write(new MessageAction(sa.SenderId, completedMessage));
    }

    private Task OnSendUpAction(SendUpAction sa) =>
        response.WriteAsync($$"""
			<style>
			    #send:active {
				    background: url("/send-down/{{RoomId}}/{{Id}}/{{Guid.NewGuid()}}");
				}
			</style>
			""",
            cancellationToken);

    private Task OnMessageAction(MessageAction ma) =>
        response.WriteAsync($$"""
			    <p><strong>{{ma.SenderId}}: </strong>{{ma.Message}}</p>
			    """,
        cancellationToken);

    private async Task WriteInitialResponse()
	{
		var initialHtml = $$"""
			<!DOCTYPE html>
			<html>
				<head>
					<title>ChatCSS</title>
					<style>
						#messages {
							margin-top: 1rem;
							display: flex;
							flex-direction: column-reverse;
						}
						#messages p {
							margin: 0;
						}
						.current {
						    margin-bottom: 1rem;
							order: 1;
						}
						.current:has(~ .current) {
						    display: none;
						}
						#cowboy:active {
							background: url("./key-down/{{RoomId}}/{{Id}}/cowboy/{{Guid.NewGuid()}}");
						}
						#hacker:active {
							background: url("./key-down/{{RoomId}}/{{Id}}/hacker/{{Guid.NewGuid()}}");
						}
						#send:active {
							background: url("./send-down/{{RoomId}}/{{Id}}/{{Guid.NewGuid()}}");
						}
					</style>
				</head>
				<body>
					<h1>This is my CSS-only Chat!</h1>
					<p>Inspired by <a href="https://github.com/kkuchta/css-only-chat/">github.com/kkuchta/css-only-chat</a>.</p>
					<p>Please disable JavaScript, or not, you don't have to, but you could.</p>
					<p>Currently we only have two "letters".</p>

					<div id="keyboard">
						<button id="cowboy">🤠</button>
						<button id="hacker">👩‍💻</button>
						<button id="send">Send</button>
					</div>

					<div id="messages">
			            <p class="current"><strong>Message: </strong></p>
			""";
        response.ContentType = "text/html; charset=utf8";
		await response.WriteAsync(initialHtml, cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		await room.DisposeAsync();
	}
}
