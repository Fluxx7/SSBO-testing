shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "vertex_shift.gdshaderinc"
#include "sine_funcs.gdshaderinc"
#include "water_algorithms.gdshaderinc"

group_uniforms managed;
uniform sampler2D wave_texture;
uniform uint waveCount;
global uniform vec3 camera_coords;
group_uniforms;

group_uniforms toggles;
uniform bool use_derivative_displacement = true;
uniform float deriv_displacement_mult : hint_range(0.0, 10.0, 0.1) = 1.7;
uniform bool useVertexShift = true;
group_uniforms;



Wave get_wave(uint index, sampler2D waves){
    float tex_width = float(waveCount);
    vec2 uv0 = vec2((float(index) + 0.5) / tex_width, 0.25); // y = 0th row
    vec2 uv1 = vec2((float(index) + 0.5) / tex_width, 0.75); // y = 1st row
    vec3 p0 = texture(waves, uv0).rgb;
    vec3 p1 = texture(waves, uv1).rgb;
    return Wave(vec2(p0.r, p0.g), p1.r, p1.g, p1.b);
}


float wave_displacement(vec2 uv, Wave w) {
	return expsine(uv, w);
}

vec2 wave_derivatives(vec2 uv, Wave w) {
	return d_expsine(uv, w);
}


uniform float amp_mod = 1.0;
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
	for (uint i = 0u; i < waveCount; i++) {
		Wave w = get_wave(i, wave_texture);
		w.amplitude *= amp_mod;
		prev_derivs = w.amplitude* wave_derivatives(uv, w);
		uv += -prev_derivs * w.direction * w.amplitude * deriv_displacement_mult;

		displacement += w.amplitude*wave_displacement(uv,w);
	}
	if (use_derivative_displacement) {
		VERTEX.xz = uv;
	}
	VERTEX.y = displacement * dist_factor;// * distance_factor;

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
	for (uint i = 0u; i < waveCount; i++) {
		Wave w = get_wave(i, wave_texture);
		w.amplitude *= amp_mod;
		prev_derivs = w.amplitude * wave_derivatives(uv, w);
		uv += -prev_derivs * w.direction * w.amplitude * deriv_displacement_mult;

		derivatives += prev_derivs;
		displacement += w.amplitude*wave_displacement(uv,w);

	}
	wave_height = displacement;
	ALBEDO = water_color.rgb;

	vec3 model_normal = vec3(-derivatives.x, 1.0, -derivatives.y);
	model_normal *= mix(0.015, 1.0, exp(-dist * 0.0175));

	NORMAL = normalize(VIEW_MATRIX * vec4(model_normal, 0.0)).xyz;

	
	ROUGHNESS = 0.0;
}



void light() { 	
	brdf_lighting(LIGHT, LIGHT_COLOR, VIEW, VIEW_MATRIX, NORMAL, vec2(0.0), wave_height, SPECULAR_LIGHT, DIFFUSE_LIGHT);
}