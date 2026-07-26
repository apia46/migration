[GlobalClass]
public partial class CreaturesManager : Node
{
    #nullable disable
    public static World World;
    #nullable enable

    public override void _PhysicsProcess(double delta)
    {
        FloodFillAawagas();
    }

    public static void SpawnCreature<T>(Vector2 position) where T : Node2D, ICreature<T>
	{
		T creature = T.Scene.Instantiate<T>();
		creature.World = World;
        T.Creatures[T.IdIterator] = creature;
        creature.Id = T.IdIterator++;
		World.AddChild(creature);
		creature.Position = position;
	}

	public static void RemoveCreature<T>(T creature) where T : Node2D, ICreature<T>
	{
		creature.QueueFree();
        T.Creatures.Remove(creature.Id);
	}

    static void FloodFillAawagas()
	{
		foreach (Aawaga creature in Aawaga.Creatures.Values) {
            creature.FloodFilled = false;
            creature.ConnectedToSurface = false;
        }
        
        static bool TouchingSurface(Aawaga creature) {
            foreach (Node2D body in creature.GetCollidingBodies())
                if (body is TileMapLayer) return true;
            return false;
        }

        static void FloodFill(Aawaga creature)
        {
            if(creature.FloodFilled) return;
            creature.FloodFilled = true;
            creature.ConnectedToSurface = true;
            foreach (Node2D body in creature.GetCollidingBodies())
                if (body is Aawaga next) FloodFill(next);
        }
        
        foreach (Aawaga creature in Aawaga.Creatures.Values)
            if (!creature.FloodFilled && TouchingSurface(creature)) FloodFill(creature);
	}
}

public interface ICreature<T> where T:ICreature<T>
{
    public static abstract PackedScene Scene {get; set;}
    public static abstract Dictionary<int, T> Creatures {get; set;}
    public static abstract int IdIterator {get; set;}
    public int Id {get; set;}
    public World World {get; set;}
}
