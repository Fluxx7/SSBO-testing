#[compute]
#version 450

layout(local_size_x = 2, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std140) uniform restrict paramBuffer {
    float baseAmplitude;
    float baseFrequency;
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

    float dirX = sin(multiplier)*direction_distribution.x + direction_determinant.x;
    float dirY = cos(multiplier)*direction_distribution.y + direction_determinant.y;
    float amplitude = baseAmplitude * pow(lacunarity, gl_GlobalInvocationID.x);
    float frequency = baseFrequency * pow(gain, gl_GlobalInvocationID.x);
    float phase = cos(multiplier) * 10.0;
    imageStore(waveTexture, ivec2(gl_GlobalInvocationID.x, 0), vec4(dirX, dirY, phase, 1.0));
    imageStore(waveTexture, ivec2(gl_GlobalInvocationID.x, 1), vec4(amplitude, frequency, 0.0, 1.0));
}