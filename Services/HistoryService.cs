using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NoFences.Model;

namespace NoFences.Services
{
    public class HistoryService : IHistoryService
    {
        private const int MaxHistorySize = 50;
        private readonly Stack<FenceAction> undoStack = new Stack<FenceAction>();
        private readonly Stack<FenceAction> redoStack = new Stack<FenceAction>();
        private readonly ILoggingService _loggingService;
        private readonly IFenceService _fenceService;

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        public HistoryService(ILoggingService loggingService, IFenceService fenceService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _fenceService = fenceService ?? throw new ArgumentNullException(nameof(fenceService));
        }

        public void RecordAction(FenceAction action)
        {
            if (action == null) return;

            undoStack.Push(action);
            redoStack.Clear(); // Clear redo stack when new action is recorded

            // Limit stack size
            if (undoStack.Count > MaxHistorySize)
            {
                var items = undoStack.ToList();
                items.RemoveAt(items.Count - 1);
                undoStack.Clear();
                items.Reverse();
                foreach (var item in items)
                    undoStack.Push(item);
            }

            _loggingService.LogInfo($"History: Recorded action {action.Type}");
        }

        public void Undo()
        {
            if (!CanUndo)
            {
                _loggingService.LogWarning("History: Cannot undo, stack is empty");
                return;
            }

            var action = undoStack.Pop();
            redoStack.Push(action);

            try
            {
                ApplyReverseAction(action);
                _loggingService.LogInfo($"History: Undid action {action.Type}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"History: Failed to undo action {action.Type}", ex);
                // Re-add to undo stack if failed
                undoStack.Push(action);
                redoStack.Pop();
            }
        }

        public void Redo()
        {
            if (!CanRedo)
            {
                _loggingService.LogWarning("History: Cannot redo, stack is empty");
                return;
            }

            var action = redoStack.Pop();
            undoStack.Push(action);

            try
            {
                ApplyForwardAction(action);
                _loggingService.LogInfo($"History: Redid action {action.Type}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"History: Failed to redo action {action.Type}", ex);
                // Re-add to redo stack if failed
                redoStack.Push(action);
                undoStack.Pop();
            }
        }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
            _loggingService.LogInfo("History: Cleared all history");
        }

        public List<FenceAction> GetHistory()
        {
            return undoStack.ToList();
        }

        private void ApplyReverseAction(FenceAction action)
        {
            var fence = _fenceService.GetAllFences().FirstOrDefault(f => f.Id.ToString() == action.FenceId);
            if (fence == null) return;

            switch (action.Type)
            {
                case FenceAction.ActionType.FileAdded:
                    fence.Files.Remove(action.FilePath);
                    break;

                case FenceAction.ActionType.FileRemoved:
                    if (!fence.Files.Contains(action.FilePath))
                        fence.Files.Add(action.FilePath);
                    break;

                case FenceAction.ActionType.FenceRenamed:
                    fence.Name = action.OldValue?.ToString();
                    break;

                case FenceAction.ActionType.PropertyChanged:
                    // Restore old value (simplified)
                    var prop = typeof(FenceInfo).GetProperty(action.PropertyName);
                    prop?.SetValue(fence, action.OldValue);
                    break;
            }

            _fenceService.UpdateFence(fence);
        }

        private void ApplyForwardAction(FenceAction action)
        {
            var fence = _fenceService.GetAllFences().FirstOrDefault(f => f.Id.ToString() == action.FenceId);
            if (fence == null) return;

            switch (action.Type)
            {
                case FenceAction.ActionType.FileAdded:
                    if (!fence.Files.Contains(action.FilePath))
                        fence.Files.Add(action.FilePath);
                    break;

                case FenceAction.ActionType.FileRemoved:
                    fence.Files.Remove(action.FilePath);
                    break;

                case FenceAction.ActionType.FenceRenamed:
                    fence.Name = action.NewValue?.ToString();
                    break;

                case FenceAction.ActionType.PropertyChanged:
                    var prop = typeof(FenceInfo).GetProperty(action.PropertyName);
                    prop?.SetValue(fence, action.NewValue);
                    break;
            }

            _fenceService.UpdateFence(fence);
        }
    }
}
