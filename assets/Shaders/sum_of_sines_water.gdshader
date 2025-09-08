shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "vertex_shift.gdshaderinc"
#include "sine_funcs.gdshaderinc"
#include "water_algorithms.gdshaderinc"

group_uniforms managed;
global uniform vec3 camera_coords;
group_uniforms;

buffer restrict in {
	Wave wave_array[42];
}(bindname = waveBuffer, format = std430);

group_uniforms toggles;
uniform bool use_derivative_displacement = true;
uniform bool useVertexShift = true;
group_uniforms;

uniform float cutoff_dist : hint_range(0.0, 8000.0, 10.0) = 1000.0;
uniform float cutoff_strength : hint_range(0.0, 0.005, 0.0001) = 0.0007;


void vertex() {
	if (useVertexShift) {
		VERTEX.xz += xzShift(VERTEX, camera_coords, NODE_POSITION_WORLD);
	}

	float dist_factor = clamp(exp(-(length(VERTEX.xz - CAMERA_POSITION_WORLD.xz) - cutoff_dist)*cutoff_strength), 0.05, 1.0);
	float displacement = 0.0;
	vec2 prev_derivs = vec2(0.0);
	vec2 uv = VERTEX.xz;
	for (int i = 0; i < wave_array.length(); i++) {
		Wave w = wave_array[i];
		float x = 0.0;
		displacement += w.amplitude*expsine(uv,w, x);
		prev_derivs = w.amplitude * d_expsine(uv, w, x);
		uv += -prev_derivs * w.direction * w.amplitude * 1.7;
	}

	if (use_derivative_displacement) {
		VERTEX.xz = uv;
	}
	VERTEX.y += displacement * dist_factor;// * distance_factor;

}

varying float wave_height;
group_uniforms colors;
uniform vec4 water_color : source_color = vec4(0.1, 0.18, 0.35, 1.0);
group_uniforms;
void fragment() {
	float dist = length(VERTEX.xz);
	vec2 derivatives = vec2(0.0);
	float displacement = 0.0;
	vec2 prev_derivs = vec2(0.0);
	vec2 uv = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz;
	for (int i = 0; i < wave_array.length(); i++) {
		Wave w = wave_array[i];
		float x = 0.0;
		displacement += w.amplitude*expsine(uv,w, x);
		prev_derivs = w.amplitude * d_expsine(uv, w, x);
		uv += -prev_derivs * w.direction * w.amplitude * 1.7;

		derivatives += prev_derivs;
	}

	wave_height = displacement;
	ALBEDO = water_color.rgb;

	vec3 model_normal = vec3(-derivatives.x, 1.0, -derivatives.y);
	model_normal *= mix(0.015, 1.0, exp(-dist * 0.0175));

	NORMAL = normalize(VIEW_MATRIX * vec4(model_normal, 0.0)).xyz;


	ROUGHNESS = 0.02;
}



void light() {
	brdf_lighting(LIGHT, LIGHT_COLOR, VIEW, VIEW_MATRIX, NORMAL, vec2(0.0), wave_height, SPECULAR_LIGHT, DIFFUSE_LIGHT);
}
