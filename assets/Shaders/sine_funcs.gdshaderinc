group_uniforms managed;
uniform float time = 0.0;
group_uniforms;

struct Wave{
	vec2 direction;
	float amplitude;
	float frequency;
	float phase;
};

float sine(vec2 uv, Wave w) {
	return sin(dot(w.direction, uv)*w.frequency+w.phase*time);
}

vec2 d_sine(vec2 uv, Wave w) {
	float phase_time = w.phase*time;
	float d_sin = cos(dot(w.direction, uv)*w.frequency+phase_time);
	float dX = w.frequency * w.direction.x * w.amplitude * d_sin;
	float dZ = w.frequency * w.direction.y * w.amplitude * d_sin;
	return vec2(dX, dZ);
}

float expsine(vec2 uv, Wave w) {
	return exp(sin(dot(w.direction, uv)*w.frequency+w.phase*time)-1.0);
}

vec2 d_expsine(vec2 uv, Wave w) {
	float phase_time = w.phase*time;
	float d_sin = cos(dot(w.direction, uv)*w.frequency+phase_time);
	float dX = w.frequency * w.direction.x * w.amplitude * d_sin * expsine(uv, w);
	float dZ = w.frequency * w.direction.y * w.amplitude * d_sin * expsine(uv, w);
	return vec2(dX, dZ);
}