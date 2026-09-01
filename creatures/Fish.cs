// inspiration from https://github.com/jackaperkins/boids
[GlobalClass]
public partial class Fish : CharacterBody2D, IGrabbable, ICreature<Fish>
{
	public int Id { get; set; }

    #nullable disable
    Sprite2D Sprite;
    PointLight2D Light;
    #nullable restore

    const float AVOID_PLAYER_DISTANCE_SQUARED = 1e5f;
    const float FRIEND_RADIUS_SQUARED = 100000;
    const float MAX_SPEED = 200;

    public bool Grabbed = false;
    public static PackedScene Scene { get; set; } = GD.Load<PackedScene>("res://creatures/fish.tscn");
    public static Dictionary<int, Fish> Creatures { get; set; } = [];
    public static int IdIterator { get; set; }
	public float CollisionRadius { get; set; }

    double ThinkTimer = 0;

    public override void _Ready()
    {
        Color color = Color.FromHsv(Game.RNG.Range(0.5f,0.8f), 0.25f, 1);
        Sprite = GetNode<Sprite2D>("%Sprite");
        Light = GetNode<PointLight2D>("%Light");
        Sprite.Modulate = color;
        Light.Color = color;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Grabbed) return;
        ThinkTimer += delta;
        if (ThinkTimer > 0.05) {
            float effectiveDelta = (float)ThinkTimer;
            ThinkTimer = 0;
            Vector2 newVelocity = Velocity;
            Vector2 friendForce = Vector2.Zero;
            Vector2 avoidForce = Vector2.Zero;
            Vector2 cohesionForce = Vector2.Zero;
            int friends = 0;
            foreach (Fish fish in Creatures.Values) {
                if (fish == this) continue;
                float distanceSquared = Mathf.Max(1f,fish.Position.DistanceSquaredTo(Position));
                if (distanceSquared > FRIEND_RADIUS_SQUARED) continue;
                friendForce += fish.Velocity / distanceSquared;
                avoidForce += (Position-fish.Position).Normalized() / distanceSquared;
                cohesionForce += fish.Position - Position;
                friends++;
            }
            if (friends > 0) {
                newVelocity += friendForce / friends * World.CreaturesManager.FISH_FRIEND_FORCE * effectiveDelta;
                newVelocity += avoidForce / friends * World.CreaturesManager.FISH_AVOID_EACHOTHER_FORCE * effectiveDelta;
                newVelocity += cohesionForce / friends * World.CreaturesManager.FISH_COHESION_FORCE * effectiveDelta;
            }
            float distanceSquaredToPlayer = Mathf.Max(1f,Position.DistanceSquaredTo(World.Player.Position));
            Light.Enabled = distanceSquaredToPlayer < Player.OFFSCREEN_RANGE_SQUARED;
            if (distanceSquaredToPlayer < AVOID_PLAYER_DISTANCE_SQUARED) {
                newVelocity += (Position-World.Player.Position).Normalized() / distanceSquaredToPlayer * World.CreaturesManager.FISH_AVOID_PLAYER_FORCE * effectiveDelta;
            }
            newVelocity += Game.RNG.Offset(World.CreaturesManager.FISH_RANDOM_FORCE*effectiveDelta);
            Velocity = newVelocity.LimitLength(MAX_SPEED);
            Rotation = Velocity.Angle();
        }
        MoveAndSlide();
        CreaturesManager.CreatureMoved(this);
    }

    public bool Grabbable() => true;

    public void Grab() {
        Light.ShadowEnabled = false;
        Grabbed = true;
    }

    public void Ungrab() {
        Light.ShadowEnabled = true;
        Grabbed = false;
        Velocity = Vector2.Zero;
    }

    public void Throw(Vector2 force)
    {
        Ungrab();
        Velocity = force;
    }
}