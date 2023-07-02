using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ChatCSS;

public class ChatRoom : IAsyncDisposable
{
	private static readonly ConcurrentDictionary<Guid, ChatRoom> rooms = new();

	public static IReadOnlyDictionary<Guid, ChatRoom> Rooms => rooms;

	public static async Task<ChatRoom> Get(Guid id)
	{
		var room = rooms.GetOrAdd(id, _ => new ChatRoom(id));
		await room.AddReference();
		return room;
	}

	public static async ValueTask<ChatRoom?> BindAsync(HttpContext context, ParameterInfo parameterInfo)
	{
		var name = parameterInfo.Name;
		if (parameterInfo.Name is null)
		{
			name = parameterInfo.GetCustomAttribute<FromRouteAttribute>()?.Name;
		}
		if (name is null)
		{
			return null;
		}
		name += "Id";

		if (context.GetRouteValue(name) is not string idString)
		{
			return null;
		}

		if (!Guid.TryParse(idString, out var id))
		{
			return null;
		}

		return await Get(id);
	}

	public Guid Id { get; }

	private event Action<Action>? OnAction;

	private ChatRoom(Guid id)
	{
		Id = id;
	}

	public void Write(Action action)
	{
		OnAction?.Invoke(action);
	}

	public async Task<Action> ReadAsync(CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<Action>();
		OnAction += Callback;
		cancellationToken.Register(Cancel);
		return await tcs.Task;

		void Callback(Action t)
		{
			if (tcs.TrySetResult(t))
			{
				OnAction -= Callback;
			}
		}

		void Cancel()
		{
			if (tcs.TrySetCanceled(cancellationToken))
			{
				OnAction -= Callback;
			}
		}
	}

	private readonly SemaphoreSlim memberCountSemaphore = new(1, 1);
	public int MemberCount;

	private async Task AddReference()
	{
		await memberCountSemaphore.WaitAsync();
		MemberCount++;
		memberCountSemaphore.Release();
	}

	public async ValueTask DisposeAsync()
	{
		await memberCountSemaphore.WaitAsync();
		MemberCount--;
		if (MemberCount == 0)
		{
			rooms.TryRemove(Id, out _);
		}
		memberCountSemaphore.Release();
	}
}
