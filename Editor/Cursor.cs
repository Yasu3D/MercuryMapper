namespace MercuryMapper.Editor;

public class Cursor
{
    private enum RolloverState
    {
        None,
        Counterclockwise,
        Clockwise,
    }
    
    public int Position { get; set; } = 0;
    public int Size { get; set; } = 15;

    public int MinSize { get; set; } = 4;
    public int MaxSize { get; set; } = 60;

    public int DragPosition { get; private set; }
    public bool WasDragged { get; private set; }

    private int previousDragPosition = 0;
    private int relativeDragPosition = 0;
    private int initialDragPosition = 0;
    private int dragCounter = 0;
    private RolloverState dragRolloverState = RolloverState.None;

    private const int DragDetectionThreshold = 5;

    /// <summary>
    /// Moves the cursor's position around the ring.
    /// [Code taken 1:1 from BAKKA Avalonia]
    /// </summary>
    /// <param name="position">Position around the circle where 0 is the start. Values increase counterclockwise.</param>
    /// <returns>Updated cursor position</returns>
    public int Move(int position)
    {
        Position = position;

        // If the cursor is moving, it is not being dragged. Sync it with the current position
        // to start at the correct position when a drag starts.
        DragPosition = position;
        previousDragPosition = position;
        relativeDragPosition = 0;
        initialDragPosition = position;
        dragRolloverState = RolloverState.None;
        dragCounter = 0;
        WasDragged = false;

        return Position;
    }
    
    /// <summary>
    /// Drags the cursor which changes its size and position.
    /// [Code taken 1:1 from BAKKA Avalonia]
    /// </summary>
    /// <param name="position">Position of the cursor during the drag</param>
    public void Drag(int position)
    {
        if (dragCounter < DragDetectionThreshold)
        {
            dragCounter++;
        }

        if (dragCounter >= DragDetectionThreshold)
        {
            WasDragged = true;
        }

        if (position == DragPosition)
        {
            // Only update if the position changed. This ensures that the current and
            // previous drag positions are different to properly tell which direction
            // the cursor moved.
            return;
        }

        previousDragPosition = DragPosition;
        DragPosition = position;
        WasDragged = true;

        // Rollover calculation is tricky. You could move the mouse through the center of the circle
        // which technically isn't moving clockwise or counterclockwise. Assume that the shorter of
        // clockwise vs counterclockwise deltas is the direction we moved in. If we moved perfectly
        // through the center of the circle such that the deltas are equal, choose counterclockwise.
        int deltaClockwise = (previousDragPosition + 60 - DragPosition) % 60;
        int deltaCounterclockwise = (DragPosition + 60 - previousDragPosition) % 60;
        bool movedClockwise = deltaClockwise < deltaCounterclockwise;

        switch (dragRolloverState)
        {
            case RolloverState.Counterclockwise:
            {
                // If rolled over counterclockwise, the mouse moved clockwise, and mouse down position
                // is between the delta, we are no longer rolled over
                int delta = (initialDragPosition + 60 - DragPosition) % 60;
                if (movedClockwise && delta <= deltaClockwise)
                {
                    dragRolloverState = RolloverState.None;
                    relativeDragPosition -= delta;
                }
            }
                break;

            case RolloverState.Clockwise:
            {
                // If rolled over clockwise, the mouse moved counterclockwise, and mouse down position
                // is between the delta, we are no longer rolled over
                int delta = (DragPosition + 60 - (initialDragPosition) - 1) % 60;
                if (!movedClockwise && delta <= deltaCounterclockwise)
                {
                    dragRolloverState = RolloverState.None;
                    relativeDragPosition += delta + 1;
                }
            }
                break;

            default:
            {
                if (movedClockwise)
                {
                    relativeDragPosition = int.Max(relativeDragPosition - deltaClockwise, -60);

                    if (relativeDragPosition <= -60)
                    {
                        dragRolloverState = RolloverState.Clockwise;
                    }
                }
                else
                {
                    relativeDragPosition = int.Min(relativeDragPosition + deltaCounterclockwise, 60);

                    if (relativeDragPosition >= 60)
                    {
                        dragRolloverState = RolloverState.Counterclockwise;
                    }
                }
            }
                break;
        }

        // Calculate size and position based on mouse click position and relative drag position.
        // First, on a drag, the initial size is reset to the minimum size. If moving
        // counterclockwise, the size grows in that direction. Otherwise, if moving clockwise,
        // the position shifts clockwise by the minimum size before growing in that direction.
        // Essentially, we draw the cursor across the dragged positions which the initial shifting
        // accomplishes when moving clockwise.
        if (relativeDragPosition >= 0)
        {
            // Counterclockwise of mouse down position
            Position = initialDragPosition;
            Size = int.Clamp(relativeDragPosition + 1, MinSize, MaxSize);
        }
        else
        {
            // Clockwise of mouse down position
            if (relativeDragPosition >= -(MinSize - 1))
            {
                // Shift
                Position = (initialDragPosition + 60 + relativeDragPosition) % 60;
                Size = MinSize;
            }
            else
            {
                // Grow
                Position = (initialDragPosition + 60 + relativeDragPosition) % 60;
                Size = int.Min(MinSize - (relativeDragPosition + (MinSize - 1)), MaxSize);
            }
        }
    }

    public void IncrementSize(int delta)
    {
        Size = int.Clamp(Size + delta, MinSize, MaxSize);
    }
}