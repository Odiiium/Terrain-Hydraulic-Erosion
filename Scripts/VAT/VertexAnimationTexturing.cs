using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
public class VertexAnimationTexturing : MonoBehaviour
{
    [SerializeField] private GameObject _gm;
    [SerializeField] private SkinnedMeshRenderer _renderer;
    [SerializeField] private Animation _animation;
    [SerializeField] private Mesh _mesh;

    private readonly string _folderPath = "Assets/VAT";

    [SerializeField] List<FrameData> _frames = new();

    private void Start()
    {
        CopyMesh();
        BakeTexture().Forget();
    }

    private void CopyMesh()
    {
        Mesh shared = _renderer.sharedMesh;

        _mesh = new Mesh();
        _mesh.vertices = shared.vertices;
        _mesh.normals = shared.normals;
        _mesh.uv = shared.uv;
        _mesh.tangents = shared.tangents;

        BakeMesh();
    }

    private void BakeMesh()
    {
        _renderer.BakeMesh(_mesh);
    }

    private async UniTask BakeTexture()
    {
        for (int i = 0; i < 10; i++)
        {
            await UniTask.DelayFrame(1);
            _frames.Add(PrepareFrame(i));

        }
    }

    private FrameData PrepareFrame(int id)
    { 
        _animation.clip.SampleAnimation(_gm, 0);
        _renderer.BakeMesh(_mesh);

        return new FrameData()
        {
            Positions = _mesh.vertices,
            Normals = _mesh.normals,
            FrameId = id
        };
    }

    [Serializable]
    private struct FrameData
    {
        public Vector3[] Positions;
        public Vector3[] Normals;
        public int FrameId;
    }
}
