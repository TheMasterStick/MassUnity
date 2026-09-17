using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;

namespace MassRPG.EditorCore.World
{
    public readonly struct WorldCellEdit
    {
        public WorldCellEdit(GridLocation location, AuthoredTileCell value)
        {
            Location = location;
            Value = value;
        }

        public GridLocation Location { get; }
        public AuthoredTileCell Value { get; }
    }

    /// <summary>
    /// Engine-independent authoring session used beneath the eventual Unity World Editor UI.
    /// Edits are page-aware, undoable and dirty-track only the pages that changed.
    /// </summary>
    public sealed class WorldEditSession
    {
        private readonly List<EditOperation> _undo = new List<EditOperation>();
        private readonly List<EditOperation> _redo = new List<EditOperation>();
        private readonly HashSet<WorldPageKey> _dirtyPages = new HashSet<WorldPageKey>();

        public WorldEditSession(AuthoredWorldPageStore store, int historyLimit = 100)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            if (historyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(historyLimit));
            HistoryLimit = historyLimit;
        }

        public AuthoredWorldPageStore Store { get; }
        public int HistoryLimit { get; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int DirtyPageCount => _dirtyPages.Count;
        public IEnumerable<WorldPageKey> DirtyPages => _dirtyPages;
        public string UndoLabel => CanUndo ? _undo[_undo.Count - 1].Label : string.Empty;
        public string RedoLabel => CanRedo ? _redo[_redo.Count - 1].Label : string.Empty;

        public int Apply(string label, IEnumerable<WorldCellEdit> edits)
        {
            if (edits == null) throw new ArgumentNullException(nameof(edits));
            var changesByLocation = new Dictionary<GridLocation, CellChange>();

            foreach (var edit in edits)
            {
                if (!WorldConstants.IsInsideWorld(edit.Location.Tile)) continue;
                Store.GetOrCreatePage(edit.Location);
                if (!Store.TryGetCell(edit.Location, out var current))
                    throw new InvalidOperationException("Edited page was created but its cell could not be resolved.");

                if (changesByLocation.TryGetValue(edit.Location, out var prior))
                {
                    prior.After = edit.Value;
                    changesByLocation[edit.Location] = prior;
                }
                else
                {
                    changesByLocation.Add(edit.Location, new CellChange(edit.Location, current, edit.Value));
                }
                Store.SetCell(edit.Location, edit.Value);
            }

            var changes = new List<CellChange>();
            foreach (var pair in changesByLocation)
            {
                if (pair.Value.Before.Equals(pair.Value.After)) continue;
                changes.Add(pair.Value);
                MarkDirty(pair.Value.Location);
            }

            if (changes.Count == 0) return 0;
            _undo.Add(new EditOperation(string.IsNullOrWhiteSpace(label) ? "World edit" : label, changes));
            if (_undo.Count > HistoryLimit) _undo.RemoveAt(0);
            _redo.Clear();
            return changes.Count;
        }

        public int PaintGround(GridLocation center, int brushSize, ContentId groundId, BrushShape shape = BrushShape.Square)
        {
            var edits = new List<WorldCellEdit>();
            foreach (var location in WorldBrush.Cells(center, brushSize, shape))
            {
                Store.GetOrCreatePage(location);
                Store.TryGetCell(location, out var cell);
                edits.Add(new WorldCellEdit(location, new AuthoredTileCell(
                    groundId,
                    cell.Elevation,
                    cell.Flags,
                    cell.MovementBlockedEdges,
                    cell.LineOfSightBlockedEdges,
                    cell.ElevationTransitionEdges)));
            }
            return Apply("Paint ground", edits);
        }

        public int SetElevation(GridLocation center, int brushSize, short elevation, BrushShape shape = BrushShape.Square)
        {
            var edits = new List<WorldCellEdit>();
            foreach (var location in WorldBrush.Cells(center, brushSize, shape))
            {
                Store.GetOrCreatePage(location);
                Store.TryGetCell(location, out var cell);
                edits.Add(new WorldCellEdit(location, new AuthoredTileCell(
                    cell.GroundId,
                    elevation,
                    cell.Flags,
                    cell.MovementBlockedEdges,
                    cell.LineOfSightBlockedEdges,
                    cell.ElevationTransitionEdges)));
            }
            return Apply("Set elevation", edits);
        }

        public int RaiseElevation(GridLocation center, int brushSize, short delta, BrushShape shape = BrushShape.Square)
        {
            var edits = new List<WorldCellEdit>();
            foreach (var location in WorldBrush.Cells(center, brushSize, shape))
            {
                Store.GetOrCreatePage(location);
                Store.TryGetCell(location, out var cell);
                var next = checked((short)(cell.Elevation + delta));
                edits.Add(new WorldCellEdit(location, new AuthoredTileCell(
                    cell.GroundId,
                    next,
                    cell.Flags,
                    cell.MovementBlockedEdges,
                    cell.LineOfSightBlockedEdges,
                    cell.ElevationTransitionEdges)));
            }
            return Apply(delta >= 0 ? "Raise elevation" : "Lower elevation", edits);
        }

        public int SetTileFlags(GridLocation center, int brushSize, TileFlags setFlags, TileFlags clearFlags, BrushShape shape = BrushShape.Square)
        {
            var edits = new List<WorldCellEdit>();
            foreach (var location in WorldBrush.Cells(center, brushSize, shape))
            {
                Store.GetOrCreatePage(location);
                Store.TryGetCell(location, out var cell);
                var flags = (cell.Flags | setFlags) & ~clearFlags;
                edits.Add(new WorldCellEdit(location, new AuthoredTileCell(
                    cell.GroundId,
                    cell.Elevation,
                    flags,
                    cell.MovementBlockedEdges,
                    cell.LineOfSightBlockedEdges,
                    cell.ElevationTransitionEdges)));
            }
            return Apply("Paint tile flags", edits);
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            var operation = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            for (var i = 0; i < operation.Changes.Count; i++)
            {
                var change = operation.Changes[i];
                Store.SetCell(change.Location, change.Before);
                MarkDirty(change.Location);
            }
            _redo.Add(operation);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            var operation = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            for (var i = 0; i < operation.Changes.Count; i++)
            {
                var change = operation.Changes[i];
                Store.SetCell(change.Location, change.After);
                MarkDirty(change.Location);
            }
            _undo.Add(operation);
            return true;
        }

        public IReadOnlyList<WorldPageDocument> BuildDirtyPageDocuments()
        {
            var documents = new List<WorldPageDocument>(_dirtyPages.Count);
            foreach (var key in _dirtyPages)
            {
                if (Store.TryGetPage(key, out var page)) documents.Add(WorldPageCodec.Encode(page));
            }
            return documents;
        }

        public void MarkPageSaved(WorldPageKey key) => _dirtyPages.Remove(key);
        public void MarkAllSaved() => _dirtyPages.Clear();

        private void MarkDirty(GridLocation location)
        {
            if (WorldAddressing.TryResolve(location, out var address, Store.PageSize))
                _dirtyPages.Add(address.Key);
        }

        private struct CellChange
        {
            public CellChange(GridLocation location, AuthoredTileCell before, AuthoredTileCell after)
            {
                Location = location;
                Before = before;
                After = after;
            }

            public GridLocation Location;
            public AuthoredTileCell Before;
            public AuthoredTileCell After;
        }

        private sealed class EditOperation
        {
            public EditOperation(string label, List<CellChange> changes)
            {
                Label = label;
                Changes = changes;
            }

            public string Label { get; }
            public List<CellChange> Changes { get; }
        }
    }
}
