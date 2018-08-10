using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PainterChannel
{
    public RenderTexture data;
    public int dimension;
    public bool blendable;
}

public class RenderTextureCache
{
    Dictionary<RenderTextureDescriptor, List<RenderTexture>> cache;

    public RenderTextureCache()
    {
        cache = new Dictionary<RenderTextureDescriptor, List<RenderTexture>>();
    }

    public bool TryGetCompatibleTexture(RenderTextureDescriptor descriptor, out RenderTexture renderTexture)
    {
        renderTexture = null;
        List<RenderTexture> list = null;
        RenderTextureDescriptor hackDescriptor = new RenderTextureDescriptor(descriptor.width, descriptor.height, descriptor.colorFormat);
        if (cache.TryGetValue(hackDescriptor, out list))
        {
            if (list.Count > 0)
            {
                renderTexture = list[0];
                list.RemoveAt(0);
            }
        }
        return (renderTexture != null);
    }

    public void Add(RenderTextureDescriptor descriptor, RenderTexture renderTexture)
    {
        List<RenderTexture> list = null;
		RenderTextureDescriptor hackDescriptor = new RenderTextureDescriptor(descriptor.width, descriptor.height, descriptor.colorFormat);
        if (!cache.TryGetValue(hackDescriptor, out list))
        {
            list = new List<RenderTexture>();
            cache.Add(hackDescriptor, list);
        }
        list.Add(renderTexture);
    }
};


public class PainterContext : IPainterContext
{
    public bool exporting = false;
    public bool IsExporting()
    {
        return exporting;
    }
    
    LayerBase currentPainterLayer;
    GenericLayer currentGenericLayer;
    Painter currentPainter;
    public Painter GetPainter()
    {
        return currentPainter;
    }

    Dictionary<string, PainterChannel> channels;
    RenderTextureCache discardedRenderTextures;
    List<RenderTexture> currentLayerTempRenderTextures;

    // TODO: Move me out of here
    Material m_LayerBlendingMaterial;

    public PainterContext(Painter painter)
    {
        channels = new Dictionary<string, PainterChannel>();
        discardedRenderTextures = new RenderTextureCache();
        currentLayerTempRenderTextures = new List<RenderTexture>();

        Shader blendingShader = Shader.Find("Hidden/Painter/LayerBlending");
        m_LayerBlendingMaterial = new Material(blendingShader);

        currentPainter = painter;
    }

    private void StartLayer(GenericLayer layer)
    {
        currentGenericLayer = layer;
        currentPainterLayer = layer.painterLayer;

        // TODO: check if currentLayerTempRenderTextures is empty
    }

    private void EndLayer()
    {
        // any remaining temp render textures are discarded (for re-use)
        foreach (RenderTexture rt in currentLayerTempRenderTextures)
        {
            discardedRenderTextures.Add(rt.descriptor, rt);
        }
        currentLayerTempRenderTextures.Clear();
        currentGenericLayer = null;
        currentPainterLayer = null;
    }

    public RenderTexture GetChannel(string channelName)
    {
        PainterChannel channel = null;
        if (channels.TryGetValue(channelName, out channel))
        {
            return channel.data;
        }
        return null;
    }

    public PainterChannel GetChannel(Painter.PaintableChannelType channelIndex)
    {
        if (channels.ContainsKey(channelIndex.ToString()))
            return channels[channelIndex.ToString()];

        Debug.LogError("Failed to find painter channel " + channelIndex);
        return null;
    }

    public bool TryGetChannelDescriptor(string channelName, out RenderTextureDescriptor descriptor)
    {
        RenderTexture renderTexture = GetChannel(channelName);
        if (renderTexture != null)
        {
            descriptor = renderTexture.descriptor;
            return true;
        }
        else
        {
            descriptor = new RenderTextureDescriptor();
            return false;
        }
    }

    public RenderTexture CreateRenderTexture(RenderTextureDescriptor descriptor)
    {
        RenderTexture result = null;
        if (!discardedRenderTextures.TryGetCompatibleTexture(descriptor, out result))
        {
            // create a new one
            result = new RenderTexture(descriptor) { name = "Painter RT"  };
        }
        currentLayerTempRenderTextures.Add(result);
        return result;
    }

    public RenderTexture CreateCompatibleRenderTexture(string channelName)
    {
        RenderTextureDescriptor descriptor;
        if (TryGetChannelDescriptor(channelName, out descriptor))
        {
            return CreateRenderTexture(descriptor);
        }
        else
        {
            return null;
        }
    }

