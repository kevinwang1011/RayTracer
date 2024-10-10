using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using RayTracerUtils;
public class RayTracingMaster : MonoBehaviour
{
    private static RayTracingMaster _instance;
    public static RayTracingMaster Instance()
    {
        return _instance;
    }

    private GraphicsFence _graphicsFence;
    private CommandBuffer _commandBuffer;
    public ComputeShader RayTracingShader;
    public ComputeShader AccumulationShader;
    private bool _isRendering = false;
    private bool _isAccumulating = false;
    private double _renderingStartTime;
    public Material DisplayMaterial;
    private Texture2DArray _textureArray;
    private Texture2D[] _textures = new Texture2D[0];
    private Texture2D _outputTexture = null;
    private string _outputPath = Application.dataPath + "/../PNGs/";

    private RenderTexture _computeShaderResult;
    [SerializeField][Tooltip("Auto Generated")] private RenderTexture _displayTexture;

    private Camera _camera;
    private void Awake()
    {
        _instance = this;

        _camera = GetComponent<Camera>();
        _textureArray = new Texture2DArray(512, 512, 3, TextureFormat.RGBA32, false);
        _textureArray.wrapMode = TextureWrapMode.Repeat;

        InitRenderTexture();
        EventManager.Instance.AddListener(EventManager.EventType.Render, () => { RenderImage(); });

        // Set FPS to 60
        Application.targetFrameRate = 60;

        RayTracerKernelID = RayTracingShader.FindKernel("RayTracerBVH");
        AccumulationKernelID = AccumulationShader.FindKernel("Accumulation");

    }

    const int MAX_PASSES = 10;
    int _currentPass = 0;

