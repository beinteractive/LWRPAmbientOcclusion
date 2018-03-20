using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

[Serializable]
[PostProcess(typeof(LWRPAORenderer), PostProcessEvent.BeforeTransparent, "Custom/LWRP Ambient Occlusion")]
public sealed class LWRPAO : PostProcessEffectSettings
{
}

public class LWRPAORenderer : PostProcessEffectRenderer<LWRPAO>
{
    private static readonly int ShaderIDsFogParams = Shader.PropertyToID("_FogParams");
    private static readonly int ShaderIDsAOColor = Shader.PropertyToID("_AOColor");
    private static readonly int ShaderIDsMSVOcclusionTexture = Shader.PropertyToID("_MSVOcclusionTexture");
    
    private enum Pass
    {
        DepthCopy,
        CompositionDeferred,
        CompositionForward,
        DebugOverlay
    }
    
    private MethodInfo CastRendererMethod;
    private MethodInfo PreparePropertySheetMethod;

    private PostProcessResources PostProcessResources;
    private PropertySheet PropertySheet;
    private RenderTexture AmbientOnlyAO;

    private void ResolveCastRendererMethod()
    {
        if (CastRendererMethod != null && PreparePropertySheetMethod != null)
        {
            return;
        }
        {
            var m = typeof(PostProcessBundle).GetMethod("CastRenderer", BindingFlags.Instance | BindingFlags.NonPublic);
            var method = m != null ? m.MakeGenericMethod(typeof(AmbientOcclusionRenderer)) : null;

            CastRendererMethod = method;
        }
        {
            var m = typeof(MultiScaleVO).GetMethod("PreparePropertySheet", BindingFlags.Instance | BindingFlags.NonPublic);

            PreparePropertySheetMethod = m;
        }
    }
    
    private void SetResources(PostProcessResources resources)
    {
        PostProcessResources = resources;
    }
    
    private void PreparePropertySheet(PostProcessRenderContext ctx, AmbientOcclusion settings)
    {
        var sheet = ctx.propertySheets.Get(PostProcessResources.shaders.multiScaleAO);
        sheet.ClearKeywords();
        sheet.properties.SetVector(ShaderIDsAOColor, Color.white - settings.color.value);
        PropertySheet = sheet;
    }

    private void CheckAOTexture(PostProcessRenderContext context)
    {
        if (AmbientOnlyAO == null || !AmbientOnlyAO.IsCreated() || AmbientOnlyAO.width != context.width || AmbientOnlyAO.height != context.height)
        {
            RuntimeUtilities.Destroy(AmbientOnlyAO);

            AmbientOnlyAO = new RenderTexture(context.width, context.height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
                enableRandomWrite = true
            };
            AmbientOnlyAO.Create();
        }
    }

    private void PushDebug(PostProcessRenderContext context)
    {
        if (context.IsDebugOverlayEnabled(DebugOverlay.AmbientOcclusion))
        {
            context.PushDebugOverlay(context.command, AmbientOnlyAO, PropertySheet, (int)Pass.DebugOverlay);
        }
    }
    
    public override void Render(PostProcessRenderContext ctx)
    {
        ResolveCastRendererMethod();
        
        var cam = ctx.camera;
        var cmd = ctx.command;
        var layer = cam.GetComponent<PostProcessLayer>();

        var aoBundle = layer.GetBundle<AmbientOcclusion>();
        var aoSettings = (AmbientOcclusion) aoBundle.settings;
        if (aoSettings.IsEnabledAndSupported(ctx) && CastRendererMethod != null)
        {
            var aoRenderer = (AmbientOcclusionRenderer) CastRendererMethod.Invoke(aoBundle, null);
            var aoMethod = aoRenderer.Get();
            if (aoMethod is MultiScaleVO)
            {
                var msvo = (MultiScaleVO) aoMethod;
                
                cmd.BeginSample("Ambient Occlusion");
                msvo.SetResources(ctx.resources);
                PreparePropertySheetMethod.Invoke(msvo, new object[] { ctx });
                
                SetResources(ctx.resources);
                PreparePropertySheet(ctx, aoSettings);
                CheckAOTexture(ctx);

                // In Forward mode, fog is applied at the object level in the grometry pass so we need
                // to apply it to AO as well or it'll drawn on top of the fog effect.
                if (ctx.camera.actualRenderingPath == RenderingPath.Forward && RenderSettings.fog)
                {
                    PropertySheet.EnableKeyword("APPLY_FORWARD_FOG");
                    PropertySheet.properties.SetVector(
                        ShaderIDsFogParams,
                        new Vector3(RenderSettings.fogDensity, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance)
                    );
                }

                msvo.GenerateAOMap(cmd, cam, AmbientOnlyAO, null, false);
                PushDebug(ctx);
                cmd.SetGlobalTexture(ShaderIDsMSVOcclusionTexture, AmbientOnlyAO);
                cmd.BlitFullscreenTriangle(ctx.source, ctx.destination);
                cmd.BlitFullscreenTriangle(BuiltinRenderTextureType.None, ctx.destination, PropertySheet, (int)Pass.CompositionForward);
                cmd.EndSample("Ambient Occlusion");
            }
        }
    }
}