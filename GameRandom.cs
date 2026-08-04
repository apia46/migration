public class GameRandom : Random
{
    public bool FlipCoin(float bias = 0.5f) => NextSingle() < bias;
    public int Range(int low, int high) => (int)NextInt64(low, high);
    public float Range(float low, float high) => NextSingle() * (high-low) + low;
    public double Range(double low, double high) => NextDouble() * (high-low) + low;
}
