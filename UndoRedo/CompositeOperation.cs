using System.Collections.Generic;
using System.Linq;

namespace MercuryMapper.UndoRedo;

public class CompositeOperation(IEnumerable<IOperation> operations) : IOperation
{
    public IEnumerable<IOperation> Operations { get; } = operations;

    public void Undo()
    {
        foreach (IOperation operation in Operations.Reverse()) operation.Undo();
    }

    public void Redo()
    {
        foreach (IOperation operation in Operations) operation.Redo();
    }
}