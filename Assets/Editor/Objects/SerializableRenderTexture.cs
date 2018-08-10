using System;
//using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


[Serializable]
public class SerializableRenderTexture
{
    // serialized meta data describing the render texture
    [SerializeField]
    private int m_width;

    [SerializeField]
    private int m_height;

    [SerializeField]
    private RenderTextureFormat m_rtFormat;

    [SerializeField]
    private RenderTextureReadWrite m_gammaMode;

    // serialized texture -- always valid, but may not always be up to date
    [SerializeField]
    private Texture2D m_texture;

    // non-serialized render target -- not always valid (destroyed by serialization)
    // but if valid, always up to date
    private RenderTexture m_renderTexture;

    // our name is the texture name
    public string name
    {
        get { return m_texture.name; }
        set { m_texture.name = value; if (m_renderTexture != null) m_renderTexture.name = value;  }
    }

    public SerializableRenderTexture(string name, int width, int height, RenderTextureFormat rtFormat, RenderTextureReadWrite gammaMode)
    {
        this.m_width = width;
        this.m_height = height;
        this.m_rtFormat = rtFormat;
        this.m_gammaMode = gammaMode;
        m_renderTexture = new RenderTexture(width, height, 0, rtFormat, gammaMode);
        m_renderTexture.name = name;
        TextureFormat textureFormat = RenderTextureFormatToTextureFormat(rtFormat);
        bool mipChain = false;
        m_texture = new Texture2D(width, height, textureFormat, mipChain, m_renderTexture.sRGB);
        m_texture.name = name;
    }

    public RenderTexture GetRenderTexture()
    {
        if (m_renderTexture == null)
        {
            // create RT, and fill it from the texture
            m_renderTexture = new RenderTexture(m_width, m_height, 0, m_rtFormat, m_gammaMode);
            if ((m_renderTexture != null) && (m_texture != null))
            {
                m_renderTexture.name = m_texture.name;
                RenderTexture oldRT = RenderTexture.active;
                Graphics.Blit(m_texture, m_renderTexture);
                RenderTexture.active = oldRT;
            }
        }
        return m_renderTexture;
    }

    public Texture2D PrepareForSerialization(bool freeRenderTexture)
    {
        if (m_renderTexture != null)
        {
            // copy render texture back to texture
            RenderTexture oldRT = RenderTexture.active;
            RenderTexture.active = m_renderTexture;
            m_texture.ReadPixels(new Rect(0, 0, m_renderTexture.width, m_renderTexture.height), 0, 0);
            m_texture.Apply();                        // TODO: not sure if we actually need the apply... this uploads to GPU?
            EditorUtility.SetDirty(m_texture);        // marks this as dirty, so it will be serialized
            RenderTexture.active = oldRT;

            if (freeRenderTexture)
            {
                m_renderTexture.Release();
                m_renderTexture = null;
            }
        }

        return m_texture;
    }

    public void Destroy()
    {
        if (m_renderTexture != null)
        {
            m_renderTexture.Release();
            m_renderTexture = null;
        }
        if (m_texture != null)
        {
            UnityEngine.Object.DestroyImmediate(m_texture, true);
            m_texture = null;
        }
    }

    public int width { get { return m_width; } }
    public int height { get { return m_height; } }

    // TODO: can we use the new unified graphics formats for this instead?
    static public TextureFormat RenderTextureFormatToTextureFormat(RenderTextureFormat rtFormat)
    {
        switch (rtFormat)
        {
            case RenderTextureFormat.ARGB32: return TextureFormat.RGBA32;
            case RenderTextureFormat.Depth: return TextureFormat.RFloat;
            case RenderTextureFormat.ARGBHalf: return TextureFormat.RGBAHalf;
            case RenderTextureFormat.Shadowmap: return TextureFormat.RFloat;
            case RenderTextureFormat.RGB565: return TextureFormat.RGB565;
            case RenderTextureFormat.ARGB4444: return TextureFormat.ARGB4444;
            case RenderTextureFormat.ARGB1555: return TextureFormat.RGBA32;
            case RenderTextureFormat.Default: return TextureFormat.RGBA32;
            case RenderTextureFormat.ARGB2101010: return TextureFormat.RGBAHalf;
            case RenderTextureFormat.DefaultHDR: return TextureFormat.RGBAHalf;
            case RenderTextureFormat.ARGB64: return TextureFormat.RGBAHalf;      // :(
            case RenderTextureFormat.ARGBFloat: return TextureFormat.RGBAFloat;
            case RenderTextureFormat.RGFloat: return TextureFormat.RGFloat;
            case RenderTextureFormat.RGHalf: return TextureFormat.RGHalf;
            case RenderTextureFormat.RFloat: return TextureFormat.RFloat;
            case RenderTextureFormat.RHalf: return TextureFormat.RHalf;
            case RenderTextureFormat.R8: return TextureFormat.RGBA32;
            case RenderTextureFormat.ARGBInt: return TextureFormat.RGBAFloat;     // :(
            case RenderTextureFormat.RGInt: return TextureFormat.RGFloat;       // :(
            case RenderTextureFormat.RInt: return TextureFormat.RFloat;        // :(
            case RenderTextureFormat.BGRA32: return TextureFormat.BGRA32;
            case RenderTextureFormat.RGB111110Float: return TextureFormat.RGBAHalf;
            case RenderTextureFormat.RG32: return TextureFormat.RGHalf;        // :(
            case RenderTextureFormat.RGBAUShort: return TextureFormat.RGBAHalf;
            case RenderTextureFormat.RG16: return TextureFormat.RG16;
            default: return TextureFormat.RGBAHalf;
        }
    }
};

