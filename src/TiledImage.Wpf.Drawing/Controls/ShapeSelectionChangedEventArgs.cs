using System.Windows;
using Geometry.Shapes.Shapes;

namespace TiledImage.Wpf.Drawing;

/// <summary>
/// Event args for when shape selection changes.
/// </summary>
public class ShapeSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// The shapes that were added to the selection.
    /// </summary>
    public IReadOnlyList<IShape> AddedShapes { get; }

    /// <summary>
    /// The shapes that were removed from the selection.
    /// </summary>
    public IReadOnlyList<IShape> RemovedShapes { get; }

    /// <summary>
    /// The current selected shapes after the change.
    /// </summary>
    public IReadOnlyList<IShape> SelectedShapes { get; }

    public ShapeSelectionChangedEventArgs(
        RoutedEvent routedEvent,
        IEnumerable<IShape> addedShapes,
        IEnumerable<IShape> removedShapes,
        IEnumerable<IShape> selectedShapes)
        : base(routedEvent)
    {
        AddedShapes = addedShapes.ToList().AsReadOnly();
        RemovedShapes = removedShapes.ToList().AsReadOnly();
        SelectedShapes = selectedShapes.ToList().AsReadOnly();
    }
}
