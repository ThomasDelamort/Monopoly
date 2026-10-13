// Navy ground with a faint cyan grid that fades out with distance, like the concept art backdrop.
shader_type spatial;
render_mode unshaded, shadows_disabled;

uniform vec4 base_color : source_color = vec4(0.106, 0.153, 0.259, 1.0);
uniform vec4 line_color : source_color = vec4(0.27, 0.6, 0.8, 1.0);
uniform float cell = 1.6;
uniform float fade_start = 8.0;
uniform float fade_end = 34.0;
uniform float strength = 0.28;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 g = world_pos.xz / cell;
	vec2 grid = abs(fract(g - 0.5) - 0.5) / fwidth(g);
	float line = 1.0 - min(min(grid.x, grid.y), 1.0);
	float d = length(world_pos.xz);
	float fade = 1.0 - smoothstep(fade_start, fade_end, d);
	ALBEDO = mix(base_color.rgb, line_color.rgb, line * strength * fade);
}
