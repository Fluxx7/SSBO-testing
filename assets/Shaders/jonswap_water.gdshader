shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "water_algorithms.gdshaderinc"
global uniform vec3 camera_coords;


uniform sampler2D displacementMap;
uniform sampler2D gradientMap;

// these are just for visualization
uniform sampler2D gaussian;
uniform sampler2D spectrumTexture;
uniform sampler2D normalMap;
 
uniform float cutoff_dist : hint_range(0.0, 8000.0, 10.0) = 1000.0;
uniform float cutoff_strength : hint_range(0.0, 0.005, 0.0001) = 0.0007;


void vertex() {
	float dist_factor = clamp(exp(-(length(VERTEX.xz - CAMERA_POSITION_WORLD.xz) - cutoff_dist)*cutoff_strength), 0.05, 1.0);
	VERTEX.y += texture(displacementMap, UV).g;// * vec3(1.0, dist_factor, 1.0);
}

varying vec2 v_derivatives;
varying float wave_height;
void fragment() {
	ALBEDO = air_bubble_color.rgb;
	vec2 coords = UV;
	vec4 gradientSample = texture(gradientMap, coords);
	v_derivatives = gradientSample.xy;
	wave_height = texture(displacementMap, coords).y;
	NORMAL = normalize(vec3(-gradientSample.x, 1.0, -gradientSample.y));
	ROUGHNESS = 1.0;//0.02;
}

void light() {
	brdf_lighting(LIGHT, LIGHT_COLOR, VIEW, VIEW_MATRIX, NORMAL, v_derivatives, wave_height, SPECULAR_LIGHT, DIFFUSE_LIGHT);
}
