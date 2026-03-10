using System.Collections.Generic;
using System.Linq;

namespace sugi.cc.ImageProcessTool
{
    public static class ImageProcessGraphTopology
    {
        public static bool TryCollectExecutableSubgraph(
            ImageProcessGraphAsset graph,
            out List<ImageProcessNodeData> executableNodes,
            out List<ImageProcessEdgeData> executableEdges,
            out string error)
        {
            executableNodes = new List<ImageProcessNodeData>();
            executableEdges = new List<ImageProcessEdgeData>();

            if (graph == null)
            {
                error = "Graph asset is null.";
                return false;
            }

            graph.EnsureNodeIds();

            var nodes = graph.Nodes.Where(n => n != null).ToList();
            var edges = graph.Edges.Where(e => e != null).ToList();
            var byId = nodes.ToDictionary(n => n.nodeId, n => n);
            var incoming = new Dictionary<string, List<ImageProcessEdgeData>>();

            foreach (var edge in edges)
            {
                if (!byId.ContainsKey(edge.outputNodeId) || !byId.ContainsKey(edge.inputNodeId))
                {
                    error = "Edge references a missing node.";
                    return false;
                }

                if (edge.outputNodeId == edge.inputNodeId)
                {
                    error = "Self loop is not allowed.";
                    return false;
                }

                if (!incoming.TryGetValue(edge.inputNodeId, out var list))
                {
                    list = new List<ImageProcessEdgeData>();
                    incoming[edge.inputNodeId] = list;
                }

                list.Add(edge);
            }

            var reachable = new HashSet<string>();
            var stack = new Stack<string>(
                nodes
                    .Where(n => n.nodeKind == ImageProcessNodeKind.Output)
                    .Select(n => n.nodeId));

            while (stack.Count > 0)
            {
                var nodeId = stack.Pop();
                if (!reachable.Add(nodeId))
                {
                    continue;
                }

                if (!incoming.TryGetValue(nodeId, out var incomingEdges))
                {
                    continue;
                }

                foreach (var edge in incomingEdges)
                {
                    stack.Push(edge.outputNodeId);
                }
            }

            executableNodes = nodes.Where(n => reachable.Contains(n.nodeId)).ToList();
            executableEdges = edges.Where(e => reachable.Contains(e.outputNodeId) && reachable.Contains(e.inputNodeId)).ToList();
            error = string.Empty;
            return true;
        }

        public static bool TryBuildExecutionOrder(
            ImageProcessGraphAsset graph,
            out List<ImageProcessNodeData> orderedNodes,
            out string error)
        {
            orderedNodes = new List<ImageProcessNodeData>();

            if (!TryCollectExecutableSubgraph(graph, out var nodes, out var edges, out error))
            {
                return false;
            }

            var byId = nodes.ToDictionary(n => n.nodeId, n => n);
            var inDegree = nodes.ToDictionary(n => n.nodeId, _ => 0);
            var outgoing = nodes.ToDictionary(n => n.nodeId, _ => new List<string>());

            foreach (var edge in edges)
            {
                if (!byId.ContainsKey(edge.outputNodeId) || !byId.ContainsKey(edge.inputNodeId))
                {
                    error = "Edge references a missing node.";
                    return false;
                }

                if (edge.outputNodeId == edge.inputNodeId)
                {
                    error = "Self loop is not allowed.";
                    return false;
                }

                outgoing[edge.outputNodeId].Add(edge.inputNodeId);
                inDegree[edge.inputNodeId]++;
            }

            var queue = new Queue<string>(inDegree.Where(x => x.Value == 0).Select(x => x.Key));

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                orderedNodes.Add(byId[currentId]);

                foreach (var nextId in outgoing[currentId])
                {
                    inDegree[nextId]--;
                    if (inDegree[nextId] == 0)
                    {
                        queue.Enqueue(nextId);
                    }
                }
            }

            if (orderedNodes.Count != nodes.Count)
            {
                error = "Cycle detected in graph.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