    private void Update()
    {
        if (_isRendering)
        {
            if (_graphicsFence.passed)
            {
                _isRendering = false;
                Debug.Log($"Rendering Pass #{_currentPass} took {(Time.unscaledTimeAsDouble - _renderingStartTime) * 1000d} ms");



                _isAccumulating = true;

                _commandBuffer = new CommandBuffer();
                _commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
                _graphicsFence = _commandBuffer.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.ComputeProcessing);

                RenderTexture temp = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                Graphics.Blit(_displayTexture, temp);


                AccumulationShader.SetTexture(AccumulationKernelID, "_PrevFrame", temp);
                AccumulationShader.SetTexture(AccumulationKernelID, "_NextFrame", _computeShaderResult);
                AccumulationShader.SetTexture(AccumulationKernelID, "_Result", _displayTexture);
                AccumulationShader.SetInt("_Frame", _currentPass);

                _renderingStartTime = Time.unscaledTimeAsDouble;
                AccumulationShader.Dispatch(AccumulationKernelID, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1);


                Graphics.ExecuteCommandBufferAsync(_commandBuffer, ComputeQueueType.Default);

                RenderTexture.ReleaseTemporary(temp);
            }
        }
        else if (_isAccumulating)
        {
            if (_graphicsFence.passed)
            {
                _isAccumulating = false;
                Debug.Log($"Accumulating took {(Time.unscaledTimeAsDouble - _renderingStartTime) * 1000d} ms");

                var oldRT = RenderTexture.active;
                RenderTexture.active = _displayTexture;
                _outputTexture.ReadPixels(new Rect(0, 0, _displayTexture.width, _displayTexture.height), 0, 0);
                _outputTexture.Apply();
                System.IO.File.WriteAllBytes(_outputPath + _currentPass.ToString() + ".jpg", _outputTexture.EncodeToJPG(95));

                RenderTexture.active = oldRT;
                _currentPass++;

                if (_currentPass < MAX_PASSES)
                {
                    Render();
                }
                else
                {
                    _currentPass = 0;
                    _isAccumulating = false;
                    Debug.Log("Finished");
                }
            }
        }
    }

    private void OnDestroy()
    {
        // _meshObjectBuffer?.Release();
        // _vertexBuffer?.Release();
        // _indexBuffer?.Release();
        // _texCoordBuffer?.Release();
        // _normalBuffer?.Release();
        _MaterialBuffer?.Release();
        _TriangleBuffer?.Release();
        _BVHNodeBuffer?.Release();
        _computeShaderResult?.Release();
    }


    private void RenderImage()
    {
        RebuildMeshObjectBuffers();
        SetShaderParameters();
        Render();
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        // SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        // SetComputeBuffer("_Vertices", _vertexBuffer);
        // SetComputeBuffer("_Indices", _indexBuffer);
        // SetComputeBuffer("_TexCoords", _texCoordBuffer);
        // SetComputeBuffer("_Normals", _normalBuffer);

        SetComputeBuffer("Materials", _MaterialBuffer);
        SetComputeBuffer("Triangles", _TriangleBuffer);
        SetComputeBuffer("Nodes", _BVHNodeBuffer);

        RayTracingShader.SetTexture(0, "_Texs", _textureArray);

    }

    int RayTracerKernelID;
    int AccumulationKernelID;

    private void Render()
    {
        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(RayTracerKernelID, "Result", _computeShaderResult);
        RayTracingShader.SetInt("Frame", Time.frameCount * Mathf.FloorToInt(Time.unscaledTime));
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 16.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 16.0f);

        _commandBuffer = new CommandBuffer();
        _commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        _graphicsFence = _commandBuffer.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.ComputeProcessing);
        RayTracingShader.Dispatch(RayTracerKernelID, threadGroupsX, threadGroupsY, 1);
        Graphics.ExecuteCommandBufferAsync(_commandBuffer, ComputeQueueType.Default);
        _isRendering = true;
        _renderingStartTime = Time.unscaledTimeAsDouble;
    }
    private void InitRenderTexture()
    {
        if (_computeShaderResult == null || _computeShaderResult.width != Screen.width || _computeShaderResult.height != Screen.height)
        {
            // Release render texture if we already have one
            if (_computeShaderResult != null)
                _computeShaderResult.Release();
            // Get a render target for Ray Tracing
            _computeShaderResult = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            _computeShaderResult.enableRandomWrite = true;
            Debug.Log($"Creating Render Texture with {Screen.width}x{Screen.height}");
            _computeShaderResult.Create();


        }

        _displayTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
        _displayTexture.enableRandomWrite = true;
        _displayTexture.Create();

        _outputTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBAFloat, false);

        SetupMaterialTexture();

    }

    private void SetupMaterialTexture()
    {
        DisplayMaterial.SetTexture("_MainTex", _displayTexture);
    }




    #region Datas

    public static List<Vector3> _vertices = new List<Vector3>();
    public static List<Vector3> _normals = new List<Vector3>();
    public static List<Vector2> _texCoords = new List<Vector2>();
    public static List<int> _indices = new List<int>();
    public static List<int> _faceMaterials = new List<int>();
    public static List<RayTracingMaterialShader> _materials = new List<RayTracingMaterialShader>();

    #endregion

    #region Compute Buffers

    // private ComputeBuffer _meshObjectBuffer;
    // private ComputeBuffer _vertexBuffer;
    // private ComputeBuffer _normalBuffer;
    // private ComputeBuffer _indexBuffer;
    // private ComputeBuffer _texCoordBuffer;

    private ComputeBuffer _MaterialBuffer;
    private ComputeBuffer _TriangleBuffer;
    private ComputeBuffer _BVHNodeBuffer;

    #endregion

    private uint _currentSample = 0;
    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();
    private static List<MeshObject> _meshObjects = new List<MeshObject>();

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }
    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }
        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;
        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();
        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
            // Add vertex data, if the vertex buffer wasn't empty before
            // the vertices need to be offset due to the previous data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices.Select(v => obj.transform.TransformPoint(v)));
            _normals.AddRange(mesh.normals);

            if (mesh.uv.Length > 0)
                _texCoords.AddRange(mesh.uv);
            else
                _texCoords.AddRange(Enumerable.Repeat(Vector2.zero, mesh.vertices.Length));

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                // Add index data - if the vertex buffer wasn't empty before
                // the indices need to be offset due to the previous data
                int firstIndex = _indices.Count;
                _indices.AddRange(mesh.GetIndices(i).Select(index => index + firstVertex));

                int hasMaterial = -1;
                Vector4 albedo = Vector4.one;

                if (meshRenderer.materials[i].mainTexture != null)
                {
                    hasMaterial = _materials.FindIndex(m => m.texID != -1 && _textures[m.texID] == (Texture2D)meshRenderer.materials[i].mainTexture);
                    if (hasMaterial == -1)
                    {
                        _textures = _textures.Append((Texture2D)meshRenderer.materials[i].mainTexture).ToArray();
                        _materials.Add(new RayTracingMaterialShader()
                        {
                            albedo = albedo,
                            emission = obj.material[i].emissionColor,
                            emissionStrength = obj.material[i].emissionStrength,
                            smoothness = obj.material[i].smoothness,
                            specular = obj.material[i].specularColor,
                            specularProbability = obj.material[i].specularProbability,
                            texID = _textures.Length - 1
                        });
                        hasMaterial = _materials.Count - 1;
                        Debug.Log($"Adding Material {hasMaterial} with Albedo {albedo}, texID: {_textures.Length - 1}");
                    }
                    _faceMaterials.AddRange(Enumerable.Repeat(hasMaterial, mesh.GetIndices(i).Length / 3));
                }
                else
                {
                    albedo = meshRenderer.materials[i].color;
                    // Check if there already exist a pure color material
                    hasMaterial = _materials.FindIndex(m => m.texID == -1 && m.albedo == albedo);
                    if (hasMaterial == -1)
                    {
                        _materials.Add(new RayTracingMaterialShader()
                        {
                            albedo = albedo,
                            emission = obj.material[i].emissionColor,
                            emissionStrength = obj.material[i].emissionStrength,
                            smoothness = obj.material[i].smoothness,
                            specular = obj.material[i].specularColor,
                            specularProbability = obj.material[i].specularProbability,
                            texID = -1
                        });
                        hasMaterial = _materials.Count - 1;
                        string info = $"Adding Material {hasMaterial} {meshRenderer.materials[i].name} with Albedo {albedo}, texID: -1,";
                        info += $"Emission: {obj.material[i].emissionColor}, Emission Strength: {obj.material[i].emissionStrength}, Smoothness: {obj.material[i].smoothness}, Specular: {obj.material[i].specularColor}, Specular Probability: {obj.material[i].specularProbability}";
                        Debug.Log(info);
                    }
                    _faceMaterials.AddRange(Enumerable.Repeat(hasMaterial, mesh.GetIndices(i).Length / 3));
                }


                // // Bounding Box

                // Bounds bounds = new Bounds(_vertices[_indices[firstIndex]], Vector3.one * 0.01f);
                // int indexCount = mesh.GetIndices(i).Length;

                // for (int j = 0; j < indexCount; j += 3)
                // {
                //     int a = _indices[firstIndex + j];
                //     int b = _indices[firstIndex + j + 1];
                //     int c = _indices[firstIndex + j + 2];

                //     Vector3 posA = _vertices[a];
                //     Vector3 posB = _vertices[b];
                //     Vector3 posC = _vertices[c];
                //     bounds.Encapsulate(posA);
                //     bounds.Encapsulate(posB);
                //     bounds.Encapsulate(posC);
                // }

                // //Add the object itself
                // _meshObjects.Add(new MeshObject()
                // {
                //     localToWorldMatrix = obj.transform.localToWorldMatrix,
                //     indices_offset = firstIndex,
                //     indices_count = _indices.Count - firstIndex,
                //     _texID = hasTexture,
                //     boundsMin = bounds.min,
                //     boundsMax = bounds.max,

                //     material = new RayTracingMaterialShader()
                //     {
                //         albedo = albedo,
                //         emission = obj.material.emissionColor,
                //         emissionStrength = obj.material.emissionStrength,
                //         smoothness = obj.material.smoothness,
                //         specular = obj.material.specularColor,
                //         specularProbability = obj.material.specularProbability,
                //         flag = (int)obj.material.flag,
                //         texID = hasTexture
                //     }
                // });


            }

            // #region Testing

            // string vertexAttributes = "";
            // foreach (VertexAttributeDescriptor vad in mesh.GetVertexAttributes())
            // {
            //     vertexAttributes += vad.ToString() + ", ";
            // }

            // Debug.Log($"Object {obj.name} has {mesh.vertexAttributeCount} vertex attributes, which are:" + vertexAttributes);
            // Debug.Log($"Found {_textures.Length} textures");

            // #endregion

        }
        BVHConstructor.ConstructBVH();

        for (int i = 0; i < _textures.Length; i++)
        {

            Debug.Log($"Setting Texture {_textures[i].name} to Texture Array at index {i}");
            Graphics.CopyTexture(_textures[i], 0, 0, _textureArray, i, 0);

        }

        #region Compute_Buffer_Creation

        // CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        // CreateComputeBuffer(ref _indexBuffer, _indices, 4);
        // CreateComputeBuffer(ref _texCoordBuffer, _texCoords, 8);
        // CreateComputeBuffer(ref _normalBuffer, _normals, 12);
        CreateComputeBuffer(ref _TriangleBuffer, BVHConstructor.reorderedTriangles);
        CreateComputeBuffer(ref _MaterialBuffer, _materials);
        CreateComputeBuffer(ref _BVHNodeBuffer, BVHConstructor.GetNodes());


        #endregion

        Debug.Log($"Rebuilding Mesh Object Buffers with {_meshObjects.Count} objects, {_vertices.Count} vertices and {_indices.Count} indices");
        Debug.Log($"With {_texCoords.Count} texCoords and {_normals.Count} normals");


    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data)
    where T : struct
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            // Set data on the buffer
            buffer.SetData(data);
        }
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, T[] data)
    where T : struct
    {
        buffer = new ComputeBuffer(data.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(T)));
        buffer.SetData(data);
    }
    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        // Debug.Log($"Attempting to set Compute Buffer {name}");
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
            Debug.Log($"Setting Compute Buffer {name}");
        }
    }
}

