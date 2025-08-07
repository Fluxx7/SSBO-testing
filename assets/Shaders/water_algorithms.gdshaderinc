uniform bool useGGX = false;

// Source: https://github.com/godotengine/godot/blob/7b56111c297f24304eb911fe75082d8cdc3d4141/drivers/gles3/shaders/scene.glsl#L995
float ggx_distribution(in float cos_theta, in float alpha) {
	float a_sq = alpha*alpha;
	float d = 1.0 + (a_sq - 1.0) * cos_theta * cos_theta;
	return a_sq / (PI * d*d);
}


#define REFLECTANCE 0.0201

uniform vec2 moments;

float dotclamped(vec3 a, vec3 b){
	return max(dot(a, b),2e-5);
}

float dotclampedzero(vec3 a, vec3 b){
	return clamp(dot(a, b),2e-5, 1.0);
}

float dotclampedneg(vec3 a, vec3 b){
	return clamp(dot(a, b),-1.0,1.0);
}


float smith_masking_shadowing(in float cos_theta, in float alpha) {
	float a = cos_theta / (alpha * sqrt(1.0 - cos_theta*cos_theta)); // Approximate: 1.0 / (alpha * tan(acos(cos_theta)))
	float a_sq = a*a;
	return a < 1.6 ? (1.0 - 1.259*a + 0.396*a_sq) / (3.535*a + 2.181*a_sq) : 0.0;
}


// done (?)
float beckmann_distribution(in float ax, in float az) {
	float inner_term = -(moments.x/pow(ax,2.0))-(moments.y/pow(az,2.0));
	return exp(inner_term) * 1.0/(PI*ax*az);
}



// done (?)
float projected_anisotropic_roughness(in float ax, in float az, in float phi) {
	float ax_sq = pow(ax, 2.0);
	float az_sq = pow(az, 2.0);
	float cos_sq_phi = pow(cos(phi),2.0);
	float sin_sq_phi = pow(sin(phi),2.0);
	return sqrt(ax_sq * cos_sq_phi + az_sq * sin_sq_phi);
}


float walter_estimation(in vec3 incoming, in vec3 outgoing, in vec3 normal, in float ax, in float az) {
	vec2 angles_inc = vec2(dot(normal, incoming), atan(incoming.x, incoming.z));
	vec2 angles_out = vec2(dot(normal, outgoing), atan(outgoing.x, outgoing.z));
	
	float alpha_inc = projected_anisotropic_roughness(ax, az, angles_inc.y);
	float alpha_out = projected_anisotropic_roughness(ax, az, angles_out.y);
	return 1.0 / (1.0 + smith_masking_shadowing(angles_inc.x, alpha_inc) + smith_masking_shadowing(angles_out.x, alpha_out));
}


// done
float schlick_fresnel(float cos_theta, float av) {
	float upper_term = pow(1.0-cos_theta,5.0 * exp(-2.69 * av));
	float lower_term = 1.0 + 22.7*pow(av, 1.5);
	return REFLECTANCE + (1.0 - REFLECTANCE) * upper_term / lower_term;
}

vec3 specular(vec3 light, vec3 light_color, vec3 view, vec3 normal, vec2 derivatives) {
	vec3 halfway = normalize(light + view);
	
	
	float ax = abs(derivatives.x);
	float az = abs(derivatives.y);
	
	float av = projected_anisotropic_roughness(ax, az, atan(view.x, view.z));
	
	
	float distribution = ggx_distribution(dot(normal, halfway),av);//beckmann_distribution(ax, az);
	
	vec3 upper_term = light_color * schlick_fresnel(dot(normal, view), av) * distribution;
	float lower_term = 4.0 * dot(view, vec3(0.0,0.0,1.0)) * walter_estimation(light, view, normal, ax, az);
	return upper_term / lower_term;
}

