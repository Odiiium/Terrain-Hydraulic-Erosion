using Unity.AI.Navigation;
using UnityEngine;

public class SRDebug : MonoBehaviour
{
    [SerializeField] private Animation _animation;
    [SerializeField] private SkinnedMeshRenderer _renderder;
    [SerializeField] private  Mesh mesh;

    private Vector3[] vertices;

    void Start()
    {
        Application.targetFrameRate = 60;
        DebugVertices();
        Debug.LogError("Start updating");
        _animation.Play();

        Mesh mesh = new();
    }


    void Update()
    {
        if (mesh == null)
            return;

        DebugVertices();
    }

    private void DebugVertices()
    {
        _renderder.BakeMesh(mesh);
        vertices = mesh.vertices;

        Debug.LogError($"first : {vertices[0]}, second : {vertices[1]}");
    }
}
