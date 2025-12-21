using System.Collections.Generic;
using NoFences.Model;

namespace NoFences.Services
{
    public interface IHistoryService
    {
        void RecordAction(FenceAction action);
        bool CanUndo { get; }
        bool CanRedo { get; }
        void Undo();
        void Redo();
        void Clear();
        List<FenceAction> GetHistory();
    }
}
