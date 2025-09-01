shader_type compute;

struct Wave {
	vec2 position;
	float amplitude;
	float frequency;
	float phase;
};

buffer uniform restrict {
	int waveCount;
	float baseSeed;
    float baseAmplitude;
    float baseFrequency;
    float basePhase;
    float lacunarity;
    float gain;
}(bindname = paramBuffer, format = std140);

buffer out restrict {
	Wave waves[];
}(bindname = waveBuffer, format = std430);

void sinwavegen() : kernel(xyzthreads(2, 1, 1)) {
	if (G_INVOCATION_ID.x >= waveCount) return;
	float multiplier = baseSeed * (G_INVOCATION_ID.x + 1);

	Wave w;
	float dirX = sin(multiplier);
	float dirY = cos(multiplier);
	w.position = vec2(dirX, dirY);
	w.amplitude = baseAmplitude * pow(lacunarity, G_INVOCATION_ID.x);
	w.frequency = baseFrequency * pow(gain, G_INVOCATION_ID.x);
	w.phase = basePhase * pow(1.07, G_INVOCATION_ID.x);
	waves[G_INVOCATION_ID.x] = w;
}