shader_type compute;

struct Wave {
	vec2 direction;
	float amplitude;
	float frequency;
	float phase;
};

buffer uniform restrict {
    float baseAmplitude;
    float baseFrequency;
    float basePhase;
    float lacunarity;
    float gain;
}(bindname = paramBuffer, format = std140);

uniform image2D out restrict waveTexture(format = rgba16f);

buffer out restrict {
	Wave waves[];
}(bindname = waveBuffer, format = std430);

uniform int waveCount : push_constant;

Wave gen_wave(float multiplier) {
	Wave w;
	float dirX = sin(multiplier);
	float dirY = cos(multiplier);
	w.position = vec2(dirX, dirY);
	w.amplitude = baseAmplitude * pow(lacunarity, G_INVOCATION_ID.x);
	w.frequency = baseFrequency * pow(gain, G_INVOCATION_ID.x);
	w.phase = basePhase * pow(1.07, G_INVOCATION_ID.x);
	return w;
}

void buffer_gen(float baseSeed) : kernel(xyzthreads(2, 1, 1)) {
	if (G_INVOCATION_ID.x >= waveCount) return;
	float multiplier = baseSeed * (G_INVOCATION_ID.x + 1);
	waves[G_INVOCATION_ID.x] = gen_wave(multiplier);
}

void tex_gen(float baseSeed) : kernel(xyzthreads(2, 1, 1)) {
	if (G_INVOCATION_ID.x >= waveCount) return;
	float multiplier = baseSeed * (G_INVOCATION_ID.x + 1);
	Wave w = gen_wave(multiplier);
	
	imageStore(waveTexture, ivec2(G_INVOCATION_ID.x, 0), vec4(w.direction.x, w.direction.y, 0.0, 0.0));
	imageStore(waveTexture, ivec2(G_INVOCATION_ID.x, 1), vec4(w.amplitude, w.frequency, w.phase, 0.0));
}