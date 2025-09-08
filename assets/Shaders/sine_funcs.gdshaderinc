group_uniforms managed;
uniform float time = 0.0;
group_uniforms;

struct Wave{
	vec2 direction;
	float amplitude;
	float frequency;
	float phase;
};

float expsine(vec2 uv, Wave w, out float x) {
	float phase_time = w.phase*time;
	x = dot(w.direction, uv)*w.frequency+phase_time;
	return exp(sin(x)-1.0);
}

vec2 d_expsine(vec2 uv, Wave w, float x) {
	vec2 d_sin = w.direction * w.frequency * cos(x) * exp(sin(x)-1.0);
	return d_sin;
}