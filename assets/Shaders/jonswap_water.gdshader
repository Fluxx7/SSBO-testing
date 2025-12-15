shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;
#include "water_algorithms.gdshaderinc"
global uniform vec3 camera_coords;

uniform vec4 mapWorldScale = vec4(1.0);
uniform sampler2D displacementMap;
uniform sampler2D gradientMap;

 
uniform float cutoff_dist : hint_range(0.0, 8000.0, 10.0) = 1000.0;
uniform float cutoff_strength : hint_range(0.0, 0.005, 0.0001) = 0.0007;

varying float dist_factor;
void vertex() {
	UV = VERTEX.xz;
	dist_factor = clamp(exp(-(length(VERTEX.xz - CAMERA_POSITION_WORLD.xz) - cutoff_dist)*cutoff_strength), 0.05, 1.0);
	vec3 vertex_displacement = texture(displacementMap, vec2(UV * mapWorldScale.x)).xyz * mapWorldScale.z;
	vertex_displacement.xz *= mapWorldScale.y;
	VERTEX += vertex_displacement * dist_factor;// * vec3(1.0, dist_factor, 1.0);
}

varying vec2 v_derivatives;
varying float wave_height;
void fragment() {
	ALBEDO = air_bubble_color.rgb;
	vec2 coords = UV;
	vec4 gradientSample = texture(gradientMap, vec2(coords * mapWorldScale.x)) * mapWorldScale.w;
	v_derivatives = gradientSample.xy;
	wave_height = texture(displacementMap, vec2(coords * mapWorldScale.x)).g * mapWorldScale.z;
	NORMAL = mix(normalize(VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz, (VIEW_MATRIX * vec4(normalize(vec3(-gradientSample.x, 1.0, -gradientSample.y)),0.0)).xyz, dist_factor);;
	ROUGHNESS = 0.02;
}

void light() {
	brdf_lighting(LIGHT, LIGHT_COLOR, VIEW, VIEW_MATRIX, NORMAL, v_derivatives, wave_height, SPECULAR_LIGHT, DIFFUSE_LIGHT);
	
}
