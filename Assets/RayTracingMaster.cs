﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;
    private RenderTexture _target;
    private RenderTexture _converged; 
    private Camera _camera;

    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public float SphereMaxRadius = 5;
    public float SphereMinRadius = 1;
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 1.0f;
    private ComputeBuffer _sphereBuffer;

    public void OnEnable()
    {
        SetUpScene();
    }

    public void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }

    private void SetUpScene()
    {
        Random.InitState(2);
        List<Sphere> spheres = new List<Sphere>();

        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();

            sphere.radius = Random.Range(SphereMinRadius, SphereMaxRadius);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            foreach (Sphere other in spheres)
            {
                while(SpheresColliding(sphere, other) && sphere.radius > SphereMinRadius)
                {
                    sphere.radius -= .1f;
                    sphere.position.y = sphere.radius;
                }
                if(SpheresColliding(sphere, other))
                   goto SkipSphere;
            }

            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            spheres.Add(sphere);

        SkipSphere:
            continue;
        }

        // Assign to compute buffer
        _sphereBuffer = new ComputeBuffer(spheres.Count, 40);
        _sphereBuffer.SetData(spheres);
    }

    public void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetTexture(0,"_SkyboxTexture", SkyboxTexture);

        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);

        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
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

        Graphics.Blit(_target, _converged);
        Graphics.Blit(_converged, destination);
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

        if (_converged == null || _converged.width != Screen.width || _converged.height != Screen.height)
        {
            if (_converged != null)
            {
                _converged.Release();
            }

            _converged = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();
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
}
