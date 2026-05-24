#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

#define PI 3.1415926535897932384626433

layout(rgba32f, set = 0, binding = 0) restrict writeonly uniform image2D baseSpectrum;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float windSpeed;
    float A;
    float tile_length;
};

float philips_spectrum(float A, vec2 k_vec) {
    const float g = 9.81;
    float k = length(k_vec);
    float kRcp = 1.0;
    if (k > 1e-6) {
        kRcp = 1.0/k;
    }
    return A * exp( -1.0f * g * g / pow(windSpeed, 4.0) * kRcp * kRcp) * pow(kRcp, 4.0);
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    vec2 k_vec = 2.0 * PI * (id - texSize * 0.5) / tile_length;
    imageStore(baseSpectrum, id, vec4(philips_spectrum(A, k_vec), 0.0, 0.0, 1.0));
}