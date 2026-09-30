namespace GateRush.Core
{
    /// <summary>
    /// Which kind of spawner a block slot belongs to: a generator's queue (M6)
    /// or an elevator's wave (M9). See <see cref="LevelContext.TryGetSpawner"/>.
    /// </summary>
    public enum SpawnerKind
    {
        Generator,
        Elevator
    }
}
