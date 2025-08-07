shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "vertex_shift.gdshaderinc"
#include "sine_funcs.gdshaderinc"
#include "water_algorithms.gdshaderinc"

#define MAX_WAVES 64


uniform sampler2D wave_texture;
uniform uint waveCount;
global uniform vec3 camera_coords;

varying float wave_height;
varying float fresnel;
uniform bool use_expsine = true;
uniform bool useVertexShift = false;





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
	for (uint i = 0u; i < waveCount; i++) {
		Wave w = get_wave(i, wave_texture);
		w.amplitude *= amp_mod;
		vec2 uv = vec2(VERTEX.x+prev_derivs.x, VERTEX.z+prev_derivs.y);

		displacement += w.amplitude*wave_displacement(uv, w);
		prev_derivs = wave_derivatives(uv, w);
	}
	wave_height = displacement;
	VERTEX.y += displacement * distance_factor;//

}

#define roughness 0.65
uniform vec4 water_color : source_color = vec4(0.1, 0.17, 0.27, 1.0);
varying vec2 v_derivatives;
void fragment() {
	float dist = length(VERTEX.xz);

	vec2 derivatives = vec2(0.0);
	for (uint i = 0u; i < waveCount; i++) {
		Wave w = get_wave(i, wave_texture);
		w.amplitude *= amp_mod;
		derivatives += wave_derivatives(UV, w);
	}
	v_derivatives = derivatives;

	ALBEDO = water_color.rgb;

	vec3 gradient = vec3(-derivatives.x, 1.0, -derivatives.y);
	gradient *= mix(0.015, 1.0, exp(-dist * 0.0175));
	NORMAL = (VIEW_MATRIX * vec4(normalize(vec3(-gradient.x, 1.0, -gradient.z)), 0.0)).xyz;

	fresnel = schlick_fresnel(dotclampedzero(NORMAL, VIEW), roughness);
	
	ROUGHNESS = 0.4;
}




void light() {
	vec3 halfway = normalize(LIGHT + VIEW);
	float dot_nl = max(dot(NORMAL, LIGHT), 2e-5);
	float dot_nv = max(dot(NORMAL, VIEW), 2e-5);

	// --- SPECULAR ---
	float light_mask = smith_masking_shadowing(dot_nl, roughness);
	float view_mask = smith_masking_shadowing(dot_nv, roughness);
	float microfacet_distribution = ggx_distribution(dot(NORMAL, halfway), roughness);
	float geometric_attenuation = 1.0 / (1.0 + light_mask + view_mask);
	SPECULAR_LIGHT += specular(LIGHT, LIGHT_COLOR, VIEW, NORMAL, v_derivatives) * ATTENUATION;

	// --- DIFFUSE ---
	const vec3 sss_modifier = vec3(0.9,1.15,0.85); // Subsurface scattering produces a 'greener' color.
	float sss_height = 1.0*max(0.0, wave_height + 2.5) * pow(max(dot(LIGHT, -VIEW), 0.0), 4.0) * pow(0.5 - 0.5 * dot(LIGHT, NORMAL), 3.0);
	float sss_near = 0.5*pow(dot_nv, 2.0);
	float lambertian = 0.5*dot_nl;
	DIFFUSE_LIGHT += ((sss_height + sss_near) * sss_modifier / (1.0 + light_mask) + lambertian) * (1.0 - fresnel) * ATTENUATION * LIGHT_COLOR;
}