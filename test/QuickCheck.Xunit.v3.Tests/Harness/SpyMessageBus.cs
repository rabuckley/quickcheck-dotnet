using Xunit.Sdk;
using Xunit.v3;

namespace QuickCheck.Xunit.Tests.Harness;

internal sealed class SpyMessageBus : IMessageBus
{
    private readonly List<IMessageSinkMessage> _messages = [];

    public IReadOnlyList<IMessageSinkMessage> Messages => _messages;

    public bool QueueMessage(IMessageSinkMessage message)
    {
        lock (_messages)
        {
            _messages.Add(message);
        }

        return true;
    }

    public void Dispose()
    {
    }
}
