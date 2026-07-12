using Godot;
using SpaceFactory.Presentation.Settings;

namespace SpaceFactory.Presentation.World;

public partial class MiningParticleBurst : Node2D
{
    private readonly List<Particle> _particles = [];
    private readonly Color _color;
    private float _elapsed;

    public MiningParticleBurst(Color color, ulong seed)
    {
        _color = color;
        var random = new RandomNumberGenerator { Seed = seed };
        for (var index = 0; index < GraphicsQualityRuntime.MiningParticleCount; index++)
        {
            _particles.Add(new Particle(
                Vector2.Zero,
                Vector2.FromAngle(random.RandfRange(0, Mathf.Tau)) * random.RandfRange(35, 105),
                random.RandfRange(1.5f, 3.8f)));
        }
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        for (var index = 0; index < _particles.Count; index++)
        {
            var particle = _particles[index];
            particle = particle with
            {
                Position = particle.Position + (particle.Velocity * (float)delta),
                Velocity = particle.Velocity.MoveToward(Vector2.Zero, 90 * (float)delta),
            };
            _particles[index] = particle;
        }

        QueueRedraw();
        if (_elapsed >= 0.65f)
        {
            QueueFree();
        }
    }

    public override void _Draw()
    {
        var alpha = Mathf.Clamp(1 - (_elapsed / 0.65f), 0, 1);
        foreach (var particle in _particles)
        {
            DrawCircle(particle.Position, particle.Radius, new Color(_color, alpha));
        }
    }

    private readonly record struct Particle(Vector2 Position, Vector2 Velocity, float Radius);
}
