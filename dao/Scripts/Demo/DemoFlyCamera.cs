using Godot;

namespace Dao.Demo;

[GlobalClass]
public partial class DemoFlyCamera : Camera3D
{
    [Export(PropertyHint.Range, "1,400,1")] public float MoveSpeed { get; set; } = 68.0f;
    [Export(PropertyHint.Range, "1,1000,1")] public float SprintSpeed { get; set; } = 210.0f;
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float MouseSensitivity { get; set; } = 0.12f;

    private float _yaw = -35.0f;
    private float _pitch = -24.0f;

    public override void _Ready()
    {
        Current = true;
        Far = 9000.0f;
        Near = 0.05f;
        Fov = 72.0f;
        Position = new Vector3(140.0f, 430.0f, 430.0f);
        ApplyRotation();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= mouseMotion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - mouseMotion.Relative.Y * MouseSensitivity, -86.0f, 86.0f);
            ApplyRotation();
        }

        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _Process(double delta)
    {
        Vector3 direction = Vector3.Zero;
        Basis basis = GlobalTransform.Basis;

        if (Input.IsKeyPressed(Key.W))
        {
            direction -= basis.Z;
        }

        if (Input.IsKeyPressed(Key.S))
        {
            direction += basis.Z;
        }

        if (Input.IsKeyPressed(Key.A))
        {
            direction -= basis.X;
        }

        if (Input.IsKeyPressed(Key.D))
        {
            direction += basis.X;
        }

        if (Input.IsKeyPressed(Key.Space))
        {
            direction += Vector3.Up;
        }

        if (Input.IsKeyPressed(Key.Shift))
        {
            direction -= Vector3.Up;
        }

        if (direction.LengthSquared() <= 0.0001f)
        {
            return;
        }

        float speed = Input.IsKeyPressed(Key.Ctrl) ? SprintSpeed : MoveSpeed;
        GlobalPosition += direction.Normalized() * speed * (float)delta;
    }

    private void ApplyRotation()
    {
        RotationDegrees = new Vector3(_pitch, _yaw, 0.0f);
    }
}
