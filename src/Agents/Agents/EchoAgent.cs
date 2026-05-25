using Kuestenlogik.Surgewave.AI.Agents;

namespace Kuestenlogik.Surgewave.Samples.AgentDemo.Agents;

/// <summary>
/// Simple echo agent that returns the input message.
/// Demonstrates the basic agent structure.
/// </summary>
public sealed class EchoAgent : SurgewaveAgentBase
{
    public override string AgentId => "echo-agent";

    public override string Name => "Echo Agent";

    public override string Description => "A simple agent that echoes back messages.";

    public override IReadOnlyList<AgentSkill> Skills =>
    [
        new AgentSkill
        {
            Id = "echo",
            Name = "Echo",
            Description = "Echoes the input message back to the user."
        }
    ];

    public override Task<AgentResponse> ProcessMessageAsync(
        AgentMessage message,
        SurgewaveAgentContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(TextResponse($"Echo: {message.Content}"));
    }
}
