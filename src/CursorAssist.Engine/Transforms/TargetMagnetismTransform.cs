using CursorAssist.Engine.Core;

namespace CursorAssist.Engine.Transforms;

/// <summary>
/// Deterministic target magnetism assist transform.
/// Within activation radius: lerps cursor toward nearest target center.
/// Hysteresis prevents flicker at the engagement boundary.
///
/// Parameters sourced from AssistiveConfig:
///   - MagnetismRadiusVpx (activation distance)
///   - MagnetismStrength (pull factor [0,1])
///   - MagnetismHysteresisVpx (disengage margin beyond radius)
///   - SnapRadiusVpx (below this, snap to center)
/// </summary>
public sealed class TargetMagnetismTransform : IStatefulTransform
{
    public string TransformId => "assist.target-magnetism";

    private bool _engaged;
    private string? _lockedTargetId;

    public InputSample Apply(in InputSample input, TransformContext context)
    {
        var config = context.Config;
        if (config is null || config.MagnetismStrength <= 0f || config.MagnetismRadiusVpx <= 0f)
            return input;

        var targets = context.Targets;
        if (targets.Count == 0)
        {
            Disengage();
            return input;
        }

        // Find nearest target
        float nearestDist = float.MaxValue;
        TargetInfo? nearest = null;

        foreach (var target in targets)
        {
            float dist = target.DistanceToCenter(input.X, input.Y);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = target;
            }
        }

        if (nearest is null)
        {
            Disengage();
            return input;
        }

        var t = nearest.Value;
        float radius = config.MagnetismRadiusVpx;
        float hysteresis = config.MagnetismHysteresisVpx;
        float strength = config.MagnetismStrength;
        float snapRadius = config.SnapRadiusVpx;

        // Hysteresis: engage at radius, disengage at radius + hysteresis
        if (_engaged && _lockedTargetId == t.Id)
        {
            if (nearestDist > radius + hysteresis)
            {
                Disengage();
                return input;
            }
        }
        else
        {
            if (nearestDist > radius)
            {
                return input;
            }
            _engaged = true;
            _lockedTargetId = t.Id;
        }

        // Below snap radius: hard snap to center
        if (nearestDist <= snapRadius && snapRadius > 0f)
        {
            return input with { X = t.CenterX, Y = t.CenterY };
        }

        // Lerp toward center, scaled by strength and proximity
        // Closer = stronger pull (quadratic falloff from radius edge)
        float proximity = 1f - (nearestDist / radius);
        proximity = proximity * proximity; // quadratic
        float effectiveStrength = strength * proximity;

        float newX = input.X + (t.CenterX - input.X) * effectiveStrength;
        float newY = input.Y + (t.CenterY - input.Y) * effectiveStrength;

        return input with { X = newX, Y = newY };
    }

    public void Reset()
    {
        Disengage();
    }

    private void Disengage()
    {
        _engaged = false;
        _lockedTargetId = null;
    }
}
