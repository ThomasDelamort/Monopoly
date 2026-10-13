// Glowing ring under a player token: left-to-right two-color gradient, soft inner fill,
// pulsing when the player is the active one (matches the player-select cards).
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec4 color_a : source_color = vec4(0.85, 1.0, 1.0, 1.0);
uniform vec4 color_b : source_color = vec4(0.55, 0.95, 1.0, 1.0);
uniform float active = 0.0;
uniform float intensity = 1.4;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float ring = smoothstep(0.74, 0.82, r) * (1.0 - smoothstep(0.86, 0.96, r));
	float halo = (1.0 - smoothstep(0.55, 1.0, abs(r - 0.84) * 6.0)) * 0.25;
	float fill = (1.0 - smoothstep(0.1, 0.82, r)) * 0.12;
	vec3 col = mix(color_a.rgb, color_b.rgb, clamp(p.x * 0.5 + 0.5, 0.0, 1.0));
	float pulse = 0.65 + active * (0.55 + 0.3 * sin(TIME * 4.0));
	ALBEDO = col * intensity * pulse;
	ALPHA = clamp((ring + halo + fill * (0.4 + active)) * pulse, 0.0, 1.0);
}
