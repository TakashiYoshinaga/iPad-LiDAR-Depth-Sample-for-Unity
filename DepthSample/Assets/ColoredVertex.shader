Shader "Custom/ColoredVertex"
{
	Properties{
		point_size("Point Size", Float) = 5.0
	}
		SubShader{
		   Pass {
			  CGPROGRAM
			  #pragma vertex vert
			  #pragma fragment frag

			  #include "UnityCG.cginc"

			  struct appdata
			  {
				 float4 vertex : POSITION;
				 float4 color: COLOR;
			  };

			  struct v2f
			  {
				 float4 vertex : SV_POSITION;
				 float4 color : COLOR;
				 float size : PSIZE;
			  };

			  float4x4 depthCameraTUnityWorld;
			  float point_size;

			  v2f vert(appdata v)
			  {
				 v2f o;
				 o.vertex = UnityObjectToClipPos(v.vertex);
				 o.size = point_size;
				 o.color = v.color;
				 // Color should be based on pose relative info
				// o.color = mul(depthCameraTUnityWorld, v.vertex);
				 return o;
			  }

			  fixed4 frag(v2f i) : SV_Target
			  {
				 return i.color;
			  }
			  ENDCG
		   }
	}
}
