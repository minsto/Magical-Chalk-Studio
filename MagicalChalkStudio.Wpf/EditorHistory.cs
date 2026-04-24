using System.Collections.Generic;
using System.Linq;

namespace MagicalChalkStudio
{
    public sealed class EditorSnapshot
    {
        public List<PlacedBlock> Blocks { get; }
        public int CurrentLayer { get; }

        public EditorSnapshot(List<PlacedBlock> blocks, int currentLayer)
        {
            Blocks = blocks.Select(b => b.Clone()).ToList();
            CurrentLayer = currentLayer;
        }
    }

    public sealed class EditorHistory
    {
        private readonly Stack<EditorSnapshot> _undo = new Stack<EditorSnapshot>();
        private readonly Stack<EditorSnapshot> _redo = new Stack<EditorSnapshot>();

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        public void SeedInitial(List<PlacedBlock> blocks, int layer)
        {
            Clear();
            _undo.Push(new EditorSnapshot(blocks, layer));
        }

        public void PushAfterChange(List<PlacedBlock> blocks, int layer)
        {
            _redo.Clear();
            _undo.Push(new EditorSnapshot(blocks, layer));
        }

        public bool TryUndo(List<PlacedBlock> target, ref int currentLayer)
        {
            if (_undo.Count <= 1) return false;
            var current = _undo.Pop();
            _redo.Push(current);
            var prev = _undo.Peek();
            ReplaceFrom(target, prev.Blocks);
            currentLayer = prev.CurrentLayer;
            return true;
        }

        public bool TryRedo(List<PlacedBlock> target, ref int currentLayer)
        {
            if (_redo.Count == 0) return false;
            var next = _redo.Pop();
            _undo.Push(next);
            ReplaceFrom(target, next.Blocks);
            currentLayer = next.CurrentLayer;
            return true;
        }

        private static void ReplaceFrom(List<PlacedBlock> target, List<PlacedBlock> source)
        {
            target.Clear();
            foreach (var b in source)
                target.Add(b.Clone());
        }
    }
}
