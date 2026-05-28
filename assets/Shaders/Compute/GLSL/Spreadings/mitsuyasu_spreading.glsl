#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

#define PI 3.1415926535897932384626433


layout(rgba32f, set = 0, binding = 0) restrict uniform image2D baseSpectrum;

layout(rgba32f, set = 0, binding = 1) restrict readonly uniform image2D spectrumCoefficients;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float tile_length;
    float windDirection;
};

float directional_spreading(vec2 k_vec) {
    float wave_direction = atan(k_vec.y, k_vec.x);
    float theta = wave_direction - windDirection;
    if (abs(theta) > (PI / 2.0)) {
        return 0.0;
    }
    return 2.0 * pow(cos(theta), 2.0) / PI;
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    float base_sample = imageLoad(baseSpectrum, id).r;
    vec2 coeffs = imageLoad(spectrumCoefficients, id).rg;
    vec2 k_vec = 2.0 * PI * (id - texSize * 0.5) / tile_length;
    float direction_spectrum = base_sample * directional_spreading(k_vec);
    float output_coeff = sqrt(direction_spectrum) / sqrt(2.0);
    imageStore(baseSpectrum, id, vec4(output_coeff * coeffs.x, output_coeff * coeffs.y, 0.0, 1.0));
}