    public void WriteChannel(string channelName, RenderTexture channelData, int channelDimension = 3)
    {
        PainterChannel targetChannel = null;
        if (!channels.TryGetValue(channelName, out targetChannel))
        {
            targetChannel = new PainterChannel();
            int dimension = 3;
            if(Painter.ChannelDimensions.TryGetValue(channelName, out dimension))
                targetChannel.dimension = dimension;
            else
                targetChannel.dimension = channelDimension;
            bool blendable;
            if (Painter.ChannelBlendable.TryGetValue(channelName, out blendable))
                targetChannel.blendable = blendable;
            else
                targetChannel.blendable = true;
            channels.Add(channelName, targetChannel);
        }

        // apply blend here, if necessary
        if ((currentPainterLayer != null) && currentPainterLayer.isBlendable && targetChannel.blendable)
        {
            // figure out if we have a mask or not
            RenderTexture maskRT = null;
            if (currentPainter.LayerHasMask(currentGenericLayer))
            {
                maskRT = GetChannel(Painter.Channels.Mask);
            }

            RenderTexture original = targetChannel.data;
            if (original == channelData)
            {
                Error("Blend-able layers must return a new RenderTarget, they cannot reuse the original");
            }
            else
            {
                RenderTexture dest = CreateRenderTexture(original.descriptor);

                // blend channelData on top of original
                m_LayerBlendingMaterial.SetTexture("_Overlay", channelData);
                m_LayerBlendingMaterial.SetFloat("_Opacity", Mathf.Clamp01(currentGenericLayer.opacity));
                if (maskRT)
                {
                    m_LayerBlendingMaterial.SetTexture("_Mask", maskRT);
                }
                else
                {
                    // Is this even cached, or does it build it on demand each time?
                    // This should really come from defaultTexture for the mask.
                    m_LayerBlendingMaterial.SetTexture("_Mask", Texture2D.whiteTexture);
                }
                Graphics.Blit(original, dest, m_LayerBlendingMaterial, (int)currentGenericLayer.blendMode);

                // discard original
                discardedRenderTextures.Add(original.descriptor, original);

                // set channel to new value
                targetChannel.data = dest;

                // no longer a temp target, remove from the temp target list
                currentLayerTempRenderTextures.Remove(dest);
            }
        }
        else
        {
            // if not the same render texture, discard the old render texture (for potential reuse later)
            if ((targetChannel.data != null) && (targetChannel.data != channelData))
            {
                discardedRenderTextures.Add(targetChannel.data.descriptor, targetChannel.data);
            }

            // assign the channel
            targetChannel.data = channelData;

            // new texture is no longer a temp target, so remove from the temp target list
            currentLayerTempRenderTextures.Remove(channelData);
        }
    }

    public bool IsLayerEnabled()
    {
        return currentGenericLayer.isEnabled;
    }

    public RenderTexture GetLayerMask()
    {
        return null;    // return currentGenericLayer.layerMask;   // TODO
    }

    public void Error(string description)
    {
        Debug.LogError("Painter ERROR: " + description);
    }

    public void DiscardAllChannels()
    {
        foreach (var kvp in channels)
        {
            discardedRenderTextures.Add(kvp.Value.data.descriptor, kvp.Value.data);
        }
        channels.Clear();
    }

    public void ResetChannelsToDefaults(IEnumerable<GenericChannel> channels)
    {
        DiscardAllChannels();

        // hack to work around issue with WriteChannel();
        currentPainterLayer = null;
        currentGenericLayer = null;

        foreach (GenericChannel channel in channels)
        {
            // initialize a render target for each channel
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(channel.format.width, channel.format.height, channel.format.format);
            RenderTexture channelTarget = CreateRenderTexture(descriptor);

            RenderTexture oldRT = UnityEngine.RenderTexture.active;
            if (channel.defaultTexture != null)
            {
                // initialize to default texture
                Graphics.Blit(channel.defaultTexture, channelTarget);
            }
            else
            {
                // initialize to default color
                UnityEngine.RenderTexture.active = channelTarget;
                GL.Clear(true, true, channel.defaultColor);
            }
            UnityEngine.RenderTexture.active = oldRT;

            // setup painter channel
            WriteChannel(channel.name, channelTarget);
        }
    }

    public void RebuildAllChannels(IEnumerable<GenericChannel> channels, IEnumerable<GenericLayer> layers)
    {
        // reset all of our channels to match the generic channel list
        ResetChannelsToDefaults(channels);

        // evaluate all enabled layers
        RenderTexture oldRT = UnityEngine.RenderTexture.active;
        foreach (GenericLayer layer in layers)
        {
            if (layer.isEnabled)
            {
                StartLayer(layer);
                layer.painterLayer.Eval(this);
                EndLayer();
            }
        }
        UnityEngine.RenderTexture.active = oldRT;
    }

    public Mesh GetMesh()
    {
        return currentPainter.sharedMesh;
    }

    public MaterialData GetMaterialData()
    {
        return currentPainter.MaterialData;
    }

    public SerializableRenderTexture CreateSerializedRenderTexture(
        string name, int width, int height, RenderTextureFormat format, RenderTextureReadWrite gammaMode)
    {
        SerializableRenderTexture srt = new SerializableRenderTexture(name, width, height, format, gammaMode);
        currentPainter.StoreObjectInAsset(srt.PrepareForSerialization(false));
        return srt;
    }
}

