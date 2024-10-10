
using UnityEngine;
using Unity.Mathematics;
using System;
using System.Collections.Generic;

namespace RayTracerUtils
{
    public struct RayTracingMaterialShader
    {
        public Vector4 albedo;
        public Vector4 emission;
        public Vector4 specular;
        public float emissionStrength;
        public float smoothness;
        public float specularProbability;
        public int texID;
    };

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
        public int _texID;
        public RayTracingMaterialShader material;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
    }
    [Serializable]
    public class RayTracingMaterial
    {
        //     public enum MaterialFlag
        //     {
        //         None,
        //         InvisibleLight
        //     }

        public Color color;
        public Color emissionColor;
        public Color specularColor;
        public float emissionStrength;
        [Range(0, 1)] public float smoothness;
        [Range(0, 1)] public float specularProbability;
        public int texID;
        // public MaterialFlag flag;

        public void SetDefaultValues()
        {
            color = Color.white;
            emissionColor = Color.white;
            emissionStrength = 0;
            specularColor = Color.white;
            smoothness = 0;
            specularProbability = 1;
            texID = -1;
        }
    }

    public class NodeList
    {
        public Node[] Nodes = new Node[256];
        int Index;

        public int Add(Node node)
        {
            if (Index >= Nodes.Length)
            {
                Array.Resize(ref Nodes, Nodes.Length * 2);
            }

            int nodeIndex = Index;
            Nodes[Index++] = node;
            return nodeIndex;
        }

        public int NodeCount => Index;

    }


    public struct Node
    {
        public float3 BoundsMin;
        public float3 BoundsMax;
        // Index of first child (if triangle count is negative) otherwise index of first triangle
        public int StartIndex;
        public int TriangleCount;

        public Node(BoundingBox bounds) : this()
        {
            BoundsMin = bounds.Min;
            BoundsMax = bounds.Max;
            StartIndex = -1;
            TriangleCount = -1;
        }

        public Node(BoundingBox bounds, int startIndex, int triCount)
        {
            BoundsMin = bounds.Min;
            BoundsMax = bounds.Max;
            StartIndex = startIndex;
            TriangleCount = triCount;
        }

        public float3 CalculateBoundsSize() => BoundsMax - BoundsMin;
        public float3 CalculateBoundsCentre() => (BoundsMin + BoundsMax) / 2;
    }

    public struct BoundingBox
    {
        public float3 Min;
        public float3 Max;
        public float3 Centrer => (Min + Max) / 2;
        public float3 Size => Max - Min;
        bool hasPoint;

        public void GrowToInclude(float3 min, float3 max)
        {
            if (hasPoint)
            {
                Min.x = min.x < Min.x ? min.x : Min.x;
                Min.y = min.y < Min.y ? min.y : Min.y;
                Min.z = min.z < Min.z ? min.z : Min.z;
                Max.x = max.x > Max.x ? max.x : Max.x;
                Max.y = max.y > Max.y ? max.y : Max.y;
                Max.z = max.z > Max.z ? max.z : Max.z;
            }
            else
            {
                hasPoint = true;
                Min = min;
                Max = max;
            }
        }
    }

    public readonly struct BVHTriangle
    {
        public readonly float3 Center;
        public readonly float3 Min;
        public readonly float3 Max;
        public readonly int Index;

        public BVHTriangle(float3 center, float3 min, float3 max, int index)
        {
            Center = center;
            Min = min;
            Max = max;
            Index = index;
        }
    }

    public struct Triangle
    {
        public float3 v0, v1, v2;
        public float3 n0, n1, n2;
        public float2 uv0, uv1, uv2;
        public int matID;
        public Triangle(Vector3 v0, Vector3 v1, Vector3 v2,
                Vector3 n0, Vector3 n1, Vector3 n2,
                Vector2 uv0, Vector2 uv1, Vector2 uv2, int matID)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.n0 = n0;
            this.n1 = n1;
            this.n2 = n2;
            this.uv0 = uv0;
            this.uv1 = uv1;
            this.uv2 = uv2;
            this.matID = matID;
        }
    }
}
