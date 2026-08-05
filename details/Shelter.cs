[GlobalClass]
public partial class Shelter : Area2D, IDetail<Shelter>
{
    #nullable disable
    Sprite2D EmptySprite;
    Sprite2D FullSprite;
    #nullable enable

    public static PackedScene Scene {get; set;} = GD.Load<PackedScene>("res://details/shelter.tscn");
    public static Dictionary<int, Shelter> Instances {get; set;} = [];
    public static int IdIterator {get; set;}
    public int Id {get; set;}

    public override void _Ready()
    {
        // this is stupid
        EmptySprite = GetNode<Sprite2D>("%EmptySprite");
        FullSprite = GetNode<Sprite2D>("%FullSprite");
    }

    public void Enter()
    {
        FullSprite.Visible = true;
        EmptySprite.Visible = false;
    }

    public void Exit()
    {
        FullSprite.Visible = false;
        EmptySprite.Visible = true;
    }
}
