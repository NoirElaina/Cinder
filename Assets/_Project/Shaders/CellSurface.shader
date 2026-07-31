// Cell Surface：整个模拟窗口一次 DrawCall 的细格渲染。
// CPU 侧由 PackCellsJob 打包（位布局见该文件），本着色器负责：
// 调色板团块着色、颗粒抖动、边缘提亮、液面高光、火焰闪烁、
// 程序化背景岩壁/天空，以及光照通道的暗环境合成。
// 无 LightMode 标签 => URP 按 SRPDefaultUnlit 渲染。
Shader "Cinder/CellSurface"
{
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            ZWrite On
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            // 每格一个 uint：[0..9] 物质 | [10..15] 变体 | [16..23] 光照 | [24..31] State
            StructuredBuffer<uint> _Cells;
            // [id * 8 + band] = RGBA8（低位 R），band 0 暗 -> 7 亮
            StructuredBuffer<uint> _Palettes;
            // [id] = [0..3] 类型 | [4..11] 自发光 | [12..19] 颗粒 | [20..27] 边光
            StructuredBuffer<uint> _MatParams;

            int _WinW;
            int _WinH;
            int _OriginX;
            int _OriginY;
            float _CellsPerUnit;
            float _SurfaceY;   // 平均地表世界 Y，天空渐变参考
            int _DebugMode;    // 0 正常 1 温度热力图

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 wpos : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // 确定性整数哈希（与 CPU 侧 SimHash 同族，不要求逐位一致）
            float ihash(int2 c, uint salt)
            {
                uint h = (uint)c.x * 374761393u + (uint)c.y * 668265263u + salt * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return float(h ^ (h >> 16)) * (1.0 / 4294967295.0);
            }

            float vnoise(float2 p, uint salt)
            {
                float2 i = floor(p);
                float2 f = p - i;
                f = f * f * (3.0 - 2.0 * f);
                int2 c = (int2)i;
                float a = ihash(c, salt);
                float b = ihash(c + int2(1, 0), salt);
                float d = ihash(c + int2(0, 1), salt);
                float e = ihash(c + int2(1, 1), salt);
                return lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
            }

            float3 unpackRGB(uint v)
            {
                return float3(v & 255u, (v >> 8) & 255u, (v >> 16) & 255u) * (1.0 / 255.0);
            }

            uint cellAt(int2 local)
            {
                local = clamp(local, int2(0, 0), int2(_WinW - 1, _WinH - 1));
                return _Cells[local.y * _WinW + local.x];
            }

            // 程序化背景：洞穴暗岩壁 + 地表以上的天空渐变。
            // 噪声按细格量化：同一格内所有屏幕像素同色，保持像素风锐利边缘
            float3 background(float2 cellF, float light, float2 wpos)
            {
                float2 cq = floor(cellF) + 0.5;
                float n = vnoise(cq * 0.035, 77u) * 0.55 + vnoise(cq * 0.16, 78u) * 0.45;
                float3 wall = lerp(float3(0.050, 0.040, 0.048), float3(0.120, 0.098, 0.092), n);
                wall *= 0.20 + 0.80 * light;

                // 天空只看世界高度，不受光照场门控（窗口外也不会发黑）
                float skyAmt = saturate((wpos.y - _SurfaceY) * 0.03);
                float3 sky = lerp(float3(0.17, 0.22, 0.31), float3(0.06, 0.08, 0.14),
                    saturate((wpos.y - _SurfaceY) * 0.006));
                return lerp(wall, sky, skyAmt);
            }

            float3 heatmap(float u)
            {
                float3 c = lerp(float3(0.05, 0.10, 0.60), float3(0.10, 0.80, 0.90), saturate(u * 4.0));
                c = lerp(c, float3(0.20, 0.90, 0.20), saturate(u * 4.0 - 1.0));
                c = lerp(c, float3(1.00, 0.90, 0.10), saturate(u * 4.0 - 2.0));
                c = lerp(c, float3(1.00, 0.15, 0.05), saturate(u * 4.0 - 3.0));
                return c;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 cellF = i.wpos * _CellsPerUnit;
                int2 cell = (int2)floor(cellF);
                int2 local = cell - int2(_OriginX, _OriginY);

                if (local.x < 0 || local.y < 0 || local.x >= _WinW || local.y >= _WinH)
                {
                    // 窗口外：延用最近窗口边缘格的光照，避免屏幕两侧出现突变暗带
                    float edgeLight = float((cellAt(local) >> 16) & 0xFFu) * (1.0 / 255.0);
                    return float4(background(cellF, edgeLight, i.wpos), 1.0);
                }

                uint packedCell = _Cells[local.y * _WinW + local.x];
                uint mat = packedCell & 0x3FFu;

                if (_DebugMode == 1)
                {
                    float t = float((packedCell >> 16) & 0x1FFFu);
                    return float4(heatmap(saturate((t - 250.0) / 1200.0)), 1.0);
                }

                uint variant = (packedCell >> 10) & 0x3Fu;
                float light = float((packedCell >> 16) & 0xFFu) * (1.0 / 255.0);

                if (mat == 0u)
                    return float4(background(cellF, light, i.wpos), 1.0);

                uint prm = _MatParams[mat];
                uint kind = prm & 15u;
                float emission = float((prm >> 4) & 255u) * (1.0 / 255.0);
                float grain = float((prm >> 12) & 255u) * (1.0 / 255.0);
                float edgeAmt = float((prm >> 20) & 255u) * (1.0 / 255.0);

                // 火焰：亮芯闪烁，无视光照场（自己就是光源）
                if (kind == 5u)
                {
                    float flick = ihash(cell, (uint)(_Time.y * 24.0) + variant);
                    float3 fire = lerp(float3(1.0, 0.32, 0.04), float3(1.0, 0.88, 0.30), flick);
                    return float4(fire, 1.0);
                }

                // 色带选择：团块噪声定基带，逐格颗粒抖动（全部按细格量化）
                float2 cq = float2(cell) + 0.5;
                float cluster = vnoise(cq * 0.085, 11u);
                float jitter = (ihash(cell, variant + 91u) - 0.5) * grain * 6.0;
                int band = clamp((int)(cluster * 8.0 + jitter), 0, 7);
                float3 albedo = unpackRGB(_Palettes[mat * 8u + (uint)band]);

                bool upEmpty = (cellAt(local + int2(0, 1)) & 0x3FFu) == 0u;

                // 气体：浓度 = 寿命分数 × 上飘翻卷噪声，新烟浓、将散的烟稀、
                // 内部浓淡随时间涌动（仍按细格量化，保持像素风锐利边缘）
                if (kind == 4u)
                {
                    float3 bg = background(cellF, light, i.wpos);
                    float life = float((packedCell >> 24) & 0xFFu) * (1.0 / 255.0);
                    float swirl = vnoise(cq * 0.11 + float2(0.0, -_Time.y * 2.2), 21u);
                    float a = (0.30 + 0.45 * life) * (0.45 + 0.55 * swirl);
                    a *= saturate(life * 3.0);   // 残寿末段加速转稀，消散不突兀
                    float3 lit = albedo * (0.25 + 0.75 * light);
                    return float4(lerp(bg, lit, saturate(a)), 1.0);
                }

                if (kind == 3u)
                {
                    // 液体：液面高光 + 微弱流光（按细格量化，不出现格内渐变）
                    if (upEmpty) albedo *= 1.0 + edgeAmt * 0.5;
                    albedo *= 0.95 + 0.05 * vnoise(cq * 0.2 + _Time.y * 1.5, 5u);
                }
                else
                {
                    // 固体/粉末：上缘受光提亮，包在内部的格子微压暗
                    if (upEmpty) albedo *= 1.0 + edgeAmt * 0.45;
                    else
                    {
                        bool leftEmpty = (cellAt(local + int2(-1, 0)) & 0x3FFu) == 0u;
                        bool rightEmpty = (cellAt(local + int2(1, 0)) & 0x3FFu) == 0u;
                        if (!leftEmpty && !rightEmpty) albedo *= 0.96;
                    }
                }

                // 暗环境合成：光照做 gamma 提对比，自发光物质无视黑暗
                float lit = pow(light, 1.4);
                float3 col = albedo * (0.05 + 0.95 * lit);
                col = lerp(col, albedo * (1.0 + emission * 0.6), emission);
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
