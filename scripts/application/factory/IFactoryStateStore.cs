namespace SpaceFactory.Application.Factory;

public interface IFactoryStateStore
{
    FactoryStateData Load();

    bool Save(FactoryStateData state);
}
