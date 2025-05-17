Shader "Simple/URPUnlitHologram"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0,1,1,0.5) // Cyan, semi-transparent

        [Header(Pulse Animation)]
        _MinBrightness ("Min Brightness", Range(0,1)) = 0.3
        _BrightnessPulseSpeed ("Brightness Pulse Speed", Float) = 1.0

        [Header(Scanlines Distortion)]
        _ScanlineFrequency ("Scanlines (num across screen)", Float) = 100.0
        _ScanlineScrollSpeed ("Scanline Scroll Speed", Float) = 10.0
        _ScanlineHardness ("Scanline Hardness", Range(1, 10)) = 3.0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.3
        _DistortionFrequency ("Distortion Waves (num across screen X)", Float) = 5.0
        _DistortionWaveSpeed ("Distortion Wave Speed", Float) = 2.0
        _DistortionAmount ("Distortion Amount (fraction of screen Y)", Range(0,0.2)) = 0.05

        [Header(Fresnel Effect)]
        _FresnelColor ("Fresnel Color", Color) = (1,1,1,0.8)
        _FresnelPower ("Fresnel Power", Range(0.1, 20.0)) = 4.0
        _FresnelIntensity ("Fresnel Intensity", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Transparent" // Holograms are typically transparent
            "Queue"="Transparent"      // Render after opaque objects
        }

        Pass
        {
            Name "UnlitHologramForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha // Alpha blending
            ZWrite Off                     // Don't write to depth buffer

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP Core Library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Material Properties
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _MinBrightness;
                float _BrightnessPulseSpeed;
                float _ScanlineFrequency;
                float _ScanlineScrollSpeed;
                half _ScanlineHardness;
                half _ScanlineIntensity;
                float _DistortionFrequency;
                float _DistortionWaveSpeed;
                float _DistortionAmount;
                half4 _FresnelColor;
                half _FresnelPower;
                half _FresnelIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0; // World space normal
                float3 viewDirWS    : TEXCOORD1; // World space view direction (camera to vertex)
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);
                
                OUT.normalWS = normalInputs.normalWS;
                OUT.viewDirWS = GetCameraPositionWS() - positionInputs.positionWS; // Vector from surface to camera
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Global Brightness Pulse
                half globalBrightnessPulse = (sin(_Time.y * _BrightnessPulseSpeed) + 1.0h) * 0.5h; // 0..1
                globalBrightnessPulse = lerp(_MinBrightness, 1.0h, globalBrightnessPulse); // Remap to MinBrightness..1

                // Screen-space coordinates (normalized 0-1)
                // IN.positionCS.xy are screen pixel coordinates from SV_Position
                float yNorm = IN.positionCS.y / _ScreenParams.y;
                float xNorm = IN.positionCS.x / _ScreenParams.x;

                // Scanlines & Distortion
                // Distortion wave moves horizontally, offsets Y coordinate for scanlines
                float distortionOffset = sin(xNorm * _DistortionFrequency + _Time.y * _DistortionWaveSpeed) * _DistortionAmount;
                float distortedYNorm = yNorm + distortionOffset;

                // Scanline calculation
                float scanlineRaw = sin(distortedYNorm * _ScanlineFrequency - _Time.y * _ScanlineScrollSpeed);
                scanlineRaw = (scanlineRaw + 1.0h) * 0.5h; // Remap to 0..1
                scanlineRaw = pow(scanlineRaw, _ScanlineHardness); // Sharpen lines

                // scanlineModulation: 1.0 means no effect, (1.0 - _ScanlineIntensity) means full effect
                half scanlineModulation = lerp(1.0h - _ScanlineIntensity, 1.0h, scanlineRaw);

                // Base color & alpha modulated by pulse and scanlines
                half3 baseEffectColor = _BaseColor.rgb * globalBrightnessPulse * scanlineModulation;
                half baseEffectAlpha = _BaseColor.a * scanlineModulation;

                // Fresnel Effect
                half3 normalWS = normalize(IN.normalWS);
                half3 viewDirWS = normalize(IN.viewDirWS);
                // fresnelDot: 0 if view is parallel to surface, 1 if perpendicular
                half fresnelDot = dot(normalWS, viewDirWS); 
                // fresnelTerm: 0 at center (view perpendicular), 1 at rim (view parallel)
                half fresnelTerm = pow(1.0h - saturate(fresnelDot), _FresnelPower);
                fresnelTerm *= _FresnelIntensity; // Apply overall fresnel strength

                // Combine base effect with Fresnel
                // Color: lerp from base to fresnel color (also pulsed for consistency)
                half3 fresnelModulatedColor = _FresnelColor.rgb * globalBrightnessPulse;
                half3 finalColor = lerp(baseEffectColor, fresnelModulatedColor, fresnelTerm);
                
                // Alpha: lerp from base alpha to fresnel alpha
                half finalAlpha = lerp(baseEffectAlpha, _FresnelColor.a, fresnelTerm);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}