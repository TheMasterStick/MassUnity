using System;
using System.Collections.Generic;

namespace MassRPG.Core.World
{
    public sealed class GridPathResult
    {
        private GridPathResult(bool success, IReadOnlyList<GridLocation> steps, int visitedNodes, string code)
        {
            Success = success;
            Steps = steps;
            VisitedNodes = visitedNodes;
            Code = code;
        }

        public bool Success { get; }
        public IReadOnlyList<GridLocation> Steps { get; }
        public int VisitedNodes { get; }
        public string Code { get; }

        public static GridPathResult Found(IReadOnlyList<GridLocation> steps, int visitedNodes)
            => new GridPathResult(true, steps, visitedNodes, "ok");

        public static GridPathResult Failed(string code, int visitedNodes)
            => new GridPathResult(false, Array.Empty<GridLocation>(), visitedNodes, code);
    }

    /// <summary>
    /// Local eight-direction pathfinding for the canonical 1x1 grid. Whole-world route planning is
    /// a separate higher-level system. Logical diagonal and straight steps have equal base cost,
    /// matching the RuneScape-like grid; presentation speed is independent.
    /// </summary>
    public static class GridPathfinder
    {
        private static readonly GridCoord[] Directions =
        {
            new GridCoord(1, 0), new GridCoord(-1, 0), new GridCoord(0, 1), new GridCoord(0, -1),
            new GridCoord(1, 1), new GridCoord(1, -1), new GridCoord(-1, 1), new GridCoord(-1, -1)
        };

        public static GridPathResult FindPath(IGridTraversalMap map, GridLocation start, GridLocation goal, int maxVisitedNodes = 25000)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (maxVisitedNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxVisitedNodes));
            if (!start.SameLayer(goal)) return GridPathResult.Failed("different_layer", 0);
            if (start == goal) return GridPathResult.Found(Array.Empty<GridLocation>(), 0);
            if (!map.IsWalkable(goal)) return GridPathResult.Failed("goal_blocked", 0);

            var open = new List<GridLocation> { start };
            var openSet = new HashSet<GridLocation> { start };
            var closed = new HashSet<GridLocation>();
            var cameFrom = new Dictionary<GridLocation, GridLocation>();
            var gScore = new Dictionary<GridLocation, int> { [start] = 0 };
            var visited = 0;

            while (open.Count > 0)
            {
                var currentIndex = FindBestOpenIndex(open, gScore, goal);
                var current = open[currentIndex];
                open.RemoveAt(currentIndex);
                openSet.Remove(current);

                if (current == goal)
                    return GridPathResult.Found(Reconstruct(cameFrom, start, goal), visited);

                if (!closed.Add(current)) continue;
                visited++;
                if (visited > maxVisitedNodes)
                    return GridPathResult.Failed("search_limit", visited);

                for (var i = 0; i < Directions.Length; i++)
                {
                    var next = Next(current, Directions[i]);
                    if (closed.Contains(next)) continue;
                    if (!GridTraversal.CanStep(map, current, next)) continue;

                    var tentative = gScore[current] + 10;
                    if (gScore.TryGetValue(next, out var known) && tentative >= known) continue;

                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    if (openSet.Add(next)) open.Add(next);
                }
            }

            return GridPathResult.Failed("unreachable", visited);
        }

        /// <summary>
        /// Breadth-first local search to the nearest reachable tile satisfying an authority-owned
        /// predicate. Useful for interactions such as approaching a multi-tile combat target where
        /// the client must not choose the authoritative stopping tile itself.
        /// </summary>
        public static GridPathResult FindNearestMatching(
            IGridTraversalMap map,
            GridLocation start,
            Func<GridLocation, bool> isGoal,
            int maxVisitedNodes = 25000)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (isGoal == null) throw new ArgumentNullException(nameof(isGoal));
            if (maxVisitedNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxVisitedNodes));
            if (isGoal(start)) return GridPathResult.Found(Array.Empty<GridLocation>(), 0);

            var queue = new Queue<GridLocation>();
            var visitedSet = new HashSet<GridLocation> { start };
            var cameFrom = new Dictionary<GridLocation, GridLocation>();
            queue.Enqueue(start);
            var visited = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                visited++;
                if (visited > maxVisitedNodes) return GridPathResult.Failed("search_limit", visited);

                for (var i = 0; i < Directions.Length; i++)
                {
                    var next = Next(current, Directions[i]);
                    if (!visitedSet.Add(next)) continue;
                    if (!GridTraversal.CanStep(map, current, next)) continue;
                    cameFrom[next] = current;
                    if (isGoal(next)) return GridPathResult.Found(Reconstruct(cameFrom, start, next), visited);
                    queue.Enqueue(next);
                }
            }

            return GridPathResult.Failed("unreachable", visited);
        }

        private static GridLocation Next(GridLocation current, GridCoord direction)
            => new GridLocation(
                new GridCoord(current.Tile.X + direction.X, current.Tile.Y + direction.Y),
                current.Plane,
                current.Storey);

        private static int FindBestOpenIndex(List<GridLocation> open, Dictionary<GridLocation, int> gScore, GridLocation goal)
        {
            var bestIndex = 0;
            var bestScore = int.MaxValue;

            for (var i = 0; i < open.Count; i++)
            {
                var node = open[i];
                var heuristic = GridMath.RangeDistance(node.Tile, goal.Tile) * 10;
                var score = gScore[node] + heuristic;
                if (score >= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static IReadOnlyList<GridLocation> Reconstruct(
            Dictionary<GridLocation, GridLocation> cameFrom,
            GridLocation start,
            GridLocation goal)
        {
            var reverse = new List<GridLocation>();
            var current = goal;

            while (current != start)
            {
                reverse.Add(current);
                current = cameFrom[current];
            }

            reverse.Reverse();
            return reverse;
        }
    }
}
