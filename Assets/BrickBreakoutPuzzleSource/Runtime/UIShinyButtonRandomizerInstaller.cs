using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>Automatically adds per-Graphic shine phases to shiny UI elements under this Canvas.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class UIShinyButtonRandomizerInstaller : MonoBehaviour
{
    private static readonly int ShineOffsetId = Shader.PropertyToID("_ShineOffset");
    private static readonly int ShineTimeId = Shader.PropertyToID("_ShineTime");
    private readonly Dictionary<Graphic, Material> sourceMaterials = new Dictionary<Graphic, Material>();
    private readonly Dictionary<Graphic, Material> randomizedMaterials = new Dictionary<Graphic, Material>();

    private void Start()
    {
        RefreshRandomizers();
    }

    private void OnTransformChildrenChanged()
    {
        if (isActiveAndEnabled)
            RefreshRandomizers();
    }

    private void Update()
    {
        Shader.SetGlobalFloat(ShineTimeId, Time.unscaledTime);
    }

    /// <summary>Call again after creating shiny UI elements at runtime.</summary>
    public void RefreshRandomizers()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic == null) continue;

            if (randomizedMaterials.TryGetValue(graphic, out Material currentInstance))
            {
                if (graphic.material == currentInstance) continue;
                ReleaseMaterial(graphic, false);
            }

            Material source = graphic.material;
            if (source == null || !source.HasProperty(ShineOffsetId)) continue;

            Material instance = new Material(source)
            {
                name = source.name + " (Shine Phase)",
                hideFlags = HideFlags.HideAndDontSave
            };
            instance.SetFloat(ShineOffsetId, Random.Range(0f, 1f));
            sourceMaterials[graphic] = source;
            randomizedMaterials[graphic] = instance;
            graphic.material = instance;
        }
    }

    private void OnDestroy()
    {
        var graphics = new List<Graphic>(randomizedMaterials.Keys);
        foreach (Graphic graphic in graphics)
            ReleaseMaterial(graphic, true);
    }

    private void ReleaseMaterial(Graphic graphic, bool restoreSource)
    {
        if (!randomizedMaterials.TryGetValue(graphic, out Material instance)) return;

        if (restoreSource && graphic != null && graphic.material == instance && sourceMaterials.TryGetValue(graphic, out Material source))
            graphic.material = source;

        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);

        randomizedMaterials.Remove(graphic);
        sourceMaterials.Remove(graphic);
    }
}
