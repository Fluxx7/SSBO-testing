shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "sine_funcs.gdshaderinc"
#include "water_algorithms.gdshaderinc"

global uniform sampler2D waveTexture;
uniform int wave_count;

group_uniforms managed;
global uniform vec3 camera_coords;
group_uniforms;
 
group_uniforms toggles; 
uniform bool use_derivative_displacement = false;
group_uniforms;

uniform float cutoff_dist : hint_range(0.0, 8000.0, 10.0) = 1000.0;
uniform float cutoff_strength : hint_range(0.0, 0.005, 0.0001) = 0.0007;
uniform float chopMod = 1.0;

Wave get_wave(int index, sampler2D waves){
	vec3 p0 = texelFetch(waves, ivec2(index, 0), 0).xyz;
	vec3 p1 = texelFetch(waves, ivec2(index, 1), 0).xyz;
	return Wave(
	vec2(p0.r, p0.g), // Direction
	p1.r, // Amplitude
	p1.g, // Frequency
	p1.b, // Phase 
	p0.b); // Phase Modifier
}

vec2 chopFunc(vec2 derivatives, float frequency) {
	float wave_vec = 2.0 * PI * frequency;
	return -chopMod * derivatives / wave_vec;
}

void vertex() {

	float dist_factor = clamp(exp(-(length(VERTEX.xz - CAMERA_POSITION_WORLD.xz) - cutoff_dist)*cutoff_strength), 0.05, 1.0);
	float displacement = 0.0;
	vec2 uv = VERTEX.xz;
	for (int i = 0; i < wave_count; i++) {
		Wave w = get_wave(i, waveTexture);
		float x = 0.0;
		float magnitude =  w.amplitude*expsine(uv,w, x);
		displacement += magnitude;
		uv += chopFunc(w.amplitude * d_expsine(uv, w, x), w.frequency);
	}

	if (use_derivative_displacement) {
		VERTEX.xz = uv;
	}
	VERTEX.y += displacement * dist_factor;

}

varying float wave_height;
group_uniforms colors;
uniform vec4 water_color : source_color = vec4(0.0, 0.086, 0.204, 1.0);
group_uniforms;

void fragment() {
	float dist = length(VERTEX.xz);
	vec2 derivatives = vec2(0.0);
	float displacement = 0.0;
	vec2 prev_derivs = vec2(0.0);
	vec2 uv = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz;
	for (int i = 0; i < wave_count; i++) {
		Wave w = get_wave(i, waveTexture); 
		float x = 0.0;
		float magnitude = w.amplitude*expsine(uv,w, x);
		displacement += magnitude;
		prev_derivs = w.amplitude * d_expsine(uv, w, x);
		uv += chopFunc(prev_derivs, w.frequency);

		derivatives += prev_derivs; 
	}

	wave_height = displacement;
	ALBEDO = water_color.rgb;

	vec3 model_normal = vec3(-derivatives.x, 1.0, -derivatives.y);
	model_normal.xz *= mix(0.001, 1.0, exp(-dist * 0.002));

	NORMAL = normalize(VIEW_MATRIX * vec4(model_normal, 0.0)).xyz;


	ROUGHNESS = 0.125;
	METALLIC = 0.0;
	SPECULAR = 0.5;
}



void light() {
	brdf_lighting(LIGHT, LIGHT_COLOR, VIEW, VIEW_MATRIX, NORMAL, vec2(0.0), wave_height, SPECULAR_LIGHT, DIFFUSE_LIGHT);
	
}
