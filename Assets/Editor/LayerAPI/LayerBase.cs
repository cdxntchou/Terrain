using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.UIElements;


// attribute used by layer classes to populate UI
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class LayerDescription : Attribute
{
    public string LayerName = "No Layer Defined";
    public bool CanBeRoot = true;
    public bool Hide = false;
}


[LayerDescription(CanBeRoot = true, LayerName = "Layer Base Class (You Shouldn't See This)")]
abstract public class LayerBase : ScriptableObject
{
    // return true if this layer wants the standard blending options (opacity, mask, blend mode, etc.)
    // when true, the layer must write to channels using new render targets (so we can blend them with the originals)
    // when false, any written channels just replace the existing render target, so you can re-use the existing render targets if you want
    public abstract bool isBlendable { get; }

    public virtual bool isMask { get { return false; } }        // hax :)

    public static string GetLayerTypeName(LayerBase layer)
    {
        var type = layer.GetType();
        return GetLayerTypeName(type);
    }
    public static string GetLayerTypeName(Type layerType)
    {
        LayerDescription layerData = (LayerDescription) Attribute.GetCustomAttribute(layerType, typeof(LayerDescription));
        return layerData.LayerName;
    }

    public static bool IsHidden(Type layerType)
    {
        LayerDescription layerData = (LayerDescription) Attribute.GetCustomAttribute(layerType, typeof(LayerDescription));
        return layerData.Hide;
    }

    // when a new layer is created by the system, it will call this function to initialize it
    // put any initial state setup here (DON'T USE A CONSTRUCTOR)
    public virtual void InitializeOnCreation() { }

    // evaluate this layer
    public abstract void Eval(IPainterContext context);

    // this is called when we want to build the layer panel UI for this layer
    public virtual void InitializeEditorUI(Painter painter, VisualContainer root) { }

    public virtual void DoClear(Painter painter) { }

    public virtual void OnMouseDown(Vector2 mousePos, Painter painter, ViewportType viewportType, Camera camera, Collider collider, Rect bounds) { }
    public virtual void OnMouseUp(Vector2 mousePos, Painter painter) { }
    public virtual void OnMouseMove(Vector2 mousePos, Painter painter, ViewportType viewportType, Camera camera, Collider collider, Rect bounds) { }
    public virtual void OnMouseLeave() { }

    public virtual IEnumerable<SerializableRenderTexture> GetSerializableRenderTextures() { return null; }
}





