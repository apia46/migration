[GlobalClass]
public partial class Settings : Control
{
    public static bool FancyVisuals = true;

    public override void _Ready()
    {
        GetNode<CheckBox>("%FancyVisuals").Toggled += (on) => FancyVisuals = on;
    }
}
