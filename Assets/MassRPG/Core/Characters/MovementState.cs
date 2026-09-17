using System;
using System.Collections.Generic;
using MassRPG.Core.World;

namespace MassRPG.Core.Characters
{
    /// <summary>
    /// Authoritative logical movement plan. Presentation may interpolate between logical steps,
    /// but the client must not mutate this path or the resulting player location directly.
    /// </summary>
    public sealed class MovementState
    {
        private readonly List<GridLocation> _steps = new List<GridLocation>();
        private int _nextStepIndex;

        public IReadOnlyList<GridLocation> PlannedSteps => _steps;
        public bool IsMoving => _nextStepIndex < _steps.Count;
        public int RemainingSteps => Math.Max(0, _steps.Count - _nextStepIndex);
        public GridLocation? Destination => _steps.Count == 0 ? (GridLocation?)null : _steps[_steps.Count - 1];

        public void ReplacePath(IReadOnlyList<GridLocation> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            _steps.Clear();
            for (var i = 0; i < steps.Count; i++) _steps.Add(steps[i]);
            _nextStepIndex = 0;
        }

        public void Clear()
        {
            _steps.Clear();
            _nextStepIndex = 0;
        }

        public bool TryPeekNext(out GridLocation next)
        {
            if (!IsMoving)
            {
                next = default;
                return false;
            }

            next = _steps[_nextStepIndex];
            return true;
        }

        public bool TryConsumeNext(out GridLocation next)
        {
            if (!TryPeekNext(out next)) return false;
            _nextStepIndex++;
            if (_nextStepIndex >= _steps.Count) Clear();
            return true;
        }
    }
}
