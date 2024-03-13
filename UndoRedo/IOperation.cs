namespace MercuryMapper.UndoRedo;

public interface IOperation
{
    void Undo();
    void Redo();
}