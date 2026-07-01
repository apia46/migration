using Godot;

[Tool]
[GlobalClass]
public partial class DetailEditor : Node2D
{
    TileMapLayer? before;
    TileMapLayer? after;
    public override void _Ready()
	{
		before = GetNode<TileMapLayer>("%Before");
		after = GetNode<TileMapLayer>("%After");
    }
}