using UnityEngine;
using RayTracerUtils;
using System.Linq;
using System.Collections.Generic;
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    [SerializeField] public RayTracingMaterial[] material;

    public void OnValidate()
    {

    }
    private void OnEnable()
    {
        RayTracingMaster.RegisterObject(this);
    }
    private void OnDisable()
    {


    }


}