#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

#define PI 3.1415926535897932384626433

layout(rgba32f, set = 0, binding = 0) restrict writeonly uniform image2D baseSpectrum;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float windSpeed;
    float fetch;
    float depth;
    float tile_length;
};

float jonswap_spectrum(vec2 k_vec) {
    const float g = 9.81;
    const float A = 0.076 * pow(windSpeed * windSpeed / (fetch * g), 0.22);
    const float peak_enhancement = 3.3f;
    const float omega_peak = 22.0 * g * g / (windSpeed * fetch);
    float k = length(k_vec);
    float kRcp = 1.0;

     if (k > 1e-6) {
        kRcp = 1.0/k;
    }

    const float phi = k > omega_peak ? 0.09 : 0.07;
    const float r = exp(- pow(k - omega_peak, 2.0) / (2.0 * phi * phi * omega_peak * omega_peak));

    float first_term = A * g * g * pow(kRcp, 5.0);
    float second_term = exp(-1.25f * pow(omega_peak * kRcp, 4.0));

   
    return first_term * second_term * pow(peak_enhancement, r);
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    vec2 k_vec = 2.0 * PI * (id - texSize * 0.5) / tile_length;
    imageStore(baseSpectrum, id, vec4(jonswap_spectrum(k_vec), 0.0, 0.0, 1.0));
}