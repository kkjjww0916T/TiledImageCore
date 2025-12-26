using System.ComponentModel;
using System.Runtime.CompilerServices;
using Geometry.Primitives;
using Geometry.Shapes.Types;

namespace Geometry.Shapes.Shapes;

/// <summary>
/// Base class for all shapes with INotifyPropertyChanged support.
/// </summary>
public abstract class ShapeBase : IShape, INotifyPropertyChanged
{
    public Guid Id { get; } = Guid.NewGuid();

    private double _centerX;
    public double CenterX
    {
        get => _centerX;
        set => SetProperty(ref _centerX, value);
    }

    private double _centerY;
    public double CenterY
    {
        get => _centerY;
        set => SetProperty(ref _centerY, value);
    }

    private double _rotation;
    public double Rotation
    {
        get => _rotation;
        set => SetProperty(ref _rotation, NormalizeAngle(value));
    }

    private ShapeStyle _style = ShapeStyle.Default;
    public ShapeStyle Style
    {
        get => _style;
        set => SetProperty(ref _style, value);
    }

    public Point2D Center => new(CenterX, CenterY);

    public abstract Rect2D GetLocalBounds();

    public virtual Rect2D GetWorldBounds()
    {
        var local = GetLocalBounds();
        if (Rotation == 0) return local;

        var center = new Point2D(CenterX, CenterY);
        var corners = new Point2D[]
        {
            local.TopLeft.RotateAroundDegrees(center, Rotation),
            local.TopRight.RotateAroundDegrees(center, Rotation),
            local.BottomRight.RotateAroundDegrees(center, Rotation),
            local.BottomLeft.RotateAroundDegrees(center, Rotation)
        };

        return Rect2D.FromPoints(corners);
    }

    public void Move(double deltaX, double deltaY)
    {
        CenterX += deltaX;
        CenterY += deltaY;
    }

    /// <summary>
    /// Gets the rotated corners of the local bounds.
    /// </summary>
    public Point2D[] GetWorldCorners()
    {
        var local = GetLocalBounds();
        var center = new Point2D(CenterX, CenterY);

        if (Rotation == 0)
        {
            return new[]
            {
                local.TopLeft,
                local.TopRight,
                local.BottomRight,
                local.BottomLeft
            };
        }

        return new Point2D[]
        {
            local.TopLeft.RotateAroundDegrees(center, Rotation),
            local.TopRight.RotateAroundDegrees(center, Rotation),
            local.BottomRight.RotateAroundDegrees(center, Rotation),
            local.BottomLeft.RotateAroundDegrees(center, Rotation)
        };
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0) angle += 360.0;
        return angle;
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
