#[compute]
#version 450

layout(local_size_x = 2, local_size_y = 1, local_size_z = 1) in;

struct Wave {
    vec2 direction;
    float amplitude;
    float frequency;
    float phase;
};

layout(set = 0, binding = 0, std140) uniform restrict paramBuffer {
    float baseAmplitude;
    float baseFrequency;
    float basePhase;
    float lacunarity;
    float gain;
};

layout(set = 0, binding = 1, std430) buffer writeonly restrict waveBuffer {
    Wave waves[];
};

layout(push_constant) restrict readonly uniform PushConstants {
    int waveCount;
    float baseSeed;
};

void main() {
    if (gl_GlobalInvocationID.x >= waveCount) return;
    float multiplier = baseSeed * (gl_GlobalInvocationID.x + 1);

    Wave w;
    float dirX = sin(multiplier);
    float dirY = cos(multiplier);
    w.direction = vec2(dirX, dirY);
    w.amplitude = baseAmplitude * pow(lacunarity, gl_GlobalInvocationID.x);
    w.frequency = baseFrequency * pow(gain, gl_GlobalInvocationID.x);
    w.phase = basePhase * pow(1.07, gl_GlobalInvocationID.x);
    waves[gl_GlobalInvocationID.x] = w;
    
}