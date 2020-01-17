using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public float SphereMaxRadius = 5;
    public float SphereMinRadius = 1;
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 1.0f;
    
    private RenderTexture _target;
    private Camera _camera;

    private ComputeBuffer _sphereBuffer;

    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingMeshs = new List<RayTracingObject>();
    private static List<RayTracingSphere> _rayTracingSpheres = new List<RayTracingSphere>();
    private static Dictionary<RayTracingObject, MeshObject> _rayTracingObjectToMesh = new Dictionary<RayTracingObject, MeshObject>();
    private static Dictionary<RayTracingSphere, Sphere> _rayTracingObjectToSphere = new Dictionary<RayTracingSphere, Sphere>();

    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;
    
    public void OnEnable()
    {
        SetUpScene();
    }

    public void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();

        if (_meshObjectBuffer != null)
            _meshObjectBuffer.Release();

        if (_vertexBuffer != null)
            _vertexBuffer.Release();

        if (_indexBuffer != null)
            _indexBuffer.Release();
    }

    private void SetUpScene()
    {
        Random.InitState(3);

        for (int i = 0; i < 1; i++)
        {
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            CreateRandomRayTracingSphere().transform.position = new Vector3(randomPos.x, 10, randomPos.y);
        }
    }

    private GameObject CreateRandomRayTracingCube()
    {
        Color color = Random.ColorHSV();
        bool metal = Random.value < 0.5f;
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.AddComponent<RayTracingCube>();
        cube.AddComponent<Rigidbody>();
        cube.GetComponent<RayTracingCube>().Albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
        cube.GetComponent<RayTracingCube>().Specular = new Vector3(color.a, color.g, color.b);

        return cube;
    }

    private GameObject CreateRandomRayTracingSphere()
    {
        Color color = Random.ColorHSV();
        bool metal = Random.value < 0.5f;
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.localScale = new Vector3(5, 5, 5);
        sphere.AddComponent<RayTracingSphere>();
        sphere.AddComponent<Rigidbody>();
        sphere.GetComponent<RayTracingSphere>().Radius = sphere.transform.localScale.x/2f;
        sphere.GetComponent<RayTracingSphere>().Albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
        sphere.GetComponent<RayTracingSphere>().Specular = new Vector3(color.a, color.g, color.b);

        return sphere;
    }

    public void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetFloat("_Seed", Random.value);

        RayTracingShader.SetTexture(0,"_SkyboxTexture", SkyboxTexture);

        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        SetComputeBuffer("_Spheres", _sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);

        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();
        RepositionMeshObjects();
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        InitRenderTexture();

        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit(_target, destination);
    }

    private void InitRenderTexture()
    {
        if(_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            if(_target != null)
            {
                _target.Release();
            }

            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }

    public static void RegisterObject(RayTracingObject obj)
    {
        if (!(obj is RayTracingSphere))
        {
            _rayTracingMeshs.Add(obj);
            _meshObjectsNeedRebuilding = true;
        }
        else
        {
            _rayTracingSpheres.Add((RayTracingSphere)obj);
            Sphere sphere = new Sphere();
            _rayTracingObjectToSphere.Add((RayTracingSphere)obj, sphere);
        }
    }
    public static void UnregisterObject(RayTracingObject obj)
    {
        if (!(obj is RayTracingSphere))
        {
            _rayTracingMeshs.Remove(obj);
            _meshObjectsNeedRebuilding = true;
        }
        else
        {
            _rayTracingSpheres.Remove((RayTracingSphere)obj);
            _rayTracingObjectToSphere.Remove((RayTracingSphere)obj);
        }
    }
    
    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    };

    private bool SpheresColliding(Sphere sphere1, Sphere sphere2)
    {
        float minDist = sphere1.radius + sphere2.radius;
        return (Vector3.SqrMagnitude(sphere1.position - sphere2.position) < minDist * minDist);
    }

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
        public Vector3 albedo;
        public Vector3 specular;
    }
    private static int SizeOfMeshObject = 96;

    private void RepositionMeshObjects()
    {
        foreach (RayTracingObject obj in _rayTracingMeshs)
        {
            MeshObject meshObject = _rayTracingObjectToMesh[obj];
            meshObject.localToWorldMatrix = obj.transform.localToWorldMatrix;
            _rayTracingObjectToMesh[obj] = meshObject;
        }
        CreateComputeBuffer(ref _meshObjectBuffer, _rayTracingObjectToMesh.Values.ToList(), SizeOfMeshObject);

        foreach(RayTracingSphere obj in _rayTracingSpheres)
        {
            Sphere sphere = _rayTracingObjectToSphere[obj];
            sphere.position = obj.transform.position;
            sphere.radius = obj.Radius;
            sphere.albedo = obj.Albedo;
            sphere.specular = obj.Specular;
            _rayTracingObjectToSphere[obj] = sphere;
        }
        CreateComputeBuffer(ref _sphereBuffer, _rayTracingObjectToSphere.Values.ToList(), 40);

    }

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }
        _meshObjectsNeedRebuilding = false;
        // Clear all lists
        _rayTracingObjectToMesh.Clear();
        _vertices.Clear();
        _indices.Clear();
        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingMeshs)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);
            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));
            // Add the object itself
            _rayTracingObjectToMesh.Add(obj, new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length,
                albedo = obj.Albedo,
                specular = obj.Specular
            });
        }

        CreateComputeBuffer(ref _meshObjectBuffer, _rayTracingObjectToMesh.Values.ToList(), SizeOfMeshObject);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
    where T : struct
    {
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
    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }
}
