#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba16f, set = 1, binding = 0) restrict writeonly uniform image2D heightTexture;
layout(rgba16f, set = 1, binding = 1) restrict writeonly uniform image2D normalTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    int waveCount;
    int texSize;
    float time;
};

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    vec2 uv = vec2(gl_GlobalInvocationID.xy);
    vec4 displacement = vec4(0.0, 0.0, 0.0, 1.0);
    vec2 derivatives = vec2(0.0);
    for (int i = 0; i < waveCount; i++) {
        Wave w = waves[i];
        float x = dot(w.direction, uv) * w.frequency + w.phase * time;
        float base = w.amplitude * exp( sin(x - 1.0) );
        derivatives += w.direction * w.frequency * cos(x) * base;
        displacement.y += base;
    }
    vec4 normal = vec4(-derivatives.x, 1.0, -derivatives.y, 1.0);
   
    imageStore(heightTexture, ivec2(gl_GlobalInvocationID.xy), displacement);
    imageStore(normalTexture, ivec2(gl_GlobalInvocationID.xy), normal);
}