using System;
using UnityEngine;
public static class TextureExtensions
{
    public static RenderTexture ClearTexture(this RenderTexture texture)
    {
        Graphics.Blit(Texture2D.blackTexture, texture);
        return texture;
    }

    public static RenderTexture NewRenderTexture(Vector2Int size, RenderTextureFormat format, Action<RenderTexture> onTextureCreated = null)
    {
        RenderTexture rt = new RenderTexture(size.x, size.y, 0, format) { enableRandomWrite = true };
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();

        onTextureCreated?.Invoke(rt);

        return rt;
    }
}