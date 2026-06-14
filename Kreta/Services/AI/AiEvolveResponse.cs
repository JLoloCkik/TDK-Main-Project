namespace Kreta.Services.AI;

public record AiEvolveResponse(
    string Action,
    string Target,
    string Label,
    string HandlerName,
    string HandlerMethod,
    string RuntimeScript
);