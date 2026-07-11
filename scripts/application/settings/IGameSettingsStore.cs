using SpaceFactory.Core.Settings;

namespace SpaceFactory.Application.Settings;

public interface IGameSettingsStore
{
    GameSettings Load();

    void Save(GameSettings settings);
}
