
group_uniforms Vertex_Shift_controls;
uniform float wm = 0.99;
uniform float k = 1.0;
uniform float m = 10.0;
uniform vec2 shiftClamp = vec2(0.05,0.78);
uniform float epsilon = 0.00001;
group_uniforms;

group_uniforms managed;
uniform vec2 size = vec2(2000.0);
group_uniforms;


// need to make shift circular, need to only add drag to vertices for edges opposite from the camera
vec2 xzShiftOld(vec3 vertex, vec3 cam, vec3 node) {
	vec3 camera = clamp(cam, vec3(node.x - size.x/2.0, node.y, node.z-size.y/2.0), vec3(node.x + size.x/2.0, node.y, node.z+size.y/2.0));
	vec2 uv = camera.xz - vertex.xz;
	vec2 half_size = size/2.0;

	float center_dist_x = abs(abs(vertex.x)-abs(node.x));
	float center_dist_z = abs(abs(vertex.z)-abs(node.z));

	float edge_distance_x = half_size.x - center_dist_x;
	float edge_distance_z = half_size.y - center_dist_z;

	uv *= clamp(pow(wm,exp(-k*distance(vertex, camera))),shiftClamp.x,shiftClamp.y);

	if (!(half_size.x >= center_dist_x - epsilon && half_size.x <= center_dist_x + epsilon)) {
		uv.x *= min(pow(1.0 - exp(-m*edge_distance_x),2.0),1.0);
	} else {
		uv.x = 0.0;
	}
	if (!(half_size.y >= center_dist_z - epsilon && half_size.y <= center_dist_z + epsilon)) {
		uv.y *= min(pow(1.0 - exp(-m*edge_distance_z),2.0),1.0);
	} else {
		uv.y = 0.0;
	}

	return uv;
}

vec2 xzShift(vec3 vertex, vec3 cam, vec3 node) {
	vec2 half_size = size/2.0;
    vec2 to_cam = cam.xz - vertex.xz;
    vec2 dir = normalize(to_cam);

    // Compute ray intersection with square bounds (AABB)
    vec2 bounds_min = node.xz - vec2(half_size);
    vec2 bounds_max = node.xz + vec2(half_size);

    float t_min = 0.0;
    float t_max = 1e9;

    for (int i = 0; i < 2; i++) { // X and Z axes
        if (abs(dir[i]) < 1e-5) continue; // Parallel, skip

        float t1 = (bounds_min[i] - cam.xz[i]) / dir[i];
        float t2 = (bounds_max[i] - cam.xz[i]) / dir[i];

        if (t1 > t2) { float temp = t1; t1 = t2; t2 = temp; }

        t_min = max(t_min, t1);
        t_max = min(t_max, t2);
    }

    float edge_dist = t_max;
    float cam_dist = length(to_cam);

    float ratio = edge_dist / (edge_dist + cam_dist) -0.5;
    float shift_factor = 1.0 - exp(-m*ratio); // closer = more shift

    return to_cam * min(shift_factor,shiftClamp.y) * wm;
}

//vec2 xzShift(vec3 vertex, vec3 cam, vec3 node) {
	//return xzShiftNew(vertex,cam,node);
//}