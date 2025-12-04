using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using EButton = ButtonEditor.ButtonAttribute;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Mesh")]
    [SerializeField] private Material _material;

    [Header("Chunk settings")]
    [SerializeField] private bool _ignoreHeight;
    [SerializeField] private bool _isTerrainCentralized;
    [SerializeField] private int _chunkCountX, _chunkCountY;
    [SerializeField] private float _chunkSize;
    [SerializeField] private IndexFormat _format;

    [DynamicRange(nameof(_format), nameof(IndexFormat.UInt16), byte.MaxValue + 1, nameof(IndexFormat.UInt32), ushort.MaxValue + 1)]
    [SerializeField] private int _lineVerticesCount;

    [Header("Noise generation")]
    [SerializeField][Range(.01f, 10)] private float _amplitude;
    [SerializeField][Range(.001f, 5)] private float _scaleValue;
    [SerializeField][Range(1, 10)] private int _octavesCount;
    [SerializeField] private float _persistance;

    [Header("Data")]
    [SerializeField] private List<NoiseOctave.FunctionType> noiseFunctions;
    [ShowInInspector] private List<NoiseOctave> _noiseOctaves;
    [ShowInInspector] private List<Chunk> _chunks;
    [ShowInInspector] private List<GameObject> _planes;

    private NoiseGenerator _noiseGenerator;

    public MeshFilter MeshFilter { get => _filter ??= GetComponent<MeshFilter>(); }
    private MeshFilter _filter;
    public MeshRenderer Renderer { get => _renderer ??= GetComponent<MeshRenderer>(); }
    private MeshRenderer _renderer;

    public static Mesh CurrentMesh { get; private set; }

    public event Action<Texture2D> OnHeightMapGenerated;

    [EButton(nameof(Regenerate))]
    public void Regenerate()
    {
        _chunks.ForEach(x => Destroy(x.MeshFilter.gameObject));
        _chunks.Clear();
        GenerateTerrain();
    }

    [EButton(nameof(GenerateTerrain))]
    public void GenerateTerrain()
    {
        _chunks ??= new List<Chunk>();
        _noiseGenerator = new NoiseGenerator(_amplitude, _scaleValue, _persistance);
        _noiseOctaves = _noiseGenerator.GenerateRandomOctaves(_octavesCount, noiseFunctions).ToList();

        for (int y = 0; y < _chunkCountY; y++)
        {
            for (int x = 0; x < _chunkCountX; x++)
            {
                Chunk chunk = GenerateChunk(x + y * _chunkCountX, x, y);
                _chunks.Add(chunk);
            }
        }
    }

    [EButton("Generate height map")]
    public Texture2D GenerateHeightMap()
    {
        float[,] heights = GenerateHeightsMatrice();
        Texture2D texture = CreateHeightTexture(heights);
        SaveTextureAsPng(texture, "Assets/Generated/texture.png");

        OnHeightMapGenerated?.Invoke(texture);

        return texture;
    }


    private Chunk GenerateChunk(int id, int x, int y)
    {
        GameObject chunkObject = new GameObject();
        chunkObject.name = $"Chunk_{id}";

        Chunk chunk = new Chunk()
        {
            Id = id,
            Object = chunkObject,
            MeshFilter = chunkObject.AddComponent<MeshFilter>(),
            Renderer = chunkObject.AddComponent<MeshRenderer>()
        };

        chunkObject.transform.parent = this.transform;

        Vector3[] chunkVertices = GetChunkVertices(x, y, _format);
        int[] triangles = GetTriangles();
        Vector2[] uvs = GetUvs(x,y);
        FillChunkMesh(ref chunk, chunkVertices, triangles, uvs, _material, _format);

        return chunk;
    }

    private Vector2[] GetUvs(int x, int y)
    {
        Vector2[] uvs = new Vector2[_lineVerticesCount * _lineVerticesCount];

        float vertexDistance = _chunkSize / (_lineVerticesCount - 1);

        Vector2 absoluteSize = GetAbsoluteTerrainSize();

        int idx = 0;

        Vector2 terrainSize = GetVertexTerrainSize();

        for (int i = 0; i < _lineVerticesCount; i++)
        {
            for (int j = 0; j < _lineVerticesCount; j++)
            {
                float worldX = x * _chunkSize + j * vertexDistance;
                float worldZ = y * _chunkSize + i * vertexDistance;

                uvs[idx] = new Vector2(worldX / absoluteSize.x, worldZ / absoluteSize.y);
                idx++;
            }
        }

        return uvs;
    }

    private Vector3[] GetChunkVertices(int chunkX, int chunkY, IndexFormat format)
    {
        int verticesTotal = (_lineVerticesCount - 1) * (_lineVerticesCount - 1);

        if ((format == IndexFormat.UInt16 && verticesTotal > ushort.MaxValue) || (format == IndexFormat.UInt32 && verticesTotal > int.MaxValue))
            throw new ArgumentOutOfRangeException($"Mesh with vertices count = {_lineVerticesCount * _lineVerticesCount} is greater than limit and cannot be created");

        Vector3[] vertices = new Vector3[_lineVerticesCount * _lineVerticesCount];

        float vertexDistance = _chunkSize / (_lineVerticesCount - 1);
        float xPos, yPos, height;

        Vector2 posVector = Vector2.zero;
        Vector3 centerOffset = new Vector3(_chunkCountX, 0, _chunkCountY) * _chunkSize * -.5f;

        for (int i = 0, v = 0; i < _lineVerticesCount; i++)
            for (int j = 0; j < _lineVerticesCount; j++)
            {
                xPos = chunkX * _chunkSize + j * vertexDistance;
                yPos = chunkY * _chunkSize + i * vertexDistance;
                posVector.x = xPos;
                posVector.y = yPos;

                height = _noiseGenerator.CalculateNoiseByOctaves(posVector, _noiseOctaves);

                vertices[v] = new Vector3(xPos, height, yPos);

                if (_isTerrainCentralized)
                    vertices[v] += centerOffset;

                v++;
            }
        return vertices;
    }

    private int[] GetTriangles()
    {
        int edgesInRow = _lineVerticesCount - 1;
        int[] triangles = new int[edgesInRow * edgesInRow * 6];
        int tris = 0;
        int vert = 0;

        for (int j = 0; j < edgesInRow; j++)
        {
            for (int i = 0; i < edgesInRow; i++)
            {
                triangles[tris] = vert;
                triangles[tris + 1] = vert + edgesInRow + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + edgesInRow + 1;
                triangles[tris + 5] = vert + edgesInRow + 2;
                vert++;
                tris += 6;
            }
            vert++;
        }

        return triangles;
    }

    private void FillChunkMesh(ref Chunk chunk, Vector3[] vertices, int[] triangles, Vector2[] uvs, Material material, IndexFormat format = IndexFormat.UInt16)
    {
        chunk.Renderer.material = material;

        Mesh mesh = new()
        {
            indexFormat = format,
            vertices = vertices,
            triangles = triangles,
            uv = uvs
        };

        chunk.MeshFilter.mesh = mesh;

        chunk.MeshFilter.mesh.RecalculateNormals();

        if (_ignoreHeight)
            chunk.MeshFilter.mesh.vertices = vertices.Select(x => x.WithY(0)).ToArray();

        chunk.MeshFilter.mesh.RecalculateBounds();
    }

    public float[,] GenerateHeightsMatrice()
    {
        Vector2Int terrainSize = GetVertexTerrainSize();

        float[,] heightMap = new float[terrainSize.x, terrainSize.y];

        float vertexDistance = _chunkSize / (_lineVerticesCount - 1);

        for (int y = 0; y < terrainSize.y; y++)
            for (int x = 0; x < terrainSize.x; x++)
            {
                Vector2 pos = new Vector2(x, y) * vertexDistance;
                float height = _noiseGenerator.CalculateNoiseByOctaves(pos, _noiseOctaves);
                heightMap[x, y] = height;
            }

        return heightMap;
    }

    public Texture2D CreateHeightTexture(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        float maximum = heightMap.Max();
        float minimum = heightMap.Min();

        Color[] colors = new Color[width * height];

        for (int iter = 0, y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float pixelHeight = heightMap[x, y];
                float value = Mathf.InverseLerp(minimum, maximum, pixelHeight);
                Color color = new Color(value, 0, 0, 1);
                colors[iter++] = color;
            }

        Texture2D texture = new(width, height);

        texture.SetPixels(colors);
        texture.Apply();

        return texture;
    }

    private void SaveTextureAsPng(Texture2D texture, string path)
    {
        byte[] png = texture.EncodeToPNG();

        if (!System.IO.File.Exists(path))
            System.IO.File.Create(path);

        System.IO.File.WriteAllBytes(path, png);

        AssetDatabase.Refresh();
    }

    public List<Chunk> GetChunks() => _chunks;

    public Vector2 GetAbsoluteTerrainSize() => new Vector2(_chunkSize * _chunkCountX, _chunkSize * _chunkCountY);

    public Vector2Int GetVertexTerrainSize() => new Vector2Int(_chunkCountX, _chunkCountY) * _lineVerticesCount + new Vector2Int(_chunkCountX > 1 ? -1 : 0, _chunkCountY > 1 ? -1 : 0);

    public float GetAmplitude() => _amplitude;
}

[System.Serializable]
public struct Chunk
{
    public int Id;
    public GameObject Object;
    public MeshRenderer Renderer;
    public MeshFilter MeshFilter;
}