shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "vertex_shift.gdshaderinc"
#include "sine_funcs.gdshaderinc"
#include "water_algorithms.gdshaderinc"

#define MAX_WAVES 64

group_uniforms managed;
uniform sampler2D wave_texture;
uniform uint waveCount;
global uniform vec3 camera_coords;
group_uniforms;

varying float wave_height;
varying float fresnel;

group_uniforms toggles;
uniform bool use_expsine = true;
uniform bool useVertexShift = false;
group_uniforms;



Wave get_wave(uint index, sampler2D waves){
    float tex_width = float(waveCount);
    vec2 uv0 = vec2((float(index) + 0.5) / tex_width, 0.25); // y = 0th row
    vec2 uv1 = vec2((float(index) + 0.5) / tex_width, 0.75); // y = 1st row
    vec3 p0 = texture(waves, uv0).rgb;
    vec3 p1 = texture(waves, uv1).rgb;
    return Wave(vec2(p0.r - 1.0, p0.g - 1.0), p1.r, p1.g, p1.b);
}


float wave_displacement(vec2 uv, Wave w) {
	float displacement = 0.0;
	if (use_expsine) {
		displacement = expsine(uv, w);
	} else {
		displacement = sine(uv,w);
	}
	return displacement;
}

vec2 wave_derivatives(vec2 uv, Wave w) {
	vec2 deriv = vec2(0.0);
	if (use_expsine) {
		deriv = d_expsine(uv, w);
	} else {
		deriv = d_sine(uv, w);
	}
	return deriv;
}


uniform float amp_mod = 1.0;
uniform float cutoff_dist = 250.0;
uniform float cutoff_strength = 0.007;

void vertex() {
	if (useVertexShift) {
		VERTEX.xz += xzShift(VERTEX, camera_coords, NODE_POSITION_WORLD);
	}

	float distance_factor = clamp(exp(-(length(VERTEX.xz - CAMERA_POSITION_WORLD.xz) - cutoff_dist)*0.007), 0.05, 1.0);

	float displacement = 0.0;
	vec2 prev_derivs = vec2(0.0);
	vec2 uv = VERTEX.xz;
	for (uint i = 0u; i < waveCount; i++) {
		Wave w = get_wave(i, wave_texture);
		w.amplitude *= amp_mod;
		uv += prev_derivs * w.direction * w.amplitude;
		prev_derivs = wave_derivatives(uv, w);
		displacement += w.amplitude*wave_displacement(uv,w);
	}
	VERTEX.xz = uv;
	VERTEX.y = displacement;// * distance_factor;

}



group_uniforms colors;
uniform vec4 water_color : source_color = vec4(0.1, 0.17, 0.27, 1.0);
group_uniforms;
varying vec2 v_derivatives;
void fragment() {
	float dist = length(VERTEX.xz);

	vec2 derivatives = vec2(0.0);
	float displacement = 0.0;
	vec2 prev_derivs = vec2(0.0);
	vec2 uv = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz;
	for (uint i = 0u; i < waveCount; i++) {
		Wave w = get_wave(i, wave_texture);
		w.amplitude *= amp_mod;
		prev_derivs = wave_derivatives(uv, w);
		uv += prev_derivs * w.direction * w.amplitude;

		derivatives += prev_derivs;
		displacement += w.amplitude*wave_displacement(uv,w);

	}
	v_derivatives = derivatives;
	wave_height = displacement;


	ALBEDO = water_color.rgb;

	vec3 gradient = vec3(-derivatives.x, 1.0, -derivatives.y);
	gradient *= mix(0.015, 1.0, exp(-dist * 0.0175));
	NORMAL = (VIEW_MATRIX * vec4(normalize(vec3(-gradient.x, 1.0, -gradient.z)), 0.0)).xyz;


	fresnel = mix(schlick_fresnel(dot(NORMAL, VIEW), roughness), 1.0, REFLECTANCE);
	ROUGHNESS = 0.4;
}



void light() {

	SPECULAR_LIGHT += specular(LIGHT, LIGHT_COLOR, VIEW, NORMAL, v_derivatives) * ATTENUATION;


	DIFFUSE_LIGHT += diffuse(LIGHT, LIGHT_COLOR, VIEW, NORMAL, wave_height) * (1.0 - fresnel) * ATTENUATION;
}