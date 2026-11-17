using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Internal renderer used by <see cref="StackedOutlineText"/>. Draws the stacked
/// outline/shadow copies of the glyphs behind the text.
/// Created automatically - don't add it by hand.
///
/// Geometry goes through the normal Graphic pipeline (SetVerticesDirty -> OnPopulateMesh),
/// never straight into the CanvasRenderer, so it is safe no matter when the text changes.
/// </summary>
[AddComponentMenu("")]
[RequireComponent(typeof(CanvasRenderer))]
public class StackedOutlineLayer : MaskableGraphic
{
    Texture _atlas;
    List<UIVertex> _verts;
    List<int> _indices;

    public override Texture mainTexture => _atlas != null ? _atlas : s_WhiteTexture;

    public void SetAtlas(Texture atlas)
    {
        if (_atlas == atlas) return;
        _atlas = atlas;
        SetMaterialDirty();
    }

    public void SetGeometry(List<UIVertex> verts, List<int> indices)
    {
        _verts = verts;
        _indices = indices;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_verts != null && _indices != null && _verts.Count > 0)
            vh.AddUIVertexStream(_verts, _indices);
    }
}
