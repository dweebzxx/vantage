using Godot;

namespace Vantage.Server;

public static class ArenaProbe
{
    public static bool Check(PhysicsDirectSpaceState2D space)
    {
        (string Face, Vector2 From, Vector2 To, bool X, float Expected)[] rays =
        [
            ("Outer top", new(0,0), new(0,-300), false, -127),
            ("Outer right", new(0,0), new(400,0), true, 258),
            ("Outer left", new(0,120), new(-400,120), true, -222),
            ("Outer bottom", new(0,0), new(0,400), false, 161),
            ("West right", new(0,0), new(-150,0), true, -62),
            ("West left", new(-150,0), new(0,0), true, -94),
            ("West top", new(-78,-90), new(-78,90), false, -31),
            ("West bottom", new(-78,90), new(-78,-90), false, 33),
            ("East left", new(100,-40), new(200,-40), true, 130),
            ("East right", new(210,-40), new(100,-40), true, 162),
            ("East top", new(155,-90), new(155,30), false, -63),
            ("East bottom", new(155,40), new(155,-90), false, -31),
            ("South left", new(20,85), new(170,85), true, 66),
            ("South right", new(180,85), new(20,85), true, 130),
            ("South top", new(90,20), new(90,140), false, 65),
            ("South bottom", new(90,140), new(90,20), false, 97),
        ];
        bool passed = true;
        foreach (var ray in rays)
        {
            using var query = PhysicsRayQueryParameters2D.Create(ray.From, ray.To, 1);
            query.CollideWithAreas = false;
            var hit = space.IntersectRay(query);
            if (!hit.TryGetValue("position", out Variant position))
            {
                GD.PrintErr($"[ArenaProbe] {ray.Face}; missing hit");
                passed = false;
                continue;
            }
            Vector2 point = position.AsVector2();
            float actual = ray.X ? point.X : point.Y;
            float error = Mathf.Abs(actual - ray.Expected);
            GD.Print($"[ArenaProbe] {ray.Face}; expected={ray.Expected}; actual={actual}; error={error}");
            passed &= error <= 0.01f;
        }
        return passed;
    }
}
