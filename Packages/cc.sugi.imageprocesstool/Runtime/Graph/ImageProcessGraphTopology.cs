using System.Collections.Generic;
using System.Linq;

namespace sugi.cc.ImageProcessTool
{
    public static class ImageProcessGraphTopology
    {
        public static bool TryBuildExecutionOrder(
            ImageProcessGraphAsset graph,
            out List<ImageProcessNodeData> orderedNodes,
            out string error)
        {
            orderedNodes = new List<ImageProcessNodeData>();

            if (graph == null)
            {
                error = "Graph asset is null.";
                return false;
            }

            graph.EnsureNodeIds();

            var nodes = graph.Nodes.ToList();
            var edges = graph.Edges.ToList();

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

