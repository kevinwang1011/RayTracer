using UnityEngine;
using Unity.Mathematics;
using RayTracerUtils;
using System;
using System.Collections.Generic;


public class BVHConstructor
{
    static BVHTriangle[] allTriangles;
    public static List<Triangle> reorderedTriangles;
    static NodeList allNodes = new NodeList();
    public static Node[] GetNodes() => allNodes.Nodes.AsSpan(0, allNodes.NodeCount).ToArray();
    public static void ConstructBVH()
    {

        BoundingBox bounds = new BoundingBox();
        allTriangles = new BVHTriangle[RayTracingMaster._indices.Count / 3];

        for (int i = 0; i < RayTracingMaster._indices.Count; i += 3)
        {
            float3 a = RayTracingMaster._vertices[RayTracingMaster._indices[i]];
            float3 b = RayTracingMaster._vertices[RayTracingMaster._indices[i + 1]];
            float3 c = RayTracingMaster._vertices[RayTracingMaster._indices[i + 2]];
            float3 max = math.max(math.max(a, b), c);
            float3 min = math.min(math.min(a, b), c);
            allTriangles[i / 3] = new BVHTriangle((a + b + c) / 3, min, max, i);
            bounds.GrowToInclude(min, max);
        }

        allNodes.Add(new Node(bounds));
        Split(0, 0, RayTracingMaster._indices.Count / 3, 0);

        reorderedTriangles = new List<Triangle>(allTriangles.Length);
        Debug.Log($"Constructed BVH with {allNodes.Nodes.Length} nodes and {allTriangles.Length} triangles, reordered to {reorderedTriangles.Count} triangles");
        Debug.Log($"Face materials: {RayTracingMaster._faceMaterials.Count}, materials: {RayTracingMaster._materials.Count}");
        for (int i = 0; i < allTriangles.Length; i++)
        {
            BVHTriangle buildTri = allTriangles[i];
            Vector3 a = RayTracingMaster._vertices[RayTracingMaster._indices[buildTri.Index]];
            Vector3 b = RayTracingMaster._vertices[RayTracingMaster._indices[buildTri.Index + 1]];
            Vector3 c = RayTracingMaster._vertices[RayTracingMaster._indices[buildTri.Index + 2]];
            Vector3 norm_a = RayTracingMaster._normals[RayTracingMaster._indices[buildTri.Index + 0]];
            Vector3 norm_b = RayTracingMaster._normals[RayTracingMaster._indices[buildTri.Index + 1]];
            Vector3 norm_c = RayTracingMaster._normals[RayTracingMaster._indices[buildTri.Index + 2]];
            Vector2 uv_a = RayTracingMaster._texCoords[RayTracingMaster._indices[buildTri.Index + 0]];
            Vector2 uv_b = RayTracingMaster._texCoords[RayTracingMaster._indices[buildTri.Index + 1]];
            Vector2 uv_c = RayTracingMaster._texCoords[RayTracingMaster._indices[buildTri.Index + 2]];

            reorderedTriangles.Add(new Triangle(a, b, c,
                                                norm_a, norm_b, norm_c,
                                                uv_a, uv_b, uv_c,
                                                RayTracingMaster._faceMaterials[buildTri.Index / 3]));

        }

    }

    static void Split(int parentIndex, int triGlobalStart, int triNum, int depth)
    {
        const int MaxDepth = 32;
        Node parent = allNodes.Nodes[parentIndex];
        Vector3 size = parent.CalculateBoundsSize();
        float parentCost = NodeCost(size, triNum);

        (int splitAxis, float splitPos, float cost) = ChooseSplit(parent, triGlobalStart, triNum);

        if (cost < parentCost && depth < MaxDepth)
        {
            BoundingBox boundsLeft = new();
            BoundingBox boundsRight = new();
            int numOnLeft = 0;

            for (int i = triGlobalStart; i < triGlobalStart + triNum; i++)
            {
                BVHTriangle tri = allTriangles[i];
                if (tri.Center[splitAxis] < splitPos)
                {
                    boundsLeft.GrowToInclude(tri.Min, tri.Max);

                    BVHTriangle swap = allTriangles[triGlobalStart + numOnLeft];
                    allTriangles[triGlobalStart + numOnLeft] = tri;
                    allTriangles[i] = swap;
                    numOnLeft++;
                }
                else
                {
                    boundsRight.GrowToInclude(tri.Min, tri.Max);
                }
            }

            int numOnRight = triNum - numOnLeft;
            int triStartLeft = triGlobalStart + 0;
            int triStartRight = triGlobalStart + numOnLeft;

            // Split parent into two children
            int childIndexLeft = allNodes.Add(new(boundsLeft, triStartLeft, 0));
            int childIndexRight = allNodes.Add(new(boundsRight, triStartRight, 0));

            // Update parent
            parent.StartIndex = childIndexLeft;
            allNodes.Nodes[parentIndex] = parent;

            // Recursively split children
            Split(childIndexLeft, triGlobalStart, numOnLeft, depth + 1);
            Split(childIndexRight, triGlobalStart + numOnLeft, numOnRight, depth + 1);
        }
        else
        {
            // Parent is actually leaf, assign all triangles to it
            parent.StartIndex = triGlobalStart;
            parent.TriangleCount = triNum;
            allNodes.Nodes[parentIndex] = parent;
        }
    }

    static (int axis, float pos, float cost) ChooseSplit(Node node, int start, int count)
    {
        if (count <= 1) return (0, 0, float.PositiveInfinity);

        float bestSplitPos = 0;
        int bestSplitAxis = 0;
        const int numSplitTests = 5;

        float bestCost = float.MaxValue;

        // Estimate best split pos
        for (int axis = 0; axis < 3; axis++)
        {
            for (int i = 0; i < numSplitTests; i++)
            {
                float splitT = (i + 1) / (numSplitTests + 1f);
                float splitPos = Mathf.Lerp(node.BoundsMin[axis], node.BoundsMax[axis], splitT);
                float cost = EvaluateSplit(axis, splitPos, start, count);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSplitPos = splitPos;
                    bestSplitAxis = axis;
                }
            }
        }

        return (bestSplitAxis, bestSplitPos, bestCost);
    }

    static float EvaluateSplit(int splitAxis, float splitPos, int start, int count)
    {
        BoundingBox boundsLeft = new();
        BoundingBox boundsRight = new();
        int numOnLeft = 0;
        int numOnRight = 0;

        for (int i = start; i < start + count; i++)
        {
            BVHTriangle tri = allTriangles[i];
            if (tri.Center[splitAxis] < splitPos)
            {
                boundsLeft.GrowToInclude(tri.Min, tri.Max);
                numOnLeft++;
            }
            else
            {
                boundsRight.GrowToInclude(tri.Min, tri.Max);
                numOnRight++;
            }
        }

        float costA = NodeCost(boundsLeft.Size, numOnLeft);
        float costB = NodeCost(boundsRight.Size, numOnRight);
        return costA + costB;
    }

    static float NodeCost(Vector3 size, int numTriangles)
    {
        float halfArea = size.x * size.y + size.x * size.z + size.y * size.z;
        return halfArea * numTriangles;
    }
}