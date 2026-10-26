// 월드 지도(UI/WorldMapView) — 칸마다 구워 둔 지도 텍스처와 땅마다의 표(주인 · 침략 가능 · 이벤트 상태)를 읽어 지도를 그린다(사용자 10/27 「셰이더로」).
// 그림은 옛 CPU 칠하기(10/26 승인 그림)를 칸 단위로 그대로 옮긴 것 — 색은 0~255 자리에서 다루고, 밝기 곱 · 섞기는 CPU가 같은 C# 식으로 미리 계산한 밝기 표(_Lut)를 읽는다.
// _MainTex(칸) : R = 땅 번호 + 1(0 = 바다) · G = 오른쪽 칸의 다른 땅 + 1 · B = 아래 칸의 다른 땅 + 1 · A = 비트(0~2 지도 안 바다까지 거리 · 3 땅 가장자리 2칸 안 · 4 장식 · 5~6 기복도 톤)
// _NearIds · _NearDist : 이 칸에서 4칸(체비쇼프) 안의 다른 땅 넷(가까운 순) + 1 · 그 땅까지 거리 — 주인에 따라 달라지는 선(내 땅 테두리 · 번짐 · 그림자 · 영주 색 띠 · 고른 땅 테두리)을 표의 주인과 견준다
// _Lands(땅 수 × 2) : 0줄 = 주인 색 RGB · 주인 + 1 / 1줄 = 지형색 RGB · 이벤트 상태 + 침략 가능 × 8
// _Lut(256 × 줄) : 값을 줄마다의 밝기로 바꾼 값(ROW_) · _Arrows : 침략 화살표(턴마다 CPU가 그린다, 알파 0 = 없음) · _Ramp : 0~255 → 화면 색(sRGB 텍스처 256×1)
// 마스크 · 스텐실 속성은 UI/Default 그대로
Shader "GnorpWar/WorldMap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cells", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _NearIds, _NearDist, _Arrows, _Lands, _Lut, _Ramp;
            float4 _MainTex_TexelSize, _Lands_TexelSize, _Lut_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            // 내 영주 번호 · 가리킨 땅 · 고른 땅(없으면 -1) · 침략할 수 있는 땅에 빗금
            float _Mine, _Hovered, _Selected, _Hatch;
            // 물결 빠르기 갈래(0~4)마다 지금 흘러간 칸 — CPU가 0.1초마다 넣는다
            float4 _WaveShift;
            float _WaveShift4;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = v.texcoord;
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                OUT.color = v.color * _Color;
                return OUT;
            }

            // 밝기 표의 줄(WorldMapView.Bake와 같은 순서) — 채움(남의 땅 · 가리킴/침략 가능/열린 이벤트 땅 = 1 · 가리킨 침략 가능 땅 · 내 땅) · 그 채움의 같은 영주 땅 사이 선(+4) ·
            // 영주 색 띠 · 이벤트 빗금(보통 · 가리킴) · 이벤트 땅 밝기(닫힘 · 깜박임 · 방문 가능 가리킴 · 방문 가능 · 열림 가리킴) · 내 땅 번짐 · 장식(밝게 · 어둡게)
            #define ROW_FILL 0
            #define ROW_ONE 1
            #define ROW_OPEN_HOVER 2
            #define ROW_MINE 3
            #define ROW_INNER 4
            #define ROW_BAND 8
            #define ROW_EVENT_HATCH 9
            #define ROW_EVENT_HATCH_HOVER 10
            #define ROW_EVENT_CLOSED 11
            #define ROW_EVENT_LIT 12
            #define ROW_EVENT_VISITABLE_HOVER 13
            #define ROW_EVENT_VISITABLE 14
            #define ROW_EVENT_HOVER 15
            #define ROW_GLOW 16
            #define ROW_DECOR_UP 17
            #define ROW_DECOR_DOWN 18
            // 두께(칸) — 내 땅 흰 테두리 · 번짐 · 그림자 · 영주 색 띠(사용자 10/26 「더 두껍게」 4칸) · 해안선(국경선보다 한 단계 굵게) · 빗금 간격
            #define MINE_BORDER 2
            #define MINE_GLOW 4
            #define MINE_SHADOW 1
            #define LORD_BAND 4
            #define COAST_WIDTH 2
            #define HATCH_STEP 5
            // 이벤트 땅 상태 — 없음 · 열림 · 방문 가능 · 닫힘 · 깜박임
            #define EVENT_OPEN 1
            #define EVENT_VISITABLE 2
            #define EVENT_CLOSED 3
            #define EVENT_LIT 4

            static const float3 EdgeColor = float3(16, 16, 16);
            static const float3 SelectedColor = float3(255, 217, 90);
            // 바다(사용자 10/25 남청 「1안」) — 바탕 · 물결 줄
            static const float3 SeaFill = float3(31, 78, 140), SeaWave = float3(58, 110, 178);
            // 물결 — 이 칸 간격의 줄마다 짧은 가로 줄(줄마다 어긋나게) — 줄 간격 · 한 줄의 반복 · 줄 길이(칸)
            #define WAVE_ROW 6
            #define WAVE_SPAN 14
            #define WAVE_LENGTH 3

            float Byte(float v) { return round(v * 255.0); }
            float3 Bytes(float3 v) { return round(v * 255.0); }
            float4 Bytes(float4 v) { return round(v * 255.0); }
            float Mod(float a, float b) { return a - b * floor(a / b); }

            // 값마다 밝기 표의 그 줄을 읽는다
            float3 Lut(float3 c, float row)
            {
                float v = (row + 0.5) * _Lut_TexelSize.y;
                float3 u = (c + 0.5) / 256.0;
                return float3(Byte(tex2Dlod(_Lut, float4(u.r, v, 0, 0)).r), Byte(tex2Dlod(_Lut, float4(u.g, v, 0, 0)).r), Byte(tex2Dlod(_Lut, float4(u.b, v, 0, 0)).r));
            }

            float4 Land(float region, float row)
            {
                return tex2Dlod(_Lands, float4((region + 0.5) * _Lands_TexelSize.x, (row + 0.5) * 0.5, 0, 0));
            }

            // 그 땅의 주인 + 1(0 = 주인 없음)
            float OwnerOf(float region) { return Byte(Land(region, 0).a); }

            float3 SeaPixel(float x, float y)
            {
                float row = floor(y / WAVE_ROW);
                float speed = Mod(row * 7, 5);
                float shift = speed < 0.5 ? _WaveShift.x : speed < 1.5 ? _WaveShift.y : speed < 2.5 ? _WaveShift.z : speed < 3.5 ? _WaveShift.w : _WaveShift4;
                return Mod(y, WAVE_ROW) == 0 && Mod(x - shift + row * (WAVE_SPAN / 2), WAVE_SPAN) < WAVE_LENGTH ? SeaWave : SeaFill;
            }

            // 이벤트 땅 = 산맥 띠의 음영 기복도 톤 셋(사용자 10/26)
            float3 RockTone(float tone)
            {
                return tone < 0.5 ? float3(70, 45, 30) : tone < 1.5 ? float3(120, 82, 52) : float3(180, 120, 66);
            }

            float3 LandPixel(float x, float y, float region, float4 cell, float2 uv)
            {
                float4 land0 = Land(region, 0), land1 = Land(region, 1);
                float3 color = Bytes(land0.rgb), terrain = Bytes(land1.rgb);
                float owner = Byte(land0.a), meta = Byte(land1.a);
                float state = Mod(meta, 8);
                bool openFlag = meta >= 8;
                float mine = _Mine + 1;
                bool own = owner == mine;
                bool hovered = region == _Hovered;
                bool reachable = !own && openFlag;
                float fill = own ? (hovered ? ROW_ONE : ROW_MINE)
                    : reachable ? (hovered ? ROW_OPEN_HOVER : ROW_ONE)
                    : hovered ? ROW_ONE : ROW_FILL;
                float3 ground = own ? color : terrain;
                float3 pixel = Lut(ground, fill);
                bool hatchCell = Mod(x + y, HATCH_STEP) == 0;
                if (_Hatch > 0.5 && openFlag && hatchCell)
                    pixel = color;

                float flags = Byte(cell.a);
                float seaDist = Mod(flags, 8);
                bool nearEdge = Mod(floor(flags / 8), 2) > 0.5;
                bool decor = Mod(floor(flags / 16), 2) > 0.5;
                float tone = floor(flags / 32);

                // 이벤트 땅(주인 없음)인데 상태가 없을 때 — 검은 바탕에 옅은 빗금
                if (owner == 0)
                    pixel = hatchCell ? Lut(color, hovered ? ROW_EVENT_HATCH_HOVER : ROW_EVENT_HATCH) : 0;
                // 이벤트 땅 — 기복도 톤 × 상태 밝기, 방문 가능 · 깜박임은 흰 굵은 테두리. 장식은 없다
                if (state != 0)
                {
                    float light = state == EVENT_CLOSED ? ROW_EVENT_CLOSED : state == EVENT_LIT ? ROW_EVENT_LIT
                        : state == EVENT_VISITABLE ? (hovered ? ROW_EVENT_VISITABLE_HOVER : ROW_EVENT_VISITABLE) : hovered ? ROW_EVENT_HOVER : ROW_ONE;
                    pixel = state != EVENT_CLOSED && state != EVENT_OPEN && nearEdge ? 255 : Lut(RockTone(tone), light);
                    if (region == _Selected && nearEdge)
                        pixel = SelectedColor;
                    return pixel;
                }

                // 오른쪽 · 아래 칸의 다른 땅 — 주인이 다르면 국경선, 같으면 옅은 선
                bool lordEdge = false, landEdge = false;
                float right = Byte(cell.g) - 1, down = Byte(cell.b) - 1;
                if (right >= 0) { if (OwnerOf(right) != owner) lordEdge = true; else landEdge = true; }
                if (down >= 0) { if (OwnerOf(down) != owner) lordEdge = true; else landEdge = true; }
                bool coast = seaDist <= COAST_WIDTH;

                // 가까운 다른 땅들 — 주인을 표에서 읽어 견준다(바다는 seaDist)
                float4 ids = Bytes(tex2Dlod(_NearIds, float4(uv, 0, 0))) - 1;
                float4 dists = Bytes(tex2Dlod(_NearDist, float4(uv, 0, 0)));
                bool otherBorder = seaDist <= MINE_BORDER, otherGlow = seaDist <= MINE_GLOW, band = seaDist <= LORD_BAND;
                bool nearMine = false, nearSelected = false;
                float selectedRange = own ? MINE_BORDER : MINE_SHADOW;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float id = ids[k], d = dists[k];
                    if (id < 0)
                        continue;
                    float other = OwnerOf(id);
                    otherBorder = otherBorder || (d <= MINE_BORDER && other != mine);
                    otherGlow = otherGlow || (d <= MINE_GLOW && other != mine);
                    nearMine = nearMine || (d <= MINE_SHADOW && other == mine);
                    band = band || (d <= LORD_BAND && other != owner);
                    nearSelected = nearSelected || (id == _Selected && d <= selectedRange);
                }

                // 내 땅 덩어리의 바깥 가장자리는 흰 굵은 선 · 그 안쪽은 내 색이 밝게 번진 띠 · 맞닿은 남의 땅 쪽에는 검은 그림자
                // 남의 땅 국경 · 해안선은 검은 선, 그 안쪽 둘레는 영주 색 띠(사용자 10/15 · 10/26)
                if (own && (coast || lordEdge || otherBorder))
                    pixel = 255;
                else if (own && otherGlow)
                    pixel = Lut(color, ROW_GLOW);
                else if (!own && nearMine)
                    pixel = 0;
                else if (coast || lordEdge)
                    pixel = EdgeColor;
                else if (!own && band)
                    pixel = Lut(color, ROW_BAND);
                else if (landEdge)
                    pixel = Lut(ground, fill + ROW_INNER);
                // 고른 땅 테두리는 이웃 땅 쪽에 그려진 국경선까지 덮는다(사용자 10/15)
                if (region == _Selected ? nearEdge : _Selected >= 0 && nearSelected)
                    pixel = SelectedColor;
                // 지형 장식 — 내 땅은 살짝 밝게, 남의 땅은 살짝 어둡게(이벤트 땅은 위에서 이미 끝났다)
                if (decor)
                    pixel = Lut(pixel, own ? ROW_DECOR_UP : ROW_DECOR_DOWN);
                return pixel;
            }

            // 0~255 값을 화면에 낼 색으로 — sRGB 텍스처(_Ramp, 256×1, i번 칸 = i)를 거쳐 옛 그림(sRGB 텍스처)과 같은 변환을 탄다
            float3 ToOutput(float3 bytes)
            {
                float3 u = (bytes + 0.5) / 256.0;
                return float3(tex2Dlod(_Ramp, float4(u.r, 0.5, 0, 0)).r, tex2Dlod(_Ramp, float4(u.g, 0.5, 0, 0)).r, tex2Dlod(_Ramp, float4(u.b, 0.5, 0, 0)).r);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 at = clamp(floor(IN.texcoord * size), 0, size - 1);
                float2 uv = (at + 0.5) * _MainTex_TexelSize.xy;
                // 칸 좌표는 위 왼쪽이 0(텍스처는 아래 줄부터)
                float x = at.x, y = size.y - 1 - at.y;
                float4 cell = tex2Dlod(_MainTex, float4(uv, 0, 0));
                float region = Byte(cell.r) - 1;
                float3 pixel = region < 0 ? SeaPixel(x, y) : LandPixel(x, y, region, cell, uv);
                float4 arrow = tex2Dlod(_Arrows, float4(uv, 0, 0));
                if (arrow.a > 0.5)
                    pixel = Bytes(arrow.rgb);

                float4 color = float4(ToOutput(pixel), 1) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                float2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
