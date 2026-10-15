// Additive overlay on burning discs: licks of orange flame crawling up the surface.
// The noise is sampled in world space and scrolls upward, so the fire rises whichever way the board faces.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;

#include "res://shaders/noise.gdshaderinc"

// 0 = not burning, 1 = burning; above 1 is the bright flash as it catches.
uniform float heat = 1.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec3 p = world_pos * 3.0 - vec3(0.0, TIME * 2.2, 0.0);
	float n = vnoise(p) * 0.65 + vnoise(p * 2.3 + 7.1) * 0.35;
	float flame = smoothstep(0.35, 0.85, n);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 1.5);
	vec3 hot = mix(vec3(0.9, 0.18, 0.02), vec3(1.0, 0.8, 0.3), flame);
	ALBEDO = hot * (0.25 + 0.75 * flame + 0.5 * rim) * heat;
}
