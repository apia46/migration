[GlobalClass]
public partial class CreaturesManager : Node
{
    #nullable disable
    public World World;
    #nullable enable

    readonly PackedScene AawagaScene = GD.Load<PackedScene>("creatures/aawaga.tscn");

    public Dictionary<int, Aawaga> Aawagas = [];
    public int AawagaIdIterator = 0;

    public override void _PhysicsProcess(double delta)
    {
        FloodFillAawagas();
    }

    public void SpawnCreature(Vector2 position)
	{
		Aawaga aawaga = AawagaScene.Instantiate<Aawaga>();
		aawaga.World = World;
        Aawagas[AawagaIdIterator] = aawaga;
        aawaga.Id = AawagaIdIterator++;
		AddChild(aawaga);
		aawaga.Position = position;
	}

	public void RemoveCreature(Aawaga creature)
	{
		creature.QueueFree();
        Aawagas.Remove(creature.Id);
	}

    void FloodFillAawagas()
	{
		foreach (Aawaga creature in Aawagas.Values) {
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
        
        foreach (Aawaga creature in Aawagas.Values)
            if (!creature.FloodFilled && TouchingSurface(creature)) FloodFill(creature);
        

	}
}