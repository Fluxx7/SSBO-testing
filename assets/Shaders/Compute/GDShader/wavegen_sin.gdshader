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
    float phaseModifier;
    float lacunarity;
    float gain;
}(bindname = paramBuffer);

buffer out restrict {
    Wave waves[];
}(bindname = waveBuffer, std430);

void gen_waves(int waveCount, float baseSeed) : kernel[2,1,1] {
    if (gInvocationID.x >= waveCount) return;
    float multiplier = baseSeed * (gInvocationID.x + 1);
    vec2 direction_flip = vec2(sin(baseSeed), cos(baseSeed));
    vec4 direction_lock = vec4(-0.96, 0.96, -0.96, 0.96);
    if (direction_flip.x <= 0.0) {
        if (direction_flip.y <= 0.0) {
            direction_lock.x = direction_flip.x;
            direction_lock.y = 1.0 + direction_flip.x;
            direction_lock.z = direction_flip.y;
            direction_lock.w = 1.0 + direction_flip.y;
        } else {
            direction_lock.x = 1.0 - direction_flip.y;
            direction_lock.y = direction_flip.y;
            direction_lock.z = direction_flip.x;
            direction_lock.w = 1.0 + direction_flip.x;
        }
    } else {
        if (direction_flip.y <= 0.0) {
            direction_lock.x = direction_flip.y;
            direction_lock.y = 1.0 + direction_flip.y;
            direction_lock.z = 1.0 - direction_flip.x;
            direction_lock.w = direction_flip.x;
        } else {
            direction_lock.x = 1.0 - direction_flip.x;
            direction_lock.y = direction_flip.x;
            direction_lock.z = 1.0 - direction_flip.y;
            direction_lock.w = direction_flip.y;
        }
    }

    vec2 direction_determinant = vec2((direction_lock.x + direction_lock.y) * 0.5, (direction_lock.z + direction_lock.w) + 0.5);
    vec2 direction_distribution = vec2(abs(direction_lock.x - direction_determinant.x), abs(direction_lock.z - direction_determinant.y));

    Wave w;
    float dirX = sin(multiplier)*direction_distribution.x + direction_determinant.x;
    float dirY = cos(multiplier)*direction_distribution.y + direction_determinant.y;
    w.direction = vec2(dirX, dirY);
    w.amplitude = baseAmplitude * pow(lacunarity, gl_GlobalInvocationID.x);
    w.frequency = baseFrequency * pow(gain, gl_GlobalInvocationID.x);
    w.phase = basePhase * pow(phaseModifier, gl_GlobalInvocationID.x);
    waves[gInvocationID.x] = w;
}