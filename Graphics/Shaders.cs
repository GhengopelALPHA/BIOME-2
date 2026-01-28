using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Biome2.Graphics;
internal static class Shaders {
	public const string GridVertex = @"
#version 450 core

layout(location = 0) in vec2 aLocalPos;
layout(location = 1) in vec2 aInstancePos;

uniform mat4 uViewProj;
uniform float uCellSize;
uniform vec2 uGridSize;

out vec2 vCellUv;
flat out ivec2 vCellCoord;

void main()
{
    // Local quad scaled to cell size.
    vec2 worldPos = aInstancePos + (aLocalPos * uCellSize);

    // vCellUv is used to draw border lines in fragment shader.
    vCellUv = aLocalPos;
    // Compute integer cell coordinate and pass to fragment shader without interpolation.
    ivec2 cell = ivec2(floor(aInstancePos / uCellSize + vec2(0.5)));
    vCellCoord = cell;

    gl_Position = uViewProj * vec4(worldPos.xy, 0.0, 1.0);
}";

	public const string GridFragment = @"
#version 450 core

in vec2 vCellUv;
flat in ivec2 vCellCoord;
out vec4 fragColor;

uniform int uShowGrid;
uniform float uPixelsPerUnit;
uniform float uGridThicknessPx;
uniform float uCellSize;
uniform sampler2D uCellColors;

void main()
{
    // Fetch the per-cell color using integer texel fetch for exact texel.
    ivec2 coord = vCellCoord;
    vec4 texColor = texelFetch(uCellColors, coord, 0);

    // If grid is disabled, output the texel color directly so adjacent
    // cells with the same color render contiguously (no seams).
    if (uShowGrid == 0) {
        fragColor = texColor;
        return;
    }

    // Otherwise compute an inset and anti-aliased interior mask to show grid gaps.
    float worldThickness = uGridThicknessPx / max(uPixelsPerUnit, 0.01);
    float inset = worldThickness / (2.0 * max(uCellSize, 0.0001));
    inset = clamp(inset, 0.0, 0.1);

    // Anti-aliasing fade expressed in UV coordinates (approx. half a screen
    // pixel converted into cell-local UV space).
    float uvPixel = (1.0 / max(uPixelsPerUnit, 0.0001)) / max(uCellSize, 0.0005);
    float fade = clamp(uvPixel * 0.5, 1e-6, 0.5);

    float left = smoothstep(inset - fade, inset + fade, vCellUv.x);
    float right = smoothstep(inset - fade, inset + fade, 1.0 - vCellUv.x);
    float down = smoothstep(inset - fade, inset + fade, vCellUv.y);
    float up = smoothstep(inset - fade, inset + fade, 1.0 - vCellUv.y);

    float interior = left * right * down * up;

    vec3 borderColor = vec3(0.0, 0.0, 0.0);
    vec3 color = mix(borderColor, texColor.rgb, interior);

    fragColor = vec4(color, texColor.a);
}
";

	public const string AxisVertex = @"
#version 450 core

layout(location = 0) in vec2 aPos;
uniform mat4 uViewProj;

void main()
{
    gl_Position = uViewProj * vec4(aPos.xy, 0.0, 1.0);
}";

	public const string AxisFragment = @"
#version 450 core

uniform vec3 uColor;
out vec4 fragColor;

void main()
{
    fragColor = vec4(uColor, 1.0);
}";
}
