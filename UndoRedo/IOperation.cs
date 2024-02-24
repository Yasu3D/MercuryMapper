namespace MercuryMapper.UndoRedo;

public interface IOperation
{
    string Description { get; }
    void Undo();
    void Redo();
}