using System;

namespace ChatCSS;

public record Action(Guid SenderId);

public record KeyDownAction(Guid SenderId, string Key) : Action(SenderId);

public record KeyUpAction(Guid SenderId, string Key) : Action(SenderId);

public record SendDownAction(Guid SenderId) : Action(SenderId);
public record SendUpAction(Guid SenderId) : Action(SenderId);
public record MessageAction(Guid SenderId, string Message) : Action(SenderId);
