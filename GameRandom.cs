public class GameRandom : Random
{
    public bool FlipCoin() => NextSingle() > 0.5f;
    public float Range(float low, float high) => NextSingle() * (high-low) + low;
    public double Range(double low, double high) => NextDouble() * (high-low) + low;
}
