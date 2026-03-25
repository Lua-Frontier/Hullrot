using System.Linq;
using Content.Shared.GameTicking.Prototypes;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.LobbyScreen;

public sealed class LobbyBackgroundControl : TextureRect
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private List<LobbyBackgroundPrototype> _backgrounds = new();
    private int _currentIndex = 0;

    protected override void EnteredTree()
    {
        base.EnteredTree();
        IoCManager.InjectDependencies(this);
        _backgrounds = _prototypeManager.EnumeratePrototypes<LobbyBackgroundPrototype>().ToList();
        if (_backgrounds.Count > 0)
            LoadBackground(_currentIndex);
    }

    public void NextBackground()
    {
        if (_backgrounds.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _backgrounds.Count;
        LoadBackground(_currentIndex);
    }

    public void PreviousBackground()
    {
        if (_backgrounds.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _backgrounds.Count) % _backgrounds.Count;
        LoadBackground(_currentIndex);
    }

    public void SetBackground(int index)
    {
        if (_backgrounds.Count == 0) return;
        _currentIndex = Math.Clamp(index, 0, _backgrounds.Count - 1);
        LoadBackground(_currentIndex);
    }

    private void LoadBackground(int index)
    {
        try
        {
            Texture = _resourceCache.GetResource<TextureResource>(_backgrounds[index].Background).Texture;
        }
        catch
        {
        }
    }
}
