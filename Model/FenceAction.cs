using System;

namespace NoFences.Model
{
    /// <summary>
    /// Represents an action performed on a fence for undo/redo functionality
    /// </summary>
    public class FenceAction
    {
        public enum ActionType
        {
            FileAdded,
            FileRemoved,
            FileMoved,
            FenceCreated,
            FenceDeleted,
            FenceRenamed,
            FenceResized,
            FenceMoved,
            PropertyChanged
        }

        public string FenceId { get; set; }
        public ActionType Type { get; set; }
        public DateTime Timestamp { get; set; }
        
        // Serialized state information
        public string StateBeforeJson { get; set; }
        public string StateAfterJson { get; set; }
        
        // For file operations
        public string FilePath { get; set; }
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }

        public FenceAction()
        {
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Type} on Fence {FenceId}";
        }
    }
}
