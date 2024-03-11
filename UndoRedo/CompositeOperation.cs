using System.Collections.Generic;
using System.Linq;

namespace MercuryMapper.UndoRedo;

public class CompositeOperation(string description, IEnumerable<IOperation> operations) : IOperation
{
    public IEnumerable<IOperation> Operations { get; } = operations;
    public string Description { get; } = description;

    public void Undo()
    {
        foreach (IOperation operation in Operations.Reverse()) operation.Undo();
    }

    public void Redo()
    {
        foreach (IOperation operation in Operations) operation.Redo();
    }
}