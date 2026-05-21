Shader "Unlit/Lava_Built-In" {

    Properties {
        _Mask_ST ("Mask_ST", vector) = (0.2, 0.2, -0.1, 0.0)
        _Mask1_ST ("Mask1_ST", vector) = (0.1, 0.1, 0.01, 0.0)
        _Color_A ("Color_A", color) = (0.5396226, 0.1924314, 0.1293057)
        _Color_B ("Color_B", color) = (0.6830187, 0.3029822, 0.1069632)
        _Color_C ("Color_C", color) = (0.8641509, 0.5230879, 0.1548949)
        _ColorGradient ("ColorGradient", vector) = (0.0, 0.24, 0.47)
        _Gradient ("Gradient", vector) = (0.0, 0.44, 0.0, 0.0)
        _Height ("Height", float) = 0.1
        _Noise_0 ("Noise_0", 2D) = "white" {}
        _Noise_1 ("Noise_1", 2D) = "white" {}

    }

    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vertexFunc
            #pragma fragment fragmentFunc
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            uniform float4 _Mask_ST, _Mask1_ST;
            uniform float4 _Color_A, _Color_B, _Color_C;
            uniform float4 _ColorGradient, _Gradient;
            uniform float _Height;
            uniform Texture2D _Noise_0, _Noise_1;

            uniform SamplerState _BilinearRepeatSampler;

            struct Interpolators
            {
                float4 positionWS : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 positionCS : SV_POSITION;
            };

            Interpolators vertexFunc(float3 positionOS : POSITION) {
                Interpolators output;
                output.positionWS = mul(unity_ObjectToWorld, float4(positionOS, 1));

                float h = _Noise_0.SampleLevel(_BilinearRepeatSampler, output.positionWS.xz * _Mask_ST.xy + _Time.y * _Mask_ST.zw, 0);
                float h1 = _Noise_1.SampleLevel(_BilinearRepeatSampler, output.positionWS.xz * _Mask1_ST.xy + _Time.y * _Mask1_ST.zw, 0);

                output.positionWS.y += smoothstep(_Gradient.x, _Gradient.y, (h + h1) * 0.5) * _Height;

                output.positionCS = UnityObjectToClipPos(positionOS);
                UNITY_TRANSFER_FOG(output, output.positionCS);
                return output;
            }

            float4 fragmentFunc(Interpolators interpolators) : SV_TARGET
            {
                float mask = _Noise_0.Sample(_BilinearRepeatSampler, interpolators.positionWS.xz * _Mask_ST.xy + _Time.y * _Mask_ST.zw);
                float mask1 = _Noise_1.Sample(_BilinearRepeatSampler, interpolators.positionWS.xz * _Mask1_ST.xy + _Time.y * _Mask1_ST.zw);

                float gradient = (mask + mask1) * 0.5;

                float4 color = lerp(_Color_A, _Color_B, smoothstep(_ColorGradient.x, _ColorGradient.y, gradient));
                color = lerp(color, _Color_C, smoothstep(_ColorGradient.y, _ColorGradient.z, gradient));
                UNITY_APPLY_FOG(interpolators.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}
