shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "water_algorithms.gdshaderinc"
global uniform vec3 camera_coords;

global uniform sampler2D displacementMap : filter_linear_mipmap_anisotropic;
global uniform sampler2D gradientMap : filter_linear_mipmap_anisotropic;
global uniform sampler2D foamMap : filter_linear_mipmap_anisotropic;
global uniform sampler2D normalMap : filter_nearest_mipmap_anisotropic;
global uniform int debugRender;
global uniform float tileLength;
global uniform uint renderFlags;

uniform float cutoff_dist : hint_range(0.0, 8000.0, 10.0) = 1000.0;
uniform float cutoff_strength : hint_range(0.0, 0.005, 0.0001) = 0.003;

varying float dist_factor;
varying vec2 coords;
void vertex() {
	coords = VERTEX.xz / tileLength;
	dist_factor = clamp(exp(-(length(VERTEX.xz - CAMERA_POSITION_WORLD.xz) - cutoff_dist)*cutoff_strength), 0.05, 1.0);
	vec3 vertex_displacement = texture(displacementMap, coords).xyz;
	VERTEX += vertex_displacement * vec3(1.0, dist_factor, 1.0);
}

varying vec2 v_derivatives;
varying float wave_height;
varying float foam_factor;
void fragment() {
	float dist = length(VERTEX.xz);
	vec2 gradientSample = texture(gradientMap, coords).rb;
	float foamSample = texture(foamMap, coords).x;
	foam_factor = 0.0;
	if ((renderFlags & 8u) == 8u) {
		foam_factor = smoothstep(0.0, 1.0, foamSample*0.75) * exp(-dist*0.00075);
	}
	v_derivatives = gradientSample;
	wave_height = texture(displacementMap, coords).g;
	vec3 local_normal = normalize(vec3(-gradientSample.x, 1.0, -gradientSample.y));
	vec2 debug_normal = texture(normalMap, coords).rb;
	ROUGHNESS = 0.02 + foam_factor * 0.5;
	float specular = 0.0;
	if ((renderFlags & 4u) == 4u) {
		specular = 0.5;
	}
	
	switch (debugRender) {
		default:
			SPECULAR = specular;
			ALBEDO = mix(air_bubble_color.rgb, vec3(1.0), foam_factor);
			NORMAL = mix((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz, (VIEW_MATRIX * vec4(local_normal,0.0)).xyz, dist_factor);
			break;
		case 1: // Displacement
			ALBEDO = texture(displacementMap, coords).rgb * exp(-dist*0.00075) / 5.0;
			ROUGHNESS = 1.0;
			SPECULAR = 0.0;
			break;
		case 2: // Normals
			ALBEDO = vec3(debug_normal.x, 1.0, debug_normal.y) * exp(-dist*0.00075) / 2.0;
			ROUGHNESS = 1.0;
			SPECULAR = 0.0;
			break;
		case 3: // Gradients
			ALBEDO = texture(gradientMap, coords).rgb * exp(-dist*0.00075);
			ROUGHNESS = 1.0;
			SPECULAR = 0.0;
			break;
	}
}

void light() {
	if (debugRender == 0) {
		vec3 diffuse = vec3(0);
		vec3 specular = vec3(0);
		vec3 foam = foam(LIGHT, LIGHT_COLOR, VIEW);
		brdf_lighting(LIGHT, LIGHT_COLOR, VIEW, VIEW_MATRIX, NORMAL, v_derivatives, wave_height, specular, diffuse);
		if ((renderFlags & 1u) == 0u) {
			diffuse = vec3(0);
		}
		if ((renderFlags & 2u) == 0u) {
			specular = vec3(0);
		}
		SPECULAR_LIGHT += specular;
		DIFFUSE_LIGHT += mix(diffuse, foam, foam_factor);
	} else {
		SPECULAR_LIGHT = vec3(0);
		DIFFUSE_LIGHT = ALBEDO;
	}
	
}
