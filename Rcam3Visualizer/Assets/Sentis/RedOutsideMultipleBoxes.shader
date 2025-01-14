Shader "Custom/RedOutsideMultipleBoxes"
{
    Properties
    {
        _MainTex ("Render Texture", 2D) = "white" {}
        _BoxCount ("Number of Boxes", int) = 1
        _RED ("Redness", int) = 1
        // Arrays cannot be initialized in Properties. We define them directly in HLSL.
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            sampler2D _MainTex;
            int _BoxCount;
            int _RED;
            float4 _BoxMinMax[16]; // Declare array in HLSL (maximum 16 boxes)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            bool IsInsideBox(float2 uv, float4 box)
            {
                // Center of the box (ellipse center)
                float2 center = float2((box.x + box.z) * 0.5, (box.y + box.w) * 0.5);

                // Semi-major and semi-minor axes
                float2 radii = float2((box.z - box.x) * 0.5, (box.w - box.y) * 0.5);

                // Normalized distance from center to uv point
                float2 normalized = (uv - center) / radii;

                // Check if point lies inside the ellipse
                return dot(normalized, normalized) <= 1.0;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                for (int j = 0; j < _BoxCount; j++)
                {
                    if (IsInsideBox(uv, _BoxMinMax[j]))
                    {
                        return tex2D(_MainTex, uv);
                    }
                }
                return float4(_RED, 0, 0, 1);
                
            }
            ENDCG
        }
    }
}
