public class GameRandom : Random
{
    public bool FlipCoin() { return NextSingle() > 0.5f; }
    public float Range(float low, float high) { return NextSingle() * (high-low) + low; }
    public double Range(double low, double high) { return NextDouble() * (high-low) + low; }
}
