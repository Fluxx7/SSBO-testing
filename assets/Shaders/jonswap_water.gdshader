shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;

#include "vertex_shift.gdshaderinc"
#include "water_algorithms.gdshaderinc"
global uniform vec3 camera_coords;
uniform bool useVertexShift = true;

void vertex() {
	if (useVertexShift) {
		VERTEX.xz += xzShift(VERTEX, camera_coords, NODE_POSITION_WORLD);
	}

}



void fragment() {
	ALBEDO = air_bubble_color.rgb;
	NORMAL = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
	ROUGHNESS = 0.4;
}

void light() {
	vec2 v_derivatives = vec2(0.0);
	float wave_height = 0.0;
	
	brdf_lighting(LIGHT, LIGHT_COLOR, VIEW, VIEW_MATRIX, NORMAL, v_derivatives, wave_height, SPECULAR_LIGHT, DIFFUSE_LIGHT);
}
