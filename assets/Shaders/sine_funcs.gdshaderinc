group_uniforms managed;
uniform float time = 0.0;
group_uniforms;

struct Wave{
	vec2 direction;
	float amplitude;
	float frequency;
	float phase;
};

float expsine(vec2 uv, Wave w) {
	return exp(sin(dot(w.direction, uv)*w.frequency+w.phase*time)-1.0);
}

vec2 d_expsine(vec2 uv, Wave w) {
	float phase_time = w.phase*time;
	float x = dot(w.direction, uv)*w.frequency+phase_time;
	vec2 d_sin = w.direction * w.frequency * cos(x) * expsine(uv, w);
	return d_sin;
}