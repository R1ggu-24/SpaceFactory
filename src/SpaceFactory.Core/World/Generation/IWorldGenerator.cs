namespace SpaceFactory.Core.World.Generation;

public interface IWorldGenerator
{
    GeneratedSector Generate(SectorGenerationRequest request);
}
