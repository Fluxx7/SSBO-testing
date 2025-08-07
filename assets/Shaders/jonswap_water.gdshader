shader_type spatial;
render_mode world_vertex_coords, shadows_disabled;

#include "vertex_shift.gdshaderinc"
global uniform vec3 camera_coords;
uniform bool useVertexShift = true;

void vertex() {
	if (useVertexShift) {
		VERTEX.xz += xzShift(VERTEX, camera_coords, NODE_POSITION_WORLD);
	}

}

void fragment() {

}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
