using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT;

public class STTProviderFactory
{
    private readonly Dictionary<STTProviderType, Func<ISTTProvider>> _creators = new();

    public void Register(STTProviderType type, Func<ISTTProvider> creator)
    {
        _creators[type] = creator;
    }

    public ISTTProvider Create(STTProviderConfig config)
    {
        if (!_creators.TryGetValue(config.ProviderType, out var creator))
            throw new NotSupportedException(
                $"STT provider '{config.ProviderType}' is not registered. " +
                $"Available: {string.Join(", ", _creators.Keys)}");

        return creator();
    }

    public bool IsRegistered(STTProviderType type) => _creators.ContainsKey(type);

    public IReadOnlyCollection<STTProviderType> RegisteredProviders => _creators.Keys;
}
