// Additive pulsing overlay drawn on top of the winning discs.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;

uniform vec3 glow : source_color = vec3(1.0, 0.95, 0.8);

void fragment() {
	float pulse = 0.5 + 0.5 * sin(TIME * 6.0);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 1.5);
	ALBEDO = glow * (0.18 + 0.4 * pulse) * (0.4 + rim);
}
