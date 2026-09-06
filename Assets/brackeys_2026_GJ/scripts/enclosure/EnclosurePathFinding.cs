using System.Collections.Generic;
using UnityEngine;

public class EnclosurePathfinder : MonoBehaviour
{
    [SerializeField] private float cellSize = 0.5f;
    [SerializeField, Min(0f)] private float agentRadius = 0.22f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private bool allowDiagonalMovement = false;

    private Node[,] grid;

    private Vector2 gridOrigin;
    private int gridWidth;
    private int gridHeight;

    public bool IsReady => grid != null;

    private class Node
    {
        public Vector2 worldPosition;
        public int x;
        public int y;
        public bool walkable;

        public int gCost;
        public int hCost;
        public Node parent;

        public int fCost => gCost + hCost;

        public Node(Vector2 worldPosition, int x, int y, bool walkable)
        {
            this.worldPosition = worldPosition;
            this.x = x;
            this.y = y;
            this.walkable = walkable;

            gCost = int.MaxValue;
            hCost = 0;
            parent = null;
        }
    }

    public void Build(Bounds bounds)
    {
        gridWidth = Mathf.CeilToInt(bounds.size.x / cellSize);
        gridHeight = Mathf.CeilToInt(bounds.size.y / cellSize);

        gridOrigin = new Vector2(
            bounds.min.x,
            bounds.min.y
        );

        grid = new Node[gridWidth, gridHeight];

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayer);
        filter.useTriggers = true;

        Collider2D[] results = new Collider2D[1];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2 worldPosition = gridOrigin + new Vector2(
                    (x + 0.5f) * cellSize,
                    (y + 0.5f) * cellSize
                );


                bool walkable = IsWalkable(worldPosition, filter, results);

                grid[x, y] = new Node(
                    worldPosition,
                    x,
                    y,
                    walkable
                );
            }
        }
    }

    public List<Vector2> FindPath(Vector2 startWorld, Vector2 targetWorld)
    {

        if (grid == null)
        {
            return null;
        }

        foreach (Node node in grid)
        {
            node.gCost = int.MaxValue;
            node.hCost = 0;
            node.parent = null;
        }

        Node start = NodeFromWorld(startWorld);
        Node target = NodeFromWorld(targetWorld);
        if (start == null)
        {
            return null;
        }

        if (target == null)
        {
            return null;
        }
        if (!start.walkable)
        {
            return null;
        }

        if (!target.walkable)
        {
            return null;
        }

        if (start == target)
            return new List<Vector2> { targetWorld };

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        openSet.Add(start);

        start.gCost = 0;
        start.hCost = GetDistance(start, target);

        while (openSet.Count > 0)
        {
            Node current = openSet[0];

            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < current.fCost ||
                    (openSet[i].fCost == current.fCost &&
                     openSet[i].hCost < current.hCost))
                {
                    current = openSet[i];
                }
            }

            openSet.Remove(current);
            closedSet.Add(current);

            if (current == target)
            {
                List<Vector2> path = RetracePath(start, target);
                return path;
            }

            foreach (Node neighbour in GetNeighbours(current))
            {
                if (!neighbour.walkable || closedSet.Contains(neighbour))
                    continue;

                int newCost = current.gCost +
                              GetDistance(current, neighbour);

                if (newCost < neighbour.gCost ||
                    !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newCost;
                    neighbour.hCost = GetDistance(neighbour, target);
                    neighbour.parent = current;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }
        }
        return null;
    }

    private List<Vector2> RetracePath(Node start, Node target)
    {
        List<Vector2> path = new List<Vector2>();

        Node current = target;

        while (current != start)
        {
            path.Add(current.worldPosition);
            current = current.parent;

            if (current == null)
                return null;
        }

        path.Reverse();

        return SimplifyPath(path);
    }

    private List<Vector2> SimplifyPath(List<Vector2> path)
    {
        if (path.Count <= 2)
            return path;

        List<Vector2> simplified = new List<Vector2> { path[0] };
        Vector2 previousDirection = (path[1] - path[0]).normalized;

        for (int i = 2; i < path.Count; i++)
        {
            Vector2 direction = (path[i] - path[i - 1]).normalized;

            if (direction != previousDirection)
            {
                simplified.Add(path[i - 1]);
            }

            previousDirection = direction;
        }

        simplified.Add(path[path.Count - 1]);

        return simplified;
    }

    private bool IsWalkable(
        Vector2 worldPosition,
        ContactFilter2D filter,
        Collider2D[] results
    )
    {
        if (Physics2D.OverlapPoint(worldPosition, filter, results) == 0)
            return false;

        if (agentRadius <= 0f)
            return true;

        return Physics2D.OverlapPoint(worldPosition + Vector2.left * agentRadius, filter, results) > 0 &&
            Physics2D.OverlapPoint(worldPosition + Vector2.right * agentRadius, filter, results) > 0 &&
            Physics2D.OverlapPoint(worldPosition + Vector2.up * agentRadius, filter, results) > 0 &&
            Physics2D.OverlapPoint(worldPosition + Vector2.down * agentRadius, filter, results) > 0;
    }

    private IEnumerable<Node> GetNeighbours(Node node)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                if (!allowDiagonalMovement &&
                    Mathf.Abs(x) + Mathf.Abs(y) == 2)
                    continue;

                int checkX = node.x + x;
                int checkY = node.y + y;

                if (checkX < 0 || checkX >= gridWidth ||
                    checkY < 0 || checkY >= gridHeight)
                    continue;

                if (x != 0 && y != 0 &&
                    (!grid[node.x + x, node.y].walkable ||
                     !grid[node.x, node.y + y].walkable))
                    continue;

                yield return grid[checkX, checkY];
            }
        }
    }

    private Node NodeFromWorld(Vector2 worldPosition)
    {
        int x = Mathf.FloorToInt(
            (worldPosition.x - gridOrigin.x) / cellSize
        );

        int y = Mathf.FloorToInt(
            (worldPosition.y - gridOrigin.y) / cellSize
        );

        if (x < 0 || x >= gridWidth ||
            y < 0 || y >= gridHeight)
            return null;

        return grid[x, y];
    }

    private int GetDistance(Node a, Node b)
    {
        int distanceX = Mathf.Abs(a.x - b.x);
        int distanceY = Mathf.Abs(a.y - b.y);

        if (allowDiagonalMovement)
        {
            int diagonal = Mathf.Min(distanceX, distanceY);
            int straight = Mathf.Abs(distanceX - distanceY);

            return diagonal * 14 + straight * 10;
        }

        return (distanceX + distanceY) * 10;
    }

    private void OnDrawGizmosSelected()
    {
        if (grid == null)
            return;

        foreach (Node node in grid)
        {
            Gizmos.color = node.walkable
                ? new Color(0f, 1f, 0f, 0.15f)
                : new Color(1f, 0f, 0f, 0.05f);

            Gizmos.DrawCube(
                node.worldPosition,
                Vector3.one * cellSize * 0.9f
            );
        }
    }
}
