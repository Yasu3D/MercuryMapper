using System.Collections.Generic;

namespace MercuryMapper.UndoRedo;

public class CompositeOperation(string description, IEnumerable<IOperation> operations) : IOperation
{
    public IEnumerable<IOperation> Operations { get; } = operations;
    public string Description { get; } = description;

    public void Undo()
    {
        foreach (IOperation operation in Operations) operation.Undo();
    }

    public void Redo()
    {
        foreach (IOperation operation in Operations) operation.Redo();
    }
}