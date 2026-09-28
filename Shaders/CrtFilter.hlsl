// CrtFilter.hlsl
//
// Post-process pass for the WinUI 3 viewport. Draws a full-screen triangle (no vertex/index
// buffers) that samples the guest framebuffer (t0) and reproduces a CRT monitor's look:
// barrel curvature, per-channel chromatic aberration, sine-wave scanlines, an RGB aperture
// grille mask and a corner vignette. Mode selects how much of that is actually applied:
//   0 = None (Sharp)              - plain point/bilinear passthrough, no effects
//   1 = Scanlines (Flat)          - scanlines + aperture mask, screen stays flat
//   2 = Authentic CRT (Curved)    - adds curvature, chromatic aberration, bloom and vignette
//
// Compiled offline by CMake (fxc.exe) into CrtFilter.vs.cso / CrtFilter.ps.cso, which
// EmuBridge.c loads next to 86box_core.dll (see ensure_crt_pipeline in EmuBridge.c).

cbuffer CrtParams : register(b0)
{
    float2 GuestResolution;  // guest framebuffer size in pixels (scanline pitch is tied to this)
    float2 OutputResolution; // destination viewport size in pixels (aperture mask pitch)
    float  Curvature;        // 0.0 .. 0.15 screen curvature amount
    float  Mode;             // 0, 1 or 2 - see above
    float  Bloom;            // 0.0 .. 1.0 phosphor bloom intensity
    float  _Pad0;            // pads the cbuffer to a multiple of 16 bytes
};

Texture2D    GuestTexture : register(t0);
SamplerState GuestSampler : register(s0);

struct VsOut
{
    float4 Position : SV_POSITION;
    float2 Uv        : TEXCOORD0;
};

// Full-screen triangle from just the vertex id - no vertex buffer needed. Produces (0,0),
// (2,0) and (0,2) in UV space, which covers the whole [0,1] viewport with one triangle.
VsOut VSMain(uint id : SV_VertexID)
{
    VsOut o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.Uv       = uv;
    o.Position = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
    return o;
}

// Classic barrel distortion: pushes UV outward from center by an amount proportional to the
// squared distance from center, so the picture bulges like curved glass.
float2 DistortUV(float2 uv, float amount)
{
    float2 cc = uv * 2.0 - 1.0;
    float  r2 = dot(cc, cc);
    cc *= 1.0 + amount * r2;
    return cc * 0.5 + 0.5;
}

float4 PSMain(VsOut input) : SV_TARGET
{
    bool authentic = Mode >= 1.5;
    bool anyCrt    = Mode >= 0.5;

    float  curvature = authentic ? Curvature : 0.0;
    float2 uv         = DistortUV(input.Uv, curvature);

    // Curved glass leaves the corners of the viewport outside the [0,1] picture: those pixels
    // are the CRT's black bezel rather than stretched/garbage texture samples.
    if (authentic && (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0))
        return float4(0.0, 0.0, 0.0, 1.0);

    // Chromatic aberration: each colour channel's electron beam converges slightly differently,
    // so sample red and blue with a small UV offset that grows away from the screen center.
    float2 caOffset = authentic ? (uv - 0.5) * 0.0035 : float2(0.0, 0.0);
    float  r = GuestTexture.Sample(GuestSampler, uv - caOffset).r;
    float  g = GuestTexture.Sample(GuestSampler, uv).g;
    float  b = GuestTexture.Sample(GuestSampler, uv + caOffset).b;
    float3 color = float3(r, g, b);

    if (anyCrt) {
        // Smooth sine-wave scanline modulation, one cycle per guest scanline row.
        float scanY   = uv.y * max(GuestResolution.y, 1.0);
        float wave    = 0.5 + 0.5 * sin(scanY * 6.28318530718 - 1.57079632679);
        float scanline = lerp(1.0, 0.55 + 0.45 * wave, 0.85);
        color *= scanline;

        // RGB aperture grille: alternating red/green/blue-biased triads across the width.
        float  maskX = frac(uv.x * max(OutputResolution.x, 1.0) / 3.0);
        float3 mask  = (maskX < 0.333) ? float3(1.15, 0.85, 0.85)
                      : (maskX < 0.666) ? float3(0.85, 1.15, 0.85)
                      :                    float3(0.85, 0.85, 1.15);
        color *= lerp(float3(1.0, 1.0, 1.0), mask, 0.35);
    }

    if (authentic) {
        // Phosphor bloom: bright pixels spill a little extra light around themselves.
        float luma = dot(color, float3(0.299, 0.587, 0.114));
        color += luma * luma * Bloom * 0.6;

        // Corner vignette falloff, as the curved tube gets dimmer away from center.
        float2 vc      = uv - 0.5;
        float  vdist   = length(vc) * 1.4142136;
        float  vignette = 1.0 - smoothstep(0.6, 1.0, vdist) * 0.55;
        color *= vignette;
    }

    return float4(saturate(color), 1.0);
}
