#[compute]
#version 450

layout(local_size_x = 2, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std140) uniform restrict paramBuffer {
    float baseAmplitude;
    float baseFrequency;
    float basePhase;
    float lacunarity;
    float gain;
};

layout(rgba16f, set = 0, binding = 1) restrict writeonly uniform image2D waveTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    int waveCount;
    float baseSeed;
};

void main() {
    if (gl_GlobalInvocationID.x >= waveCount) return;
    float multiplier = baseSeed * (gl_GlobalInvocationID.x + 1);

    float dirX = sin(multiplier);
    float dirY = cos(multiplier);
    float amplitude = baseAmplitude * pow(lacunarity, gl_GlobalInvocationID.x);
    float frequency = baseFrequency * pow(gain, gl_GlobalInvocationID.x);
    float phase = basePhase * pow(1.07, gl_GlobalInvocationID.x);
    imageStore(waveTexture, ivec2(gl_GlobalInvocationID.x, 0), vec4(dirX, dirY, 0.0, 0.0));
    imageStore(waveTexture, ivec2(gl_GlobalInvocationID.x, 1), vec4(amplitude, frequency, phase, 0.0));
}