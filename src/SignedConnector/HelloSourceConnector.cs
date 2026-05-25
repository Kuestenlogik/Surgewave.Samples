using System.Runtime.CompilerServices;

namespace Kuestenlogik.Surgewave.Samples.SignedConnector;

/// <summary>
/// Trivial source connector — emits a single message and finishes. The point of this sample
/// is the build/sign/publish workflow around it, not what the connector does at runtime; a
/// real connector would inherit from <c>ISourceConnector</c> and pump messages into Surgewave.
/// </summary>
public sealed class HelloSourceConnector
{
    public string Id { get; }

    public HelloSourceConnector(string id = "hello")
    {
        Id = id;
    }

    public async IAsyncEnumerable<string> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return $"hello from {Id}";
    }
}
