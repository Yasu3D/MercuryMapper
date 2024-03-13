using System;
using System.Collections.Generic;

namespace MercuryMapper.UndoRedo;

public class UndoRedoManager
{
    private Stack<IOperation> UndoStack { get; } = new();
    private Stack<IOperation> RedoStack { get; } = new();
    
    public bool CanUndo => UndoStack.Count > 0;
    public bool CanRedo => RedoStack.Count > 0;

    public IOperation PeekUndo => UndoStack.Peek();
    public IOperation PeekRedo => RedoStack.Peek();

    public event EventHandler? OperationHistoryChanged;

    public void Push(IOperation operation)
    {
        UndoStack.Push(operation);
        RedoStack.Clear();
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void InvokeAndPush(IOperation operation)
    {
        operation.Redo();
        Push(operation);
    }

    public IOperation Undo()
    {
        IOperation operation = UndoStack.Pop();
        operation.Undo();
        RedoStack.Push(operation);
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
        return operation;
    }

    public IOperation Redo()
    {
        IOperation operation = RedoStack.Pop();
        operation.Redo();
        UndoStack.Push(operation);
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
        return operation;
    }

    public void Clear()
    {
        UndoStack.Clear();
        RedoStack.Clear();
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}